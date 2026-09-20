namespace InkMath.AuthService.DTOs
{
    public class MapaNivelesResponseDto
    {
        public List<MundoDto> Mundos { get; set; } = new();
    }

    public class MapaNivelDto
    {
        public long NivelId { get; set; }
        public long? TestId { get; set; } // Enlace al test
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Orden { get; set; }
        public string Estado { get; set; } = "No Iniciado";
        public int Puntaje { get; set; }
        public int Intentos { get; set; }
    }

    public class NivelDto
    {
        public long NivelId { get; set; }
        public long TestId { get; set; }
        public int NumeroNivel { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Tema { get; set; } = string.Empty;
        public string Estado { get; set; } = "No Iniciado";
        public int Puntaje { get; set; }
        public int Intentos { get; set; }
        public List<PreguntaDto> Preguntas { get; set; } = new();
    }

    public class MundoDto
    {
        public long MundoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public List<NivelDto> Niveles { get; set; } = new();
    }

    public class PreguntaDto
    {
        public long PreguntaId { get; set; }
        public string Enunciado { get; set; } = string.Empty;
        public List<OpcionDto> Opciones { get; set; } = new();
    }

    public class OpcionDto
    {
        public long OpcionId { get; set; }
        public string Texto { get; set; } = string.Empty;
    }

    public class EvaluacionDto
    {
        public long TestId { get; set; }
        public string NombreTest { get; set; } = string.Empty;
        public int TotalPreguntas { get; set; }
        public List<PreguntaDetalleDto> Preguntas { get; set; } = new();
    }

    public class PreguntaDetalleDto
    {
        public long PreguntaId { get; set; }
        public string Pregunta { get; set; } = string.Empty;
        public long TipoPreguntaId { get; set; }
        public List<OpcionDetalleDto> Opciones { get; set; } = new();
    }

    public class OpcionDetalleDto
    {
        public long OpcionId { get; set; }
        public string TextoOpcion { get; set; } = string.Empty;
    }
}
