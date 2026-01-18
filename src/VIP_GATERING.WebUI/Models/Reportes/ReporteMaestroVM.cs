using VIP_GATERING.Domain.Entities;
using VIP_GATERING.WebUI.Models;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class ReporteMaestroVM
{
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }

    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }
    public int? LocalizacionId { get; set; }
    public int? EmpleadoId { get; set; }
    public string? Tipo { get; set; }
    public string? Estado { get; set; }

    public IReadOnlyList<Empresa> Empresas { get; set; } = Array.Empty<Empresa>();
    public IReadOnlyList<Sucursal> Sucursales { get; set; } = Array.Empty<Sucursal>();
    public IReadOnlyList<Localizacion> Localizaciones { get; set; } = Array.Empty<Localizacion>();
    public IReadOnlyList<Empleado> Empleados { get; set; } = Array.Empty<Empleado>();

    public PagedResult<Row> Paginado { get; set; } = new();

    public sealed class Row
    {
        public DateOnly Fecha { get; set; }
        public string DiaSemana { get; set; } = string.Empty;
        public string Hora { get; set; } = string.Empty;
        public string HoraAlmuerzo { get; set; } = string.Empty;
        public string Horario { get; set; } = string.Empty;

        public string Empresa { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public string Localizacion { get; set; } = string.Empty;
        public string EmpleadoCodigo { get; set; } = string.Empty;
        public string EmpleadoNombre { get; set; } = string.Empty;
        public string UsuarioEmpleado { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;
        public string Opcion { get; set; } = string.Empty;
        public string Plato { get; set; } = string.Empty;
        public int Cantidad { get; set; }

        public decimal PrecioUnitario { get; set; }
        public decimal SubtotalBase { get; set; }
        public decimal ItbisTotal { get; set; }
        public decimal Total { get; set; }

        public string SubsidioAplicado { get; set; } = string.Empty;
        public decimal PorcentajeSubsidio { get; set; }
        public decimal EmpresaPaga { get; set; }
        public decimal ItbisEmpresa { get; set; }
        public decimal EmpleadoPaga { get; set; }
        public decimal ItbisEmpleado { get; set; }

        public string NumeroCierre { get; set; } = string.Empty;
        public string EstadoNomina { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public string EstadoFacturacion { get; set; } = string.Empty;
        public string UsuarioProceso { get; set; } = string.Empty;
    }
}
