using MongoDB.Driver;
using InkMath.AuthService.Models;

namespace InkMath.AuthService.Services
{
    public interface IAuditoriaService
    {
        Task RegistrarEventoAsync(long usuarioId, string accion, string descripcion);
        Task RegistrarEventoDetalladoAsync(LogAuditoria log);
    }

    public class AuditoriaService : IAuditoriaService
    {
        private readonly IMongoCollection<LogAuditoria> _coleccion;

        public AuditoriaService(IConfiguration config)
        {
            var client = new MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = client.GetDatabase(config["MongoSettings:DatabaseName"]);
            _coleccion = database.GetCollection<LogAuditoria>(config["MongoSettings:AuditoriaCollection"]);
        }

        public async Task RegistrarEventoAsync(long usuarioId, string accion, string descripcion)
        {
            var log = new LogAuditoria
            {
                FechaEvento = DateTime.UtcNow,
                Modulo = "SISTEMA",
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
    }
}
