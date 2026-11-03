using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;

namespace InkMath.AuthService.Services
{
    public interface ITestService
    {
        Task<TestDetalleResponseDto> CrearTestTransaccionalAsync(CrearTestDto dto);
        Task<bool> ActualizarTestAsync(long id, CrearTestDto dto, string? usuarioIp = null);
        Task<bool> CambiarEstadoPreguntaAsync(long testId, long preguntaId, bool estaActiva, long maestroId);
        Task SincronizarAulasAsync(long testId, List<long> aulaIds);
        Task<bool> AnularTestAsync(long testId, long usuarioId, string nombreUsuario, string rolAuth, string justificacion);
        Task<(bool Exito, string Mensaje)> AsignarTestsAAulasAsync(long maestroId, List<long> testIds, List<long> aulaIds);
        Task<RespuestaPaginadaDto<TestDetalleResponseDto>> ObtenerTestsPaginadosPorMaestroAsync(long maestroId, ConsultaTestsPaginadaDto dto);
        Task<List<TestDetalleResponseDto>> ObtenerTestsPorMaestroAsync(long maestroId);
        Task<List<TestImpresionDto>> ObtenerDetalleTestsLoteAsync(long maestroId, List<long> testIds);
    }

    public class TestService : ITestService
    {
        private readonly IAuditoriaService _auditoriaService;
        private readonly ICacheService _cacheService;
        private readonly AplicationDbContext _context;

        public TestService(AplicationDbContext context, IAuditoriaService auditoriaService, ICacheService cacheService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _cacheService = cacheService;
        }

