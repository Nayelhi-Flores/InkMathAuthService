using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("aula_tests")]
    public class AulaTest
    {
        [Column("aula_id")]
        public long AulaId { get; set; }

        [Column("test_id")]
        public long TestId { get; set; }

        [Column("fecha_asignacion")]
        public DateTimeOffset FechaAsignacion { get; set; } = DateTimeOffset.UtcNow;
    }
}