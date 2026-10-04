using Azure;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ITokenService _tokenService;
        private readonly IAulaService _aulaService;

        public AuthController(IAuthService authService, ITokenService tokenService, IAulaService aulaService)
        {
            _authService = authService;
            _tokenService = tokenService;
            _aulaService = aulaService;
        }

        [EnableRateLimiting("RegisterLimiter")]
        [HttpPost("registro")]
        public async Task<IActionResult> Registrar([FromForm] RegistroDto dto)
        {
            var (exito, mensaje, usuarioId) = await _authService.RegistrarAsync(dto);
            if (!exito) return BadRequest(new { mensaje });

            return Ok(new { mensaje, usuarioId });
        }

        [EnableRateLimiting("RegisterLimiter")]
        [HttpPost("registro-express")]
        public async Task<IActionResult> RegistrarExpress([FromForm] RegistroExpressDto dto)
        {
            // Generar correo único basado en el nombre y un aleatorio corto
            string idUnico = Guid.NewGuid().ToString().Substring(0, 4);
            string emailTemp = $"alumno.{Guid.NewGuid():N}@inkmath.test";
            string passwordTemp = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "aA1!";

            var registroDto = new RegistroDto
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Email = emailTemp,
                Password = passwordTemp,
                RoleId = 3,             // Rol Estudiante
                AceptoTerminos = true,  // Aceptado automático para el piloto
                Website = ""
            };

            // Registrar el usuario en la base de datos
            var (exito, mensaje, usuarioId) = await _authService.RegistrarAsync(registroDto);
            if (!exito || !usuarioId.HasValue)
                return BadRequest(new { mensaje });

            // Si tiene un código de clase, se vincula a la sección del docente
            if (!string.IsNullOrWhiteSpace(dto.CodigoClase))
            {
                var (vinculoExito, vinculoMensaje) = await _aulaService.VincularAlumnoAClaseAsync(usuarioId.Value, dto.CodigoClase);

                if (!vinculoExito)
                {
                    return BadRequest(new { mensaje = $"Error al vincular con el aula: {vinculoMensaje}" });
                }
            }

            // Iniciar sesión automáticamente (Auto-login)
            var loginDto = new LoginDto
            {
                Email = emailTemp,
                Password = passwordTemp
            };

            var (loginExito, loginMensaje, usuario) = await _authService.LoginAsync(loginDto);
            if (!loginExito || usuario == null)
                return StatusCode(500, new { mensaje = "Se creó el usuario pero falló el inicio de sesión automático." });

            // Acreditar bono de bienvenida por primer inicio de sesión
            var (saldoActual, esPrimerLogin) = await _authService.AcreditarBonoBienvenidaAsync(usuario.Id);

            // Generar JWT Token y Cookie de Sesión
            var token = _tokenService.GenerarToken(usuario);

            EstablecerCookieSesion(token);

            // Retornar payload completo
            return Ok(new
            {
                mensaje = "Registro e inicio de sesión express exitoso",
                usuarioId = usuario.Id,
                nombre = usuario.Nombre,
                rolId = usuario.RoleId,
                saldoMonedas = saldoActual,
                esPrimerLogin
            });
        }

        [EnableRateLimiting("LoginLimiter")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var (exito, mensaje, usuario) = await _authService.LoginAsync(dto);
            if (!exito || usuario == null) return Unauthorized(new { mensaje });

            // Acredita el bono de bienvenida si es su primera vez
            var (saldoActual, esPrimerLogin) = await _authService.AcreditarBonoBienvenidaAsync(usuario.Id);

            var token = _tokenService.GenerarToken(usuario);

            EstablecerCookieSesion(token);

            return Ok(new
            {
                mensaje,
                usuarioId = usuario.Id,
                nombre = usuario.Nombre,
                rolId = usuario.RoleId,
                saldoMonedas = saldoActual,
                esPrimerLogin
            });
        }

        private void EstablecerCookieSesion(string token) =>
        Response.Cookies.Append("jwt_session", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt_session", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
            return Ok(new { mensaje = "Sesión cerrada correctamente." });
        }
    }
}
