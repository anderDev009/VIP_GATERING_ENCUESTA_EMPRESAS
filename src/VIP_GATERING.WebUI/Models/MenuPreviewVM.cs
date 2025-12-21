namespace VIP_GATERING.WebUI.Models;

public class MenuPreviewVM
{
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaTermino { get; set; }
    public string? EmpresaNombre { get; set; }
    public string? FilialNombre { get; set; }
    public string OrigenScope { get; set; } = "Empresa";
    public List<MenuPreviewDiaVM> Dias { get; set; } = new();
}

public class MenuPreviewDiaVM
{
    public DayOfWeek DiaSemana { get; set; }
    public string? HorarioNombre { get; set; }
    public int HorarioOrden { get; set; }
    public string? A { get; set; }
    public string? B { get; set; }
    public string? C { get; set; }
    public string? D { get; set; }
    public string? E { get; set; }
    public int OpcionesMaximas { get; set; } = 3;
}
