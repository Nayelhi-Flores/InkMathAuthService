using InkMath.AuthService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InkMath.AuthService.Models;

[ApiController]
[Route("api/[controller]")]
public class CatalogosController : ControllerBase
{
    private readonly AplicationDbContext _context;

    public CatalogosController(AplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("tipos-pregunta")]
    public async Task<IActionResult> ObtenerTiposPregunta()
    {
        var tipos = await _context.TiposPregunta.ToListAsync();
        return Ok(tipos);
    }
}