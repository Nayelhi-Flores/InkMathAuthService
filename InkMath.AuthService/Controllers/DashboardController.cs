using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "1")]
public class DashboardController : ControllerBase
{
    private readonly IReporteService _reporteService;

    public DashboardController(IReporteService reporteService)
    {
        _reporteService = reporteService;
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
        var resultado = await _reporteService.ObtenerReporteTransaccionalAsync(filtro);
        return Ok(resultado);
    }
}