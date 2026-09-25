using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("usuarios")]
    public class Usuario
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [Column("apellido")]
        public string Apellido { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("hash_contrasena")]
        public string HashContrasena { get; set; } = string.Empty;

        [Column("role_id")]
        public long RoleId { get; set; } = 3;

        [Column("creado_en")]
        public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

        [Column("esta_activo")]
        public bool EstaActivo { get; set; } = true;
    }
}
