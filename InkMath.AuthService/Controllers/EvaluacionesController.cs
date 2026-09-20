using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("resultados")]
        public async Task<IActionResult> RegistrarResultado([FromBody] RegistrarIntentoTestDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out long estudianteId))
            {
                return Unauthorized(new { mensaje = "Token no válido o sin identificador de estudiante." });
            }

            dto.EstudianteId = estudianteId;

            var resultado = await _evaluacionService.ProcesarIntentoTestAsync(dto);
            return Ok(resultado);
        }

        [HttpGet("test/{testId}")]
        public async Task<IActionResult> ObtenerPreguntasTest(long testId, [FromQuery] int limitePreguntas = 15)
        {
            var resultado = await _evaluacionService.ObtenerEvaluacionPorTestIdAsync(testId, limitePreguntas);

            if (resultado == null)
            {
                return NotFound(new { mensaje = "El test solicitado no existe o no está activo." });
            }

            return Ok(resultado);
        }
    }
}