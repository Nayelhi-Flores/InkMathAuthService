using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;

using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Cryptography;

namespace InkMath.AuthService.Services
{
    public interface IAulaService
    {
        Task<string> GenerarCodigoUnicoAulaAsync();
        Task<(bool Exito, string Mensaje, string? Codigo)> CrearAulaAsync(long maestroId, string nombreAula);
        Task<(bool Exito, string Mensaje)> VincularAlumnoAClaseAsync(long estudianteId, string codigoAcceso);
        Task<List<AulaDto>> ObtenerAulasPorUsuarioAsync(long usuarioId, string rolNombre);
        Task<(bool Exito, string Mensaje)> EliminarAulaLogicoAsync(long aulaId, long maestroId);
        Task<(bool exito, string mensaje)> ActualizarAulaAsync(long aulaId, long maestroId, string nuevoNombre);
        Task<(bool Exito, string Mensaje, RecursoResponseDto? Recurso)> AgregarRecursoAsync(long maestroId,string titulo,long tipoRecursoId,string referencia, List<long>? aulaIds = null);
        Task<RespuestaPaginadaDto<AulaResponseDto>> ObtenerAulasPaginadasPorMaestroAsync(long maestroId, ConsultaAulasPaginadaDto dto);
        Task<RespuestaPaginadaDto<RecursoResponseDto>> ObtenerRecursosPorMaestroPaginadosAsync(long maestroId, ConsultaRecursosPaginadaDto dto);
        Task<List<RecursoResponseDto>> ObtenerRecursosPorAulaAsync(long aulaId, long usuarioId, string rolId);
        Task<(bool Exito, string Mensaje)> AsignarRecursoAAulasAsync(long maestroId, List<long> recursoIds, List<long> aulaIds);
    }

    public class AulaService : IAulaService
    {
        private readonly AplicationDbContext _context;
        private readonly IAuditoriaService _auditoriaService;

