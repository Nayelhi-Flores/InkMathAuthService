using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("respuestas_estudiante")]
    public class RespuestaEstudiante
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("intento_id")]
        [ForeignKey("Intento")]
        public long IntentoId { get; set; }

        [Column("pregunta_id")]
        public long PreguntaId { get; set; }

        [Column("opcion_id")]
        public long? OpcionId { get; set; }

        [Column("respuesta_texto")]
        public string? RespuestaTexto { get; set; }
    }
}