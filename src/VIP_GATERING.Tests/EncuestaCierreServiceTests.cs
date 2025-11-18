using FluentAssertions;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.Tests;

public class EncuestaCierreServiceTests
{
    private readonly EncuestaCierreService _service = new();

    private static Menu CrearMenuBase() => new()
    {
        FechaInicio = new DateOnly(2025, 1, 6),
        FechaTermino = new DateOnly(2025, 1, 10)
    };

    [Fact]
    public void Cierra_automaticamente_en_fecha_de_inicio()
    {
        var menu = CrearMenuBase();
        var referencia = menu.FechaInicio.ToDateTime(TimeOnly.MinValue);

        var resultado = _service.EstaCerrada(menu, referencia);

        resultado.Should().BeTrue();
    }

    [Fact]
    public void Reapertura_manual_sobre_esquema_cerrado_permanece_abierto()
    {
        var menu = CrearMenuBase();
        var referencia = menu.FechaInicio.AddDays(2).ToDateTime(TimeOnly.MinValue);
        menu.EncuestaReabiertaManualmente = true;

        var resultado = _service.EstaCerrada(menu, referencia);

        resultado.Should().BeFalse();
    }

    [Fact]
    public void Cierre_manual_siempre_tiene_prioridad()
    {
        var menu = CrearMenuBase();
        menu.EncuestaCerradaManualmente = true;
        menu.EncuestaReabiertaManualmente = true;

        var resultado = _service.EstaCerrada(menu, DateTime.UtcNow);

        resultado.Should().BeTrue();
    }
}
