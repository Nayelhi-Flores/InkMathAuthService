namespace InkMath.AuthService.DTOs
{
    public record AulaDto(long Id, string Nombre, string CodigoAcceso, int TotalEstudiantes);

    public record PerfilUsuarioDto(
        long Id,
        string Nombre,
        string Email,
        string Rol,
        string Plan,
        List<AulaDto> Aulas
    );
}
