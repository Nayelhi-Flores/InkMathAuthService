using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("aulas")]
    public class Aula
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("codigo_acceso")]
        public string CodigoAcceso { get; set; } = string.Empty;

        [Required]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("maestro_id")]
        public long MaestroId { get; set; }

        [Column("institucion_id")]
        public long? InstitucionId { get; set; }

        [Column("creado_en")]
        public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    }
}