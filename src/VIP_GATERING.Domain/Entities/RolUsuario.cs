namespace VIP_GATERING.Domain.Entities;

public class RolUsuario
{
    public int Id { get; set; }
    public int RolId { get; set; }
    public Rol? Rol { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}

