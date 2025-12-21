namespace VIP_GATERING.Domain.Entities;

public class EmpleadoLocalizacion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public Guid LocalizacionId { get; set; }
    public Localizacion? Localizacion { get; set; }
}
