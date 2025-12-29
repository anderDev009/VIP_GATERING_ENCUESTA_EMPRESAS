using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Application.Services;

public record MenuEdicionResultado(
    IReadOnlyDictionary<Guid, bool> EdicionPorOpcion,
    bool TieneVentanaActiva,
    bool Cerrado,
    string? MensajeBloqueo,
    DateTime? ProximoLimiteUtc);

public interface IMenuEdicionService
{
    Task<MenuEdicionResultado> CalcularVentanaAsync(Menu menu, IEnumerable<OpcionMenu> opciones, DateTime referenciaUtc, bool esSemanaActual, CancellationToken ct = default);
}

public class MenuEdicionService : IMenuEdicionService
{
    private readonly IMenuConfiguracionService _configSvc;
    private readonly IFechaServicio _fechas;

    public MenuEdicionService(IMenuConfiguracionService configSvc, IFechaServicio fechas)
    {
        _configSvc = configSvc;
        _fechas = fechas;
    }

    public async Task<MenuEdicionResultado> CalcularVentanaAsync(Menu menu, IEnumerable<OpcionMenu> opciones, DateTime referenciaUtc, bool esSemanaActual, CancellationToken ct = default)
    {
        var config = await _configSvc.ObtenerAsync(ct);
        var dict = opciones.ToDictionary(o => o.Id, _ => false);

        var fechaRef = DateOnly.FromDateTime(referenciaUtc.Date);
        var semanaConcluida = fechaRef > menu.FechaTermino;

        var cerradoManual = menu.EncuestaCerradaManualmente && !menu.EncuestaReabiertaManualmente;
        if (cerradoManual)
            return new MenuEdicionResultado(dict, false, true, "El menu esta cerrado manualmente.", null);

        if (semanaConcluida && esSemanaActual)
            return new MenuEdicionResultado(dict, false, true, "La semana ya concluyo.", null);

        if (menu.EncuestaReabiertaManualmente)
        {
            foreach (var k in dict.Keys) dict[k] = true;
            return new MenuEdicionResultado(dict, true, false, null, null);
        }

        var fechaInicio = menu.FechaInicio;
        var cerradaPorFecha = fechaRef >= fechaInicio;

        if (!esSemanaActual)
        {
            if (cerradaPorFecha)
                return new MenuEdicionResultado(dict, false, true, "El menu esta cerrado para esta semana.", null);

            foreach (var k in dict.Keys) dict[k] = true;
            return new MenuEdicionResultado(dict, true, false, null, null);
        }

        if (!config.PermitirEdicionSemanaActual)
            return new MenuEdicionResultado(dict, false, true, "La edicion de la semana actual esta deshabilitada.", null);

        var diasAnticipo = Math.Clamp(config.DiasAnticipoSemanaActual, 0, 7);
        var horaLimite = TimeOnly.FromTimeSpan(config.HoraLimiteEdicion);
        DateTime? proximoLimite = null;

        foreach (var o in opciones)
        {
            var fechaDia = _fechas.ObtenerFechaDelDia(fechaInicio, o.DiaSemana);
            if (fechaDia <= fechaRef)
                continue;
            var limite = fechaDia.ToDateTime(horaLimite).AddDays(-diasAnticipo);
            if (referenciaUtc < limite)
            {
                dict[o.Id] = true;
                if (proximoLimite == null || limite < proximoLimite) proximoLimite = limite;
            }
        }

        var tieneVentana = dict.Values.Any(v => v);
        var mensaje = tieneVentana ? null : "Fuera de horario de cambios para la semana actual.";
        var cerrado = !tieneVentana && cerradaPorFecha;
        return new MenuEdicionResultado(dict, tieneVentana, cerrado, mensaje, proximoLimite);
    }
}



