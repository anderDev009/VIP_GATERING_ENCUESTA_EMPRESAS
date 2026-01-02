namespace VIP_GATERING.Domain.Entities;

public class OpcionMenu
{
    public int Id { get; set; }

    public int? OpcionIdA { get; set; }
    public Opcion? OpcionA { get; set; }

    public int? OpcionIdB { get; set; }
    public Opcion? OpcionB { get; set; }

    public int? OpcionIdC { get; set; }
    public Opcion? OpcionC { get; set; }

    public int? OpcionIdD { get; set; }
    public Opcion? OpcionD { get; set; }

    public int? OpcionIdE { get; set; }
    public Opcion? OpcionE { get; set; }

    public int OpcionesMaximas { get; set; } = 3;

    public int MenuId { get; set; }
    public Menu? Menu { get; set; }

    public DayOfWeek DiaSemana { get; set; }

    public int? HorarioId { get; set; }
    public Horario? Horario { get; set; }

    public ICollection<RespuestaFormulario> Respuestas { get; set; } = new List<RespuestaFormulario>();
}
