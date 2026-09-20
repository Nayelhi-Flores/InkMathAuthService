using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using Microsoft.EntityFrameworkCore;

namespace InkMath.AuthService.Services
{
    public interface INivelService
    {
        Task<List<MapaNivelDto>> ObtenerMapaNivelesPorEstudianteAsync(long estudianteId);
    }

    public class NivelService : INivelService
    {
        private readonly AplicationDbContext _context; 

        public NivelService(AplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<MapaNivelDto>> ObtenerMapaNivelesPorEstudianteAsync(long estudianteId)
        {
            var niveles = await _context.Niveles
                .Where(n => n.EstaActivo)
                .OrderBy(n => n.Orden)
                .ToListAsync();

            var progresos = await _context.ProgresosNivel
                .Include(p => p.EstadoProgreso)
                .Where(p => p.EstudianteId == estudianteId)
                .ToDictionaryAsync(p => p.NivelId);

            return niveles.Select(nivel => {
                progresos.TryGetValue(nivel.Id, out var progreso);
                return new MapaNivelDto
                {
                    NivelId = nivel.Id,
                    Titulo = nivel.Titulo,
                    Orden = nivel.Orden,
                    TestId = nivel.TestId,
                    Estado = progreso?.EstadoProgreso?.Nombre ?? "No Iniciado",
                    Puntaje = progreso?.Puntaje ?? 0,
                    Intentos = progreso?.Intentos ?? 0
                };
            }).ToList();
        }
    }
}
