namespace VIP_GATERING.Domain.Entities;

public class Empresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string? Rnc { get; set; }

    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}

