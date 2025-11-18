using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Models;

public class MenuSemanaVM
{
    public IEnumerable<OpcionMenu> Dias { get; set; } = Enumerable.Empty<OpcionMenu>();
    public IEnumerable<Opcion> Opciones { get; set; } = Enumerable.Empty<Opcion>();
}

