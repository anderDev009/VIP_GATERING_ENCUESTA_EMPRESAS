namespace VIP_GATERING.Domain.Entities;

public class Sucursal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public bool Borrado { get; set; } = false;

    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}
