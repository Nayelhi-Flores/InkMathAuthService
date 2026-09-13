using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("preguntas_test")]
    public class PreguntaTest
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("test_id")]
        public long TestId { get; set; }

        [Column("tipo_pregunta_id")]
        public long TipoPreguntaId { get; set; }

        [Column("pregunta")]
        public string Pregunta { get; set; } = string.Empty;

        [ForeignKey(nameof(TestId))]
        public TestPersonalizado? Test { get; set; }

        public ICollection<OpcionPregunta> Opciones { get; set; } = new List<OpcionPregunta>();
    }
}