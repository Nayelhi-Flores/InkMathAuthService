using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("aula_estudiante")]
    public class AulaEstudiante
    {
        [Column("aula_id")]
        public long AulaId { get; set; }

        [Column("estudiante_id")]
        public long EstudianteId { get; set; }

        [Column("fecha_inscripcion")]
        public DateTimeOffset FechaInscripcion { get; set; } = DateTimeOffset.UtcNow;
    }
}
