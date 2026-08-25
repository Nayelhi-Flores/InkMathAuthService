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

        public AuthController(IAuthService authService, ITokenService tokenService)
        {
            _authService = authService;
            _tokenService = tokenService;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> Registrar([FromForm] RegistroDto dto)
        {
            var (exito, mensaje, usuarioId) = await _authService.RegistrarAsync(dto);
            if (!exito) return BadRequest(new { mensaje });

            return Ok(new { mensaje, usuarioId });
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
