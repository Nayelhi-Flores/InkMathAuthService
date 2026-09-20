using InkMath.AuthService.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using InkMath.AuthService.Services;

[ApiController]
[Route("api/[controller]")]
public class NivelesController : ControllerBase
{
    private readonly INivelService _nivelService;

    public NivelesController(INivelService nivelService)
    {
        _nivelService = nivelService;
    }

    [HttpGet("estudiante/{estudianteId}")]
    public async Task<IActionResult> ObtenerMapaNiveles(long estudianteId)
    {
        var mapa = await _nivelService.ObtenerMapaNivelesPorEstudianteAsync(estudianteId);
        return Ok(mapa);
    }
}