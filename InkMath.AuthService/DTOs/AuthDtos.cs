using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace InkMath.AuthService.DTOs
{
    public class RegistroDto
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public IFormFile DocumentoIdentidad { get; set; } = null!;

        [Required]
        public string MfaToken { get; set; } = string.Empty; // Token de validación (Captcha/MFA)
    }

    public class LoginDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
