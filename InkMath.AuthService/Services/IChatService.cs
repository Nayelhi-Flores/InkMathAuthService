using InkMath.AuthService.Data;
using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using InkMath.AuthService.Models.InkMath.AuthService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InkMath.AuthService.Services
{
    public interface IChatService
    {
        Task<(bool Exito, string Mensaje, InteraccionResponseDto? Data)> EnviarMensajeArrecifeAsync(long estudianteId, EnviarInteraccionDto dto);
        Task<List<InteraccionResponseDto>> ObtenerMensajesAulaAsync(long aulaId, long usuarioId);
        Task<(bool Exito, string Mensaje)> VotarMensajeComoUtilAsync(long usuarioId, long mensajeId);
        Task<(bool Exito, string Mensaje)> ReportarMensajeAsync(long usuarioId, string nombreUsuario, string rolAuth, long mensajeId, string motivo);
        Task<(bool Exito, string Mensaje)> EliminarMensajePropioAsync(long estudianteId, string nombreEstudiante, string rolAuth, long mensajeId);
        Task<List<MensajeReportadoResponseDto>> ObtenerMensajesReportadosPorMaestroAsync(long maestroId);
        Task<(bool Exito, string Mensaje)> ModerarMensajeReportadoAsync(long maestroId, string nombreMaestro, string rolAuth, long mensajeId, ModerarMensajeDto dto);

    }

    public class ChatInvalidoException : Exception
    {
        public ChatInvalidoException(string mensaje) : base(mensaje) { }
    }

    public class ChatService : IChatService
    {
        private readonly IAuditoriaService _auditoriaService;
        private readonly AplicationDbContext _context;
        private readonly IConfiguration _configuration;

        // Listas blancas estrictas para preprimaria (RF-35)
        private static readonly HashSet<string> EmojisPermitidos = new() { "😀", "⭐", "🚀", "👍", "❤️", "🎯" };
        private static readonly HashSet<string> FrasesPermitidas = new() { "¡Lo logré!", "¡Excelente!", "Necesito ayuda", "¡Muy bien!" };

        public ChatService(AplicationDbContext context, IAuditoriaService auditoriaService, IConfiguration configuration)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _configuration = configuration;
        }

        public async Task<(bool Exito, string Mensaje, InteraccionResponseDto? Data)> EnviarMensajeArrecifeAsync(long estudianteId, EnviarInteraccionDto dto)
        {
            // 1. Validar que el estudiante exista y esté activo
            var estudiante = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == estudianteId && u.EstaActivo);

            if (estudiante == null)
                return (false, "Estudiante no encontrado.", null);

            // 2. Validar pertenencia al aula
            bool perteneceAula = await _context.AulaEstudiantes
                .AsNoTracking()
                .AnyAsync(ae => ae.AulaId == dto.AulaId && ae.EstudianteId == estudianteId);

            if (!perteneceAula)
                return (false, "No perteneces a esta aula.", null);

            // 3. Aplicar validación de Interacción Limitada por Edad (RF-35 - Preprimaria: 4 a 6 años)
            if (EsPreprimaria(estudiante.FechaNacimiento))
            {
                if (dto.EsEmoji)
                {
                    if (!EmojisPermitidos.Contains(dto.Contenido))
                        return (false, "Emoji no permitido para tu edad en el Arrecife de Ayuda.", null);
                }
                else
                {
                    if (!FrasesPermitidas.Contains(dto.Contenido))
                        return (false, "Frase no permitida. Solo puedes usar las frases fijas autorizadas.", null);
                }
            }
            else
            {
                // Validación general de contenido (RF-33, RNF-08 y RNF-09) para mayores
                var (esValido, mensajeError) = ValidarContenidoGeneral(dto.Contenido);
                if (!esValido)
                    return (false, mensajeError, null);
            }

            // 4. Guardar en la base de datos (PostgreSQL)
            var nuevaInteraccion = new InteraccionChat
            {
                AulaId = dto.AulaId,
                EstudianteId = estudianteId,
                Contenido = dto.Contenido.Trim(),
                EsEmoji = dto.EsEmoji,
                MensajePadreId = dto.MensajePadreId,
                EnviadoEn = DateTimeOffset.UtcNow,
                EstaActivo = true,
                EstaReportado = false
            };

            _context.InteraccionesChat.Add(nuevaInteraccion);
            await _context.SaveChangesAsync();

            var respuestaDto = new InteraccionResponseDto
            {
                Id = nuevaInteraccion.Id,
                EstudianteId = estudianteId,
                NombreEstudiante = $"{estudiante.Nombre} {estudiante.Apellido}",
                Contenido = nuevaInteraccion.Contenido,
                EsEmoji = nuevaInteraccion.EsEmoji,
                MensajePadreId = nuevaInteraccion.MensajePadreId,
                VotosUtiles = 0,
                EnviadoEn = nuevaInteraccion.EnviadoEn
            };

            return (true, "Mensaje enviado con éxito al Arrecife de Ayuda.", respuestaDto);
        }

        public async Task<List<InteraccionResponseDto>> ObtenerMensajesAulaAsync(long aulaId, long usuarioId)
        {
            return await _context.InteraccionesChat
                .AsNoTracking()
                .Where(ic => ic.AulaId == aulaId && ic.EstaActivo && !ic.EstaReportado)
                .OrderByDescending(ic => ic.EnviadoEn)
                .Take(50)
                .Select(ic => new InteraccionResponseDto
                {
                    Id = ic.Id,
                    EstudianteId = ic.EstudianteId,
                    NombreEstudiante = ic.Estudiante != null ? $"{ic.Estudiante.Nombre} {ic.Estudiante.Apellido}" : "Usuario",
                    Contenido = ic.Contenido,
                    EsEmoji = ic.EsEmoji,
                    MensajePadreId = ic.MensajePadreId,
                    VotosUtiles = ic.VotosUtiles,
                    EnviadoEn = ic.EnviadoEn
                })
                .ToListAsync();
        }

        private bool EsPreprimaria(DateTimeOffset? fechaNacimiento)
        {
            if (!fechaNacimiento.HasValue) return false;

            var edad = DateTimeOffset.UtcNow.Year - fechaNacimiento.Value.Year;
            if (fechaNacimiento.Value.Date > DateTimeOffset.UtcNow.AddYears(-edad).Date) edad--;

            return edad >= 4 && edad <= 6;
        }

        public async Task<(bool Exito, string Mensaje)> VotarMensajeComoUtilAsync(long usuarioId, long mensajeId)
        {
            var mensaje = await _context.InteraccionesChat
                .Include(ic => ic.Estudiante)
                .FirstOrDefaultAsync(ic => ic.Id == mensajeId && ic.EstaActivo);

            if (mensaje == null)
                return (false, "El mensaje especificado no existe.");

            bool perteneceAula = await _context.AulaEstudiantes
                .AsNoTracking()
                .AnyAsync(ae => ae.AulaId == mensaje.AulaId && ae.EstudianteId == usuarioId);

            var rolUsuario = await _context.Usuarios
                .AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.RoleId)
                .FirstOrDefaultAsync();

            if (!perteneceAula && rolUsuario != 2)
                return (false, "No tienes permisos para interactuar con este mensaje.");

            // Clave única para identificar si este usuario ya votó por este mensaje específico
            string idempotencyKey = $"ARRECIFE_UTIL_MSG_{mensaje.Id}_USER_{usuarioId}";

            // Verificar si ya existe una transacción de recompensa previa (es decir, ya votó)
            var transaccionExistente = await _context.TransaccionesMonedas
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);

            if (transaccionExistente != null)
            {
                // --- TOGGLE: QUITAR VOTO ---
                mensaje.VotosUtiles = Math.Max(0, mensaje.VotosUtiles - 1);

                // Si el autor del mensaje era un alumno distinto, le descontamos las monedas que se le habían otorgado
                if (mensaje.EstudianteId != usuarioId)
                {
                    long monedasPremio = 5;
                    var saldoEntity = await _context.SaldoMonedas
                        .FirstOrDefaultAsync(s => s.EstudianteId == mensaje.EstudianteId);

                    if (saldoEntity != null)
                    {
                        saldoEntity.Saldo = Math.Max(0, saldoEntity.Saldo - monedasPremio);
                        saldoEntity.ActualizadoEn = DateTimeOffset.UtcNow;
                    }

                    // Eliminamos la transacción anterior para liberar la clave de idempotencia
                    _context.TransaccionesMonedas.Remove(transaccionExistente);
                }

                await _context.SaveChangesAsync();
                return (true, "Has quitado tu voto al mensaje.");
            }
            else
            {
                // --- DAR VOTO (NUEVO) ---
                mensaje.VotosUtiles += 1;

                if (mensaje.EstudianteId != usuarioId)
                {
                    long monedasPremio = 5;
                    var saldoEntity = await _context.SaldoMonedas
                        .FirstOrDefaultAsync(s => s.EstudianteId == mensaje.EstudianteId);

                    _context.TransaccionesMonedas.Add(new TransaccionMoneda
                    {
                        EstudianteId = mensaje.EstudianteId,
                        TipoTransaccionId = 1,
                        Monto = monedasPremio,
                        IdempotencyKey = idempotencyKey,
                        CreadoEn = DateTimeOffset.UtcNow
                    });

                    if (saldoEntity == null)
                    {
                        _context.SaldoMonedas.Add(new SaldoMoneda
                        {
                            EstudianteId = mensaje.EstudianteId,
                            Saldo = monedasPremio,
                            ActualizadoEn = DateTimeOffset.UtcNow
                        });
                    }
                    else
                    {
                        saldoEntity.Saldo += monedasPremio;
                        saldoEntity.ActualizadoEn = DateTimeOffset.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                return (true, "¡Mensaje marcado como útil! Se ha premiado al autor con monedas.");
            }
        }

        public async Task<(bool Exito, string Mensaje)> ReportarMensajeAsync(long usuarioId, string nombreUsuario, string rolAuth, long mensajeId, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                return (false, "El motivo del reporte es obligatorio.");

            var mensaje = await _context.InteraccionesChat
                .FirstOrDefaultAsync(ic => ic.Id == mensajeId && ic.EstaActivo);

            if (mensaje == null)
                return (false, "El mensaje no existe o ya ha sido retirado.");

            mensaje.EstaReportado = true;
            await _context.SaveChangesAsync();

            // Log de Auditoría MongoDB detallado
            var log = new LogAuditoria
            {
                FechaEvento = DateTime.UtcNow,
                Modulo = "ARRECIFE_AYUDA",
                Accion = "REPORTAR_MENSAJE",
                EntidadAfectadaId = mensajeId.ToString(), // ID del MENSAJE reportado
                JwtUsuarioEjecutor = nombreUsuario,
                RolAuth = rolAuth,
                JustificacionCambio = $"Reportado por el usuario {nombreUsuario}. Motivo: {motivo}",
                DatosAnteriores = new { estado = "PUBLICADO", esta_reportado = false },
                DatosNuevos = new { estado = "REPORTADO", esta_reportado = true }
            };

            _ = Task.Run(() => _auditoriaService.RegistrarEventoDetalladoAsync(log));

            return (true, "El mensaje ha sido reportado y puesto bajo revisión.");
        }

        // Soft-delete por el propio estudiante
        public async Task<(bool Exito, string Mensaje)> EliminarMensajePropioAsync(long estudianteId, string nombreEstudiante, string rolAuth, long mensajeId)
        {
            var mensaje = await _context.InteraccionesChat
                .FirstOrDefaultAsync(ic => ic.Id == mensajeId && ic.EstaActivo);

            if (mensaje == null)
                return (false, "El mensaje no existe o ya fue eliminado.");

            if (mensaje.EstudianteId != estudianteId)
                return (false, "No tienes autorizacion para eliminar este mensaje.");

            mensaje.EstaActivo = false; // Soft Delete
            await _context.SaveChangesAsync();

            var log = new LogAuditoria
            {
                FechaEvento = DateTime.UtcNow,
                Modulo = "ARRECIFE_AYUDA",
                Accion = "ELIMINAR_MENSAJE_PROPIO",
                EntidadAfectadaId = mensajeId.ToString(),
                JwtUsuarioEjecutor = nombreEstudiante,
                RolAuth = rolAuth,
                JustificacionCambio = "Eliminado por el propio autor.",
                DatosAnteriores = new { estado = "PUBLICADO", esta_activo = true },
                DatosNuevos = new { estado = "ELIMINADO_SOFT", esta_activo = false }
            };

            _ = Task.Run(() => _auditoriaService.RegistrarEventoDetalladoAsync(log));

            return (true, "Mensaje eliminado correctamente.");
        }

        public async Task<List<MensajeReportadoResponseDto>> ObtenerMensajesReportadosPorMaestroAsync(long maestroId)
        {
            // Obtener las aulas que pertenecen al maestro
            var aulasMaestroIds = await _context.Aulas
                .AsNoTracking()
                .Where(a => a.MaestroId == maestroId && a.EstaActivo)
                .Select(a => a.Id)
                .ToListAsync();

            // Retornar mensajes que estén reportados y pertenezcan a las aulas del maestro
            return await _context.InteraccionesChat
                .AsNoTracking()
                .Include(ic => ic.Estudiante)
                .Include(ic => ic.Aula)
                .Where(ic => aulasMaestroIds.Contains(ic.AulaId) && ic.EstaReportado && ic.EstaActivo)
                .OrderByDescending(ic => ic.EnviadoEn)
                .Select(ic => new MensajeReportadoResponseDto
                {
                    Id = ic.Id,
                    AulaId = ic.AulaId,
                    NombreAula = ic.Aula != null ? ic.Aula.Nombre : "Aula",
                    EstudianteId = ic.EstudianteId,
                    NombreEstudiante = ic.Estudiante != null ? $"{ic.Estudiante.Nombre} {ic.Estudiante.Apellido}" : "Estudiante",
                    Contenido = ic.Contenido,
                    EsEmoji = ic.EsEmoji,
                    VotosUtiles = ic.VotosUtiles,
                    EnviadoEn = ic.EnviadoEn
                })
                .ToListAsync();
        }

        public async Task<(bool Exito, string Mensaje)> ModerarMensajeReportadoAsync(long maestroId, string nombreMaestro, string rolAuth, long mensajeId, ModerarMensajeDto dto)
        {
            var mensaje = await _context.InteraccionesChat
                .Include(ic => ic.Aula)
                .FirstOrDefaultAsync(ic => ic.Id == mensajeId && ic.EstaReportado);

            if (mensaje == null || mensaje.Aula == null)
                return (false, "El mensaje reportado no existe o ya fue moderado.");

            // Verificar que el aula pertenezca al maestro
            if (mensaje.Aula.MaestroId != maestroId)
                return (false, "No tienes permisos para moderar este mensaje.");

            string estadoAnterior = "REPORTADO";
            string estadoNuevo;

            if (dto.AprobarMensaje)
            {
                // El maestro decide que el mensaje es seguro: se le quita el reporte y vuelve a la vista pública
                mensaje.EstaReportado = false;
                estadoNuevo = "PUBLICADO";
            }
            else
            {
                // El maestro confirma la infracción: se aplica soft-delete definitivo
                mensaje.EstaActivo = false;
                mensaje.EstaReportado = false;
                estadoNuevo = "ANULADO_DEFINITIVO";
            }

            await _context.SaveChangesAsync();

            // Registro de Auditoría Detallado en MongoDB
            var log = new LogAuditoria
            {
                FechaEvento = DateTime.UtcNow,
                Modulo = "PANEL_MONITOREO_ARRECIFE",
                Accion = dto.AprobarMensaje ? "APROBAR_MENSAJE_REPORTADO" : "ANULAR_MENSAJE_REPORTADO",
                EntidadAfectadaId = mensajeId.ToString(),
                JwtUsuarioEjecutor = nombreMaestro,
                RolAuth = rolAuth,
                JustificacionCambio = string.IsNullOrWhiteSpace(dto.Justificacion) ? "Moderación desde el Panel de Monitoreo." : dto.Justificacion,
                DatosAnteriores = new { estado = estadoAnterior, contenido = mensaje.Contenido },
                DatosNuevos = new { estado = estadoNuevo, activo = mensaje.EstaActivo, reportado = mensaje.EstaReportado }
            };

            _ = Task.Run(() => _auditoriaService.RegistrarEventoDetalladoAsync(log));

            string mensajeResultado = dto.AprobarMensaje
                ? "El mensaje ha sido aprobado y devuelto al Arrecife."
                : "El mensaje ha sido anulado y retirado permanentemente.";

            return (true, mensajeResultado);
        }

        private (bool EsValido, string MensajeError) ValidarContenidoGeneral(string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido))
                return (false, "El mensaje no puede estar vacío.");

            // RNF-08: Longitud máxima controlada para un filtrado rápido
            if (contenido.Length > 250)
                return (false, "El mensaje excede la longitud permitida.");

            // RNF-09: Restricción de Datos Personales (PII) usando Expresiones Regulares (Regex)
            // Detectar posibles correos electrónicos
            var patronEmail = @"[\w-\.]+@([\w-]+\.)+[\w-]{2,4}";
            if (System.Text.RegularExpressions.Regex.IsMatch(contenido, patronEmail))
            {
                return (false, "Por tu seguridad, no está permitido compartir correos electrónicos en el chat.");
            }

            // Solo detecta si tiene formato explícito de teléfono (con guión, espacio o código internacional)
            // Ejemplos bloqueados: "5555-4444", "+502 12345678", "502-1234-5678"
            // Ejemplos permitidos (matemáticas): "1000", "500000", "3.1416"
            var patronTelefono = @"(\b\d{4}[-\s]\d{4}\b|\+\d{1,3}[-\s]?\d{1,4}[-\s]?\d{4})";
            if (System.Text.RegularExpressions.Regex.IsMatch(contenido, patronTelefono))
            {
                return (false, "Por tu seguridad, no está permitido compartir números telefónicos.");
            }

            // 1. Obtener las palabras prohibidas desde appsettings.json
            var palabrasProhibidas = _configuration.GetSection("FiltroContenido:PalabrasProhibidas").Get<string[]>()
                                     ?? Array.Empty<string>();

            // 2. Normalizar el mensaje del usuario (quitar tildes y pasar a minúsculas)
            string mensajeNormalizado = RemoverTildes(contenido.ToLowerInvariant());

            // 3. Revisar si alguna palabra prohibida está contenida en el mensaje
            foreach (var palabra in palabrasProhibidas)
            {
                string palabraNormalizada = RemoverTildes(palabra.ToLowerInvariant());

                // Verificamos si la palabra aparece completa o como subcadena
                if (mensajeNormalizado.Contains(palabraNormalizada))
                {
                    return (false, "El mensaje contiene lenguaje inapropiado.");
                }
            }

            return (true, string.Empty);
        }

        // Método auxiliar para eliminar tildes y acentos (Normalización Unicode)
        private string RemoverTildes(string texto)
        {
            var normalizedString = texto.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}