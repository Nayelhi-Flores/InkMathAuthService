using InkMath.AuthService.DTOs;
using InkMath.AuthService.Models;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InkMath.AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere token JWT válido
    public class AulasController : ControllerBase
    {
        private readonly IAulaService _aulaService;
        private readonly IReporteService _reporteService;

        public AulasController(IAulaService aulaService, IReporteService reporteService)
        {
            _aulaService = aulaService;
            _reporteService = reporteService;
        }

        [HttpPost("crear")]
        [Authorize(Roles = "2")]
        public async Task<IActionResult> CrearAula([FromBody] CrearAulaDto dto)
        {
            long usuarioId = ObtenerUsuarioIdDesdeClaim();
            if (usuarioId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje, codigo) = await _aulaService.CrearAulaAsync(usuarioId, dto.Nombre);

            if (!exito)
                return BadRequest(new { mensaje });

            return Ok(new
            {
                mensaje,
                codigoAcceso = codigo
            });
        }

        [HttpGet("paginadas")]
        [Authorize(Roles = "2")] // Exclusivo para maestros
        public async Task<IActionResult> ObtenerAulasPaginadas([FromQuery] ConsultaAulasPaginadaDto dto)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var resultado = await _aulaService.ObtenerAulasPaginadasPorMaestroAsync(maestroId, dto);
            return Ok(resultado);
        }

        [HttpGet("mis-aulas")]
        [Authorize(Roles = "2,3")]
        public async Task<IActionResult> ObtenerMisAulas()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value;

            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                         ?? User.FindFirst("role")?.Value;

            if (!long.TryParse(userIdClaim, out long usuarioId))
            {
                return Unauthorized(new { mensaje = "Usuario no autenticado." });
            }

            string rolNombre = (roleClaim == "2") ? "Maestro" : "Estudiante";

            var aulas = await _aulaService.ObtenerAulasPorUsuarioAsync(usuarioId, rolNombre);

            return Ok(aulas);
        }

        [HttpPut("{id:long}")]
        [Authorize(Roles = "2")]
        public async Task<IActionResult> ActualizarAula(long id, [FromBody] ActualizarAulaDto dto)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje) = await _aulaService.ActualizarAulaAsync(id, maestroId, dto.Nombre);

            if (!exito)
                return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpDelete("{id:long}")]
        [Authorize(Roles = "2")]
        public async Task<IActionResult> EliminarAula(long id)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje) = await _aulaService.EliminarAulaLogicoAsync(id, maestroId);

            if (!exito)
                return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpPost("recursos")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "2")]
        public async Task<IActionResult> AgregarRecurso([FromForm] CrearRecursoMultipartDto dto, [FromServices] IFileStorageService fileStorageService)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            string referenciaFinal = string.Empty;

            // 1 = Documento (PDF / PNG / JPG)
            if (dto.TipoRecursoId == 1)
            {
                var (esValido, mensaje, nombreGuardado) = await fileStorageService.GuardarArchivoSeguroAsync(
                    dto.Archivo!,
                    "Uploads/Recursos",
                    new[] { ".pdf", ".jpg", ".jpeg", ".png" },
                    new[] { "application/pdf", "image/jpeg", "image/png" }
                );

                if (!esValido)
                    return BadRequest(new { mensaje });

                referenciaFinal = $"/uploads/recursos/{nombreGuardado}";
            }
            // 2 = Enlace Externo (Whitelist Educativa)
            else if (dto.TipoRecursoId == 2)
            {
                var (esValido, mensaje) = fileStorageService.ValidarUrlEnlaceSegura(dto.UrlExterna ?? string.Empty);
                if (!esValido)
                    return BadRequest(new { mensaje });

                referenciaFinal = dto.UrlExterna!.Trim();
            }
            // 3 = Video (YouTube)
            else if (dto.TipoRecursoId == 3)
            {
                var (esValido, mensaje, urlProcesada) = fileStorageService.ValidarUrlVideoSegura(dto.UrlExterna ?? string.Empty);
                if (!esValido)
                    return BadRequest(new { mensaje });

                referenciaFinal = urlProcesada;
            }
            else
            {
                return BadRequest(new { mensaje = "Tipo de recurso no válido." });
            }

            var (exito, msg, recursoDto) = await _aulaService.AgregarRecursoAsync(
                maestroId,
                dto.Titulo,
                dto.TipoRecursoId,
                referenciaFinal,
                dto.AulaIds
            );

            if (!exito)
                return BadRequest(new { mensaje = msg });

            return Ok(recursoDto);
        }

        [HttpPost("recursos/asignar-aulas")]
        [Authorize(Roles = "2")] // Exclusivo para rol Maestro
        public async Task<IActionResult> AsignarRecursoAAulas([FromBody] AsignarRecursoAulasDto dto)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje) = await _aulaService.AsignarRecursoAAulasAsync(maestroId, dto.RecursoId, dto.AulaIds);

            if (!exito)
                return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpDelete("{aulaId:long}/recursos/{recursoId:long}")]
        [Authorize(Roles = "2")] // Exclusivo para rol Maestro
        public async Task<IActionResult> DesvincularRecurso(long aulaId, long recursoId)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje) = await _aulaService.DesvincularRecursoDeAulaAsync(maestroId, aulaId, recursoId);

            if (!exito)
                return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpPost("vincular")]
        [EnableRateLimiting("VincularLimiter")]
        [Authorize(Roles = "3")] // Estudiante
        public async Task<IActionResult> VincularAlumno([FromBody] VincularAlumnoDto dto)
        {
            long usuarioId = ObtenerUsuarioIdDesdeClaim();
            if (usuarioId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje) = await _aulaService.VincularAlumnoAClaseAsync(usuarioId, dto.CodigoAcceso);

            if (!exito)
                return BadRequest(new { mensaje });

            return Ok(new { mensaje });
        }

        [HttpGet("recursos/mis-recursos")]
        [Authorize(Roles = "2")] // Maestro
        public async Task<IActionResult> ObtenerMisRecursos([FromQuery] ConsultaRecursosPaginadaDto dto)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0) return Unauthorized();

            var resultado = await _aulaService.ObtenerRecursosPorMaestroPaginadosAsync(maestroId, dto);
            return Ok(resultado);
        }

        [HttpGet("{aulaId:long}/recursos")]
        [Authorize(Roles = "2,3")]
        public async Task<IActionResult> ObtenerRecursosDeAula(long aulaId)
        {
            long usuarioId = ObtenerUsuarioIdDesdeClaim();
            if (usuarioId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            string rolId = User.FindFirst(ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value
                ?? string.Empty;

            if (string.IsNullOrEmpty(rolId))
                return Forbid();

            var recursos = await _aulaService.ObtenerRecursosPorAulaAsync(aulaId, usuarioId, rolId);
            return Ok(recursos);
        }

        private long ObtenerUsuarioIdDesdeClaim()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

            return long.TryParse(claimId, out long usuarioId) ? usuarioId : 0;
        }
        private async Task<bool> PuedeVerReportesAsync(long aulaId) =>
            User.IsInRole("1") || await _aulaService.EsMaestroDelAulaAsync(aulaId, ObtenerUsuarioIdDesdeClaim());

        [HttpGet("aula/{aulaId}/ranking")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> ObtenerRankingAula(long aulaId)
        {
            if (!await PuedeVerReportesAsync(aulaId)) return Forbid();
            var ranking = await _reporteService.ObtenerRankingAulaAsync(aulaId);
            return Ok(ranking);
        }

        [HttpGet("aula/{aulaId}/grupal")]
        [Authorize(Roles = "1,2")]
        public async Task<IActionResult> ObtenerReporteGrupal(long aulaId)
        {
            if (!await PuedeVerReportesAsync(aulaId)) return Forbid();
            var reporte = await _reporteService.ObtenerReporteGrupalAulaAsync(aulaId);
            if (reporte == null)
            {
                return NotFound(new { message = "El aula especificada no existe o no se encuentra activa." });
            }

            return Ok(reporte);
        }

        [HttpGet("{id:long}/dashboard")]
        [Authorize(Roles = "2")] // Exclusivo para Maestros
        public async Task<IActionResult> ObtenerDashboardAula(long id)
        {
            long maestroId = ObtenerUsuarioIdDesdeClaim();
            if (maestroId == 0)
                return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var dashboard = await _aulaService.ObtenerDashboardAulaAsync(id, maestroId);

            if (dashboard == null)
                return NotFound(new { mensaje = "El aula no fue encontrada o no le pertenece." });

            return Ok(dashboard);
        }

        [HttpDelete("{aulaId:long}/salir")]
        [Authorize(Roles = "3")]
        public async Task<IActionResult> SalirDeAula(long aulaId)
        {
            long estudianteId = ObtenerUsuarioIdDesdeClaim();
            if (estudianteId == 0) return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var (exito, mensaje) = await _aulaService.SalirDeAulaAsync(estudianteId, aulaId);
            return exito ? Ok(new { mensaje }) : BadRequest(new { mensaje });
        }

        [HttpGet("{aulaId:long}/tests")]
        [Authorize(Roles = "3")]
        public async Task<IActionResult> ObtenerTestsDeAula(long aulaId)
        {
            long estudianteId = ObtenerUsuarioIdDesdeClaim();
            if (estudianteId == 0) return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var tests = await _aulaService.ObtenerTestsDeAulaParaEstudianteAsync(aulaId, estudianteId);
            if (tests == null) return StatusCode(403, new { mensaje = "No perteneces a esta aula." });
            return Ok(tests);
        }

        [HttpGet("{aulaId:long}/ranking-companeros")]
        [Authorize(Roles = "3")]
        public async Task<IActionResult> ObtenerRankingCompaneros(long aulaId)
        {
            long estudianteId = ObtenerUsuarioIdDesdeClaim();
            if (estudianteId == 0) return Unauthorized(new { mensaje = "Token inválido o expirado." });

            var ranking = await _reporteService.ObtenerRankingCompanerosAsync(aulaId, estudianteId);
            if (ranking == null) return StatusCode(403, new { mensaje = "No perteneces a esta aula." });
            return Ok(ranking);
        }

        // Los documentos NO se sirven como archivos públicos: solo quien pertenece al aula puede abrirlos
        [HttpGet("{aulaId:long}/recursos/{recursoId:long}/archivo")]
        [Authorize(Roles = "2,3")]
        public async Task<IActionResult> AbrirDocumentoDeAula(long aulaId, long recursoId, [FromServices] IWebHostEnvironment env)
        {
            long usuarioId = ObtenerUsuarioIdDesdeClaim();
            if (usuarioId == 0) return Unauthorized();

            string rolId = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? string.Empty;

            // Ya valida que el usuario pertenezca al aula
            var recursos = await _aulaService.ObtenerRecursosPorAulaAsync(aulaId, usuarioId, rolId);
            var recurso = recursos.FirstOrDefault(r => r.Id == recursoId && r.TipoRecursoId == 1);
            if (recurso == null) return NotFound();

            var nombre = Path.GetFileName(recurso.Referencia);   // evita rutas relativas (../)
            var ruta = Path.Combine(env.ContentRootPath, "Uploads", "Recursos", nombre);
            if (!System.IO.File.Exists(ruta)) return NotFound();

            string? tipo = Path.GetExtension(nombre).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => null
            };
            if (tipo == null) return NotFound();

            return PhysicalFile(ruta, tipo);
        }
    }
}
