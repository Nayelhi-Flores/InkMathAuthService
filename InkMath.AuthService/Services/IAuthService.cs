using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace InkMath.AuthService.Services
{
    public interface IAuthService
    {
        Task<(bool Exito, string Mensaje, Usuario? Usuario)> LoginAsync(LoginDto dto);
        Task<(bool Exito, string Mensaje, long? UsuarioId)> RegistrarAsync(RegistroDto dto);
    }

    public class AuthService : IAuthService
    {
        private readonly AplicationDbContext _context;
        private readonly IAuditoriaService _auditoriaService;
        private readonly IWebHostEnvironment _environment;

        public AuthService(AplicationDbContext context, IAuditoriaService auditoriaService, IWebHostEnvironment environment)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _environment = environment;
        }

        public async Task<(bool Exito, string Mensaje, Usuario? Usuario)> LoginAsync(LoginDto dto)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.HashContrasena))
            {
                long idLog = usuario?.Id ?? 0;
                _ = _auditoriaService.RegistrarEventoAsync(idLog, "INTENTO_FALLIDO_LOGIN", $"Credenciales inválidas: {dto.Email}");
                return (false, "Credenciales incorrectas.", null);
            }

            _ = _auditoriaService.RegistrarEventoAsync(usuario.Id, "LOGIN_EXITOSO", "Inicio de sesión correcto.");
            return (true, "Autenticación exitosa.", usuario);
        }

        public async Task<(bool Exito, string Mensaje, long? UsuarioId)> RegistrarAsync(RegistroDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.MfaToken))
                return (false, "Falta la validación de seguridad requerida (MFA/Captcha).", null);

            if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
                return (false, "El correo ya está registrado.", null);

            var extension = Path.GetExtension(dto.DocumentoIdentidad.FileName).ToLowerInvariant();
            var extensionesValidas = new[] { ".pdf", ".jpg", ".jpeg" };
            var mimeTypesValidos = new[] { "application/pdf", "image/jpeg", "image/pjpeg" };

            if (!extensionesValidas.Contains(extension) || !mimeTypesValidos.Contains(dto.DocumentoIdentidad.ContentType.ToLower()))
                return (false, "Error de validación: Únicamente se aceptan documentos PDF o JPEG.", null);

            if (!ValidarMagicNumbers(dto.DocumentoIdentidad))
                return (false, "El archivo enviado no coincide con la firma binaria requerida.", null);

            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.DocumentoIdentidad.CopyToAsync(stream);
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Email = dto.Email,
                HashContrasena = passwordHash,
                RoleId = 3,
                CreadoEn = DateTimeOffset.UtcNow
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            _ = _auditoriaService.RegistrarEventoAsync(usuario.Id, "CREACION_USUARIO", $"Usuario registrado: {fileName}");

            return (true, "Registro completado con éxito.", usuario.Id);
        }

        private bool ValidarMagicNumbers(IFormFile archivo)
        {
            using var stream = archivo.OpenReadStream();
            using var reader = new BinaryReader(stream);
            var headerBytes = reader.ReadBytes(4);
            stream.Position = 0;

            if (headerBytes.Length < 4) return false;

            bool esPdf = headerBytes[0] == 0x25 && headerBytes[1] == 0x50 && headerBytes[2] == 0x44 && headerBytes[3] == 0x46;
            bool esJpeg = headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF;

            return esPdf || esJpeg;
        }
    }
}
