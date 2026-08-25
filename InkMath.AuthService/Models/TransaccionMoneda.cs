using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("transacciones_monedas")]
    public class TransaccionMoneda
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("estudiante_id")]
        public long EstudianteId { get; set; }

        [Column("tipo_transaccion_id")]
        public long TipoTransaccionId { get; set; }

        [Column("monto")]
        public long Monto { get; set; }

        [Required]
        [Column("idempotency_key")]
        public string IdempotencyKey { get; set; } = string.Empty;

        [Column("creado_en")]
        public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    }
}
