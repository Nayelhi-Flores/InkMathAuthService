using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("tests_personalizados")]
    public class TestPersonalizado
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("maestro_id")]
        public long MaestroId { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("esta_activo")]
        public bool EstaActivo { get; set; } = true;

        [Column("creado_en")]
        public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

        public ICollection<PreguntaTest> Preguntas { get; set; } = new List<PreguntaTest>();
    }
}