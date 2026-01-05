using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Services;

public interface IEncuestaCierreService
{
    DateTime GetFechaCierreAutomatica(Menu menu);
    bool EstaCerrada(Menu menu, DateTime? referencia = null);
}

public class EncuestaCierreService : IEncuestaCierreService
{
    public DateTime GetFechaCierreAutomatica(Menu menu)
    {
        return DateTime.SpecifyKind(menu.FechaInicio.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
    }

    public bool EstaCerrada(Menu menu, DateTime? referencia = null)
    {
        if (menu.EncuestaCerradaManualmente) return true;
        if (menu.EncuestaReabiertaManualmente) return false;

        var fechaReferencia = DateOnly.FromDateTime((referencia ?? DateTime.Now).Date);
        var fechaCierreAuto = DateOnly.FromDateTime(GetFechaCierreAutomatica(menu));
        return fechaReferencia >= fechaCierreAuto;
    }
}
