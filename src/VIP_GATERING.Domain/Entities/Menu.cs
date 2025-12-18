namespace VIP_GATERING.Domain.Entities;

public class Menu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaTermino { get; set; }

    // Alcance: por Sucursal o por Empresa (cliente). Uno de los dos puede estar establecido.
    public Guid? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public Guid? SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    public bool EncuestaCerradaManualmente { get; set; }
    public DateTime? FechaCierreManual { get; set; }
    public bool EncuestaReabiertaManualmente { get; set; }

    public ICollection<OpcionMenu> OpcionesPorDia { get; set; } = new List<OpcionMenu>();

    // Adicionales fijos disponibles para este menú (se cobran 100% al empleado)
    public ICollection<MenuAdicional> Adicionales { get; set; } = new List<MenuAdicional>();
}
