namespace VIP_GATERING.Domain.Entities;

public class Opcion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Costo { get; set; }
    public decimal? Precio { get; set; }
    public bool EsSubsidiado { get; set; }
    public bool LlevaItbis { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Borrado { get; set; } = false;
    public ICollection<OpcionHorario> Horarios { get; set; } = new List<OpcionHorario>();
}
