namespace VIP_GATERING.Domain.Entities;

public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public ICollection<RolUsuario> RolesUsuario { get; set; } = new List<RolUsuario>();
}

