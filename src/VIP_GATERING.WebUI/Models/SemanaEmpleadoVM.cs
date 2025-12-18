using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Models;

public class SemanaEmpleadoVM
{
    public Guid EmpleadoId { get; set; }
    public Guid MenuId { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaTermino { get; set; }
    public Guid SucursalEntregaId { get; set; }
    public List<(Guid id, string nombre)> SucursalesEntregaDisponibles { get; set; } = new();
    public List<AdicionalDisponibleVM> AdicionalesDisponibles { get; set; } = new();
    public string SemanaClave { get; set; } = "siguiente";
    public List<SemanaOpcionVM> SemanasDisponibles { get; set; } = new();
    public bool Bloqueado { get; set; }
    public bool BloqueadoPorEstado { get; set; }
    public string? MensajeBloqueo { get; set; }
    public string? NotaVentana { get; set; }
    public string? EmpleadoNombre { get; set; }
    public bool EsJefe { get; set; }
    public bool EsVistaAdministrador { get; set; }
    public int RespuestasCount { get; set; }
    public int TotalDias { get; set; }
    public string OrigenMenu { get; set; } = string.Empty; // "Cliente" o "Dependiente"
    public string? EmpresaNombre { get; set; }
    public string? SucursalNombre { get; set; }
    public string? SucursalEntregaNombre { get; set; }
    public List<DiaEmpleadoVM> Dias { get; set; } = new();
    public decimal TotalEmpleado { get; set; }
    public decimal? TotalEmpresa { get; set; }
}

public class SemanaOpcionVM
{
    public string Clave { get; set; } = string.Empty; // "actual" | "siguiente"
    public string Etiqueta { get; set; } = string.Empty;
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }
}

public class DiaEmpleadoVM
{
    public Guid OpcionMenuId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public string? HorarioNombre { get; set; }
    public string? A { get; set; }
    public string? B { get; set; }
    public string? C { get; set; }
    public string? D { get; set; }
    public string? E { get; set; }
    public string? ImagenA { get; set; }
    public string? ImagenB { get; set; }
    public string? ImagenC { get; set; }
    public string? ImagenD { get; set; }
    public string? ImagenE { get; set; }
    public int OpcionesMaximas { get; set; } = 3;
    public bool Editable { get; set; } = true;
    public Guid? AdicionalOpcionId { get; set; }
    [RegularExpression("[ABCDE]", ErrorMessage = "Seleccione A, B, C, D o E")]
    public char? Seleccion { get; set; }
}

public class AdicionalDisponibleVM
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
}
