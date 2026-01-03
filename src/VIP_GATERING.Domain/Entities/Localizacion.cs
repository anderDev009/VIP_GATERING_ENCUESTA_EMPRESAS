using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.Domain.Entities;

public class Localizacion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public string? Rnc { get; set; }

    [Required]
    public string Direccion { get; set; } = string.Empty;

    [Required]
    public string IndicacionesEntrega { get; set; } = string.Empty;

    public ICollection<EmpleadoLocalizacion> EmpleadosAsignados { get; set; } = new List<EmpleadoLocalizacion>();
}
