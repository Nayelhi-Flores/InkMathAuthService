using InkMath.AuthService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "3")]
    public class MonedasController : ControllerBase
    {
        private readonly AplicationDbContext _context;

        public MonedasController(AplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>Saldo de monedas del estudiante autenticado (el ID sale del token).</summary>
        [HttpGet("mi-saldo")]
        public async Task<IActionResult> MiSaldo()
        {
            if (!long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out long estudianteId))
                return Unauthorized(new { mensaje = "Token no válido." });

            var saldo = await _context.SaldoMonedas
                .AsNoTracking()
                .Where(s => s.EstudianteId == estudianteId)
                .Select(s => (long?)s.Saldo)
                .FirstOrDefaultAsync() ?? 0;

            return Ok(new { saldo });
        }
    }
}