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
    public interface IJuegoService
    {
        JuegoInicioResponse Iniciar(string modo, long estudianteId);
        Task<JuegoResultadoResponse> FinalizarAsync(string modo, long estudianteId, JuegoResultadoRequest req);
    }

    // ---------- Datos de cada ronda (viajan firmados dentro del ticket) ----------
    public sealed record LineaPedido(int Id, int Cantidad);
    public sealed record RondaTienda(List<LineaPedido> Lineas, int Pago);
    public sealed record RondaFisica(string Modo, int? AnguloFijo, int? VelocidadFija, double Distancia);

    // ---------- Respuestas del cliente ----------
    public sealed record IntentoFisica(double Velocidad, double Angulo);
    public sealed record IntentoFraccion(int Partes, List<int> Duenos);

    public sealed record JuegoTicketPayload(string Modo, long EstudianteId, long EmitidoMs, string Rondas);

    /// <summary>Ticket firmado con HMAC-SHA256: fija modo, alumno, hora de inicio y rondas generadas.</summary>
    public static class JuegoTicket
    {
        public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);

        public static string Emitir(byte[] key, JuegoTicketPayload payload)
        {
            var cuerpo = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
            var firma = WebEncoders.Base64UrlEncode(HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(cuerpo)));
            return $"{cuerpo}.{firma}";
        }

        public static JuegoTicketPayload? Validar(byte[] key, string? ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 16384) return null;
            var partes = ticket.Split('.');
            if (partes.Length != 2) return null;
            try
            {
                var esperada = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(partes[0]));
                var recibida = WebEncoders.Base64UrlDecode(partes[1]);
                if (!CryptographicOperations.FixedTimeEquals(esperada, recibida)) return null;

                var p = JsonSerializer.Deserialize<JuegoTicketPayload>(WebEncoders.Base64UrlDecode(partes[0]));
                if (p is null) return null;

                var edad = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(p.EmitidoMs);
                return edad < TimeSpan.Zero || edad > Vigencia ? null : p;
            }
            catch
            {
                return null;
            }
        }
    }

    public class JuegoService : IJuegoService
    {
        // ---- Reglas de recompensa (mismas que las evaluaciones) ----
        private const int MonedasPorAcierto = 10;
        private const int UmbralBonusPct = 80;
        private const int Bonus = 50;
        private const double MinSegundosPorRonda = 2.0;

        // ---- Física ----
        private const double G = 9.8;
        private const double ToleranciaFisica = 1.5;
        private const int MaxIntentosPorRonda = 3;

        // ---- Tienda (debe coincidir con el catálogo del juego) ----
        private static readonly int[] PreciosTienda = { 5, 8, 6, 7, 9, 4, 10, 11, 3 };
        private static readonly int[] OrdenDesbloqueo = { 1, 2, 3, 0, 5, 8, 4, 6, 7 };
        private const int ClientesTienda = 5;

        // ---- Fracciones: (numerador, denominador) pedido por cada cliente de cada nivel ----
        private static readonly (int Num, int Den)[][] NivelesFracciones =
        {
            new[] { (1, 2), (1, 2) },
            new[] { (1, 4), (1, 4), (1, 4), (1, 4) },
            new[] { (1, 3), (1, 3), (1, 3) },
            new[] { (1, 2), (1, 4), (1, 4) },
            new[] { (1, 2), (1, 3), (1, 6) },
        };
        private static readonly int[] CortesPermitidos = { 2, 3, 4, 6, 8 };
        private const int MaxIntentosFraccion = 30;

        private static readonly HashSet<string> Modos = new() { "tienda", "fisica", "fracciones" };
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly AplicationDbContext _context;
        private readonly byte[] _key;

        public JuegoService(AplicationDbContext context, IConfiguration config)
        {
            _context = context;
            var jwtKey = config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
                throw new InvalidOperationException("Jwt:Key debe configurarse (mínimo 32 caracteres).");
            _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtKey), "inkmath-juego-v1"u8);
        }

        private static string Normalizar(string? modo)
        {
            var m = modo?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!Modos.Contains(m)) throw new EvaluacionInvalidaException("Modo de juego no válido.");
            return m;
        }

        // =====================  INICIO  =====================
        public JuegoInicioResponse Iniciar(string modo, long estudianteId)
        {
            modo = Normalizar(modo);
            object? rondas = null;
            int[]? precios = null;
            string rondasJson = "[]";

            switch (modo)
            {
                case "tienda":
                    var rt = GenerarTienda();
                    rondas = rt; precios = PreciosTienda;
                    rondasJson = JsonSerializer.Serialize(rt, Json);
                    break;
                case "fisica":
                    var rf = GenerarFisica();
                    rondas = rf;
                    rondasJson = JsonSerializer.Serialize(rf, Json);
                    break;
            }

            var ticket = JuegoTicket.Emitir(_key, new JuegoTicketPayload(
                modo, estudianteId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), rondasJson));

            return new JuegoInicioResponse { IntentoToken = ticket, Rondas = rondas, Precios = precios };
        }

        private static List<RondaTienda> GenerarTienda()
        {
            var rnd = Random.Shared;
            var lista = new List<RondaTienda>();
            for (int nivel = 0; nivel < ClientesTienda; nivel++)
            {
                var pool = OrdenDesbloqueo.Take(2 + nivel).OrderBy(_ => rnd.Next()).ToList();
                int tipos = Math.Min(rnd.Next(2, 4), pool.Count);
                var lineas = pool.Take(tipos).Select(id => new LineaPedido(id, rnd.Next(1, 4))).ToList();
                int total = lineas.Sum(l => l.Cantidad * PreciosTienda[l.Id]);
                lista.Add(new RondaTienda(lineas, total + rnd.Next(1, 9)));
            }
            return lista;
        }

        private static double Rango(double v, double anguloGrados) =>
            v * v * Math.Sin(2.0 * anguloGrados * Math.PI / 180.0) / G;

        private static List<RondaFisica> GenerarFisica()
        {
            var rnd = Random.Shared;
            var modos = new[] { "VELOCIDAD", "VELOCIDAD", "ANGULO", "ANGULO", "LIBRE" };
            var lista = new List<RondaFisica>();

            foreach (var m in modos)
            {
                RondaFisica? ronda = null;
                for (int i = 0; i < 60 && ronda is null; i++)
                {
                    if (m == "VELOCIDAD")
                    {
                        int ang = new[] { 30, 45, 60 }[rnd.Next(3)];
                        int v = rnd.Next(8, 21);
                        double d = Math.Round(Rango(v, ang), 1);
                        if (d >= 6 && d <= 38) ronda = new RondaFisica(m, ang, null, d);
                    }
                    else if (m == "ANGULO")
                    {
                        int v = rnd.Next(10, 21);
                        int ang = rnd.Next(15, 76);
                        double d = Math.Round(Rango(v, ang), 1);
                        if (d >= 6 && d <= 38) ronda = new RondaFisica(m, null, v, d);
                    }
                    else
                    {
                        ronda = new RondaFisica(m, null, null, Math.Round(10 + rnd.NextDouble() * 26, 1));
                    }
                }
                lista.Add(ronda ?? new RondaFisica(m, m == "VELOCIDAD" ? 45 : null, m == "ANGULO" ? 15 : null, 20.0));
            }
            return lista;
        }

        // =====================  RESULTADO  =====================
        public async Task<JuegoResultadoResponse> FinalizarAsync(string modo, long estudianteId, JuegoResultadoRequest req)
        {
            modo = Normalizar(modo);

            var t = JuegoTicket.Validar(_key, req.IntentoToken);
            if (t is null || t.Modo != modo || t.EstudianteId != estudianteId)
                throw new EvaluacionInvalidaException("Sesión de juego inválida o expirada. Vuelve a iniciar.");

            int aciertos, total, estrellas;
            try
            {
                (aciertos, total, estrellas) = modo switch
                {
                    "tienda" => EvaluarTienda(t, req.Respuestas),
                    "fisica" => EvaluarFisica(t, req.Respuestas),
                    _ => EvaluarFracciones(req.Respuestas)
                };
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {
                throw new EvaluacionInvalidaException("Las respuestas tienen un formato inválido.");
            }

            var transcurrido = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(t.EmitidoMs);
            if (transcurrido.TotalSeconds < total * MinSegundosPorRonda)
                throw new EvaluacionInvalidaException("El juego se envió demasiado rápido.");

            // Recompensa de la sesión (como en las evaluaciones: 10 por acierto + 50 si llega al 80%)
            long monedasSesion = aciertos * (long)MonedasPorAcierto
                + (total > 0 && aciertos * 100 >= total * UmbralBonusPct ? Bonus : 0);

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Solo se paga la mejora sobre lo ya cobrado en este modo ("superar el récord").
                // Reenviar el mismo resultado, o uno peor, paga 0.
                string prefijo = $"JUEGO_{modo.ToUpperInvariant()}_EST_{estudianteId}_";
                long yaPagado = await _context.TransaccionesMonedas
                    .Where(x => x.EstudianteId == estudianteId && x.IdempotencyKey.StartsWith(prefijo))
                    .SumAsync(x => (long?)x.Monto) ?? 0;

                long monedasGanadas = Math.Max(0, monedasSesion - yaPagado);

                var saldo = await _context.SaldoMonedas.FirstOrDefaultAsync(s => s.EstudianteId == estudianteId);
                if (monedasGanadas > 0)
                {
                    _context.TransaccionesMonedas.Add(new TransaccionMoneda
                    {
                        EstudianteId = estudianteId,
                        TipoTransaccionId = 1,   // mismo valor que usa EvaluacionService (confirmar que es el tipo correcto)
                        Monto = monedasGanadas,
                        IdempotencyKey = $"{prefijo}T_{t.EmitidoMs}",
                        CreadoEn = DateTimeOffset.UtcNow
                    });

                    if (saldo == null)
                    {
                        saldo = new SaldoMoneda
                        {
                            EstudianteId = estudianteId,
                            Saldo = monedasGanadas,
                            ActualizadoEn = DateTimeOffset.UtcNow
                        };
                        _context.SaldoMonedas.Add(saldo);
                    }
                    else
                    {
                        saldo.Saldo += monedasGanadas;
                        saldo.ActualizadoEn = DateTimeOffset.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();

                return new JuegoResultadoResponse
                {
                    Aciertos = aciertos,
                    Total = total,
                    Estrellas = estrellas,
                    MonedasGanadas = monedasGanadas,
                    NuevoSaldoMonedas = saldo?.Saldo ?? 0
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // ---------- Evaluadores (reglas idénticas a las del juego) ----------
        private static (int, int, int) EvaluarTienda(JuegoTicketPayload t, JsonElement respuestas)
        {
            var rondas = JsonSerializer.Deserialize<List<RondaTienda>>(t.Rondas, Json)!;
            var vueltos = respuestas.Deserialize<List<long?>>(Json) ?? new();

            int aciertos = 0;
            for (int i = 0; i < rondas.Count; i++)
            {
                if (i >= vueltos.Count || vueltos[i] is null) continue;
                int total = rondas[i].Lineas.Sum(l => l.Cantidad * PreciosTienda[l.Id]);
                if (vueltos[i] == rondas[i].Pago - total) aciertos++;
            }
            return (aciertos, rondas.Count, aciertos);
        }

        private static (int, int, int) EvaluarFisica(JuegoTicketPayload t, JsonElement respuestas)
        {
            var rondas = JsonSerializer.Deserialize<List<RondaFisica>>(t.Rondas, Json)!;
            var intentos = respuestas.Deserialize<List<List<IntentoFisica>>>(Json) ?? new();

            int aciertos = 0, estrellas = 0;
            for (int i = 0; i < rondas.Count; i++)
            {
                if (i >= intentos.Count || intentos[i] is null) continue;
                var ronda = rondas[i];
                int n = Math.Min(intentos[i].Count, MaxIntentosPorRonda);

                for (int k = 0; k < n; k++)
                {
                    var a = intentos[i][k];
                    if (!double.IsFinite(a.Velocidad) || !double.IsFinite(a.Angulo)) continue;

                    // Lo que fija la ronda manda sobre lo que diga el cliente
                    double v = ronda.VelocidadFija ?? Math.Clamp(Math.Round(a.Velocidad), 5, 20);
                    double ang = ronda.AnguloFijo ?? Math.Clamp(Math.Round(a.Angulo), 5, 85);

                    if (Math.Abs(Rango(v, ang) - ronda.Distancia) <= ToleranciaFisica + 1e-6)
                    {
                        aciertos++;
                        estrellas += MaxIntentosPorRonda - k;   // 3, 2 o 1
                        break;
                    }
                }
            }
            return (aciertos, rondas.Count, estrellas);
        }

        private static (int, int, int) EvaluarFracciones(JsonElement respuestas)
        {
            var niveles = respuestas.Deserialize<List<List<IntentoFraccion>>>(Json) ?? new();

            int aciertos = 0, estrellas = 0;
            for (int i = 0; i < NivelesFracciones.Length; i++)
            {
                if (i >= niveles.Count || niveles[i] is null) continue;
                var clientes = NivelesFracciones[i];
                int n = Math.Min(niveles[i].Count, MaxIntentosFraccion);

                for (int k = 0; k < n; k++)
                {
                    var intento = niveles[i][k];
                    if (!CortesPermitidos.Contains(intento.Partes) ||
                        intento.Duenos is null ||
                        intento.Duenos.Count != intento.Partes ||
                        intento.Duenos.Any(d => d < -1 || d >= clientes.Length))
                        continue;

                    // Cada cliente debe tener porciones/partes == num/den (equivalentes cuentan)
                    bool correcto = true;
                    for (int c = 0; c < clientes.Length && correcto; c++)
                    {
                        int recibidas = intento.Duenos.Count(d => d == c);
                        correcto = recibidas * clientes[c].Den == clientes[c].Num * intento.Partes;
                    }

                    if (correcto)
                    {
                        aciertos++;
                        estrellas += 3 - Math.Min(k, 2);
                        break;
                    }
                }
            }
            return (aciertos, NivelesFracciones.Length, estrellas);
        }
    }
}