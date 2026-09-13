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
    }
}
