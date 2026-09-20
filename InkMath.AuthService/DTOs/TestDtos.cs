namespace InkMath.AuthService.DTOs
{
    public class CrearPreguntaDto
    {
        public long TipoPreguntaId { get; set; }
        public string Pregunta { get; set; } = string.Empty;
        public List<string> Opciones { get; set; } = new(); // Para guardar texto_opcion
        public int OpcionCorrectaIndex { get; set; } // Índice de la opción que es_correcta
    }

    public class CrearTestDto
    {
        public long MaestroId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime? FechaDisponibleDesde { get; set; }
        public DateTime? FechaDisponibleHasta { get; set; }
        public List<long> AulaIds { get; set; } = new();
        public List<CrearPreguntaDto> Preguntas { get; set; } = new();
    }

    public class TestDetalleResponseDto
    {
        public long TestId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public long MaestroId { get; set; }
        public DateTime? FechaDisponibleDesde { get; set; }
        public DateTime? FechaDisponibleHasta { get; set; }
        public int AulasAsignadas { get; set; }
        public DateTimeOffset CreadoEn { get; set; }
        public int TotalPreguntas { get; set; }
    }

    public class AsignarTestAulaDto
    {
        public long TestId { get; set; }
        public List<long> AulaIds { get; set; } = new();
    }

    public class RegistrarRespuestaDto
    {
        public long PreguntaId { get; set; }
        public long? OpcionId { get; set; }
        public string? RespuestaTexto { get; set; }
    }

    public class RegistrarIntentoTestDto
    {
        public long TestId { get; set; }
        public long EstudianteId { get; set; }
        public DateTimeOffset FechaInicio { get; set; }
        public DateTimeOffset FechaFin { get; set; } = DateTimeOffset.UtcNow;
        public List<RegistrarRespuestaDto> Respuestas { get; set; } = new();
    }

    public class ResultadoEvaluacionResponseDto
    {
        public long IntentoId { get; set; }
        public int TotalPreguntas { get; set; }
        public int RespuestasCorrectas { get; set; }
        public int PuntajeObtenido { get; set; }
        public long MonedasGanadas { get; set; }
        public long NuevoSaldoMonedas { get; set; }
    }
}
