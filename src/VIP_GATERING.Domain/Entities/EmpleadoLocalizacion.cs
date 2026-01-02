namespace VIP_GATERING.Domain.Entities;

public class EmpleadoLocalizacion
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public int LocalizacionId { get; set; }
    public Localizacion? Localizacion { get; set; }
}
