using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class CierreFacturacionDetalleVM
{
    public string Titulo { get; set; } = string.Empty;
    public string ExportCsvAction { get; set; } = string.Empty;
    public string ExportExcelAction { get; set; } = string.Empty;
    public string ExportPdfAction { get; set; } = string.Empty;
    public string EstadoProceso { get; set; } = "Cerrado";
    public DateTime? FechaCierreUtc { get; set; }
    public bool PuedeReabrir { get; set; }

    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }

    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }

    public IEnumerable<Empresa> Empresas { get; set; } = Enumerable.Empty<Empresa>();
    public IEnumerable<Sucursal> Sucursales { get; set; } = Enumerable.Empty<Sucursal>();

    public List<Row> Filas { get; set; } = new();

    public class Row
    {
        public DateOnly Fecha { get; set; }
        public string Empresa { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public string EmpleadoCodigo { get; set; } = string.Empty;
        public string Empleado { get; set; } = string.Empty;
        public string Tanda { get; set; } = string.Empty;
        public string Seleccion { get; set; } = string.Empty;
        public string Opcion { get; set; } = string.Empty;
        public decimal Base { get; set; }
        public decimal Itbis { get; set; }
        public decimal Total { get; set; }
        public decimal EmpresaPaga { get; set; }
        public decimal EmpleadoPaga { get; set; }
        public decimal ItbisEmpresa { get; set; }
        public decimal ItbisEmpleado { get; set; }
        public bool EsAdicional { get; set; }
    }
}
