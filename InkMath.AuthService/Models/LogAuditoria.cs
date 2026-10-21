using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InkMath.AuthService.Models
{
    public class LogAuditoria
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("fecha_evento")]
        public DateTime FechaEvento { get; set; } = DateTime.UtcNow;

        [BsonElement("modulo")]
        public string Modulo { get; set; } = string.Empty;

        [BsonElement("accion")]
        public string Accion { get; set; } = string.Empty;

        [BsonElement("entidad_afectada_id")]
        public string EntidadAfectadaId { get; set; } = string.Empty;

        [BsonElement("jwt_usuario_ejecutor")]
        public string JwtUsuarioEjecutor { get; set; } = string.Empty;

        [BsonElement("rol_auth")]
        public string RolAuth { get; set; } = string.Empty;

        [BsonElement("justificacion_cambio")]
        public string JustificacionCambio { get; set; } = string.Empty;

        [BsonElement("datos_anteriores")]
        public object? DatosAnteriores { get; set; }

        [BsonElement("datos_nuevos")]
        public object? DatosNuevos { get; set; }
    }
}