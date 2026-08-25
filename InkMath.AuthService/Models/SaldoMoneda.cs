using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("saldo_monedas")]
    public class SaldoMoneda
    {
        [Key]
        [Column("estudiante_id")]
        public long EstudianteId { get; set; }

        [Column("saldo")]
        public long Saldo { get; set; } = 0;

        [Column("actualizado_en")]
        public DateTimeOffset ActualizadoEn { get; set; } = DateTimeOffset.UtcNow;
    }
}