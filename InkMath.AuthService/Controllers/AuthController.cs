using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditoriaService _auditoriaService;
        private readonly IWebHostEnvironment _environment;

        public AuthController(ApplicationDbContext context, IAuditoriaService auditoriaService, IWebHostEnvironment environment)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _environment = environment;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> Registrar([FromForm] RegistroDto dto)
        {
            // 1. Validar Captcha/MFA
            if (string.IsNullOrWhiteSpace(dto.MfaToken))
            {
                return BadRequest(new { mensaje = "Falta la validación de seguridad requerida (MFA/Captcha)." });
            }

            // 2. Validar duplicidad en SQL Server
            if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { mensaje = "El correo ya está registrado." });
            }

            // 3. Validación estricta de tipo de archivo (PDF o JPEG exclusivamente)
            var extension = Path.GetExtension(dto.DocumentoIdentidad.FileName).ToLowerInvariant();
            var extensionesValidas = new[] { ".pdf", ".jpg", ".jpeg" };
            var mimeTypesValidos = new[] { "application/pdf", "image/jpeg", "image/pjpeg" };

            if (!extensionesValidas.Contains(extension) || !mimeTypesValidos.Contains(dto.DocumentoIdentidad.ContentType.ToLower()))
            {
                return BadRequest(new { mensaje = "Error de validación: Únicamente se aceptan documentos en formato PDF o JPEG." });
            }

            if (!ValidarMagicNumbers(dto.DocumentoIdentidad))
            {
                return BadRequest(new { mensaje = "El archivo enviado no coincide con la firma binaria de un PDF o JPEG válido (Posible amenaza de seguridad)." });
            }

            // 4. Almacenamiento seguro del archivo
            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.DocumentoIdentidad.CopyToAsync(stream);
            }

            // 5. Hashing estricto de contraseña (BCrypt)
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // 6. Transacción a SQL Server
            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Email = dto.Email,
                HashContrasena = passwordHash,
                RoleId = 3
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            // 7. Registro de Bitácora Asíncrona en MongoDB
            _ = _auditoriaService.RegistrarEventoAsync(
                usuario.Id,
                "CREACION_USUARIO",
                $"Usuario registrado exitosamente. Archivo almacenado: {fileName}"
            );

            return Ok(new { mensaje = "Registro completado con éxito.", usuarioId = usuario.Id });
        }
        [EnableRateLimiting("LoginLimiter")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == dto.Email);

            // Verificación segura con BCrypt
            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.HashContrasena))
            {
                long idLog = usuario?.Id ?? 0;
                _ = _auditoriaService.RegistrarEventoAsync(
                    idLog,
                    "INTENTO_FALLIDO_LOGIN",
                    $"Credenciales inválidas para el correo: {dto.Email}"
                );

                return Unauthorized(new { mensaje = "Credenciales incorrectas." });
            }

            // Registro en auditoría de éxito
            _ = _auditoriaService.RegistrarEventoAsync(
                usuario.Id,
                "LOGIN_EXITOSO",
                "Inicio de sesión correcto."
            );

            return Ok(new { mensaje = "Autenticación exitosa.", usuarioId = usuario.Id, nombre = usuario.Nombre });
        }

        private bool ValidarMagicNumbers(IFormFile archivo)
        {
            using var stream = archivo.OpenReadStream();
            using var reader = new BinaryReader(stream);
            var headerBytes = reader.ReadBytes(4);

            stream.Position = 0;

            if (headerBytes.Length < 4) return false;

            // Magic Number para PDF (%PDF -> 0x25, 0x50, 0x44, 0x46)
            bool esPdf = headerBytes[0] == 0x25 && headerBytes[1] == 0x50 &&
                         headerBytes[2] == 0x44 && headerBytes[3] == 0x46;

            // Magic Number para JPEG (0xFF, 0xD8, 0xFF)
            bool esJpeg = headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF;

            return esPdf || esJpeg;
        }
    }
}
