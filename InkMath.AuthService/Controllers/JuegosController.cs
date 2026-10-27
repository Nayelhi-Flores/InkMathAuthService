using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "3")]
    public class JuegosController : ControllerBase
    {
        private readonly IJuegoService _juegoService;

        public JuegosController(IJuegoService juegoService)
        {
            _juegoService = juegoService;
        }

        private bool TryObtenerEstudianteId(out long id) =>
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id) && id > 0;

        // modo: tienda | fisica | fracciones
        [HttpPost("{modo}/iniciar")]
        public IActionResult Iniciar(string modo)
        {
            if (!TryObtenerEstudianteId(out long estudianteId))
                return Unauthorized(new { mensaje = "Token no válido." });
            try
            {
                return Ok(_juegoService.Iniciar(modo, estudianteId));
            }
            catch (EvaluacionInvalidaException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        [HttpPost("{modo}/resultado")]
        [Consumes("application/json")]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> Resultado(string modo, [FromBody] JuegoResultadoRequest req)
        {
            if (!TryObtenerEstudianteId(out long estudianteId))
                return Unauthorized(new { mensaje = "Token no válido." });
            try
            {
                return Ok(await _juegoService.FinalizarAsync(modo, estudianteId, req));
            }
            catch (EvaluacionInvalidaException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
            catch (DbUpdateException)
            {
                return Conflict(new { mensaje = "No se pudo registrar el resultado. Inténtalo de nuevo." });
            }
        }
    }
}