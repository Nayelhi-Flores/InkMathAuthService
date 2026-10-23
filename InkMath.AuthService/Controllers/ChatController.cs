using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación JWT
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("arrecife/enviar")]
        [Authorize(Roles = "3")] // Exclusivo para Estudiantes
        public async Task<IActionResult> EnviarMensajeArrecife([FromBody] EnviarInteraccionDto dto)
        {
            long estudianteId = ObtenerUsuarioIdDesdeClaim();
            if (estudianteId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje, data) = await _chatService.EnviarMensajeArrecifeAsync(estudianteId, dto);

            if (!exito)
                return BadRequest(new { mensaje });

            return Ok(new
            {
                mensaje,
                data
            });
        }

        [HttpGet("arrecife/aula/{aulaId:long}")]
        [Authorize(Roles = "2,3")] // Maestros y Estudiantes del aula
        public async Task<IActionResult> ObtenerMensajesAula(long aulaId)
        {
            long usuarioId = ObtenerUsuarioIdDesdeClaim();
            if (usuarioId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var mensajes = await _chatService.ObtenerMensajesAulaAsync(aulaId, usuarioId);

            return Ok(mensajes);
        }

        private long ObtenerUsuarioIdDesdeClaim()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

            return long.TryParse(claimId, out long usuarioId) ? usuarioId : 0;
        }

        [HttpPost("arrecife/{mensajeId:long}/votar-util")]
        [Authorize(Roles = "2,3")]
        public async Task<IActionResult> VotarMensajeUtil(long mensajeId)
        {
            long usuarioId = ObtenerUsuarioIdDesdeClaim();
            if (usuarioId == 0) return Unauthorized();

            var (exito, mensaje) = await _chatService.VotarMensajeComoUtilAsync(usuarioId, mensajeId);
            if (!exito) return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpDelete("arrecife/{mensajeId:long}")]
        [Authorize(Roles = "3")] // Estudiante
        public async Task<IActionResult> EliminarMensajePropio(long mensajeId)
        {
            long estudianteId = ObtenerUsuarioIdDesdeClaim();
            string nombreUsuario = User.Identity?.Name ?? estudianteId.ToString();
            string rolAuth = User.FindFirst(ClaimTypes.Role)?.Value ?? "Estudiante";

            var (exito, mensaje) = await _chatService.EliminarMensajePropioAsync(estudianteId, nombreUsuario, rolAuth, mensajeId);
            if (!exito) return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpPost("arrecife/{mensajeId:long}/reportar")]
        [Authorize(Roles = "2,3")]
        public async Task<IActionResult> ReportarMensaje(long mensajeId, [FromBody] ReportarMensajeDto dto)
        {
            long usuarioId = ObtenerUsuarioIdDesdeClaim();
            string nombreUsuario = User.Identity?.Name ?? usuarioId.ToString();
            string rolAuth = User.FindFirst(ClaimTypes.Role)?.Value ?? "Usuario";

            var (exito, mensaje) = await _chatService.ReportarMensajeAsync(usuarioId, nombreUsuario, rolAuth, mensajeId, dto.Motivo);
            if (!exito) return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpGet("monitoreo/reportados")]
        [Authorize(Roles = "2")] // Exclusivo para Maestros
        public async Task<IActionResult> ObtenerMensajesReportados()
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0) return Unauthorized();

            var reportes = await _chatService.ObtenerMensajesReportadosPorMaestroAsync(maestroId);
            return Ok(reportes);
        }

        [HttpPost("monitoreo/{mensajeId:long}/moderar")]
        [Authorize(Roles = "2")] // Exclusivo para Maestros
        public async Task<IActionResult> ModerarMensaje(long mensajeId, [FromBody] ModerarMensajeDto dto)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0) return Unauthorized();

            string nombreMaestro = User.Identity?.Name ?? maestroId.ToString();
            string rolAuth = User.FindFirst(ClaimTypes.Role)?.Value ?? "Maestro";

            var (exito, mensaje) = await _chatService.ModerarMensajeReportadoAsync(maestroId, nombreMaestro, rolAuth, mensajeId, dto);
            if (!exito) return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }
    }
}
