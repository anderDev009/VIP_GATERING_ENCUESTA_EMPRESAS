namespace VIP_GATERING.Domain.Entities;

public class Sucursal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public bool Borrado { get; set; } = false;
    public bool? SubsidiaEmpleados { get; set; }
    public SubsidioTipo? SubsidioTipo { get; set; }
    public decimal? SubsidioValor { get; set; }

    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();

    // Empleados asignados a esta sucursal como adicional
    public ICollection<EmpleadoSucursal> EmpleadosAsignados { get; set; } = new List<EmpleadoSucursal>();

    public ICollection<Localizacion> Localizaciones { get; set; } = new List<Localizacion>();
}
