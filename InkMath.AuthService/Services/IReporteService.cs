using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using Microsoft.EntityFrameworkCore;

namespace InkMath.AuthService.Services
{
    public interface IReporteService
    {
        Task<List<TopMaestroDto>> ObtenerTopMaestrosAsync(int top);
        Task<List<ReporteTestDto>> ObtenerReporteTransaccionalAsync(FiltroReporteTestDto filtro);
    }

    public class ReporteService : IReporteService
    {
        private readonly AplicationDbContext _context;

        public ReporteService(AplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<TopMaestroDto>> ObtenerTopMaestrosAsync(int top)
        {
            var sql = @"
            SELECT maestro_id, nombre, total_preguntas, posicion_ranking FROM (
                SELECT t.maestro_id, u.nombre, SUM(t.total_preguntas) AS total_preguntas,
                    RANK() OVER (ORDER BY SUM(t.total_preguntas) DESC) AS posicion_ranking
                FROM tests_personalizados t JOIN usuarios u ON u.id = t.maestro_id
                WHERE t.esta_activo GROUP BY t.maestro_id, u.nombre) r
            WHERE posicion_ranking <= 10 ORDER BY posicion_ranking, maestro_id;";

            return await _context.Database
                .SqlQueryRaw<TopMaestroDto>(sql, top)
                .ToListAsync();
        }

        public async Task<List<ReporteTestDto>> ObtenerReporteTransaccionalAsync(FiltroReporteTestDto filtro)
        {
            // Validar que al menos un filtro obligatorio esté presente
            bool tieneFiltro = filtro != null && (
                filtro.FechaInicio.HasValue ||
                filtro.FechaFin.HasValue ||
                (filtro.MaestroId.HasValue && filtro.MaestroId.Value > 0) ||
                filtro.EstaActivo.HasValue
            );

            if (!tieneFiltro)
            {
                throw new ArgumentException("Debe proporcionar al menos un filtro obligatorio (Rango de fechas, Maestro ID o Estado) para ejecutar la consulta.");
            }

            var query = from t in _context.TestsPersonalizados.IgnoreQueryFilters().AsNoTracking()
                        join u in _context.Usuarios.AsNoTracking() on t.MaestroId equals u.Id
                        select new { t, u };

            if (filtro.EstaActivo.HasValue)
                query = query.Where(x => x.t.EstaActivo == filtro.EstaActivo.Value);

            if (filtro.MaestroId.HasValue)
                query = query.Where(x => x.t.MaestroId == filtro.MaestroId.Value);

            if (filtro.FechaInicio.HasValue)
                query = query.Where(x => x.t.CreadoEn >= filtro.FechaInicio.Value);

            if (filtro.FechaFin.HasValue)
                query = query.Where(x => x.t.CreadoEn <= filtro.FechaFin.Value);

            return await query
                .OrderByDescending(x => x.t.CreadoEn)
                .Select(x => new ReporteTestDto
                {
                    Id = x.t.Id,
                    MaestroId = x.t.MaestroId,
                    NombreMaestro = x.u.Nombre,
                    NombreTest = x.t.Nombre,
                    CreadoEn = x.t.CreadoEn,
                    EstaActivo = x.t.EstaActivo,
                    TotalPreguntas = x.t.TotalPreguntas
                })
                .ToListAsync();
        }
    }
}
