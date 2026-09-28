namespace InkMath.AuthService.DTOs
{
    // DTO de entrada para las peticiones de consulta paginada
    public class ConsultaAulasPaginadaDto
    {
        public int Limite { get; set; } = 10; // Tamaño de página por defecto
        public long? UltimoId { get; set; }   // Cursor: ID del último elemento de la página anterior
        public DateTimeOffset? UltimaFecha { get; set; } // Cursor: Fecha del último elemento
        public string? Busqueda { get; set; }
    }

    public class ConsultaTestsPaginadaDto
    {
        public int Limite { get; set; } = 5;
        public long? UltimoId { get; set; }
        public DateTimeOffset? UltimaFecha { get; set; }
        public string? Busqueda { get; set; }
        public long? AulaId { get; set; }
        public DateTime? Fecha { get; set; }
    }

    // DTO de respuesta genérico con metadata del cursor
    public class RespuestaPaginadaDto<T>
    {
        public List<T> Datos { get; set; } = new();
        public long? SiguienteUltimoId { get; set; }
        public DateTimeOffset? SiguienteUltimaFecha { get; set; }
        public bool TieneMasPaginas { get; set; }
        public int TotalRegistros { get; set; }
        public int TotalPaginas { get; set; }
    }
}
