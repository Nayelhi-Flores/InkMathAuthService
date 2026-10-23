using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    namespace InkMath.AuthService.Models
    {
        [Table("interacciones_chat")]
        public class InteraccionChat
        {
            [Key]
            [Column("id")]
            public long Id { get; set; }

            [Required]
            [Column("aula_id")]
            public long AulaId { get; set; }

            [Required]
            [Column("estudiante_id")]
            public long EstudianteId { get; set; }

            [Required]
            [Column("contenido")]
            public string Contenido { get; set; } = string.Empty;

            [Column("es_emoji")]
            public bool EsEmoji { get; set; }

            [Column("enviado_en")]
            public DateTimeOffset EnviadoEn { get; set; } = DateTimeOffset.UtcNow;

            // Propiedades de navegación opcionales
            [ForeignKey(nameof(AulaId))]
            public virtual Aula? Aula { get; set; }

            [ForeignKey(nameof(EstudianteId))]
            public virtual Usuario? Estudiante { get; set; }

            [Column("mensaje_padre_id")]
            public long? MensajePadreId { get; set; }

            [Column("votos_utiles")]
            public int VotosUtiles { get; set; } = 0;

            [Column("esta_reportado")]
            public bool EstaReportado { get; set; } = false;

            // Propiedades de navegación
            [ForeignKey(nameof(MensajePadreId))]
            public virtual InteraccionChat? MensajePadre { get; set; }

            public virtual ICollection<InteraccionChat> Respuestas { get; set; } = new List<InteraccionChat>();
            [Column("esta_activo")]
            public bool EstaActivo { get; set; } = true;
        }
    }
}
