namespace VIP_GATERING.Domain.Entities;

public class RespuestaFormulario
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public int OpcionMenuId { get; set; }
    public OpcionMenu? OpcionMenu { get; set; }

    // Sucursal a la que se enviar la comida (puede diferir de la sucursal principal del empleado)
    public int SucursalEntregaId { get; set; }
    public Sucursal? SucursalEntrega { get; set; }

    // Localizacion dentro de la filial para entrega (opcional)
    public int? LocalizacionEntregaId { get; set; }
    public Localizacion? LocalizacionEntrega { get; set; }

    // Adicional fijo (se cobra 100% al empleado)
    public int? AdicionalOpcionId { get; set; }
    public Opcion? AdicionalOpcion { get; set; }

    // Seleccion: 'A', 'B', 'C', 'D' o 'E'
    public char Seleccion { get; set; }

    public bool CierreNomina { get; set; }
    public DateTime? FechaCierreNomina { get; set; }
    public bool Facturado { get; set; }
    public DateTime? FechaFacturado { get; set; }
    public string? NumeroFactura { get; set; }
    public string? UsuarioCierreNomina { get; set; }
    public string? UsuarioFacturacion { get; set; }
    public DateTime? FechaSeleccion { get; set; }

    public decimal? BaseSnapshot { get; set; }
    public decimal? ItbisSnapshot { get; set; }
    public decimal? TotalSnapshot { get; set; }
    public decimal? EmpresaPagaSnapshot { get; set; }
    public decimal? EmpleadoPagaSnapshot { get; set; }
    public decimal? ItbisEmpresaSnapshot { get; set; }
    public decimal? ItbisEmpleadoSnapshot { get; set; }

    public decimal? AdicionalBaseSnapshot { get; set; }
    public decimal? AdicionalItbisSnapshot { get; set; }
    public decimal? AdicionalTotalSnapshot { get; set; }
    public decimal? AdicionalEmpresaPagaSnapshot { get; set; }
    public decimal? AdicionalEmpleadoPagaSnapshot { get; set; }
    public decimal? AdicionalItbisEmpresaSnapshot { get; set; }
    public decimal? AdicionalItbisEmpleadoSnapshot { get; set; }
}


