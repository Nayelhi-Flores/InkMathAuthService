using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "3")]
public class NivelesController : ControllerBase
{
    private readonly INivelService _nivelService;

    public NivelesController(INivelService nivelService)
    {
        _nivelService = nivelService;
    }

    [HttpGet("mi-mapa")]
    public async Task<IActionResult> MiMapa()
    {
        if (!long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id))
            return Unauthorized();
        return Ok(await _nivelService.ObtenerMapaNivelesPorEstudianteAsync(id));
    }
}