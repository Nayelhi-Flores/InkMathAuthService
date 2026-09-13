using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("aulas_recursos")]
    public class AulaRecurso
    {
        [Column("aula_id")]
        public long AulaId { get; set; }
        public Aula Aula { get; set; } = null!;

        [Column("recurso_id")]
        public long RecursoId { get; set; }
        public Recurso Recurso { get; set; } = null!;

        [Column("asignado_en")]
        public DateTime AsignadoEn { get; set; } = DateTime.UtcNow;
    }
}
