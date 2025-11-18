namespace VIP_GATERING.Domain.Entities;

public class Horario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;
    public bool Borrado { get; set; } = false;
}
