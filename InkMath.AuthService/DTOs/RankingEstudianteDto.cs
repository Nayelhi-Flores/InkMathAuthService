namespace InkMath.AuthService.DTOs
{
    public class RankingEstudianteDto
    {
        public long EstudianteId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public int PuntajeTotalNiveles { get; set; }
        public int PuntajeTotalTests { get; set; }
        public int PuntajeAcumulado { get; set; }
        public int Posicion { get; set; }
    }
}
