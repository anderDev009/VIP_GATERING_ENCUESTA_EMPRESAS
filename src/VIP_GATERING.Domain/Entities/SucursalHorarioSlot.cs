namespace VIP_GATERING.Domain.Entities;

public class SucursalHorarioSlot
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public int HorarioId { get; set; }
    public Horario? Horario { get; set; }
    public TimeOnly Hora { get; set; }
}
