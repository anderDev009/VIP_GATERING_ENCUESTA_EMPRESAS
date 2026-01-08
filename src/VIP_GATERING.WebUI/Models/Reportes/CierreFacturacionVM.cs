using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class CierreFacturacionVM
{
    public string Titulo { get; set; } = string.Empty;
    public string AccionGenerar { get; set; } = string.Empty;
    public string TipoExport { get; set; } = string.Empty;
    public string AccionLabel { get; set; } = string.Empty;
    public string EstadoProceso { get; set; } = "Abierto";
    public bool EstaCerrado { get; set; }

    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }

    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }

    public IEnumerable<Empresa> Empresas { get; set; } = Enumerable.Empty<Empresa>();
    public IEnumerable<Sucursal> Sucursales { get; set; } = Enumerable.Empty<Sucursal>();

    public List<ResumenFilialRow> ResumenFiliales { get; set; } = new();
    public List<DetalleEmpleadoRow> DetalleEmpleados { get; set; } = new();

    public decimal TotalBase => ResumenFiliales.Sum(r => r.Base);
    public decimal TotalItbis => ResumenFiliales.Sum(r => r.Itbis);
    public decimal TotalGeneral => ResumenFiliales.Sum(r => r.Total);
    public decimal TotalEmpresa => ResumenFiliales.Sum(r => r.EmpresaPaga);
    public decimal TotalEmpleado => ResumenFiliales.Sum(r => r.EmpleadoPaga);

    public class ResumenFilialRow
    {
        public int FilialId { get; set; }
        public string Filial { get; set; } = string.Empty;
        public decimal Base { get; set; }
        public decimal Itbis { get; set; }
        public decimal Total { get; set; }
        public decimal EmpresaPaga { get; set; }
        public decimal EmpleadoPaga { get; set; }
    }

    public class DetalleEmpleadoRow
    {
        public int EmpleadoId { get; set; }
        public string Empleado { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal Base { get; set; }
        public decimal Itbis { get; set; }
        public decimal Total { get; set; }
        public decimal EmpresaPaga { get; set; }
        public decimal EmpleadoPaga { get; set; }
        public decimal ItbisEmpresa { get; set; }
        public decimal ItbisEmpleado { get; set; }
    }
}
