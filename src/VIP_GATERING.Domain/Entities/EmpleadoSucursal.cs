namespace VIP_GATERING.Domain.Entities;

public class EmpleadoSucursal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public Guid SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
}

