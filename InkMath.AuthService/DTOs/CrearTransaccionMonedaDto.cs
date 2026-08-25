using System.ComponentModel.DataAnnotations;

namespace InkMath.AuthService.DTOs
{
    public class CrearTransaccionMonedaDto
    {
        public long TipoTransaccionId { get; set; } = 1; // 1: Recompensa, 2: Consumo
        public long Monto { get; set; }
    }
}
