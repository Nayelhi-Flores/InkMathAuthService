using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("suscripciones")]
    public class Suscripcion
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("institucion_id")]
        public long? InstitucionId { get; set; }

        [Column("usuario_id")]
        public long? UsuarioId { get; set; }

        [Column("plan_id")]
        public long PlanId { get; set; }

        [Column("estado_suscripcion_id")]
        public long EstadoSuscripcionId { get; set; }

        [Column("fecha_inicio")]
        public DateTimeOffset FechaInicio { get; set; }

        [Column("fecha_fin")]
        public DateTimeOffset FechaFin { get; set; }

        [Column("creado_en")]
        public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    }
}