using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Models;

public class SemanaEmpleadoVM
{
    public Guid EmpleadoId { get; set; }
    public Guid MenuId { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaTermino { get; set; }
    public bool Bloqueado { get; set; }
    public bool BloqueadoPorEstado { get; set; }
    public string? MensajeBloqueo { get; set; }
    public string? EmpleadoNombre { get; set; }
    public bool EsJefe { get; set; }
    public bool EsVistaAdministrador { get; set; }
    public int RespuestasCount { get; set; }
    public int TotalDias { get; set; }
    public string OrigenMenu { get; set; } = string.Empty; // "Cliente" o "Sucursal"
    public string? EmpresaNombre { get; set; }
    public string? SucursalNombre { get; set; }
    public List<DiaEmpleadoVM> Dias { get; set; } = new();
}

public class DiaEmpleadoVM
{
    public Guid OpcionMenuId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public string? HorarioNombre { get; set; }
    public string? A { get; set; }
    public string? B { get; set; }
    public string? C { get; set; }
    public string? ImagenA { get; set; }
    public string? ImagenB { get; set; }
    public string? ImagenC { get; set; }
    [RegularExpression("[ABC]", ErrorMessage = "Seleccione A, B o C")]
    public char? Seleccion { get; set; }
}




