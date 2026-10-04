using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace InkMath.AuthService.Services
{
    public interface IEvaluacionService
    {
        Task<ResultadoEvaluacionResponseDto> ProcesarIntentoTestAsync(RegistrarIntentoTestDto dto);
        Task<EvaluacionDto?> ObtenerEvaluacionPorTestIdAsync(long testId, long estudianteId, int limitePreguntas = 15);
    }

    /// <summary>Error de validación de negocio (se responde 400).</summary>
    public class EvaluacionInvalidaException : Exception
    {
        public EvaluacionInvalidaException(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// Ticket firmado (HMAC-SHA256) que el servidor entrega al iniciar un test.
    /// Fija qué preguntas se sirvieron, a quién y cuándo, sin necesidad de nuevas tablas.
    /// </summary>
    public sealed record IntentoTicketPayload(long TestId, long EstudianteId, long[] PreguntaIds, long EmitidoMs);

    public static class IntentoTicket
    {
        public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(20);

        public static string Emitir(byte[] key, IntentoTicketPayload payload)
        {
            var cuerpo = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
            var firma = WebEncoders.Base64UrlEncode(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(cuerpo)));
            return $"{cuerpo}.{firma}";
        }

        public static IntentoTicketPayload? Validar(byte[] key, string? ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 4096) return null;
            var partes = ticket.Split('.');
            if (partes.Length != 2) return null;

            try
            {
                var esperada = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(partes[0]));
                var recibida = WebEncoders.Base64UrlDecode(partes[1]);
                if (!CryptographicOperations.FixedTimeEquals(esperada, recibida)) return null;

                var p = JsonSerializer.Deserialize<IntentoTicketPayload>(WebEncoders.Base64UrlDecode(partes[0]));
                if (p is null || p.PreguntaIds is null || p.PreguntaIds.Length == 0) return null;

                var edad = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(p.EmitidoMs);
                if (edad < TimeSpan.Zero || edad > Vigencia) return null;
                return p;
            }
            catch
            {
                return null; // base64 o JSON malformado
            }
        }
    }

    public class EvaluacionService : IEvaluacionService
    {
        private const int MaxPreguntasPorIntento = 30;
        private const int MaxRespuestasAceptadas = 100;
        private const int MaxLongitudRespuestaTexto = 500;
        private const double MinSegundosPorPregunta = 1.0;

        private readonly AplicationDbContext _context;
        private readonly byte[] _ticketKey;

        public EvaluacionService(AplicationDbContext context, IConfiguration config)
        {
            _context = context;

            var jwtKey = config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
                throw new InvalidOperationException("Jwt:Key debe configurarse (mínimo 32 caracteres).");

            // Clave derivada: no reutiliza directamente la clave de firma del JWT.
            _ticketKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtKey), "inkmath-intento-v1"u8);
        }

        public async Task<ResultadoEvaluacionResponseDto> ProcesarIntentoTestAsync(RegistrarIntentoTestDto dto)
        {
            // 0. El intento debe provenir de un ticket emitido por el servidor para ESTE alumno y ESTE test.
            var ticket = IntentoTicket.Validar(_ticketKey, dto.IntentoToken);
            if (ticket is null || ticket.TestId != dto.TestId || ticket.EstudianteId != dto.EstudianteId)
                throw new EvaluacionInvalidaException("Intento inválido o expirado. Vuelve a iniciar el nivel.");

            // Tiempos definidos por el servidor (se ignoran FechaInicio/FechaFin del cliente).
            var inicio = DateTimeOffset.FromUnixTimeMilliseconds(ticket.EmitidoMs);
            var fin = DateTimeOffset.UtcNow;
            if ((fin - inicio).TotalSeconds < ticket.PreguntaIds.Length * MinSegundosPorPregunta)
                throw new EvaluacionInvalidaException("El intento se envió demasiado rápido.");

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                // 1. Anti-replay: un ticket solo se puede canjear una vez (FechaInicio = momento de emisión).
                bool yaCanjeado = await _context.IntentosTest.AnyAsync(i =>
                    i.TestId == dto.TestId && i.EstudianteId == dto.EstudianteId && i.FechaInicio == inicio);
                if (yaCanjeado)
                    throw new EvaluacionInvalidaException("Este intento ya fue registrado.");

                // 2. Solo cuentan las preguntas que se sirvieron en el ticket.
                var idsServidos = ticket.PreguntaIds;
                var preguntas = await _context.PreguntasTest
                    .Where(p => p.TestId == dto.TestId && idsServidos.Contains(p.Id))
                    .Include(p => p.Opciones)
                    .ToListAsync();

                int totalPreguntas = preguntas.Count;
                if (totalPreguntas == 0)
                    throw new EvaluacionInvalidaException("El test no tiene preguntas válidas.");

                // 3. Una respuesta por pregunta (se descartan duplicados y preguntas ajenas al ticket).
                var respuestasPorPregunta = (dto.Respuestas ?? new())
                    .Take(MaxRespuestasAceptadas)
                    .GroupBy(r => r.PreguntaId)
                    .Select(g => g.First())
                    .ToDictionary(r => r.PreguntaId);

                int aciertos = 0;
                var filasRespuesta = new List<RespuestaEstudiante>(totalPreguntas);

                foreach (var pregunta in preguntas)
                {
                    respuestasPorPregunta.TryGetValue(pregunta.Id, out var resp);

                    long? opcionIdValida = null;
                    if (resp?.OpcionId is > 0)
                    {
                        // La opción debe pertenecer a la pregunta; si no, se trata como sin responder.
                        var opcion = pregunta.Opciones.FirstOrDefault(o => o.Id == resp.OpcionId.Value);
                        if (opcion != null)
                        {
                            opcionIdValida = opcion.Id;
                            if (opcion.EsCorrecta) aciertos++;
                        }
                    }

                    var texto = resp?.RespuestaTexto ?? string.Empty;
                    if (texto.Length > MaxLongitudRespuestaTexto) texto = texto[..MaxLongitudRespuestaTexto];

                    filasRespuesta.Add(new RespuestaEstudiante
                    {
                        PreguntaId = pregunta.Id,
                        OpcionId = opcionIdValida,
                        RespuestaTexto = texto
                    });
                }

                // 4. Puntaje (0-100) sobre las preguntas realmente servidas.
                int puntajeFinal = (int)Math.Round((double)aciertos / totalPreguntas * 100);

                // 5. Récord anterior
                int mejorPuntajePrevio = await _context.IntentosTest
                    .Where(i => i.TestId == dto.TestId && i.EstudianteId == dto.EstudianteId)
                    .OrderByDescending(i => i.PuntajeObtenido)
                    .Select(i => (int?)i.PuntajeObtenido)
                    .FirstOrDefaultAsync() ?? 0;

                // 6. Guardar intento y respuestas
                var nuevoIntento = new IntentoTest
                {
                    TestId = dto.TestId,
                    EstudianteId = dto.EstudianteId,
                    PuntajeObtenido = puntajeFinal,
                    FechaInicio = inicio,
                    FechaFin = fin
                };
                _context.IntentosTest.Add(nuevoIntento);
                await _context.SaveChangesAsync();

                foreach (var fila in filasRespuesta) fila.IntentoId = nuevoIntento.Id;
                _context.RespuestasEstudiante.AddRange(filasRespuesta);
                await _context.SaveChangesAsync();

                // 7. Progreso de nivel
                var nivelAsociado = await _context.Niveles.FirstOrDefaultAsync(n => n.TestId == dto.TestId);
                if (nivelAsociado != null)
                {
                    var progreso = await _context.ProgresosNivel
                        .FirstOrDefaultAsync(p => p.NivelId == nivelAsociado.Id && p.EstudianteId == dto.EstudianteId);

                    // 3 = Completado (100%), 2 = En Progreso
                    long nuevoEstadoId = aciertos == totalPreguntas ? 3 : 2;

                    if (progreso == null)
                    {
                        _context.ProgresosNivel.Add(new ProgresoNivel
                        {
                            EstudianteId = dto.EstudianteId,
                            NivelId = nivelAsociado.Id,
                            EstadoId = nuevoEstadoId,
                            Puntaje = puntajeFinal,
                            Intentos = 1
                        });
                    }
                    else
                    {
                        progreso.Intentos += 1;
                        if (puntajeFinal > progreso.Puntaje) progreso.Puntaje = puntajeFinal;
                        if (progreso.EstadoId != 3) progreso.EstadoId = nuevoEstadoId;
                    }
                    await _context.SaveChangesAsync();
                }

                // 8. Recompensa solo si supera su récord anterior
                long monedasGanadas = 0;
                if (puntajeFinal > mejorPuntajePrevio)
                {
                    int incrementoAciertos = aciertos - (int)Math.Round((double)mejorPuntajePrevio / 100 * totalPreguntas);
                    if (incrementoAciertos > 0)
                    {
                        monedasGanadas = incrementoAciertos * 10L;
                        if (puntajeFinal >= 80 && mejorPuntajePrevio < 80)
                            monedasGanadas += 50;
                    }
                }

                // 9. Acreditar
                var saldoEntity = await _context.SaldoMonedas.FirstOrDefaultAsync(s => s.EstudianteId == dto.EstudianteId);
                if (monedasGanadas > 0)
                {
                    _context.TransaccionesMonedas.Add(new TransaccionMoneda
                    {
                        EstudianteId = dto.EstudianteId,
                        TipoTransaccionId = 1,
                        Monto = monedasGanadas,
                        IdempotencyKey = $"TEST_{dto.TestId}_EST_{dto.EstudianteId}_SCORE_{puntajeFinal}",
                        CreadoEn = DateTimeOffset.UtcNow
                    });

                    if (saldoEntity == null)
                    {
                        saldoEntity = new SaldoMoneda
                        {
                            EstudianteId = dto.EstudianteId,
                            Saldo = monedasGanadas,
                            ActualizadoEn = DateTimeOffset.UtcNow
                        };
                        _context.SaldoMonedas.Add(saldoEntity);
                    }
                    else
                    {
                        saldoEntity.Saldo += monedasGanadas;
                        saldoEntity.ActualizadoEn = DateTimeOffset.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return new ResultadoEvaluacionResponseDto
                {
                    IntentoId = nuevoIntento.Id,
                    TotalPreguntas = totalPreguntas,
                    RespuestasCorrectas = aciertos,
                    PuntajeObtenido = puntajeFinal,
                    MonedasGanadas = monedasGanadas,
                    NuevoSaldoMonedas = saldoEntity?.Saldo ?? 0
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<EvaluacionDto?> ObtenerEvaluacionPorTestIdAsync(long testId, long estudianteId, int limitePreguntas = 15)
        {
            limitePreguntas = Math.Clamp(limitePreguntas, 1, MaxPreguntasPorIntento);

            var test = await _context.TestsPersonalizados
                .FirstOrDefaultAsync(t => t.Id == testId && t.EstaActivo);
            if (test == null) return null;

            var preguntas = await _context.PreguntasTest
                .Where(p => p.TestId == testId)
                .OrderBy(p => EF.Functions.Random())
                .Take(limitePreguntas)
                .Select(p => new PreguntaDetalleDto
                {
                    PreguntaId = p.Id,
                    Pregunta = p.Pregunta,
                    TipoPreguntaId = p.TipoPreguntaId,
                    Opciones = _context.OpcionesPregunta
                        .Where(o => o.PreguntaId == p.Id)
                        .Select(o => new OpcionDetalleDto
                        {
                            OpcionId = o.Id,
                            TextoOpcion = o.TextoOpcion
                            // EsCorrecta nunca se expone al cliente
                        })
                        .ToList()
                })
                .ToListAsync();

            var ticket = IntentoTicket.Emitir(_ticketKey, new IntentoTicketPayload(
                testId,
                estudianteId,
                preguntas.Select(p => p.PreguntaId).ToArray(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

            return new EvaluacionDto
            {
                TestId = test.Id,
                NombreTest = test.Nombre,
                TotalPreguntas = preguntas.Count,
                Preguntas = preguntas,
                IntentoToken = ticket
            };
        }
    }
}