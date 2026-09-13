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
                    CreadoEn = DateTimeOffset.UtcNow
                };

                _context.TestsPersonalizados.Add(nuevoTest);
                await _context.SaveChangesAsync(); // Genera el Id del Maestro

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
                    $"Se creó el test '{nuevoTest.Nombre}' (ID: {nuevoTest.Id}) con {dto.Preguntas.Count} preguntas."
                );

                return new TestDetalleResponseDto
                {
                    TestId = nuevoTest.Id,
                    Nombre = nuevoTest.Nombre,
                    MaestroId = nuevoTest.MaestroId,
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
