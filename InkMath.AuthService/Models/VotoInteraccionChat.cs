using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InkMath.AuthService.Models
{
    [Table("votos_interaccion_chat")]
    public class VotoInteraccionChat
    {
        [Key][Column("id")] public long Id { get; set; }
        [Column("mensaje_id")] public long MensajeId { get; set; }
        [Column("usuario_id")] public long UsuarioId { get; set; }
        [Column("creado_en")] public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    }
}