namespace InkMath.AuthService.DTOs
{
    public record CrearAulaDto(string Nombre);

    public record VincularAlumnoDto(string CodigoAcceso);

    public class AulaResponseDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CodigoAcceso { get; set; } = string.Empty;
        public DateTimeOffset CreadoEn { get; set; }

        public int TotalEstudiantes { get; set; }
        public int TotalRecursos { get; set; }
        public int TotalTests { get; set; }
    }
    public class AulaDashboardResponseDto
    {
        public long AulaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CodigoAcceso { get; set; } = string.Empty;
        public List<EstudianteDashboardDto> Alumnos { get; set; } = new();
        public List<TestDashboardDto> Tests { get; set; } = new();
        public List<RecursoResponseDto> Recursos { get; set; } = new();
    }

    public class EstudianteDashboardDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public int Progreso { get; set; }
    }

    public class TestDashboardDto
    {
        public long TestId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int TotalPreguntas { get; set; }
    }
}