        // Caracteres no ambiguos (se excluyen 0, O, 1, I, L)
        private const string CaracteresPermitidos = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        public AulaService(AplicationDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        public async Task<string> GenerarCodigoUnicoAulaAsync()
        {
            string codigo;
            bool existe;

            do
            {
                codigo = GenerarCadenaAleatoria(6); // Ejemplo resultado: "K8P3X9"
                existe = await _context.Aulas.AnyAsync(a => a.CodigoAcceso == codigo);
            }
            while (existe);

            return codigo;
        }

        private string GenerarCadenaAleatoria(int longitud)
        {
            var bytes = new byte[longitud];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var resultado = new char[longitud];
            for (int i = 0; i < longitud; i++)
            {
                resultado[i] = CaracteresPermitidos[bytes[i] % CaracteresPermitidos.Length];
            }

            return new string(resultado);
        }

        public async Task<(bool Exito, string Mensaje, string? Codigo)> CrearAulaAsync(long maestroId, string nombreAula)
        {
            if (string.IsNullOrWhiteSpace(nombreAula))
                return (false, "El nombre del aula es obligatorio.", null);

            // 1. Obtener el nombre del plan a través de PlanId
            string planNombre = await ObtenerNombrePlanUsuarioAsync(maestroId);

            // 2. Validar límite de 3 aulas para la versión Gratuita
            if (planNombre.Equals("Gratuito", StringComparison.OrdinalIgnoreCase))
            {
                int conteoAulas = await _context.Aulas.CountAsync(a => a.MaestroId == maestroId);
                if (conteoAulas >= 3)
                {
                    return (false, "Has alcanzado el límite máximo de 3 aulas en el plan Gratuito.", null);
                }
            }

            // 3. Crear el aula
            string codigoGenerado = await GenerarCodigoUnicoAulaAsync();

            var nuevaAula = new Aula
            {
                Nombre = nombreAula.Trim(),
                CodigoAcceso = codigoGenerado,
                MaestroId = maestroId,
                CreadoEn = DateTimeOffset.UtcNow
            };

            _context.Aulas.Add(nuevaAula);
            await _context.SaveChangesAsync();

            return (true, "Aula creada exitosamente.", codigoGenerado);
        }

        public async Task<(bool Exito, string Mensaje)> VincularAlumnoAClaseAsync(long estudianteId, string codigoAcceso)
        {
            if (string.IsNullOrWhiteSpace(codigoAcceso))
                return (false, "El código de acceso es inválido.");

            // 1. Buscar el aula mediante el codigo_acceso
            var aula = await _context.Aulas
                .FirstOrDefaultAsync(a => a.CodigoAcceso.ToLower() == codigoAcceso.Trim().ToLower());

            if (aula == null)
                return (false, "El código de clase no existe.");

            // 2. Verificar si el alumno ya está inscrito en la tabla aula_estudiante
            bool yaInscrito = await _context.AulaEstudiantes
                .AnyAsync(ae => ae.AulaId == aula.Id && ae.EstudianteId == estudianteId);

            if (yaInscrito)
                return (true, "El estudiante ya pertenece a este aula.");

            // 3. Validar límite de alumnos por aula (20 máximo en Plan Gratuito)
            string planMaestro = await ObtenerNombrePlanUsuarioAsync(aula.MaestroId);

            if (planMaestro.Equals("Gratuito", StringComparison.OrdinalIgnoreCase))
            {
                int alumnosActuales = await _context.AulaEstudiantes.CountAsync(ae => ae.AulaId == aula.Id);
                if (alumnosActuales >= 20)
                {
                    return (false, "Esta aula ha alcanzado el cupo máximo de 20 estudiantes para el plan Gratuito.");
                }
            }

            // 4. Registrar la inscripción
            var nuevaInscripcion = new AulaEstudiante
            {
                AulaId = aula.Id,
                EstudianteId = estudianteId,
                FechaInscripcion = DateTimeOffset.UtcNow
            };

            _context.AulaEstudiantes.Add(nuevaInscripcion);
            await _context.SaveChangesAsync();

            return (true, "Estudiante vinculado exitosamente al aula.");
        }

        private async Task<string> ObtenerNombrePlanUsuarioAsync(long usuarioId)
        {
            // Consultar el plan activo relacionando Suscripcion con la tabla Planes mediante PlanId
            var planNombre = await (from s in _context.Suscripciones
                                    join p in _context.Planes on s.PlanId equals p.Id
                                    where s.UsuarioId == usuarioId && s.FechaFin >= DateTimeOffset.UtcNow
                                    select p.Nombre).FirstOrDefaultAsync();

            return planNombre ?? "Gratuito";
        }

        public async Task<List<AulaDto>> ObtenerAulasPorUsuarioAsync(long usuarioId, string rolNombre)
        {
            if (rolNombre.Equals("Maestro", StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Aulas
                    .Where(a => a.MaestroId == usuarioId)
                    .Select(a => new AulaDto(
                        a.Id,
                        a.Nombre,
                        a.CodigoAcceso,
                        _context.AulaEstudiantes.Count(ae => ae.AulaId == a.Id)
                    ))
                    .ToListAsync();
            }
            else
            {
                return await (from ae in _context.AulaEstudiantes
                              join a in _context.Aulas on ae.AulaId equals a.Id
                              where ae.EstudianteId == usuarioId
                              select new AulaDto(
                                  a.Id,
                                  a.Nombre,
                                  a.CodigoAcceso,
                                  _context.AulaEstudiantes.Count(x => x.AulaId == a.Id)
                              )).ToListAsync();
            }
        }

        public async Task<(bool exito, string mensaje)> ActualizarAulaAsync(long aulaId, long maestroId, string nuevoNombre)
        {
            if (string.IsNullOrWhiteSpace(nuevoNombre))
                return (false, "El nombre del aula no puede estar vacío.");

            var aula = await _context.Aulas.FirstOrDefaultAsync(a => a.Id == aulaId && a.MaestroId == maestroId);

            if (aula == null)
                return (false, "Aula no encontrada o no pertenece al maestro actual.");

            aula.Nombre = nuevoNombre.Trim();
            await _context.SaveChangesAsync();

            return (true, "Aula actualizada exitosamente.");
        }

        public async Task<RespuestaPaginadaDto<AulaResponseDto>> ObtenerAulasPaginadasPorMaestroAsync(long maestroId,ConsultaAulasPaginadaDto dto)
        {
            // Limitar el tamaño máximo de página para evitar sobrecargar la memoria
            int limite = Math.Min(dto.Limite, 5);

            // 1. Filtrar únicamente aulas del maestro que estén ACTIVAS
            var query = _context.Aulas
                .AsNoTracking()
                .Where(a => a.MaestroId == maestroId && a.EstaActivo);

            int totalRegistros = await query.CountAsync();

            // 2. Aplicar el filtro del Cursor si el cliente envió los parámetros de la página anterior
            if (dto.UltimaFecha.HasValue && dto.UltimoId.HasValue)
            {
                query = query.Where(a =>
                    a.CreadoEn < dto.UltimaFecha.Value ||
                    (a.CreadoEn == dto.UltimaFecha.Value && a.Id < dto.UltimoId.Value));
            }

            // 3. Traer un registro extra (limite + 1) para saber si existe una página siguiente
            var registros = await query
                .OrderByDescending(a => a.CreadoEn)
                .ThenByDescending(a => a.Id)
                .Take(limite + 1)
                .Select(a => new AulaResponseDto
                {
                    Id = a.Id,
                    Nombre = a.Nombre,
                    CodigoAcceso = a.CodigoAcceso,
                    CreadoEn = a.CreadoEn,

                    TotalEstudiantes = _context.AulaEstudiantes.Count(ae => ae.AulaId == a.Id),
                    TotalRecursos = _context.AulasRecursos.Count(ar => ar.AulaId == a.Id),
                    TotalTests = _context.AulaTests.Count(at => at.AulaId == a.Id)
                })
                .ToListAsync();

            bool tieneMasPaginas = registros.Count > limite;

            // Si hay página siguiente, omitimos el elemento extra traído para el check
            var datosPaginados = tieneMasPaginas ? registros.Take(limite).ToList() : registros;
            var ultimoRegistro = datosPaginados.LastOrDefault();

            return new RespuestaPaginadaDto<AulaResponseDto>
            {
                Datos = datosPaginados,
                SiguienteUltimoId = ultimoRegistro?.Id,
                SiguienteUltimaFecha = ultimoRegistro?.CreadoEn,
                TieneMasPaginas = tieneMasPaginas,
                TotalRegistros = totalRegistros,
                TotalPaginas = (int)Math.Ceiling((double)totalRegistros / limite)
            };
        }

        public async Task<(bool Exito, string Mensaje)> EliminarAulaLogicoAsync(long aulaId, long maestroId)
        {
            var aula = await _context.Aulas
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.MaestroId == maestroId && a.EstaActivo);

            if (aula == null)
                return (false, "El aula no existe o no se encuentra activa.");

            // Borrado lógico: se cambia de estado en lugar de hacer _context.Aulas.Remove(aula)
            aula.EstaActivo = false;
            await _context.SaveChangesAsync();

            // Aquí se registra el evento en MongoDB para la bitácora de auditoría
            await _auditoriaService.RegistrarEventoAsync(
             usuarioId: maestroId,
             accion: "ELIMINAR_AULA_LOGICO",
             descripcion: $"El maestro con ID {maestroId} desactivó el aula '{aula.Nombre}' (ID: {aulaId})."
            );

            return (true, "El aula ha sido eliminada exitosamente.");
        }

        public async Task<(bool Exito, string Mensaje, RecursoResponseDto? Recurso)> AgregarRecursoAsync(long maestroId,string titulo,long tipoRecursoId,string referencia, List<long>? aulaIds = null)
        {
            if (string.IsNullOrWhiteSpace(titulo))
                return (false, "El título del recurso es obligatorio.", null);

            // 1. Validar que el tipo_recurso exista (1, 2 o 3)
            bool tipoExiste = await _context.TiposRecurso.AnyAsync(t => t.Id == tipoRecursoId);
            if (!tipoExiste)
                return (false, "El tipo de recurso especificado no existe.", null);

            // 2. Instanciar y guardar el recurso
            var nuevoRecurso = new Recurso
            {
                MaestroId = maestroId,
                TipoRecursoId = tipoRecursoId,
                Titulo = titulo.Trim(),
                Referencia = referencia,
                CreadoEn = DateTimeOffset.UtcNow
            };

            _context.Recursos.Add(nuevoRecurso);
            await _context.SaveChangesAsync();

            // Si se enviaron aulas
            if (aulaIds != null && aulaIds.Any())
            {
                var aulasValidasIds = await _context.Aulas
                    .AsNoTracking()
                    .Where(a => aulaIds.Contains(a.Id) && a.MaestroId == maestroId && a.EstaActivo)
                    .Select(a => a.Id)
                    .ToListAsync();

                if (aulasValidasIds.Any())
                {
                    var asignaciones = aulasValidasIds.Select(aId => new AulaRecurso
                    {
                        AulaId = aId,
                        RecursoId = nuevoRecurso.Id,
                        AsignadoEn = DateTime.UtcNow
                    });

                    _context.AulasRecursos.AddRange(asignaciones);
                    await _context.SaveChangesAsync();
                }
            }

            var dtoRespuesta = new RecursoResponseDto(
                nuevoRecurso.Id,
                nuevoRecurso.Titulo,
                nuevoRecurso.TipoRecursoId,
                nuevoRecurso.Referencia,
                nuevoRecurso.CreadoEn
            );

            return (true, "Recurso registrado exitosamente.", dtoRespuesta);
        }

        public async Task<RespuestaPaginadaDto<RecursoResponseDto>> ObtenerRecursosPorMaestroPaginadosAsync(long maestroId,ConsultaRecursosPaginadaDto dto)
        {
            int limite = Math.Min(dto.Limite, 50);

            var query = _context.Recursos
                .AsNoTracking()
                .Where(r => r.MaestroId == maestroId);

            if (dto.UltimaFecha.HasValue && dto.UltimoId.HasValue)
            {
                query = query.Where(r =>
                    r.CreadoEn < dto.UltimaFecha.Value ||
                    (r.CreadoEn == dto.UltimaFecha.Value && r.Id < dto.UltimoId.Value));
            }

            var registros = await query
                .OrderByDescending(r => r.CreadoEn)
                .ThenByDescending(r => r.Id)
                .Take(limite + 1)
                .Select(r => new RecursoResponseDto(
                    r.Id,
                    r.Titulo,
                    r.TipoRecursoId,
                    r.Referencia,
                    r.CreadoEn
                ))
                .ToListAsync();

            bool tieneMasPaginas = registros.Count > limite;
            var datosPaginados = tieneMasPaginas ? registros.Take(limite).ToList() : registros;
            var ultimoRegistro = datosPaginados.LastOrDefault();

            return new RespuestaPaginadaDto<RecursoResponseDto>
            {
                Datos = datosPaginados,
                SiguienteUltimoId = ultimoRegistro?.Id,
                SiguienteUltimaFecha = ultimoRegistro?.CreadoEn,
                TieneMasPaginas = tieneMasPaginas
            };
        }

        public async Task<List<RecursoResponseDto>> ObtenerRecursosPorAulaAsync(long aulaId, long usuarioId, string rolId)
        {
            bool perteneceAlAula = false;

            if (rolId == "2") // Maestro
            {
                perteneceAlAula = await _context.Aulas
                    .AsNoTracking()
                    .AnyAsync(a => a.Id == aulaId && a.MaestroId == usuarioId && a.EstaActivo);
            }
            else if (rolId == "3") // Estudiante
            {
                perteneceAlAula = await _context.AulaEstudiantes
                    .AsNoTracking()
                    .AnyAsync(ae => ae.AulaId == aulaId && ae.EstudianteId == usuarioId && ae.Aula.EstaActivo);
            }

            // Si el usuario no pertenece a la clase o no tiene un rol válido, se retorna la lista vacía
            if (!perteneceAlAula)
                return new List<RecursoResponseDto>();

            // Consultar directamente la tabla intermedia 'aulas_recursos'
            return await _context.AulasRecursos
                .AsNoTracking()
                .Where(ar => ar.AulaId == aulaId)
                .OrderByDescending(ar => ar.AsignadoEn)
                .Select(ar => new RecursoResponseDto(
                    ar.Recurso.Id,
                    ar.Recurso.Titulo,
                    ar.Recurso.TipoRecursoId,
                    ar.Recurso.Referencia,
                    ar.Recurso.CreadoEn
                ))
                .ToListAsync();
        }

        public async Task<(bool Exito, string Mensaje)> AsignarRecursoAAulasAsync(long maestroId, List<long> recursoIds,List<long> aulaIds)
        {
            if (recursoIds == null || !recursoIds.Any())
                return (false, "Debe proporcionar al menos un ID de recurso.");

            if (aulaIds == null || !aulaIds.Any())
                return (false, "Debe proporcionar al menos un ID de aula.");

            // 1. Obtener solo los recursos válidos pertenecientes al maestro
            var recursosValidosIds = await _context.Recursos
                .AsNoTracking()
                .Where(r => recursoIds.Contains(r.Id) && r.MaestroId == maestroId)
                .Select(r => r.Id)
                .ToListAsync();

            if (!recursosValidosIds.Any())
                return (false, "Ninguno de los recursos especificados es válido o pertenece al maestro.");

            // 2. Obtener solo las aulas válidas y activas pertenecientes al maestro
            var aulasValidasIds = await _context.Aulas
                .AsNoTracking()
                .Where(a => aulaIds.Contains(a.Id) && a.MaestroId == maestroId && a.EstaActivo)
                .Select(a => a.Id)
                .ToListAsync();

            if (!aulasValidasIds.Any())
                return (false, "Ninguna de las aulas especificadas es válida o está activa.");

            // 3. Obtener combinaciones que ya están registradas en la base de datos
            var relacionesExistentes = await _context.AulasRecursos
                .AsNoTracking()
                .Where(ar => aulasValidasIds.Contains(ar.AulaId) && recursosValidosIds.Contains(ar.RecursoId))
                .Select(ar => new { ar.AulaId, ar.RecursoId })
                .ToListAsync();

            var hashExistentes = new HashSet<(long AulaId, long RecursoId)>(
                relacionesExistentes.Select(r => (r.AulaId, r.RecursoId))
            );

            // 4. Generar solo las nuevas asignaciones descartando duplicados
            var nuevasAsignaciones = new List<AulaRecurso>();
            DateTime fechaAsignacion = DateTime.UtcNow;

            foreach (var aulaId in aulasValidasIds)
            {
                foreach (var recursoId in recursosValidosIds)
                {
                    if (!hashExistentes.Contains((aulaId, recursoId)))
                    {
                        nuevasAsignaciones.Add(new AulaRecurso
                        {
                            AulaId = aulaId,
                            RecursoId = recursoId,
                            AsignadoEn = fechaAsignacion
                        });
                    }
                }
            }

            if (!nuevasAsignaciones.Any())
                return (true, "Todos los recursos ya se encontraban asignados a las aulas indicadas.");

            // 5. Insertar en lote (Bulk Insert)
            _context.AulasRecursos.AddRange(nuevasAsignaciones);
            await _context.SaveChangesAsync();

            return (true, $"Se realizaron {nuevasAsignaciones.Count} asignaciones de recursos a aulas exitosamente.");
        }
    }
}