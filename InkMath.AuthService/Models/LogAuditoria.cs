using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InkMath.AuthService.Models
{
    public class LogAuditoria
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("usuario_id")]
        public long UsuarioId { get; set; }

        [BsonElement("accion")]
        public string Accion { get; set; } = string.Empty;

        [BsonElement("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
