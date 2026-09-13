namespace InkMath.AuthService.DTOs
{
    public class CrearRecursoMultipartDto
    {
        public string Titulo { get; set; } = string.Empty;

        // 1 = Documento (PDF/JPG/PNG), 2 = Enlace, 3 = Video
        public long TipoRecursoId { get; set; }

        public string? UrlExterna { get; set; }
        public IFormFile? Archivo { get; set; }
    }

    public record RecursoResponseDto(
        long Id,
        string Titulo,
        long TipoRecursoId,
        string Referencia,
        DateTimeOffset CreadoEn
    );

    public class ConsultaRecursosPaginadaDto
    {
        public int Limite { get; set; } = 10;
        public long? UltimoId { get; set; }
        public DateTimeOffset? UltimaFecha { get; set; }
    }
}
