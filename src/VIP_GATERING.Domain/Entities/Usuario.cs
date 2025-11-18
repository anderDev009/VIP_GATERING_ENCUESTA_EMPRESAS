namespace VIP_GATERING.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string ContrasenaHash { get; set; } = string.Empty; // placeholder hasta Identity

    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public ICollection<RolUsuario> RolesUsuario { get; set; } = new List<RolUsuario>();
}

