using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace InkMath.AuthService.DTOs
{
    public class FiltroReporteTestDto
    {
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public long? MaestroId { get; set; }
        public bool? EstaActivo { get; set; }
    }

    public class ReporteTestDto
    {
        [Column("id")]
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [Column("maestro_id")]
        [JsonPropertyName("maestro_id")]
        public long MaestroId { get; set; }

        [Column("nombre_maestro")]
        [JsonPropertyName("nombre_maestro")]
        public string NombreMaestro { get; set; } = string.Empty;

        [Column("nombre_test")]
        [JsonPropertyName("nombre_test")]
        public string NombreTest { get; set; } = string.Empty;

        [Column("creado_en")]
        [JsonPropertyName("creado_en")]
        public DateTimeOffset CreadoEn { get; set; }

        [Column("esta_activo")]
        [JsonPropertyName("esta_activo")]
        public bool EstaActivo { get; set; }

        [Column("total_preguntas")]
        [JsonPropertyName("total_preguntas")]
        public int TotalPreguntas { get; set; }
    }

    public class ReporteGrupalAulaDto
    {
        public long AulaId { get; set; }
        public string NombreAula { get; set; } = string.Empty;
        public int TotalEstudiantes { get; set; }
        public double PromedioGeneralNiveles { get; set; }
        public double PromedioGeneralTests { get; set; }
        public int TotalNivelesCompletados { get; set; }
        public int TotalTestsRealizados { get; set; }
    }

    public class AnalisisIndividualDto
    {
        public long EstudianteId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public List<DetalleNivelEstudianteDto> NivelesProgreso { get; set; } = new();
        public List<HistorialErrorDto> HistorialErrores { get; set; } = new();
    }

    public class DetalleNivelEstudianteDto
    {
        public long NivelId { get; set; }
        public int Puntaje { get; set; }
        public int Intentos { get; set; }
        public long EstadoId { get; set; }
    }

    public class HistorialErrorDto
    {
        public long IntentoId { get; set; }
        public long PreguntaId { get; set; }
        public string? RespuestaDada { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
