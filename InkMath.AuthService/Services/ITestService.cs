using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace InkMath.AuthService.Services
{
    public interface ITestService
    {
        Task<TestDetalleResponseDto> CrearTestTransaccionalAsync(CrearTestDto dto);
        Task<bool> AnularTestAsync(long testId, long usuarioId);
        Task<(bool Exito, string Mensaje)> AsignarTestsAAulasAsync(long maestroId, List<long> testIds, List<long> aulaIds);
        Task<RespuestaPaginadaDto<TestDetalleResponseDto>> ObtenerTestsPaginadosPorMaestroAsync(long maestroId, ConsultaTestsPaginadaDto dto);
        Task<List<TestDetalleResponseDto>> ObtenerTestsPorMaestroAsync(long maestroId);
    }

    public class TestService : ITestService
    {
        private readonly IAuditoriaService _auditoriaService;
        private readonly AplicationDbContext _context;

        public TestService(AplicationDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        public async Task<TestDetalleResponseDto> CrearTestTransaccionalAsync(CrearTestDto dto)
        {
            // Iniciar la transacción ACID en la base de datos relacional
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Insertar Encabezado Maestro (tests_personalizados)
                var nuevoTest = new TestPersonalizado
                {
                    MaestroId = dto.MaestroId,
                    Nombre = dto.Nombre,
                    FechaDisponibleDesde = dto.FechaDisponibleDesde,
                    FechaDisponibleHasta = dto.FechaDisponibleHasta,
                    CreadoEn = DateTimeOffset.UtcNow
                };

                _context.TestsPersonalizados.Add(nuevoTest);
                await _context.SaveChangesAsync(); // Genera el Id del Maestro

                // Asignación Masiva a Aulas (Si se enviaron en la petición)
                if (dto.AulaIds != null && dto.AulaIds.Count > 0)
                {
                    foreach (var aulaId in dto.AulaIds)
                    {
                        _context.AulaTests.Add(new AulaTest
                        {
                            AulaId = aulaId,
                            TestId = nuevoTest.Id,
                            FechaAsignacion = DateTimeOffset.UtcNow
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // 2. Insertar Líneas del Detalle (preguntas_test y opciones_pregunta)
                foreach (var preguntaDto in dto.Preguntas)
                {
                    var nuevaPregunta = new PreguntaTest
                    {
                        TestId = nuevoTest.Id,
                        TipoPreguntaId = preguntaDto.TipoPreguntaId,
                        Pregunta = preguntaDto.Pregunta
                    };

                    _context.PreguntasTest.Add(nuevaPregunta);
                    await _context.SaveChangesAsync(); // Genera el Id de la Pregunta

                    // 3. Insertar Sub-detalle de Opciones si la pregunta tiene opciones
                    if (preguntaDto.Opciones != null && preguntaDto.Opciones.Count > 0)
                    {
                        for (int i = 0; i < preguntaDto.Opciones.Count; i++)
                        {
                            var nuevaOpcion = new OpcionPregunta
                            {
                                PreguntaId = nuevaPregunta.Id,
                                TextoOpcion = preguntaDto.Opciones[i],
                                EsCorrecta = (i == preguntaDto.OpcionCorrectaIndex)
                            };

                            _context.OpcionesPregunta.Add(nuevaOpcion);
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                // 4. Confirmar la Transacción Relacional
                await transaction.CommitAsync();

                // 5. Registrar evento en MongoDB (logs_auditoria)
                await _auditoriaService.RegistrarEventoAsync(
                    dto.MaestroId,
                    "CREAR_TEST",
                    $"Se creó el test '{nuevoTest.Nombre}' (ID: {nuevoTest.Id}) asignado a {dto.AulaIds?.Count ?? 0} aulas."
                );

                return new TestDetalleResponseDto
                {
                    TestId = nuevoTest.Id,
                    Nombre = nuevoTest.Nombre,
                    MaestroId = nuevoTest.MaestroId,
                    FechaDisponibleDesde = nuevoTest.FechaDisponibleDesde,
                    FechaDisponibleHasta = nuevoTest.FechaDisponibleHasta,
                    AulasAsignadas = dto.AulaIds?.Count ?? 0,
                    CreadoEn = nuevoTest.CreadoEn,
                    TotalPreguntas = dto.Preguntas.Count
                };
            }
            catch (Exception)
            {
                // Deshacer todos los cambios en caso de error
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(bool Exito, string Mensaje)> AsignarTestsAAulasAsync(long maestroId, List<long> testIds, List<long> aulaIds)
        {
            if (testIds == null || !testIds.Any() || aulaIds == null || !aulaIds.Any())
                return (false, "Debe seleccionar al menos un test y un aula.");

            // Validar propiedad del maestro
            var testsValidos = await _context.TestsPersonalizados
                .Where(t => testIds.Contains(t.Id) && t.MaestroId == maestroId)
                .Select(t => t.Id)
                .ToListAsync();

            var aulasValidas = await _context.Aulas
                .Where(a => aulaIds.Contains(a.Id) && a.MaestroId == maestroId && a.EstaActivo)
                .Select(a => a.Id)
                .ToListAsync();

            if (!testsValidos.Any() || !aulasValidas.Any())
                return (false, "No se encontraron tests o aulas válidas.");

            // Obtener relaciones ya existentes en la tabla intermedia
            var existentes = await _context.AulaTests
                .Where(at => aulasValidas.Contains(at.AulaId) && testsValidos.Contains(at.TestId))
                .Select(at => new { at.AulaId, at.TestId })
                .ToListAsync();

            var hashExistentes = new HashSet<(long AulaId, long TestId)>(
                existentes.Select(x => (x.AulaId, x.TestId))
            );

            var nuevasAsignaciones = new List<AulaTest>();
            foreach (var aulaId in aulasValidas)
            {
                foreach (var testId in testsValidos)
                {
                    if (!hashExistentes.Contains((aulaId, testId)))
                    {
                        nuevasAsignaciones.Add(new AulaTest
                        {
                            AulaId = aulaId,
                            TestId = testId,
                            FechaAsignacion = DateTime.UtcNow
                        });
                    }
                }
            }

            if (nuevasAsignaciones.Any())
            {
                _context.AulaTests.AddRange(nuevasAsignaciones);
                await _context.SaveChangesAsync();
            }

            return (true, $"Se asignaron los tests a las aulas correspondientes.");
        }

        public async Task<bool> AnularTestAsync(long testId, long usuarioId)
        {
            var test = await _context.TestsPersonalizados.FindAsync(testId);
            if (test == null) return false;

            // Aplicar Soft Delete
            test.EstaActivo = false;
            await _context.SaveChangesAsync();

            // Registrar la auditoría en MongoDB
            await _auditoriaService.RegistrarEventoAsync(
                usuarioId,
                "ANULAR_TEST",
                $"Se eliminó/anuló el test con ID: {testId}."
            );

            return true;
        }

        public async Task<List<TestDetalleResponseDto>> ObtenerTestsPorMaestroAsync(long maestroId)
            {
            return await _context.TestsPersonalizados
                .Where(t => t.MaestroId == maestroId && t.EstaActivo)
                .OrderByDescending(t => t.CreadoEn)
                .Select(t => new TestDetalleResponseDto
                {
                    TestId = t.Id,
                    Nombre = t.Nombre,
                    MaestroId = t.MaestroId,
                    FechaDisponibleDesde = t.FechaDisponibleDesde,
                    FechaDisponibleHasta = t.FechaDisponibleHasta,
                    AulasAsignadas = _context.AulaTests.Count(at => at.TestId == t.Id),
                    CreadoEn = t.CreadoEn,
                    TotalPreguntas = t.Preguntas.Count
                })
                .ToListAsync();
        }

        public async Task<RespuestaPaginadaDto<TestDetalleResponseDto>> ObtenerTestsPaginadosPorMaestroAsync(long maestroId, ConsultaTestsPaginadaDto dto)
        {
            int limite = Math.Min(dto.Limite, 50);

            // 1. Base Query con No Tracking
            var query = _context.TestsPersonalizados
                .AsNoTracking()
                .Where(t => t.MaestroId == maestroId && t.EstaActivo);

            // 2. Filtro por Texto de Búsqueda (Nombre)
            if (!string.IsNullOrWhiteSpace(dto.Busqueda))
            {
                string busquedaLower = dto.Busqueda.Trim().ToLower();
                query = query.Where(t => t.Nombre.ToLower().Contains(busquedaLower));
            }

            // 3. Filtro por Aula Específica (Mediante subconsulta a AulaTests)
            if (dto.AulaId.HasValue && dto.AulaId.Value > 0)
            {
                query = query.Where(t => _context.AulaTests.Any(at => at.TestId == t.Id && at.AulaId == dto.AulaId.Value));
            }

            // 4. Filtro por Fecha Exacta (Rango del día completo UTC)
            if (dto.Fecha.HasValue)
            {
                var fechaInicio = DateTime.SpecifyKind(dto.Fecha.Value.Date, DateTimeKind.Utc);
                var fechaFin = fechaInicio.AddDays(1);

                query = query.Where(t => t.CreadoEn >= fechaInicio && t.CreadoEn < fechaFin);
            }

            int totalRegistros = await query.CountAsync();

            // 5. Aplicar Cursores de Paginación
            if (dto.UltimaFecha.HasValue && dto.UltimoId.HasValue)
            {
                query = query.Where(t =>
                    t.CreadoEn < dto.UltimaFecha.Value ||
                    (t.CreadoEn == dto.UltimaFecha.Value && t.Id < dto.UltimoId.Value));
            }

            // 6. Ejecución optimizada
            var registros = await query
                .OrderByDescending(t => t.CreadoEn)
                .ThenByDescending(t => t.Id)
                .Take(limite + 1)
                .Select(t => new TestDetalleResponseDto
                {
                    TestId = t.Id,
                    Nombre = t.Nombre,
                    MaestroId = t.MaestroId,
                    FechaDisponibleDesde = t.FechaDisponibleDesde,
                    FechaDisponibleHasta = t.FechaDisponibleHasta,
                    AulasAsignadas = _context.AulaTests.Count(at => at.TestId == t.Id),
                    CreadoEn = t.CreadoEn,
                    TotalPreguntas = t.Preguntas.Count
                })
                .ToListAsync();

            bool tieneMasPaginas = registros.Count > limite;
            var datosPaginados = tieneMasPaginas ? registros.Take(limite).ToList() : registros;
            var ultimoRegistro = datosPaginados.LastOrDefault();

            return new RespuestaPaginadaDto<TestDetalleResponseDto>
            {
                Datos = datosPaginados,
                SiguienteUltimoId = ultimoRegistro?.TestId,
                SiguienteUltimaFecha = ultimoRegistro?.CreadoEn,
                TieneMasPaginas = tieneMasPaginas,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling((double)totalRegistros / limite)
            };
        }
    }
}
