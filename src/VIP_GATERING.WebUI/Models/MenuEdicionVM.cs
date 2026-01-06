using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models;

public class MenuEdicionVM
{
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaTermino { get; set; }
    public int MenuId { get; set; }
    public List<DiaEdicion> Dias { get; set; } = new();
    public IEnumerable<Opcion> Opciones { get; set; } = Enumerable.Empty<Opcion>();
    public List<int> AdicionalesIds { get; set; } = new();
    public bool EncuestaCerrada { get; set; }
    public DateTime FechaCierreAutomatica { get; set; }
    public DateTime? FechaCierreManual { get; set; }
    public int EmpleadosCompletos { get; set; }
    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }
    public IEnumerable<(int id, string nombre)> Empresas { get; set; } = Array.Empty<(int, string)>();
    public IEnumerable<(int id, string nombre, int empresaId)> Sucursales { get; set; } = Array.Empty<(int, string, int)>();
    public IReadOnlyList<Horario> HorariosPermitidos { get; set; } = new List<Horario>();
    public string OrigenScope => SucursalId != null ? "Filial" : "Empresa";
    public string? EmpresaNombre { get; set; }
    public string? SucursalNombre { get; set; }
    public bool PuedeCerrarManualmente => !EncuestaCerrada;
    public bool PuedeReabrir => EncuestaCerrada;
    public bool PuedeEliminarEncuesta { get; set; }
    public bool AplicarATodasFiliales { get; set; }
}

public class DiaEdicion
{
    public int OpcionMenuId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public int? HorarioId { get; set; }
    public string HorarioNombre { get; set; } = "Horario general";
    public int HorarioOrden { get; set; }
    public int? A { get; set; }
    public int? B { get; set; }
    public int? C { get; set; }
    public int? D { get; set; }
    public int? E { get; set; }
    public int OpcionesMaximas { get; set; } = 3;
}

