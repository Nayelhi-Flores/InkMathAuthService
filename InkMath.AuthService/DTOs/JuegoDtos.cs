using System.Text.Json;

namespace InkMath.AuthService.DTOs
{
    public class JuegoInicioResponse
    {
        public string IntentoToken { get; set; } = string.Empty;
        public object? Rondas { get; set; }      // tienda y fisica; fracciones usa niveles fijos
        public int[]? Precios { get; set; }      // solo tienda: precios por id de ítem
    }

    public class JuegoResultadoRequest
    {
        public string IntentoToken { get; set; } = string.Empty;
        public JsonElement Respuestas { get; set; }   // la forma depende del modo
    }

    public class JuegoResultadoResponse
    {
        public int Aciertos { get; set; }
        public int Total { get; set; }
        public int Estrellas { get; set; }
        public long MonedasGanadas { get; set; }
        public long NuevoSaldoMonedas { get; set; }
    }
}