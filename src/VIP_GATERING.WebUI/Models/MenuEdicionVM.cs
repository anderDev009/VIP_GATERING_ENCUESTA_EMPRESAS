using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models;

public class MenuEdicionVM
{
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaTermino { get; set; }
    public Guid MenuId { get; set; }
    public List<DiaEdicion> Dias { get; set; } = new();
    public IEnumerable<Opcion> Opciones { get; set; } = Enumerable.Empty<Opcion>();
    public bool EncuestaCerrada { get; set; }
    public DateTime FechaCierreAutomatica { get; set; }
    public DateTime? FechaCierreManual { get; set; }
    public int EmpleadosCompletos { get; set; }
    public Guid? EmpresaId { get; set; }
    public Guid? SucursalId { get; set; }
    public IEnumerable<(Guid id, string nombre)> Empresas { get; set; } = Array.Empty<(Guid, string)>();
    public IEnumerable<(Guid id, string nombre)> Sucursales { get; set; } = Array.Empty<(Guid, string)>();
    public string OrigenScope => SucursalId != null ? "Sucursal" : "Cliente";
    public string? EmpresaNombre { get; set; }
    public string? SucursalNombre { get; set; }
    public bool PuedeCerrarManualmente => !EncuestaCerrada;
    public bool PuedeReabrir => EncuestaCerrada;
}

public class DiaEdicion
{
    public Guid OpcionMenuId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public Guid? HorarioId { get; set; }
    public string HorarioNombre { get; set; } = "Horario general";
    public int HorarioOrden { get; set; }
    public Guid? A { get; set; }
    public Guid? B { get; set; }
    public Guid? C { get; set; }
}
