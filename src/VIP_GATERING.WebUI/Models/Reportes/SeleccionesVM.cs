using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class SeleccionesVM
{
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }

    public Guid? EmpresaId { get; set; }
    public Guid? SucursalId { get; set; }

    public IEnumerable<Empresa> Empresas { get; set; } = Enumerable.Empty<Empresa>();
    public IEnumerable<Sucursal> Sucursales { get; set; } = Enumerable.Empty<Sucursal>();

    public int TotalRespuestas { get; set; }
    public decimal TotalCosto { get; set; }
    public decimal TotalPrecio { get; set; }
    public decimal TotalBeneficio => TotalCosto - TotalPrecio;

    public List<SucursalResumen> PorSucursal { get; set; } = new();
    public List<EmpleadoResumen> PorEmpleado { get; set; } = new();
    public List<SeleccionDetalleEmpleadoRow> SeleccionesEmpleado { get; set; } = new();

    public class SucursalResumen
    {
        public Guid SucursalId { get; set; }
        public string Sucursal { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal TotalCosto { get; set; }
        public decimal TotalPrecio { get; set; }
        public decimal TotalBeneficio => TotalCosto - TotalPrecio;
    }

    public class EmpleadoResumen
    {
        public Guid EmpleadoId { get; set; }
        public string Empleado { get; set; } = string.Empty;
        public Guid SucursalId { get; set; }
        public string Sucursal { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal TotalCosto { get; set; }
        public decimal TotalPrecio { get; set; }
        public decimal TotalBeneficio => TotalCosto - TotalPrecio;
    }

    public class SeleccionDetalleEmpleadoRow
    {
        public DateOnly Fecha { get; set; }
        public string DiaNombre { get; set; } = string.Empty;
        public string OpcionNombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public string Localizacion { get; set; } = string.Empty;
    }
}
