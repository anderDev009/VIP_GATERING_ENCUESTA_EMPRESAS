namespace VIP_GATERING.Domain.Entities;

public class SucursalHorario
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public int HorarioId { get; set; }
    public Horario? Horario { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
}

