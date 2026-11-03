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

    public class RankingFilaDto
    {
        public long EstudianteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public int PuntajeNiveles { get; set; }
        public int PuntajeTests { get; set; }
        public int PuntajeTotal { get; set; }
        public int Posicion { get; set; }
    }
}
