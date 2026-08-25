using MongoDB.Driver;
using InkMath.AuthService.Models;

namespace InkMath.AuthService.Services
{
    public interface IAuditoriaService
    {
        Task RegistrarEventoAsync(long usuarioId, string accion, string descripcion);
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
                UsuarioId = usuarioId,
                Accion = accion,
                Descripcion = descripcion,
                Timestamp = DateTime.UtcNow
            };

            await _coleccion.InsertOneAsync(log);
        }
    }
}
