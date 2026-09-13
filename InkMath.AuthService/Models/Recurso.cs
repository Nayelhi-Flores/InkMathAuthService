using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("recursos")]
    public class Recurso
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("maestro_id")]
        public long MaestroId { get; set; }

        [Required]
        [Column("tipo_recurso_id")]
        public long TipoRecursoId { get; set; }

        [Required]
        [Column("titulo")]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [Column("referencia")]
        public string Referencia { get; set; } = string.Empty;

        [Column("creado_en")]
        public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

        // Propiedad de navegación
        [ForeignKey(nameof(TipoRecursoId))]
        public virtual TipoRecurso? TipoRecurso { get; set; }
    }
}