namespace VIP_GATERING.Domain.Entities;

public class Rol
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;

    public ICollection<RolUsuario> RolesUsuario { get; set; } = new List<RolUsuario>();
}

