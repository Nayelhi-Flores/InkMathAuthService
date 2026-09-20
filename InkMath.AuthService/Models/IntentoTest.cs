using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("intentos_test")]
    public class IntentoTest
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("test_id")]
        public long TestId { get; set; }

        [Column("estudiante_id")]
        public long EstudianteId { get; set; }

        [Column("puntaje_obtenido")]
        public int PuntajeObtenido { get; set; }

        [Column("fecha_inicio")]
        public DateTimeOffset FechaInicio { get; set; }

        [Column("fecha_fin")]
        public DateTimeOffset FechaFin { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<RespuestaEstudiante> Respuestas { get; set; } = new List<RespuestaEstudiante>();
    }
}