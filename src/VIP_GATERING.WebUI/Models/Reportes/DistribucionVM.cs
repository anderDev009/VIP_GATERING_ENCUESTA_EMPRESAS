using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class DistribucionVM
{
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }

    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }
    public int? EmpleadoId { get; set; }
    public int? LocalizacionId { get; set; }
    public int? HorarioId { get; set; }
    public string? MensajeValidacion { get; set; }

    public IEnumerable<Empresa> Empresas { get; set; } = Enumerable.Empty<Empresa>();
    public IEnumerable<Sucursal> Sucursales { get; set; } = Enumerable.Empty<Sucursal>();
    public IEnumerable<Empleado> Empleados { get; set; } = Enumerable.Empty<Empleado>();
    public IEnumerable<Localizacion> Localizaciones { get; set; } = Enumerable.Empty<Localizacion>();
    public IEnumerable<Horario> Horarios { get; set; } = Enumerable.Empty<Horario>();

    public List<ResumenFilialRow> ResumenFiliales { get; set; } = new();
    public List<DetalleEmpleadoRow> DetalleEmpleados { get; set; } = new();
    public List<DistribucionLocalizacionRow> PorLocalizacion { get; set; } = new();
    public List<DistribucionCocinaRow> PorLocalizacionCocina { get; set; } = new();
    public List<DistribucionCocinaDetalleRow> PorLocalizacionCocinaDetalle { get; set; } = new();

    public decimal TotalBase => ResumenFiliales.Sum(r => r.Base);
    public decimal TotalItbis => ResumenFiliales.Sum(r => r.Itbis);
    public decimal TotalGeneral => ResumenFiliales.Sum(r => r.Total);
    public decimal TotalEmpresa => ResumenFiliales.Sum(r => r.EmpresaPaga);
    public decimal TotalEmpleado => ResumenFiliales.Sum(r => r.EmpleadoPaga);

    public class ResumenFilialRow
    {
        public DateOnly Fecha { get; set; }
        public int FilialId { get; set; }
        public string Filial { get; set; } = string.Empty;
        public decimal Base { get; set; }
        public decimal Itbis { get; set; }
        public decimal Total { get; set; }
        public decimal ItbisEmpresa { get; set; }
        public decimal ItbisEmpleado { get; set; }
        public decimal MontoAdicional { get; set; }
        public decimal ItbisAdicional { get; set; }
        public decimal EmpresaPaga { get; set; }
        public decimal EmpleadoPaga { get; set; }
    }

    public class DetalleEmpleadoRow
    {
        public DateOnly Fecha { get; set; }
        public string Filial { get; set; } = string.Empty;
        public string Localizacion { get; set; } = string.Empty;
        public string Empleado { get; set; } = string.Empty;
        public string EmpleadoCodigo { get; set; } = string.Empty;
        public string Tanda { get; set; } = string.Empty;
        public string Opcion { get; set; } = string.Empty;
        public string Seleccion { get; set; } = string.Empty;
        public decimal Base { get; set; }
        public decimal Itbis { get; set; }
        public decimal Total { get; set; }
        public decimal EmpresaPaga { get; set; }
        public decimal EmpleadoPaga { get; set; }
        public decimal ItbisEmpresa { get; set; }
        public decimal ItbisEmpleado { get; set; }
    }

    public class DistribucionLocalizacionRow
    {
        public DateOnly Fecha { get; set; }
        public string Filial { get; set; } = string.Empty;
        public string Localizacion { get; set; } = string.Empty;
        public string Opcion { get; set; } = string.Empty;
        public string Seleccion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal EmpresaPaga { get; set; }
        public decimal EmpleadoPaga { get; set; }
    }

    public class DistribucionCocinaRow
    {
        public DateOnly Fecha { get; set; }
        public string Filial { get; set; } = string.Empty;
        public string Localizacion { get; set; } = string.Empty;
        public int Opcion1 { get; set; }
        public int Opcion2 { get; set; }
        public int Opcion3 { get; set; }
        public int Opcion4 { get; set; }
        public int Opcion5 { get; set; }
        public int Adicionales { get; set; }
        public int TotalOpciones => Opcion1 + Opcion2 + Opcion3 + Opcion4 + Opcion5;
        public int Total => TotalOpciones + Adicionales;
    }

    public class DistribucionCocinaDetalleRow
    {
        public DateOnly Fecha { get; set; }
        public string Filial { get; set; } = string.Empty;
        public string Localizacion { get; set; } = string.Empty;
        public string EmpleadoCodigo { get; set; } = string.Empty;
        public string EmpleadoNombre { get; set; } = string.Empty;
        public string Seleccion { get; set; } = string.Empty;
        public string Opcion { get; set; } = string.Empty;
    }
}
