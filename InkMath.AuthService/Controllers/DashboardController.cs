using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "1")]
public class DashboardController : ControllerBase
{
    private readonly IReporteService _reporteService;
    private readonly AplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public DashboardController(IReporteService reporteService, AplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _reporteService = reporteService;
        _context = context;
        _auditoriaService = auditoriaService;
    }

    [HttpGet("top-maestros")]
    public async Task<IActionResult> GetTopMaestros([FromQuery] int top = 10)
    {
        var resultado = await _reporteService.ObtenerTopMaestrosAsync(top);
        return Ok(resultado);
    }

    [HttpGet("reporte-tests")]
    public async Task<IActionResult> GetReporteTests([FromQuery] FiltroReporteTestDto filtro)
    {
        try
        {
            var resultado = await _reporteService.ObtenerReporteTransaccionalAsync(filtro);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpGet("reporte-anulados")]
    public async Task<IActionResult> GetReporteAnulados([FromQuery] FiltroReporteTestDto filtro)
    {
        filtro.EstaActivo = false;
        try
        {
            var resultado = await _reporteService.ObtenerReporteTransaccionalAsync(filtro);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis()
    {
        int maestrosActivos = await _context.Usuarios.CountAsync(u => u.RoleId == 2 && u.EstaActivo);
        int estudiantesTotales = await _context.Usuarios.CountAsync(u => u.RoleId == 3 && u.EstaActivo);
        int testsActivos = await _context.TestsPersonalizados.CountAsync(t => t.EstaActivo);

        var ahora = DateTimeOffset.UtcNow;
        int suscripcionesPremium = await _context.Suscripciones
            .CountAsync(s => s.PlanId == 3 && s.FechaFin >= ahora);

        return Ok(new
        {
            maestrosActivos,
            estudiantesTotales,
            testsActivos,
            suscripcionesPremium
        });
    }

    [HttpGet("intentos-diarios")]
    public async Task<IActionResult> GetIntentosDiarios()
    {
        var hace7Dias = DateTimeOffset.UtcNow.AddDays(-7);

        // Agrupando los intentos por fecha de los últimos 7 días
        var intentos = await _context.IntentosTest
            .Where(i => i.FechaInicio >= hace7Dias)
            .GroupBy(i => i.FechaInicio.Date)
            .Select(g => new
            {
                fecha = g.Key,
                cantidad = g.Count()
            })
            .OrderBy(x => x.fecha)
            .ToListAsync();

        return Ok(intentos);
    }

    [HttpGet("usuarios-por-rol")]
    public async Task<IActionResult> GetUsuariosPorRol()
    {
        var roles = await _context.Usuarios
            .Where(u => u.EstaActivo)
            .GroupBy(u => u.RoleId)
            .Select(g => new
            {
                RoleId = g.Key,
                total = g.Count()
            })
            .ToListAsync();

        return Ok(roles);
    }

    // Formato esperado de fechas: yyyy-MM-dd (día de Guatemala). Sin fechas = hoy.
    [HttpGet("auditoria-mongo")]
    public async Task<IActionResult> GetAuditoriaMongo(
        [FromQuery] DateOnly? fechaInicio,
        [FromQuery] DateOnly? fechaFin)
    {
        try
        {
            var logs = await _auditoriaService.ObtenerLogsPorRangoAsync(fechaInicio, fechaFin);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al obtener la auditoría de MongoDB: " + ex.Message });
        }
    }
}