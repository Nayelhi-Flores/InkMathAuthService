using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace InkMath.AuthService.Services
{
    public interface IReporteService
    {
        Task<List<TopMaestroDto>> ObtenerTopMaestrosAsync(int top);
        Task<List<ReporteTestDto>> ObtenerReporteTransaccionalAsync(FiltroReporteTestDto filtro);
        Task<List<RankingEstudianteDto>> ObtenerRankingAulaAsync(long aulaId);
        Task<ReporteGrupalAulaDto?> ObtenerReporteGrupalAulaAsync(long aulaId);
        Task<AnalisisIndividualDto?> ObtenerAnalisisIndividualAsync(long estudianteId);
        Task<List<AlertaRezagoDto>> ObtenerAlertasRezagoPorMaestroAsync(long maestroId);
        Task<byte[]> ExportarReporteAulaCsvAsync(long aulaId);
        Task<List<RankingCompaneroDto>?> ObtenerRankingCompanerosAsync(long aulaId, long estudianteId);
    }

    public class ReporteService : IReporteService
    {
        private readonly AplicationDbContext _context;
        private const string RankingSql = @"
            WITH miembros AS (
                SELECT u.id, u.nombre, u.apellido
                FROM usuarios u
                JOIN aula_estudiante ae ON ae.estudiante_id = u.id
                WHERE ae.aula_id = {0} AND ae.esta_activo = TRUE AND u.esta_activo = TRUE
            ),
            niveles AS (
                SELECT pn.estudiante_id, SUM(pn.puntaje) AS puntos
                FROM progreso_nivel pn
                WHERE pn.estudiante_id IN (SELECT id FROM miembros)
                GROUP BY pn.estudiante_id
            ),
            tests AS (
                SELECT mejor.estudiante_id, SUM(mejor.puntaje) AS puntos
                FROM (
                    SELECT it.estudiante_id, it.test_id, MAX(it.puntaje_obtenido) AS puntaje
                    FROM intentos_test it
                    JOIN aula_tests at ON at.test_id = it.test_id AND at.aula_id = {0}
                    WHERE it.estudiante_id IN (SELECT id FROM miembros)
                    GROUP BY it.estudiante_id, it.test_id
                ) mejor
                GROUP BY mejor.estudiante_id
            )
            SELECT m.id AS EstudianteId,
                   m.nombre AS Nombre,
                   m.apellido AS Apellido,
                   COALESCE(n.puntos, 0)::int AS PuntajeNiveles,
                   COALESCE(t.puntos, 0)::int AS PuntajeTests,
                   (COALESCE(n.puntos, 0) + COALESCE(t.puntos, 0))::int AS PuntajeTotal,
                   RANK() OVER (ORDER BY COALESCE(n.puntos, 0) + COALESCE(t.puntos, 0) DESC)::int AS Posicion
            FROM miembros m
            LEFT JOIN niveles n ON n.estudiante_id = m.id
            LEFT JOIN tests t ON t.estudiante_id = m.id
            ORDER BY PuntajeTotal DESC, m.id;";

        public ReporteService(AplicationDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> ExportarReporteAulaCsvAsync(long aulaId)
        {
            var ranking = await ObtenerRankingAulaAsync(aulaId);

            var builder = new StringBuilder();
            // Encabezados en formato CSV
            builder.AppendLine("Posicion,EstudianteId,NombreCompleto,PuntajeNiveles,PuntajeTests,PuntajeTotal");

            foreach (var item in ranking)
            {
                builder.AppendLine($"{item.Posicion},{item.EstudianteId},\"{item.NombreCompleto}\",{item.PuntajeTotalNiveles},{item.PuntajeTotalTests},{item.PuntajeAcumulado}");
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        }

        public async Task<List<AlertaRezagoDto>> ObtenerAlertasRezagoPorMaestroAsync(long maestroId)
        {
            var sql = @"
            WITH promedios_estudiantes AS (
                SELECT 
                    ae.estudiante_id,
                    ae.aula_id,
                    a.nombre AS nombre_aula,
                    u.nombre || ' ' || u.apellido AS nombre_estudiante,
                    COALESCE(ROUND(AVG(pn.puntaje)::numeric, 2), 0) AS promedio_nivel,
                    COUNT(CASE WHEN pn.puntaje < 60 THEN 1 END) AS niveles_reprobados
                FROM aulas a
                JOIN aula_estudiante ae ON ae.aula_id = a.id
                JOIN usuarios u ON u.id = ae.estudiante_id
                LEFT JOIN progreso_nivel pn ON pn.estudiante_id = ae.estudiante_id
                WHERE a.maestro_id = {0} AND a.esta_activo = TRUE AND u.esta_activo = TRUE AND ae.esta_activo = TRUE
                GROUP BY ae.estudiante_id, ae.aula_id, a.nombre, u.nombre, u.apellido
            )
            SELECT 
                estudiante_id AS EstudianteId,
                nombre_estudiante AS NombreEstudiante,
                aula_id AS AulaId,
                nombre_aula AS NombreAula,
                promedio_nivel AS PromedioGeneral,
                niveles_reprobados AS NivelesReprobados,
                CASE 
                    WHEN promedio_nivel < 40 OR niveles_reprobados >= 5 THEN 'Alto'
                    ELSE 'Medio'
                END AS NivelRiesgo
            FROM promedios_estudiantes
            WHERE promedio_nivel < 60 OR niveles_reprobados >= 1
            ORDER BY promedio_nivel ASC;";

            return await _context.Database
                .SqlQueryRaw<AlertaRezagoDto>(sql, maestroId)
                .ToListAsync();
        }

        public async Task<AnalisisIndividualDto?> ObtenerAnalisisIndividualAsync(long estudianteId)
        {
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == estudianteId && u.EstaActivo);

            if (usuario == null) return null;

            var niveles = await _context.ProgresosNivel
                .AsNoTracking()
                .Where(pn => pn.EstudianteId == estudianteId)
                .Select(pn => new DetalleNivelEstudianteDto
                {
                    NivelId = pn.NivelId,
                    Puntaje = pn.Puntaje,
                    Intentos = pn.Intentos,
                    EstadoId = pn.EstadoId
                })
                .ToListAsync();

            // Obtener errores reales donde la opción seleccionada fue incorrecta (es_correcta = false)
            var errores = await (from it in _context.IntentosTest
                                 join re in _context.RespuestasEstudiante on it.Id equals re.IntentoId
                                 join op in _context.OpcionesPregunta on re.OpcionId equals op.Id
                                 where it.EstudianteId == estudianteId
                                       && op.EsCorrecta == false // Solo errores reales en opciones de selección
                                 orderby it.FechaFin descending
                                 select new HistorialErrorDto
                                 {
                                     IntentoId = it.Id,
                                     PreguntaId = re.PreguntaId,
                                     RespuestaDada = op.TextoOpcion,
                                     Fecha = it.FechaFin
                                 })
                                 .Take(20)
                                 .ToListAsync();

            return new AnalisisIndividualDto
            {
                EstudianteId = usuario.Id,
                NombreCompleto = $"{usuario.Nombre} {usuario.Apellido}",
                NivelesProgreso = niveles,
                HistorialErrores = errores
            };
        }

        public async Task<ReporteGrupalAulaDto?> ObtenerReporteGrupalAulaAsync(long aulaId)
        {
            var sql = @"
            WITH resumen_niveles AS (
                SELECT 
                    ae.aula_id,
                    ROUND(AVG(pn.puntaje)::numeric, 2) AS promedio_niveles,
                    COUNT(DISTINCT CASE WHEN pn.estado_id = 3 THEN pn.nivel_id END) AS niveles_completados
                FROM aula_estudiante ae
                JOIN progreso_nivel pn ON pn.estudiante_id = ae.estudiante_id
                WHERE ae.aula_id = {0} AND ae.esta_activo = TRUE
                GROUP BY ae.aula_id
            ),
            resumen_tests AS (
                SELECT 
                    ae.aula_id,
                    ROUND(AVG(it.puntaje_obtenido)::numeric, 2) AS promedio_tests,
                    COUNT(DISTINCT it.id) AS tests_realizados
                FROM aula_estudiante ae
                JOIN intentos_test it ON it.estudiante_id = ae.estudiante_id
                WHERE ae.aula_id = {0}
                GROUP BY ae.aula_id
            )
            SELECT 
                a.id AS AulaId,
                a.nombre AS NombreAula,
                (SELECT COUNT(*) FROM aula_estudiante ae WHERE ae.aula_id = a.id AND ae.esta_activo = TRUE) AS TotalEstudiantes,
                COALESCE(rn.promedio_niveles, 0) AS PromedioGeneralNiveles,
                COALESCE(rt.promedio_tests, 0) AS PromedioGeneralTests,
                COALESCE(rn.niveles_completados, 0) AS TotalNivelesCompletados,
                COALESCE(rt.tests_realizados, 0) AS TotalTestsRealizados
            FROM aulas a
            LEFT JOIN resumen_niveles rn ON rn.aula_id = a.id
            LEFT JOIN resumen_tests rt ON rt.aula_id = a.id
            WHERE a.id = {0} AND a.esta_activo = TRUE;";

            var resultado = await _context.Database
                .SqlQueryRaw<ReporteGrupalAulaDto>(sql, aulaId)
                .ToListAsync();

            return resultado.FirstOrDefault();
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

        public async Task<List<RankingEstudianteDto>> ObtenerRankingAulaAsync(long aulaId)
        {
            var filas = await _context.Database.SqlQueryRaw<RankingFilaDto>(RankingSql, aulaId).ToListAsync();
            return filas.Select(f => new RankingEstudianteDto
            {
                EstudianteId = f.EstudianteId,
                NombreCompleto = $"{f.Nombre} {f.Apellido}",
                PuntajeTotalNiveles = f.PuntajeNiveles,
                PuntajeTotalTests = f.PuntajeTests,
                PuntajeAcumulado = f.PuntajeTotal,
                Posicion = f.Posicion
            }).ToList();
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

        public async Task<List<RankingCompaneroDto>?> ObtenerRankingCompanerosAsync(long aulaId, long estudianteId)
        {
            bool pertenece = await _context.AulaEstudiantes.AsNoTracking()
                .AnyAsync(ae => ae.AulaId == aulaId && ae.EstudianteId == estudianteId);
            if (!pertenece) return null;

            var filas = await _context.Database.SqlQueryRaw<RankingFilaDto>(RankingSql, aulaId).ToListAsync();

            RankingCompaneroDto Mapear(RankingFilaDto f) => new()
            {
                Posicion = f.Posicion,
                Nombre = string.IsNullOrWhiteSpace(f.Apellido) ? f.Nombre : $"{f.Nombre} {char.ToUpperInvariant(f.Apellido.Trim()[0])}.",
                Puntaje = f.PuntajeTotal,
                EsMio = f.EstudianteId == estudianteId
            };

            var top = filas.Take(10).Select(Mapear).ToList();
            if (!top.Any(r => r.EsMio))
            {
                var mia = filas.FirstOrDefault(f => f.EstudianteId == estudianteId);
                if (mia != null) top.Add(Mapear(mia));
            }
            return top;
        }
    }
}
