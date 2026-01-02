namespace VIP_GATERING.Domain.Entities;

public class Empleado
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public bool Borrado { get; set; } = false;

    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    // Sucursales adicionales asignadas (además de SucursalId como principal)
    public ICollection<EmpleadoSucursal> SucursalesAsignadas { get; set; } = new List<EmpleadoSucursal>();

    // Localizaciones asignadas para entrega
    public ICollection<EmpleadoLocalizacion> LocalizacionesAsignadas { get; set; } = new List<EmpleadoLocalizacion>();

    public Usuario? Usuario { get; set; }

    public bool EsSubsidiado { get; set; } = true;
    public SubsidioTipo? SubsidioTipo { get; set; }
    public decimal? SubsidioValor { get; set; }
    public EmpleadoEstado Estado { get; set; } = EmpleadoEstado.Habilitado;
    public bool EsJefe { get; set; } = false;
}
