namespace VIP_GATERING.Domain.Entities;

public class OpcionHorario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OpcionId { get; set; }
    public Opcion? Opcion { get; set; }
    public Guid HorarioId { get; set; }
    public Horario? Horario { get; set; }
}
