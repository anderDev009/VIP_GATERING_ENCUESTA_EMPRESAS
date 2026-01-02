namespace VIP_GATERING.Domain.Entities;

public class RespuestaFormulario
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public int OpcionMenuId { get; set; }
    public OpcionMenu? OpcionMenu { get; set; }

    // Sucursal a la que se enviar la comida (puede diferir de la sucursal principal del empleado)
    public int SucursalEntregaId { get; set; }
    public Sucursal? SucursalEntrega { get; set; }

    // Localizacion dentro de la filial para entrega (opcional)
    public int? LocalizacionEntregaId { get; set; }
    public Localizacion? LocalizacionEntrega { get; set; }

    // Adicional fijo (se cobra 100% al empleado)
    public int? AdicionalOpcionId { get; set; }
    public Opcion? AdicionalOpcion { get; set; }

    // Seleccion: 'A', 'B', 'C', 'D' o 'E'
    public char Seleccion { get; set; }
}


