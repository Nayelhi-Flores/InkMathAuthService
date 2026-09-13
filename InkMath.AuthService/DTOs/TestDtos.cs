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
        public List<CrearPreguntaDto> Preguntas { get; set; } = new();
    }

    public class TestDetalleResponseDto
    {
        public long TestId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public long MaestroId { get; set; }
        public DateTimeOffset CreadoEn { get; set; }
        public int TotalPreguntas { get; set; }
    }
}
