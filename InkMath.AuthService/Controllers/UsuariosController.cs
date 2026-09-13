using System.Security.Claims;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InkMath.AuthService.Data;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        private readonly AplicationDbContext _context;

        public UsuariosController(AplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        public async Task<IActionResult> ObtenerPerfil()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!long.TryParse(claimId, out long usuarioId))
                return Unauthorized();

            var usuario = await (from u in _context.Usuarios
                                 join r in _context.Roles on u.RoleId equals r.Id
                                 where u.Id == usuarioId
                                 select new { u.Id, u.Nombre, u.Email, RolNombre = r.Nombre })
                                 .FirstOrDefaultAsync();

            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado." });



            var perfil = new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                usuario.RolNombre,
                TipoSuscripcion = "Gratuito"
            };

            return Ok(perfil);
        }
    }
}