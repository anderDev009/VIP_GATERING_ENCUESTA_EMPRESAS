using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class ItemsSemanaVM
{
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }

    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }

    public IEnumerable<Empresa> Empresas { get; set; } = Enumerable.Empty<Empresa>();
    public IEnumerable<Sucursal> Sucursales { get; set; } = Enumerable.Empty<Sucursal>();

    public List<ItemRow> Items { get; set; } = new();

    public decimal TotalCosto => Items.Sum(i => i.TotalCosto);
    public decimal TotalPrecio => Items.Sum(i => i.TotalPrecio);
    public decimal TotalBeneficio => Items.Sum(i => i.TotalBeneficio);
    public decimal TotalGeneral => TotalCosto; // Legacy alias

    public class ItemRow
    {
        public int? OpcionId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal CostoUnitario { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int Cantidad { get; set; }
        public decimal BeneficioUnitario => CostoUnitario - PrecioUnitario;
        public decimal TotalCosto => Cantidad * CostoUnitario;
        public decimal TotalPrecio => Cantidad * PrecioUnitario;
        public decimal TotalBeneficio => TotalCosto - TotalPrecio;
    }
}
