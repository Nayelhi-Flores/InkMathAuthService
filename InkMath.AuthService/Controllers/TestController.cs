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

        [HttpDelete("{id:long}")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> AnularTest(long id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            long.TryParse(userIdClaim, out long usuarioId);

            var exito = await _testService.AnularTestAsync(id, usuarioId);
            if (!exito)
            {
                return NotFound(new { mensaje = "El test especificado no existe o ya fue anulado." });
            }

            return Ok(new { mensaje = $"Test {id} anulado correctamente y evento auditado." });
        }
    }
}