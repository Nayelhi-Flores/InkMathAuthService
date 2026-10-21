using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TestsController : ControllerBase
    {
        private readonly ITestService _testService;

        public TestsController(ITestService testService)
        {
            _testService = testService;
        }

        [HttpPost]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> CrearTest([FromBody] CrearTestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nombre) || dto.Preguntas.Count == 0)
            {
                return BadRequest(new { mensaje = "El test debe tener un nombre y al menos una pregunta." });
            }

            if (dto.MaestroId <= 0)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (long.TryParse(userIdClaim, out long userId))
                {
                    dto.MaestroId = userId;
                }
            }

            var resultado = await _testService.CrearTestTransaccionalAsync(dto);
            return CreatedAtAction(nameof(CrearTest), new { id = resultado.TestId }, resultado);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> ActualizarTest(long id, [FromBody] CrearTestDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var resultado = await _testService.ActualizarTestAsync(id, dto);
            if (!resultado)
                return NotFound(new { mensaje = "El test especificado no fue encontrado." });

            return Ok(new { mensaje = "Test actualizado exitosamente." });
        }

        [HttpDelete("{id:long}")]
        [Authorize(Roles = "1")] // Exclusivo Administrador
        public async Task<IActionResult> AnularTest(long id, [FromBody] AnularTestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Justificacion))
            {
                return BadRequest(new { mensaje = "Debe proporcionar una justificación válida para anular el test." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            long.TryParse(userIdClaim, out long usuarioId);

            var nombreUsuario = User.FindFirst(ClaimTypes.Name)?.Value ?? "Administrador";
            var rolAuth = "Administrador";

            var exito = await _testService.AnularTestAsync(id, usuarioId, nombreUsuario, rolAuth, dto.Justificacion);

            if (!exito)
            {
                return NotFound(new { mensaje = "El test especificado no existe o ya fue anulado." });
            }

            return Ok(new { mensaje = $"Test {id} anulado correctamente y evento auditado." });
        }

        [HttpPut("{testId}/preguntas/{preguntaId}/estado")]
        [Authorize(Roles = "1,2")] // Maestro o Administrador
        public async Task<IActionResult> CambiarEstadoPregunta(
            [FromRoute] long testId,
            [FromRoute] long preguntaId,
            [FromQuery] bool estaActiva,
            [FromQuery] long maestroId = 20002)
        {
            var resultado = await _testService.CambiarEstadoPreguntaAsync(testId, preguntaId, estaActiva, maestroId);

            if (!resultado)
            {
                return NotFound(new { mensaje = "La pregunta o el test especificado no existe o no pertenece al maestro." });
            }

            return Ok(new { mensaje = $"Estado de la pregunta {preguntaId} actualizado a {(estaActiva ? "activa (1)" : "inactiva (0)")} exitosamente." });
        }

        [HttpPost("asignar-test")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> AsignarTestAAulas([FromBody] AsignarTestAulaDto dto)
        {
            if (dto == null || dto.TestIds == null || !dto.TestIds.Any() || dto.AulaIds == null || !dto.AulaIds.Any())
            {
                return BadRequest(new { mensaje = "Debe proporcionar al menos un test válido y al menos un aula válida." });
            }

            // Extraer el ID del usuario desde las Claims del JWT
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out long usuarioId))
            {
                return Unauthorized(new { mensaje = "Usuario no autenticado correctamente." });
            }

            var (exito, mensaje) = await _testService.AsignarTestsAAulasAsync(usuarioId, dto.TestIds, dto.AulaIds);

            if (!exito)
            {
                return BadRequest(new { mensaje });
            }

            return Ok(new { mensaje = mensaje ?? "Asignación realizada exitosamente." });
        }

        [HttpGet("mis-tests")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> ObtenerMisTests()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out long maestroId))
            {
                return Unauthorized(new { mensaje = "Usuario no válido." });
            }

            var tests = await _testService.ObtenerTestsPorMaestroAsync(maestroId);
            return Ok(new { datos = tests });
        }

        [HttpGet("paginados")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> ObtenerPaginados([FromQuery] ConsultaTestsPaginadaDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (!long.TryParse(userIdClaim, out long maestroId))
            {
                return Unauthorized(new { mensaje = "Usuario no autenticado." });
            }

            var resultado = await _testService.ObtenerTestsPaginadosPorMaestroAsync(maestroId, dto);
            return Ok(resultado);
        }
    }
}