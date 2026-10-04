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
    public class EvaluacionesController : ControllerBase
    {
        private readonly IEvaluacionService _evaluacionService;

        public EvaluacionesController(IEvaluacionService evaluacionService)
        {
            _evaluacionService = evaluacionService;
        }

        private bool TryObtenerEstudianteId(out long id) =>
            long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id) && id > 0;

        [HttpPost("resultados")]
        [Consumes("application/json")]
        [RequestSizeLimit(64 * 1024)]
        public async Task<IActionResult> RegistrarResultado([FromBody] RegistrarIntentoTestDto dto)
        {
            if (!TryObtenerEstudianteId(out long estudianteId))
                return Unauthorized(new { mensaje = "Token no válido o sin identificador de estudiante." });

            dto.EstudianteId = estudianteId; // nunca se confía en el valor del cuerpo

            try
            {
                var resultado = await _evaluacionService.ProcesarIntentoTestAsync(dto);
                return Ok(resultado);
            }
            catch (EvaluacionInvalidaException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
            catch (DbUpdateException)
            {
                // Conflicto de concurrencia (transacción serializable): el cliente puede reintentar
                return Conflict(new { mensaje = "No se pudo registrar el intento. Inténtalo de nuevo." });
            }
        }

        [HttpGet("test/{testId:long}")]
        public async Task<IActionResult> ObtenerPreguntasTest(long testId, [FromQuery] int limitePreguntas = 15)
        {
            if (!TryObtenerEstudianteId(out long estudianteId))
                return Unauthorized(new { mensaje = "Token no válido o sin identificador de estudiante." });

            var resultado = await _evaluacionService.ObtenerEvaluacionPorTestIdAsync(testId, estudianteId, limitePreguntas);
            if (resultado == null)
                return NotFound(new { mensaje = "El test solicitado no existe o no está activo." });

            return Ok(resultado);
        }
    }
}