using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace InkMath.AuthService.Services
{
    public interface IEvaluacionService
    {
        Task<ResultadoEvaluacionResponseDto> ProcesarIntentoTestAsync(RegistrarIntentoTestDto dto);
        Task<EvaluacionDto?> ObtenerEvaluacionPorTestIdAsync(long testId, int limitePreguntas = 15);
    }

    public class EvaluacionService : IEvaluacionService
    {
        private readonly AplicationDbContext _context;

        public EvaluacionService(AplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ResultadoEvaluacionResponseDto> ProcesarIntentoTestAsync(RegistrarIntentoTestDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Obtener las preguntas del test
                var preguntasTest = await _context.PreguntasTest
                    .Where(p => p.TestId == dto.TestId)
                    .Include(p => p.Opciones)
                    .ToListAsync();

                int totalPreguntas = preguntasTest.Count;
                int aciertos = 0;

                // 2. Evaluar respuestas
                foreach (var resp in dto.Respuestas)
                {
                    var pregunta = preguntasTest.FirstOrDefault(p => p.Id == resp.PreguntaId);
                    if (pregunta != null && resp.OpcionId.HasValue)
                    {
                        var opcionCorrecta = pregunta.Opciones.FirstOrDefault(o => o.EsCorrecta);
                        if (opcionCorrecta != null && opcionCorrecta.Id == resp.OpcionId.Value)
                        {
                            aciertos++;
                        }
                    }
                }

                // 3. Calcular puntaje (0-100)
                int puntajeFinal = totalPreguntas > 0 ? (int)Math.Round((double)aciertos / totalPreguntas * 100) : 0;

                // 4. Buscar récord anterior del estudiante en este test
                int mejorPuntajePrevio = await _context.IntentosTest
                    .Where(i => i.TestId == dto.TestId && i.EstudianteId == dto.EstudianteId)
                    .OrderByDescending(i => i.PuntajeObtenido)
                    .Select(i => (int?)i.PuntajeObtenido)
                    .FirstOrDefaultAsync() ?? 0;

                // 5. Guardar el nuevo intento siempre (permite historial de intentos)
                var nuevoIntento = new IntentoTest
                {
                    TestId = dto.TestId,
                    EstudianteId = dto.EstudianteId,
                    PuntajeObtenido = puntajeFinal,
                    FechaInicio = dto.FechaInicio,
                    FechaFin = dto.FechaFin
                };

                _context.IntentosTest.Add(nuevoIntento);
                await _context.SaveChangesAsync();

                // Guardar respuestas
                foreach (var resp in dto.Respuestas)
                {
                    long? opcionIdValida = resp.OpcionId > 0 ? resp.OpcionId : null;

                    _context.RespuestasEstudiante.Add(new RespuestaEstudiante
                    {
                        IntentoId = nuevoIntento.Id,
                        PreguntaId = resp.PreguntaId,
                        OpcionId = opcionIdValida,
                        RespuestaTexto = resp.RespuestaTexto
                    });
                }
                await _context.SaveChangesAsync();

                // Actualizar progreso de nivel
                // Verificar si este Test está vinculado a un Nivel de juego
                var nivelAsociado = await _context.Niveles
                    .FirstOrDefaultAsync(n => n.TestId == dto.TestId);

                if (nivelAsociado != null)
                {
                    var progreso = await _context.ProgresosNivel
                        .FirstOrDefaultAsync(p => p.NivelId == nivelAsociado.Id && p.EstudianteId == dto.EstudianteId);

                    // Determinar estado de progreso:
                    // ID 3 = Completado (100% correctas)
                    // ID 2 = En Progreso (ya respondió el test pero no todas son correctas)
                    long nuevoEstadoId = (aciertos == totalPreguntas && totalPreguntas > 0) ? 3 : 2;

                    if (progreso == null)
                    {
                        // Si no existía registro previo
                        progreso = new ProgresoNivel
                        {
                            EstudianteId = dto.EstudianteId,
                            NivelId = nivelAsociado.Id,
                            EstadoId = nuevoEstadoId,
                            Puntaje = puntajeFinal,
                            Intentos = 1
                        };
                        _context.ProgresosNivel.Add(progreso);
                    }
                    else
                    {
                        // Si ya existía, incrementar intentos y actualizar puntaje máximo alcanzado
                        progreso.Intentos += 1;

                        if (puntajeFinal > progreso.Puntaje)
                        {
                            progreso.Puntaje = puntajeFinal;
                        }

                        // Si ya estaba completado (3), mantenemos 3. De lo contrario se asigna el nuevo estado
                        if (progreso.EstadoId != 3)
                        {
                            progreso.EstadoId = nuevoEstadoId;
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                // 6. Calcular recompensas sólo si superó su récord anterior
                long monedasGanadas = 0;
                long nuevoSaldo = 0;

                if (puntajeFinal > mejorPuntajePrevio)
                {
                    int incrementoAciertos = aciertos - (int)Math.Round((double)mejorPuntajePrevio / 100 * totalPreguntas);
                    if (incrementoAciertos > 0)
                    {
                        monedasGanadas = incrementoAciertos * 10;
                        if (puntajeFinal >= 80 && mejorPuntajePrevio < 80)
                        {
                            monedasGanadas += 50; // Bonificación por alcanzar el umbral de excelencia
                        }
                    }
                }

                // 7. Acreditar monedas si corresponden
                if (monedasGanadas > 0)
                {
                    var transaccionMoneda = new TransaccionMoneda
                    {
                        EstudianteId = dto.EstudianteId,
                        TipoTransaccionId = 1,
                        Monto = monedasGanadas,
                        IdempotencyKey = $"TEST_{dto.TestId}_EST_{dto.EstudianteId}_SCORE_{puntajeFinal}",
                        CreadoEn = DateTimeOffset.UtcNow
                    };
                    _context.TransaccionesMonedas.Add(transaccionMoneda);

                    var saldoEntity = await _context.SaldoMonedas.FirstOrDefaultAsync(s => s.EstudianteId == dto.EstudianteId);
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
                    nuevoSaldo = saldoEntity.Saldo;
                }
                else
                {
                    var saldoExistente = await _context.SaldoMonedas.FirstOrDefaultAsync(s => s.EstudianteId == dto.EstudianteId);
                    nuevoSaldo = saldoExistente?.Saldo ?? 0;
                }

                await transaction.CommitAsync();

                return new ResultadoEvaluacionResponseDto
                {
                    IntentoId = nuevoIntento.Id,
                    TotalPreguntas = totalPreguntas,
                    RespuestasCorrectas = aciertos,
                    PuntajeObtenido = puntajeFinal,
                    MonedasGanadas = monedasGanadas,
                    NuevoSaldoMonedas = nuevoSaldo
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<EvaluacionDto?> ObtenerEvaluacionPorTestIdAsync(long testId, int limitePreguntas = 15)
        {
            var test = await _context.TestsPersonalizados
                .FirstOrDefaultAsync(t => t.Id == testId && t.EstaActivo);

            if (test == null) return null;

            var preguntas = await _context.PreguntasTest
                .Where(p => p.TestId == testId)
                .OrderBy(p => EF.Functions.Random()) // Selección aleatoria en PostgreSQL
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
                            // Se omite EsCorrecta por seguridad frente a inspección en el cliente
                        })
                        .ToList()
                })
                .ToListAsync();

            return new EvaluacionDto
            {
                TestId = test.Id,
                NombreTest = test.Nombre,
                TotalPreguntas = preguntas.Count,
                Preguntas = preguntas
            };
        }
    }
}