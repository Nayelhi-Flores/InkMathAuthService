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
        Task<PerfilArrecifeDto?> ObtenerPerfilArrecifeAsync(long estudianteId);
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

            var ultimo = await _context.InteraccionesChat.AsNoTracking()
                        .Where(ic => ic.EstudianteId == estudianteId)
                        .OrderByDescending(ic => ic.EnviadoEn)
                        .Select(ic => (DateTimeOffset?)ic.EnviadoEn)
                        .FirstOrDefaultAsync();
            if (ultimo.HasValue && (DateTimeOffset.UtcNow - ultimo.Value).TotalSeconds < 5)
                return (false, "Espera unos segundos antes de enviar otro mensaje.", null);

            // Una respuesta debe apuntar a una pregunta existente, de la misma aula (no a otra respuesta)
            if (dto.MensajePadreId.HasValue)
            {
                var padre = await _context.InteraccionesChat.AsNoTracking()
                    .Where(ic => ic.Id == dto.MensajePadreId.Value)
                    .Select(ic => new { ic.AulaId, ic.MensajePadreId, ic.EstaActivo })
                    .FirstOrDefaultAsync();
                if (padre == null || !padre.EstaActivo || padre.AulaId != dto.AulaId || padre.MensajePadreId != null)
                    return (false, "La pregunta que quieres responder no está disponible.", null);
            }
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
                EnviadoEn = nuevaInteraccion.EnviadoEn,
                EsMio = true
            };

            return (true, "Mensaje enviado con éxito al Arrecife de Ayuda.", respuestaDto);
        }

        public async Task<List<InteraccionResponseDto>?> ObtenerMensajesAulaAsync(long aulaId, long usuarioId)
        {
            bool esAlumno = await _context.AulaEstudiantes.AsNoTracking()
                .AnyAsync(ae => ae.AulaId == aulaId && ae.EstudianteId == usuarioId);
            bool esMaestro = !esAlumno && await _context.Aulas.AsNoTracking()
                .AnyAsync(a => a.Id == aulaId && a.MaestroId == usuarioId);
            if (!esAlumno && !esMaestro) return null;

            var raices = await _context.InteraccionesChat.AsNoTracking()
                .Where(ic => ic.AulaId == aulaId && ic.EstaActivo && !ic.EstaReportado && ic.MensajePadreId == null)
                .OrderByDescending(ic => ic.EnviadoEn)
                .Take(30)
                .Select(ic => ic.Id)
                .ToListAsync();

            var mensajes = await _context.InteraccionesChat.AsNoTracking()
                .Where(ic => ic.AulaId == aulaId && ic.EstaActivo && !ic.EstaReportado
                             && (raices.Contains(ic.Id)
                                 || (ic.MensajePadreId != null && raices.Contains(ic.MensajePadreId.Value))))
                .OrderBy(ic => ic.EnviadoEn)
                .Take(400)
                .Select(ic => new
                {
                    ic.Id,
                    ic.EstudianteId,
                    Nombre = ic.Estudiante != null ? ic.Estudiante.Nombre : "Usuario",
                    Apellido = ic.Estudiante != null ? ic.Estudiante.Apellido : "",
                    ic.Contenido,
                    ic.EsEmoji,
                    ic.MensajePadreId,
                    ic.VotosUtiles,
                    ic.EnviadoEn
                })
                .ToListAsync();

            var ids = mensajes.Select(m => m.Id).ToList();
            var votados = (await _context.VotosInteraccionChat.AsNoTracking()
                .Where(v => v.UsuarioId == usuarioId && ids.Contains(v.MensajeId))
                .Select(v => v.MensajeId)
                .ToListAsync()).ToHashSet();

            return mensajes.Select(m => new InteraccionResponseDto
            {
                Id = m.Id,
                EstudianteId = m.EstudianteId,
                NombreEstudiante = NombrePublico(m.Nombre, m.Apellido),
                Contenido = m.Contenido,
                EsEmoji = m.EsEmoji,
                MensajePadreId = m.MensajePadreId,
                VotosUtiles = m.VotosUtiles,
                EnviadoEn = m.EnviadoEn,
                EsMio = m.EstudianteId == usuarioId,
                YaVote = votados.Contains(m.Id)
            }).ToList();
        }

        private static string NombrePublico(string nombre, string apellido)
        {
            var inicial = string.IsNullOrWhiteSpace(apellido) ? "" : $" {char.ToUpperInvariant(apellido.Trim()[0])}.";
            return $"{nombre}{inicial}";
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
            const long Premio = 5;
            const long TopeDiario = 50;

            var mensaje = await _context.InteraccionesChat
                .FirstOrDefaultAsync(ic => ic.Id == mensajeId && ic.EstaActivo);
            if (mensaje == null) return (false, "El mensaje especificado no existe.");
            if (mensaje.EstudianteId == usuarioId) return (false, "No puedes votar tu propio mensaje.");
            if (mensaje.MensajePadreId == null) return (false, "Solo las respuestas se pueden marcar como útiles.");

            bool perteneceAula = await _context.AulaEstudiantes.AsNoTracking()
                .AnyAsync(ae => ae.AulaId == mensaje.AulaId && ae.EstudianteId == usuarioId);
            bool esMaestroDelAula = !perteneceAula && await _context.Aulas.AsNoTracking()
                .AnyAsync(a => a.Id == mensaje.AulaId && a.MaestroId == usuarioId);
            if (!perteneceAula && !esMaestroDelAula)
                return (false, "No tienes permisos para interactuar con este mensaje.");

            string key = $"ARRECIFE_UTIL_MSG_{mensajeId}_USER_{usuarioId}";

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var voto = await _context.VotosInteraccionChat
                    .FirstOrDefaultAsync(v => v.MensajeId == mensajeId && v.UsuarioId == usuarioId);

                if (voto != null)
                {
                    // Quitar voto (y devolver el premio si se había pagado)
                    _context.VotosInteraccionChat.Remove(voto);
                    mensaje.VotosUtiles = Math.Max(0, mensaje.VotosUtiles - 1);

                    var trans = await _context.TransaccionesMonedas.FirstOrDefaultAsync(t => t.IdempotencyKey == key);
                    if (trans != null)
                    {
                        var saldoAutor = await _context.SaldoMonedas
                            .FirstOrDefaultAsync(s => s.EstudianteId == mensaje.EstudianteId);
                        if (saldoAutor != null)
                        {
                            saldoAutor.Saldo = Math.Max(0, saldoAutor.Saldo - trans.Monto);
                            saldoAutor.ActualizadoEn = DateTimeOffset.UtcNow;
                        }
                        _context.TransaccionesMonedas.Remove(trans);
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    return (true, "Has quitado tu voto.");
                }

                _context.VotosInteraccionChat.Add(new VotoInteraccionChat
                {
                    MensajeId = mensajeId,
                    UsuarioId = usuarioId,
                    CreadoEn = DateTimeOffset.UtcNow
                });
                mensaje.VotosUtiles += 1;

                // Premio al autor con tope diario (frena el intercambio de votos entre cuentas)
                var inicioDia = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
                long ganadoHoy = await _context.TransaccionesMonedas
                    .Where(t => t.EstudianteId == mensaje.EstudianteId
                                && t.IdempotencyKey.StartsWith("ARRECIFE_UTIL_")
                                && t.CreadoEn >= inicioDia)
                    .SumAsync(t => (long?)t.Monto) ?? 0;
                long premio = Math.Max(0, Math.Min(Premio, TopeDiario - ganadoHoy));

                if (premio > 0)
                {
                    _context.TransaccionesMonedas.Add(new TransaccionMoneda
                    {
                        EstudianteId = mensaje.EstudianteId,
                        TipoTransaccionId = 1,
                        Monto = premio,
                        IdempotencyKey = key,
                        CreadoEn = DateTimeOffset.UtcNow
                    });

                    var saldo = await _context.SaldoMonedas.FirstOrDefaultAsync(s => s.EstudianteId == mensaje.EstudianteId);
                    if (saldo == null)
                        _context.SaldoMonedas.Add(new SaldoMoneda
                        {
                            EstudianteId = mensaje.EstudianteId,
                            Saldo = premio,
                            ActualizadoEn = DateTimeOffset.UtcNow
                        });
                    else
                    {
                        saldo.Saldo += premio;
                        saldo.ActualizadoEn = DateTimeOffset.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, premio > 0 ? "¡Marcado como útil! El autor ganó monedas." : "Marcado como útil.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
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

        public async Task<PerfilArrecifeDto?> ObtenerPerfilArrecifeAsync(long estudianteId)
        {
            var u = await _context.Usuarios.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == estudianteId && x.EstaActivo);
            if (u == null) return null;

            var aulas = await _context.AulaEstudiantes.AsNoTracking()
                .Where(ae => ae.EstudianteId == estudianteId && ae.Aula.EstaActivo)
                .Select(ae => new AulaResumenArrecifeDto { Id = ae.AulaId, Nombre = ae.Aula.Nombre })
                .OrderBy(a => a.Nombre)
                .ToListAsync();

            bool pre = EsPreprimaria(u.FechaNacimiento);
            return new PerfilArrecifeDto
            {
                UsuarioId = u.Id,
                EsPreprimaria = pre,
                Frases = pre ? FrasesPermitidas.ToList() : new(),
                Emojis = pre ? EmojisPermitidos.ToList() : new(),
                Aulas = aulas
            };
        }


    }
}