namespace VIP_GATERING.Domain.Entities;

public class Empleado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Borrado { get; set; } = false;

    public Guid SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    // Sucursales adicionales asignadas (además de SucursalId como principal)
    public ICollection<EmpleadoSucursal> SucursalesAsignadas { get; set; } = new List<EmpleadoSucursal>();

    public Usuario? Usuario { get; set; }

    public bool EsSubsidiado { get; set; } = true;
    public EmpleadoEstado Estado { get; set; } = EmpleadoEstado.Habilitado;
    public bool EsJefe { get; set; } = false;
}
