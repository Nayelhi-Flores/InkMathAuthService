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
        Task<(long saldoActual, bool esPrimerLogin)> AcreditarBonoBienvenidaAsync(long estudianteId);
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
            if (!dto.AceptoTerminos)
                return (false, "Debes aceptar los Términos y Condiciones.", null);

            if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
                return (false, "El correo ya está registrado.", null);

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var usuario = new Usuario
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Email = dto.Email,
                HashContrasena = passwordHash,
                RoleId = dto.RoleId,
                CreadoEn = DateTimeOffset.UtcNow
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            _ = _auditoriaService.RegistrarEventoAsync(usuario.Id, "CREACION_USUARIO", $"Usuario registrado: {usuario.Email}");

            return (true, "Registro completado con éxito.", usuario.Id);
        }

        public async Task<(long saldoActual, bool esPrimerLogin)> AcreditarBonoBienvenidaAsync(long estudianteId)
        {
            var saldoObj = await _context.SaldoMonedas
                .FirstOrDefaultAsync(s => s.EstudianteId == estudianteId);

            const long MONEDAS_BIENVENIDA = 300;

            // Si el estudiante no tiene registro en la tabla de monedas, se crea su saldo inicial
            if (saldoObj == null)
            {
                saldoObj = new SaldoMoneda
                {
                    EstudianteId = estudianteId,
                    Saldo = MONEDAS_BIENVENIDA,
                    ActualizadoEn = DateTimeOffset.UtcNow
                };

                _context.SaldoMonedas.Add(saldoObj);
                await _context.SaveChangesAsync();

                return (saldoObj.Saldo, true);
            }

            return (saldoObj.Saldo, false);
        }
    }
}
