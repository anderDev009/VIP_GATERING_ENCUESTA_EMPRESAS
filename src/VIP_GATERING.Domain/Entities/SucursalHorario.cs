namespace VIP_GATERING.Domain.Entities;

public class SucursalHorario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public Guid HorarioId { get; set; }
    public Horario? Horario { get; set; }
}

