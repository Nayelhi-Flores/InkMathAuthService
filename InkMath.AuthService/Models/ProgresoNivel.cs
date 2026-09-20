using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("progreso_nivel")]
    public class ProgresoNivel
    {
        [Column("estudiante_id")]
        public long EstudianteId { get; set; }

        [Column("nivel_id")]
        public long NivelId { get; set; }

        [Column("estado_id")]
        public long EstadoId { get; set; }

        [Column("puntaje")]
        public int Puntaje { get; set; }

        [Column("intentos")]
        public int Intentos { get; set; }

        [ForeignKey("NivelId")]
        public Nivel? Nivel { get; set; }

        [ForeignKey("EstadoId")]
        public EstadoProgreso? EstadoProgreso { get; set; }
    }
}
