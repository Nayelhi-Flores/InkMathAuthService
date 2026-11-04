using MongoDB.Driver;
using InkMath.AuthService.Models;

namespace InkMath.AuthService.Services
{
    public interface IAuditoriaService
    {
        Task RegistrarEventoAsync(long usuarioId, string accion, string descripcion, string modulo = "SISTEMA");
        Task RegistrarEventoDetalladoAsync(LogAuditoria log);
        Task<List<LogAuditoria>> ObtenerLogsPorRangoAsync(DateOnly? fechaInicio, DateOnly? fechaFin);
    }

    public class AuditoriaService : IAuditoriaService
    {
        // Guatemala no usa horario de verano: UTC-6 fijo
        private static readonly TimeSpan OffsetGt = TimeSpan.FromHours(-6);

        private readonly IMongoCollection<LogAuditoria> _coleccion;

        public AuditoriaService(IConfiguration config)
        {
            var client = new MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = client.GetDatabase(config["MongoSettings:DatabaseName"]);
            _coleccion = database.GetCollection<LogAuditoria>(config["MongoSettings:AuditoriaCollection"]);
        }

        public async Task RegistrarEventoAsync(long usuarioId, string accion, string descripcion, string modulo = "SISTEMA")
        {
            var log = new LogAuditoria
            {
                FechaEvento = DateTime.UtcNow,
                Modulo = modulo,
                Accion = accion,
                EntidadAfectadaId = usuarioId.ToString(),
                JwtUsuarioEjecutor = usuarioId.ToString(),
                RolAuth = "Usuario",
                JustificacionCambio = descripcion,
                DatosAnteriores = null,
                DatosNuevos = null
            };

            await _coleccion.InsertOneAsync(log);
        }

        public async Task RegistrarEventoDetalladoAsync(LogAuditoria log)
        {
            try
            {
                await _coleccion.InsertOneAsync(log);
            }
            catch (Exception ex)
            {
                // Fail-safe: Evitamos que una falla de Mongo afecte al usuario
                Console.WriteLine($"[Auditoria Error]: No se pudo registrar el log en Mongo: {ex.Message}");
            }
        }

        public async Task<List<LogAuditoria>> ObtenerLogsPorRangoAsync(DateOnly? fechaInicio, DateOnly? fechaFin)
        {
            try
            {
                // "Hoy" según el día de Guatemala
                var hoy = DateOnly.FromDateTime(DateTime.UtcNow + OffsetGt);

                // Sin fechas = hoy. Con una sola fecha = solo ese día.
                var desde = fechaInicio ?? fechaFin ?? hoy;
                var hasta = fechaFin ?? fechaInicio ?? hoy;
                if (hasta < desde) (desde, hasta) = (hasta, desde);

                // Límites del rango en hora de Guatemala, convertidos a UTC (como se guarda en Mongo)
                var inicioUtc = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), OffsetGt).UtcDateTime;
                var finUtc = new DateTimeOffset(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), OffsetGt).UtcDateTime;

                var filter = Builders<LogAuditoria>.Filter.And(
                    Builders<LogAuditoria>.Filter.Gte(l => l.FechaEvento, inicioUtc),
                    Builders<LogAuditoria>.Filter.Lt(l => l.FechaEvento, finUtc) // límite superior exclusivo
                );

                return await _coleccion
                    .Find(filter)
                    .SortByDescending(l => l.FechaEvento)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auditoria Error]: No se pudieron leer los logs de Mongo: {ex.Message}");
                return new List<LogAuditoria>();
            }
        }
    }
}