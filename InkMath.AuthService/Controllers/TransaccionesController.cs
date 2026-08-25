using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;

namespace InkMath.AuthService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransaccionesController : ControllerBase
    {
        private readonly AplicationDbContext _context;
        private readonly ISeedRepository _seedRepository;

        public TransaccionesController(AplicationDbContext context, ISeedRepository seedRepository)
        {
            _context = context;
            _seedRepository = seedRepository;
        }

        [Authorize]
        [HttpPost("crear")]
        public async Task<IActionResult> CrearTransaccion([FromBody] CrearTransaccionMonedaDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out long usuarioId))
            {
                return Unauthorized(new { mensaje = "Token inválido o no suministrado." });
            }

            var transaccion = new TransaccionMoneda
            {
                EstudianteId = usuarioId,
                TipoTransaccionId = dto.TipoTransaccionId,
                Monto = dto.Monto,
                IdempotencyKey = Guid.NewGuid().ToString(),
                CreadoEn = DateTimeOffset.UtcNow
            };

            _context.TransaccionesMonedas.Add(transaccion);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Transacción de moneda registrada exitosamente.", id = transaccion.Id });
        }

        [HttpPost("seed-data")]
        public async Task<IActionResult> InyectarDatosMasivos([FromQuery] int cantidad = 50000, [FromQuery] long estudianteId = 1)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await _seedRepository.SeedTransaccionesBulkAsync(cantidad, estudianteId);

            stopwatch.Stop();

            return Ok(new
            {
                mensaje = $"Se han inyectado {cantidad:N0} registros exitosamente mediante SqlBulkCopy.",
                tiempoEjecucion = $"{stopwatch.ElapsedMilliseconds} ms"
            });
        }
    }
}
