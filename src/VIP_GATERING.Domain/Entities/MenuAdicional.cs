namespace VIP_GATERING.Domain.Entities;

public class MenuAdicional
{
    public int Id { get; set; }

    public int MenuId { get; set; }
    public Menu? Menu { get; set; }

    public int OpcionId { get; set; }
    public Opcion? Opcion { get; set; }
}

