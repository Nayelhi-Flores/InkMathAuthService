namespace InkMath.AuthService.DTOs
{
    // DTO de entrada para las peticiones de consulta paginada
    public class ConsultaAulasPaginadaDto
    {
        public int Limite { get; set; } = 10; // Tamaño de página por defecto
        public long? UltimoId { get; set; }   // Cursor: ID del último elemento de la página anterior
        public DateTimeOffset? UltimaFecha { get; set; } // Cursor: Fecha del último elemento
    }

    // DTO de respuesta genérico con metadata del cursor
    public class RespuestaPaginadaDto<T>
    {
        public List<T> Datos { get; set; } = new();
        public long? SiguienteUltimoId { get; set; }
        public DateTimeOffset? SiguienteUltimaFecha { get; set; }
        public bool TieneMasPaginas { get; set; }
    }
}
