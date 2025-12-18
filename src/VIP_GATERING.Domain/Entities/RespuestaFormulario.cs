namespace VIP_GATERING.Domain.Entities;

public class RespuestaFormulario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public Guid OpcionMenuId { get; set; }
    public OpcionMenu? OpcionMenu { get; set; }

    // Sucursal a la que se enviará la comida (puede diferir de la sucursal principal del empleado)
    public Guid SucursalEntregaId { get; set; }
    public Sucursal? SucursalEntrega { get; set; }

    // Adicional fijo (se cobra 100% al empleado)
    public Guid? AdicionalOpcionId { get; set; }
    public Opcion? AdicionalOpcion { get; set; }

    // Seleccion: 'A', 'B', 'C', 'D' o 'E'
    public char Seleccion { get; set; }
}
