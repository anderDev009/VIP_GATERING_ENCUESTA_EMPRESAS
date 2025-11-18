namespace VIP_GATERING.Domain.Entities;

public class RespuestaFormulario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public Guid OpcionMenuId { get; set; }
    public OpcionMenu? OpcionMenu { get; set; }

    // Seleccion: 'A', 'B' o 'C'
    public char Seleccion { get; set; }
}

