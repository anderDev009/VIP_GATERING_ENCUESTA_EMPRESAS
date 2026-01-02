namespace VIP_GATERING.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ContrasenaHash { get; set; } = string.Empty; // placeholder hasta Identity

    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public ICollection<RolUsuario> RolesUsuario { get; set; } = new List<RolUsuario>();
}

