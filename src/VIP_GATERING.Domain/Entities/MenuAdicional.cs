namespace VIP_GATERING.Domain.Entities;

public class MenuAdicional
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MenuId { get; set; }
    public Menu? Menu { get; set; }

    public Guid OpcionId { get; set; }
    public Opcion? Opcion { get; set; }
}

