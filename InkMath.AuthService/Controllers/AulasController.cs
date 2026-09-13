using InkMath.AuthService.DTOs;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public AulasController(IAulaService aulaService)
        {
            _aulaService = aulaService;
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
        public async Task<IActionResult> AgregarRecurso([FromForm] CrearRecursoMultipartDto dto,[FromServices] IFileStorageService fileStorageService)
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

        [HttpPost("vincular")]
        [Authorize(Roles ="3")] // Estudiante
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
    }
}
