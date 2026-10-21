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
        private readonly IReporteService _reporteService;

        public EvaluacionesController(IEvaluacionService evaluacionService, IReporteService reporteService)
        {
            _evaluacionService = evaluacionService;
            _reporteService = reporteService;
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

        [HttpGet("estudiante/{estudianteId}/analisis")]
        public async Task<IActionResult> ObtenerAnalisisIndividual(long estudianteId)
        {
            var analisis = await _reporteService.ObtenerAnalisisIndividualAsync(estudianteId);
            if (analisis == null)
            {
                return NotFound(new { message = "Estudiante no encontrado." });
            }

            return Ok(analisis);
        }

        [HttpGet("maestro/{maestroId}/rezago")]
        [Authorize]
        public async Task<IActionResult> ObtenerAlertasRezago(long maestroId)
        {
            var alertas = await _reporteService.ObtenerAlertasRezagoPorMaestroAsync(maestroId);
            return Ok(alertas);
        }

        [HttpGet("aula/{aulaId}/exportar-csv")]
        [Authorize]
        public async Task<IActionResult> ExportarReporteAulaCsv(long aulaId)
        {
            var archivoBytes = await _reporteService.ExportarReporteAulaCsvAsync(aulaId);
            return File(archivoBytes, "text/csv", $"Reporte_Aula_{aulaId}_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}