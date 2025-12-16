namespace VIP_GATERING.Domain.Entities;

public class Empresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string? Rnc { get; set; }
    public bool SubsidiaEmpleados { get; set; } = true;
    public SubsidioTipo SubsidioTipo { get; set; } = SubsidioTipo.Porcentaje;
    public decimal SubsidioValor { get; set; } = 75m;

    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}
