namespace VIP_GATERING.Domain.Entities;

public class RolUsuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RolId { get; set; }
    public Rol? Rol { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}

