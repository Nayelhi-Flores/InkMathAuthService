using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("niveles")]
    public class Nivel
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("titulo")]
        public string Titulo { get; set; } = string.Empty;

        [Column("orden")]
        public int Orden { get; set; }

        [Column("esta_activo")]
        public bool EstaActivo { get; set; }

        [Column("test_id")]
        public long? TestId { get; set; }

        [Column("categoria_id")]
        public long? CategoriaId { get; set; }

        [Column("seccion_id")]
        public long? SeccionId { get; set; }

        [ForeignKey("TestId")]
        public TestPersonalizado? Test { get; set; }

        [ForeignKey("CategoriaId")]
        public CategoriaNivel? Categoria { get; set; }

        [ForeignKey("SeccionId")]
        public Seccion? Seccion { get; set; }

        public ICollection<ProgresoNivel> Progresos { get; set; } = new List<ProgresoNivel>();
    }
}
