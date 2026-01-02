using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class TotalesEmpleadosVM
{
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }

    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }

    public IEnumerable<Empresa> Empresas { get; set; } = Enumerable.Empty<Empresa>();
    public IEnumerable<Sucursal> Sucursales { get; set; } = Enumerable.Empty<Sucursal>();

    public List<Row> Filas { get; set; } = new();

    public int TotalSelecciones => Filas.Sum(f => f.Cantidad);
    public decimal TotalCosto => Filas.Sum(f => f.TotalCosto);
    public decimal TotalPrecio => Filas.Sum(f => f.TotalPrecio);
    public decimal TotalBeneficio => Filas.Sum(f => f.TotalBeneficio);
    public decimal TotalGeneral => TotalCosto; // Legacy alias

    public class Row
    {
        public int EmpleadoId { get; set; }
        public string Empleado { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal TotalCosto { get; set; }
        public decimal TotalPrecio { get; set; }
        public decimal TotalBeneficio => TotalCosto - TotalPrecio;
    }
}
