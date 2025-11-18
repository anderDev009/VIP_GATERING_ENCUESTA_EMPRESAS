namespace VIP_GATERING.Domain.Entities;

public class OpcionMenu
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? OpcionIdA { get; set; }
    public Opcion? OpcionA { get; set; }

    public Guid? OpcionIdB { get; set; }
    public Opcion? OpcionB { get; set; }

    public Guid? OpcionIdC { get; set; }
    public Opcion? OpcionC { get; set; }

    public Guid MenuId { get; set; }
    public Menu? Menu { get; set; }

    public DayOfWeek DiaSemana { get; set; }

    public Guid? HorarioId { get; set; }
    public Horario? Horario { get; set; }

    public ICollection<RespuestaFormulario> Respuestas { get; set; } = new List<RespuestaFormulario>();
}
