using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

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

        private long ObtenerUsuarioId()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            long.TryParse(claimId, out long usuarioId);
            return usuarioId;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> ObtenerPerfil()
        {
            long usuarioId = ObtenerUsuarioId();

            var usuario = await (from u in _context.Usuarios
                                 join r in _context.Roles on u.RoleId equals r.Id
                                 where u.Id == usuarioId && u.EstaActivo
                                 select new { u.Id, u.Nombre, u.Apellido, u.Email, RolNombre = r.Nombre })
                                 .FirstOrDefaultAsync();

            if (usuario == null)
                return NotFound(new { mensaje = "Usuario no encontrado o inactivo." });

            // Consulta considerando la relación con la tabla Planes mediante PlanId
            var planNombre = await (from s in _context.Suscripciones
                                    join p in _context.Planes on s.PlanId equals p.Id
                                    where s.UsuarioId == usuarioId && s.FechaFin >= DateTimeOffset.UtcNow
                                    orderby s.FechaFin descending
                                    select p.Nombre)
                                    .FirstOrDefaultAsync();

            var perfil = new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Apellido,
                usuario.Email,
                usuario.RolNombre,
                TipoSuscripcion = planNombre ?? "Gratuito"
            };

            return Ok(perfil);
        }

        // using System.Text.RegularExpressions;
        private static readonly Regex NombreValido =
            new(@"^[\p{L}\p{M}][\p{L}\p{M}' .-]{1,49}$", RegexOptions.Compiled);

        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> ActualizarPerfil([FromBody] ActualizarPerfilDto dto)
        {
            long usuarioId = ObtenerUsuarioId();

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.EstaActivo);
            if (usuario == null) return NotFound(new { mensaje = "Usuario no encontrado." });

            const string errorNombre = "Usa solo letras, espacios, apóstrofes o guiones (de 2 a 50 caracteres).";

            if (!string.IsNullOrWhiteSpace(dto.Nombre))
            {
                var nombre = Regex.Replace(dto.Nombre.Trim(), @"\s+", " ");
                if (!NombreValido.IsMatch(nombre)) return BadRequest(new { mensaje = "Nombre inválido. " + errorNombre });
                usuario.Nombre = nombre;
            }

            if (!string.IsNullOrWhiteSpace(dto.Apellido))
            {
                var apellido = Regex.Replace(dto.Apellido.Trim(), @"\s+", " ");
                if (!NombreValido.IsMatch(apellido)) return BadRequest(new { mensaje = "Apellido inválido. " + errorNombre });
                usuario.Apellido = apellido;
            }

            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Perfil actualizado correctamente.", usuario.Nombre, usuario.Apellido });
        }

        [HttpPost("prueba-suscripcion")]
        [Authorize]
        public async Task<IActionResult> SimularSuscripcion([FromBody] SimularSuscripcionDto dto)
        {
            long usuarioId = ObtenerUsuarioId();

            var planExiste = await _context.Planes.AnyAsync(p => p.Id == dto.PlanId);
            if (!planExiste)
                return BadRequest(new { mensaje = "El plan seleccionado no existe." });

            // Asignamos EstadoSuscripcionId = 1 (Asegúrate que 1 sea el ID del estado 'Activo' en tu base de datos)
            var nuevaSuscripcion = new Suscripcion
            {
                UsuarioId = usuarioId,
                PlanId = dto.PlanId,
                EstadoSuscripcionId = 1,
                FechaInicio = DateTimeOffset.UtcNow,
                FechaFin = DateTimeOffset.UtcNow.AddDays(30),
                CreadoEn = DateTimeOffset.UtcNow
            };

            _context.Suscripciones.Add(nuevaSuscripcion);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Suscripción de prueba activada por 30 días." });
        }

        [HttpDelete("darse-de-baja")]
        [Authorize]
        public async Task<IActionResult> DarseDeBaja()
        {
            long usuarioId = ObtenerUsuarioId();

            var usuario = await _context.Usuarios.FindAsync(usuarioId);
            if (usuario == null || !usuario.EstaActivo)
                return NotFound(new { mensaje = "El usuario no existe o ya se encuentra inactivo." });

            // Soft delete
            usuario.EstaActivo = false;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Tu cuenta ha sido desactivada exitosamente." });
        }
    }
}