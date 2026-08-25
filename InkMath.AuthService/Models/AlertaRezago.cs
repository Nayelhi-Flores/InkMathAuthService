using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("alertas_rezago")]
    public class AlertaRezago
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("estudiante_id")]
        public long EstudianteId { get; set; }

        [Column("aula_id")]
        public long AulaId { get; set; }

        [Required]
        [Column("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [Column("resuelto")]
        public bool Resuelto { get; set; } = false;

        [Column("creado_en")]
        public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    }
}