using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.Domain.Entities;

public class Empresa
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Rnc { get; set; }
    [Required]
    public string ContactoNombre { get; set; } = string.Empty;
    [Required]
    public string ContactoTelefono { get; set; } = string.Empty;
    [Required]
    public string Direccion { get; set; } = string.Empty;
    public bool SubsidiaEmpleados { get; set; } = true;
    public SubsidioTipo SubsidioTipo { get; set; } = SubsidioTipo.Porcentaje;
    public decimal SubsidioValor { get; set; } = 75m;

    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}
