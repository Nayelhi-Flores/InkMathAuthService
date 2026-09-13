using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("estados_suscripcion")]
    public class EstadoSuscripcion
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;
    }
}
