using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("aula_estudiante")]
    public class AulaEstudiante
    {
        [Column("aula_id")]
        public long AulaId { get; set; }
        public Aula Aula { get; set; } = null!;

        [Column("estudiante_id")]
        public long EstudianteId { get; set; }

        [Column("fecha_inscripcion")]
        public DateTimeOffset FechaInscripcion { get; set; } = DateTimeOffset.UtcNow;

        [Column("esta_activo")]
        public bool EstaActivo { get; set; } = true;

        [Column("fecha_baja")]
        public DateTimeOffset? FechaBaja { get; set; }
    }
}
