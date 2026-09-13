using Microsoft.AspNetCore.Mvc;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.RateLimiting;

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
            string emailTemp = $"{dto.Nombre.ToLower().Replace(" ", "")}.{idUnico}@inkmath.test";

            var registroDto = new RegistroDto
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Email = emailTemp,
                Password = "AlumnoPrueba2026!",   // Contraseña genérica válida
                RoleId = 3,                       // Rol Estudiante
                AceptoTerminos = true,            // Aceptado automático para la prueba
                Website = ""
            };

            var (exito, mensaje, usuarioId) = await _authService.RegistrarAsync(registroDto);

            if (!exito)
                return BadRequest(new { mensaje });

            // Si tiene un código de clase, se vincula a la sección del docente
            if (!string.IsNullOrWhiteSpace(dto.CodigoClase))
            {
                var (vinculoExito, vinculoMensaje) = await _aulaService.VincularAlumnoAClaseAsync(usuarioId.Value, dto.CodigoClase);
            }

            return Ok(new { mensaje = "Registro express exitoso", usuarioId, email = emailTemp });
        }

        [EnableRateLimiting("LoginLimiter")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var (exito, mensaje, usuario) = await _authService.LoginAsync(dto);
            if (!exito || usuario == null) return Unauthorized(new { mensaje });

            var token = _tokenService.GenerarToken(usuario);

            Response.Cookies.Append("jwt_session", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(8)
            });

            return Ok(new { mensaje, usuarioId = usuario.Id, nombre = usuario.Nombre, token });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt_session");
            return Ok(new { mensaje = "Sesión cerrada correctamente." });
        }
    }
}
