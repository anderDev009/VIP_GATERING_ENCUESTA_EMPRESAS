namespace VIP_GATERING.Domain.Entities;

public class EmpleadoSucursal
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
}