        public async Task<TestDetalleResponseDto> CrearTestTransaccionalAsync(CrearTestDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                long testIdGenerado;

                // Si la base de datos es SQL Server, delegamos la inserción al Procedimiento Almacenado
                if (_context.Database.IsSqlServer())
                {
                    var pMaestroId = new SqlParameter("@MaestroId", dto.MaestroId);
                    var pNombre = new SqlParameter("@Nombre", dto.Nombre);
                    var pFechaDesde = new SqlParameter("@FechaDesde", (object?)dto.FechaDisponibleDesde ?? DBNull.Value);
                    var pFechaHasta = new SqlParameter("@FechaHasta", (object?)dto.FechaDisponibleHasta ?? DBNull.Value);

                    // Serializamos las preguntas y opciones a JSON para el parámetro @PreguntasJson del SP
                    string preguntasJson = JsonSerializer.Serialize(dto.Preguntas);
                    var pPreguntasJson = new SqlParameter("@PreguntasJson", preguntasJson);

                    var pNuevoTestId = new SqlParameter("@NuevoTestId", System.Data.SqlDbType.BigInt)
                    {
                        Direction = System.Data.ParameterDirection.Output
                    };

                    await _context.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.sp_CrearTestPersonalizado @MaestroId, @Nombre, @FechaDesde, @FechaHasta, @PreguntasJson, @NuevoTestId OUTPUT",
                        pMaestroId, pNombre, pFechaDesde, pFechaHasta, pPreguntasJson, pNuevoTestId
                    );

                    testIdGenerado = (long)pNuevoTestId.Value;
                }
                else
                {
                    // Flujo portable para PostgreSQL
                    var nuevoTest = new TestPersonalizado
                    {
                        MaestroId = dto.MaestroId,
                        Nombre = dto.Nombre,
                        FechaDisponibleDesde = dto.FechaDisponibleDesde,
                        FechaDisponibleHasta = dto.FechaDisponibleHasta,
                        CreadoEn = DateTimeOffset.UtcNow
                    };

                    _context.TestsPersonalizados.Add(nuevoTest);
                    await _context.SaveChangesAsync();
                    testIdGenerado = nuevoTest.Id;

                    foreach (var preguntaDto in dto.Preguntas)
                    {
                        var nuevaPregunta = new PreguntaTest
                        {
                            TestId = testIdGenerado,
                            TipoPreguntaId = preguntaDto.TipoPreguntaId,
                            Pregunta = preguntaDto.Pregunta
                        };

                        _context.PreguntasTest.Add(nuevaPregunta);
                        await _context.SaveChangesAsync();

                        if (preguntaDto.Opciones != null && preguntaDto.Opciones.Count > 0)
                        {
                            for (int i = 0; i < preguntaDto.Opciones.Count; i++)
                            {
                                _context.OpcionesPregunta.Add(new OpcionPregunta
                                {
                                    PreguntaId = nuevaPregunta.Id,
                                    TextoOpcion = preguntaDto.Opciones[i],
                                    EsCorrecta = (i == preguntaDto.OpcionCorrectaIndex)
                                });
                            }
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // Asignación de Aulas (Común para ambos motores dentro de la misma transacción)
                if (dto.AulaIds != null && dto.AulaIds.Count > 0)
                {
                    foreach (var aulaId in dto.AulaIds)
                    {
                        _context.AulaTests.Add(new AulaTest
                        {
                            AulaId = aulaId,
                            TestId = testIdGenerado,
                            FechaAsignacion = DateTimeOffset.UtcNow
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                // Registrar auditoría NoSQL
                _ = Task.Run(() => _auditoriaService.RegistrarEventoAsync(
                    dto.MaestroId,
                    "CREAR_TEST",
                    $"Se creó el test '{dto.Nombre}' (ID: {testIdGenerado}) asignado a {dto.AulaIds?.Count ?? 0} aulas."
                ));

                return new TestDetalleResponseDto
                {
                    TestId = testIdGenerado,
                    Nombre = dto.Nombre,
                    MaestroId = dto.MaestroId,
                    FechaDisponibleDesde = dto.FechaDisponibleDesde,
                    FechaDisponibleHasta = dto.FechaDisponibleHasta,
                    AulasAsignadas = dto.AulaIds?.Count ?? 0,
                    CreadoEn = DateTimeOffset.UtcNow,
                    TotalPreguntas = dto.Preguntas.Count
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> ActualizarTestAsync(long id, CrearTestDto dto, string? usuarioIp = null)
        {
            var testAnterior = await _context.TestsPersonalizados
                 .AsNoTracking()
                 .Include(t => t.Preguntas)
                 .ThenInclude(p => p.Opciones)
                 .FirstOrDefaultAsync(t => t.Id == id);

            string? datosAnterioresJson = null;

            if (testAnterior != null)
            {
                var snapshotAnterior = new
                {
                    testAnterior.Id,
                    testAnterior.MaestroId,
                    testAnterior.Nombre,
                    testAnterior.FechaDisponibleDesde,
                    testAnterior.FechaDisponibleHasta,
                    Preguntas = testAnterior.Preguntas.Select(p => new
                    {
                        p.Id,
                        p.TipoPreguntaId,
                        p.Pregunta,
                        Opciones = p.Opciones.Select(o => new
                        {
                            o.Id,
                            o.TextoOpcion,
                            o.EsCorrecta
                        }).ToList()
                    }).ToList()
                };

                datosAnterioresJson = JsonSerializer.Serialize(snapshotAnterior);
            }

            var esSqlServer = _context.Database.IsSqlServer();

            if (esSqlServer)
            {
                var preguntasJson = JsonSerializer.Serialize(dto.Preguntas);
                var testIdParam = new SqlParameter("@TestId", id);
                var maestroIdParam = new SqlParameter("@MaestroId", dto.MaestroId);
                var nombreParam = new SqlParameter("@Nombre", dto.Nombre);
                var fechaDesdeParam = new SqlParameter("@FechaDesde", (object?)dto.FechaDisponibleDesde ?? DBNull.Value);
                var fechaHastaParam = new SqlParameter("@FechaHasta", (object?)dto.FechaDisponibleHasta ?? DBNull.Value);
                var preguntasJsonParam = new SqlParameter("@PreguntasJson", preguntasJson);

                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC dbo.sp_ActualizarTestPersonalizado @TestId, @MaestroId, @Nombre, @FechaDesde, @FechaHasta, @PreguntasJson",
                    testIdParam, maestroIdParam, nombreParam, fechaDesdeParam, fechaHastaParam, preguntasJsonParam);

                await SincronizarAulasAsync(id, dto.AulaIds);
                await _context.SaveChangesAsync();
            }
            else
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                var test = await _context.TestsPersonalizados
                    .Include(t => t.Preguntas)
                    .ThenInclude(p => p.Opciones)
                    .FirstOrDefaultAsync(t => t.Id == id && t.MaestroId == dto.MaestroId);

                if (test == null) return false;

                test.Nombre = dto.Nombre;
                test.FechaDisponibleDesde = dto.FechaDisponibleDesde;
                test.FechaDisponibleHasta = dto.FechaDisponibleHasta;

                _context.PreguntasTest.RemoveRange(test.Preguntas);

                foreach (var pDto in dto.Preguntas)
                {
                    var nuevaPregunta = new PreguntaTest
                    {
                        TestId = id,
                        TipoPreguntaId = pDto.TipoPreguntaId,
                        Pregunta = pDto.Pregunta,
                        Opciones = pDto.Opciones.Select((opt, idx) => new OpcionPregunta
                        {
                            TextoOpcion = opt,
                            EsCorrecta = idx == pDto.OpcionCorrectaIndex
                        }).ToList()
                    };
                    _context.PreguntasTest.Add(nuevaPregunta);
                }

                await SincronizarAulasAsync(id, dto.AulaIds);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            await _auditoriaService.RegistrarEventoDetalladoAsync(new LogAuditoria
            {
                FechaEvento = DateTime.UtcNow,
                Modulo = "TESTS",
                Accion = "ACTUALIZAR_TEST",
                EntidadAfectadaId = id.ToString(),
                JwtUsuarioEjecutor = dto.MaestroId.ToString(),
                RolAuth = "Maestro",
                JustificacionCambio = $"Se actualizó el test '{dto.Nombre}' (ID: {id}).",
                DatosAnteriores = datosAnterioresJson,
                DatosNuevos = JsonSerializer.Serialize(dto)
            });

            return true;
        }

        public async Task SincronizarAulasAsync(long testId, List<long> aulaIds)
        {
            if (aulaIds == null) return;

            var asignacionesExistentes = await _context.AulaTests
                .Where(ta => ta.TestId == testId)
                .ToListAsync();

            _context.AulaTests.RemoveRange(asignacionesExistentes);

            if (aulaIds.Any())
            {
                var nuevasAsignaciones = aulaIds.Select(aulaId => new AulaTest
                {
                    TestId = testId,
                    AulaId = aulaId
                });

                await _context.AulaTests.AddRangeAsync(nuevasAsignaciones);
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

        public async Task<bool> AnularTestAsync(long testId, long usuarioId, string nombreUsuario, string rolAuth, string justificacion)
        {
            var test = await _context.TestsPersonalizados.FindAsync(testId);
            if (test == null || !test.EstaActivo) return false;

            // 1. Aplicar Soft Delete atómico en SQL Server
            test.EstaActivo = false;
            await _context.SaveChangesAsync();

            // 2. Construir la auditoría detallada según la estructura requerida
            var log = new LogAuditoria
            {
                FechaEvento = DateTime.UtcNow,
                Modulo = "TESTS",
                Accion = "ANULACION_MASTER",
                EntidadAfectadaId = testId.ToString(),
                JwtUsuarioEjecutor = nombreUsuario,
                RolAuth = rolAuth,
                JustificacionCambio = justificacion,
                DatosAnteriores = new { estado = "CREADO" },
                DatosNuevos = new { estado = "ANULADO" }
            };

            // 3. Registrar en MongoDB en segundo plano (asíncrono, no bloquea al usuario)
            _ = Task.Run(() => _auditoriaService.RegistrarEventoDetalladoAsync(log));

            // 4. Invalidar caché en Redis
            string cacheKey = $"tests_maestro_{test.MaestroId}";
            await _cacheService.RemoveAsync(cacheKey);

            return true;
        }

        public async Task<List<TestDetalleResponseDto>> ObtenerTestsPorMaestroAsync(long maestroId)
        {
            string cacheKey = $"tests_maestro_{maestroId}";
            var testsEnCache = await _cacheService.GetAsync<List<TestDetalleResponseDto>>(cacheKey);

            if (testsEnCache != null)
            {
                return testsEnCache;
            }

            var testsDesdeDb = await _context.TestsPersonalizados
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

            if (testsDesdeDb.Any())
            {
                await _cacheService.SetAsync(cacheKey, testsDesdeDb, TimeSpan.FromMinutes(10));
            }

            return testsDesdeDb;
        }

        public async Task<RespuestaPaginadaDto<TestDetalleResponseDto>> ObtenerTestsPaginadosPorMaestroAsync(long maestroId, ConsultaTestsPaginadaDto dto)
        {
            string busquedaKey = string.IsNullOrWhiteSpace(dto.Busqueda) ? "all" : dto.Busqueda.Trim().ToLower();
            string aulaKey = dto.AulaId.HasValue ? dto.AulaId.Value.ToString() : "all";
            string fechaKey = dto.Fecha.HasValue ? dto.Fecha.Value.ToString("yyyyMMdd") : "all";
            string cursorKey = $"{dto.UltimoId ?? 0}_{dto.UltimaFecha?.ToString("yyyyMMddHHmmss") ?? "0"}";

            string cacheKey = $"tests_pag_m{maestroId}_lim{dto.Limite}_b{busquedaKey}_a{aulaKey}_f{fechaKey}_c{cursorKey}";

            var resultadoCached = await _cacheService.GetAsync<RespuestaPaginadaDto<TestDetalleResponseDto>>(cacheKey);
            if (resultadoCached != null)
            {
                return resultadoCached;
            }

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

            var respuesta = new RespuestaPaginadaDto<TestDetalleResponseDto>
            {
                Datos = datosPaginados,
                SiguienteUltimoId = ultimoRegistro?.TestId,
                SiguienteUltimaFecha = ultimoRegistro?.CreadoEn,
                TieneMasPaginas = tieneMasPaginas,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling((double)totalRegistros / limite)
            };

            await _cacheService.SetAsync(cacheKey, respuesta, TimeSpan.FromMinutes(5));

            return respuesta;
        }

        public async Task<bool> CambiarEstadoPreguntaAsync(long testId, long preguntaId, bool estaActiva, long maestroId)
        {
            // 1. Validar que la pregunta exista y pertenezca al test y maestro indicado
            var pregunta = await _context.PreguntasTest
                .Include(p => p.Test)
                .FirstOrDefaultAsync(p => p.Id == preguntaId && p.TestId == testId && p.Test.MaestroId == maestroId);

            if (pregunta == null) return false;

            // 2. Actualizar el estado de la pregunta (1 = activa, 0 = inactiva)
            pregunta.EstaActiva = estaActiva;

            // 3. Guardar cambios en SQL Server (Dispara el TRG_PreguntaTest_ControlTotal)
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<TestImpresionDto>> ObtenerDetalleTestsLoteAsync(long maestroId, List<long> testIds)
        {
            if (testIds == null || !testIds.Any()) return new List<TestImpresionDto>();

            var tests = await _context.TestsPersonalizados
                .AsNoTracking()
                .Where(t => testIds.Contains(t.Id) && t.MaestroId == maestroId && t.EstaActivo)
                .Include(t => t.Preguntas.Where(p => p.EstaActiva))
                .ThenInclude(p => p.Opciones)
                .ToListAsync();

            return tests.Select(t => new TestImpresionDto
            {
                TestId = t.Id,
                Nombre = t.Nombre,
                Preguntas = t.Preguntas.Select(p => new PreguntaImpresionDto
                {
                    PreguntaId = p.Id,
                    Enunciado = p.Pregunta,
                    TipoPreguntaId = p.TipoPreguntaId,
                    Opciones = p.Opciones.Select(o => new OpcionImpresionDto
                    {
                        OpcionId = o.Id,
                        TextoOpcion = o.TextoOpcion
                    }).ToList()
                }).ToList()
            }).ToList();
        }
    }
}
