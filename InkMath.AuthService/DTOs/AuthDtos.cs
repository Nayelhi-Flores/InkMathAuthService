using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace InkMath.AuthService.DTOs
{
    public class RegistroDto
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        public string Apellido { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Range(2, 3, ErrorMessage = "El rol seleccionado no es válido")]
        public int RoleId { get; set; } = 3;

        [Range(typeof(bool), "true", "true", ErrorMessage = "Debes aceptar los Términos y Condiciones.")]
        public bool AceptoTerminos { get; set; }

        public string? Website { get; set; }
    }

    public class RegistroExpressDto
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        public string Apellido { get; set; } = string.Empty;

        public string? CodigoClase { get; set; }
    }

    public class LoginDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
