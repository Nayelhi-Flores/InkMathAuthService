using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("secciones")]
    public class Seccion
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("orden")]
        public int Orden { get; set; }

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        public ICollection<Nivel> Niveles { get; set; } = new List<Nivel>();
    }
}
