namespace InkMath.AuthService.DTOs
{
    public class EnviarInteraccionDto
    {
        public long AulaId { get; set; }
        public string Contenido { get; set; } = string.Empty; // Emoji o frase fija
        public bool EsEmoji { get; set; }
        public long? MensajePadreId { get; set; }
    }

    public class InteraccionResponseDto
    {
        public long Id { get; set; }
        public long EstudianteId { get; set; }
        public string NombreEstudiante { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public bool EsEmoji { get; set; }
        public long? MensajePadreId { get; set; }
        public int VotosUtiles { get; set; }
        public DateTimeOffset EnviadoEn { get; set; }
        public bool EsMio { get; set; }
        public bool YaVote { get; set; }
    }

    public class ReportarMensajeDto
    {
        public string Motivo { get; set; } = string.Empty;
    }

    public class ModerarMensajeDto
    {
        public bool AprobarMensaje { get; set; } // True = Quita el reporte y lo publica de nuevo | False = Elimina
        public string Justificacion { get; set; } = string.Empty;
    }

    public class MensajeReportadoResponseDto
    {
        public long Id { get; set; }
        public long AulaId { get; set; }
        public string NombreAula { get; set; } = string.Empty;
        public long EstudianteId { get; set; }
        public string NombreEstudiante { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public bool EsEmoji { get; set; }
        public int VotosUtiles { get; set; }
        public DateTimeOffset EnviadoEn { get; set; }
    }
    public class AulaResumenArrecifeDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class PerfilArrecifeDto
    {
        public long UsuarioId { get; set; }
        public bool EsPreprimaria { get; set; }
        public List<string> Frases { get; set; } = new();
        public List<string> Emojis { get; set; } = new();
        public List<AulaResumenArrecifeDto> Aulas { get; set; } = new();
    }
}
