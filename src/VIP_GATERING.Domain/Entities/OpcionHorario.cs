namespace VIP_GATERING.Domain.Entities;

public class OpcionHorario
{
    public int Id { get; set; }
    public int OpcionId { get; set; }
    public Opcion? Opcion { get; set; }
    public int HorarioId { get; set; }
    public Horario? Horario { get; set; }
}
