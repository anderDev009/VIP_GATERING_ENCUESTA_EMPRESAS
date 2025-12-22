using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.Domain.Entities;

public class Localizacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;

    public Guid SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    [Required]
    public string Direccion { get; set; } = string.Empty;

    [Required]
    public string IndicacionesEntrega { get; set; } = string.Empty;

    public ICollection<EmpleadoLocalizacion> EmpleadosAsignados { get; set; } = new List<EmpleadoLocalizacion>();
}
