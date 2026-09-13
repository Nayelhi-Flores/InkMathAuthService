using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("opciones_pregunta")]
    public class OpcionPregunta
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("pregunta_id")]
        public long PreguntaId { get; set; }

        [Column("texto_opcion")]
        public string TextoOpcion { get; set; } = string.Empty;

        [Column("es_correcta")]
        public bool EsCorrecta { get; set; }

        [ForeignKey(nameof(PreguntaId))]
        public PreguntaTest? Pregunta { get; set; }
    }
}