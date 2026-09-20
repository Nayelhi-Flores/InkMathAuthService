using InkMath.AuthService.DTOs;
using InkMath.AuthService.Data;
using InkMath.AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace InkMath.AuthService.Services
{
    public interface ITestService
    {
        Task<TestDetalleResponseDto> CrearTestTransaccionalAsync(CrearTestDto dto);
        Task<bool> AnularTestAsync(long testId, long usuarioId);
        Task<bool> AsignarTestAAulasAsync(AsignarTestAulaDto dto, long usuarioId);
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

        public async Task<bool> AsignarTestAAulasAsync(AsignarTestAulaDto dto, long usuarioId)
        {
            var testExiste = await _context.TestsPersonalizados.AnyAsync(t => t.Id == dto.TestId && t.EstaActivo);
            if (!testExiste || dto.AulaIds == null || dto.AulaIds.Count == 0) return false;

            // Obtener asignaciones existentes para evitar duplicar registros en la clave compuesta
            var asignacionesExistentes = await _context.AulaTests
                .Where(at => at.TestId == dto.TestId && dto.AulaIds.Contains(at.AulaId))
                .Select(at => at.AulaId)
                .ToListAsync();

            var nuevasAulas = dto.AulaIds.Except(asignacionesExistentes).ToList();

            if (nuevasAulas.Count > 0)
            {
                foreach (var aulaId in nuevasAulas)
                {
                    _context.AulaTests.Add(new AulaTest
                    {
                        AulaId = aulaId,
                        TestId = dto.TestId,
                        FechaAsignacion = DateTimeOffset.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                // Registrar auditoría en MongoDB
                await _auditoriaService.RegistrarEventoAsync(
                    usuarioId,
                    "ASIGNAR_TEST_AULAS",
                    $"Se asignó el test ID: {dto.TestId} a {nuevasAulas.Count} nuevas aulas."
                );
            }

            return true;
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
    }
}
