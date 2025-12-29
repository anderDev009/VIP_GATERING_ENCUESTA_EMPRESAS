using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Repositories;

namespace VIP_GATERING.Tests;

public class MenuEdicionServiceTests
{
    private static AppDbContext Ctx()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        var o = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(c).Options;
        var ctx = new AppDbContext(o);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task Semana_actual_permite_hasta_mediodia_del_dia_anterior()
    {
        using var ctx = Ctx();
        IRepository<MenuConfiguracion> repoCfg = new EfRepository<MenuConfiguracion>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        var cfgSvc = new MenuConfiguracionService(repoCfg, uow);
        var fechaSvc = new FechaServicio();
        var svc = new MenuEdicionService(cfgSvc, fechaSvc);

        var menu = new Menu { FechaInicio = new DateOnly(2025, 1, 6), FechaTermino = new DateOnly(2025, 1, 10) };
        var opcion = new OpcionMenu { MenuId = menu.Id, DiaSemana = DayOfWeek.Tuesday };

        var ok = await svc.CalcularVentanaAsync(menu, new[] { opcion }, new DateTime(2025, 1, 6, 11, 0, 0, DateTimeKind.Utc), true);
        ok.EdicionPorOpcion[opcion.Id].Should().BeTrue();

        var bloqueado = await svc.CalcularVentanaAsync(menu, new[] { opcion }, new DateTime(2025, 1, 6, 13, 0, 0, DateTimeKind.Utc), true);
        bloqueado.EdicionPorOpcion[opcion.Id].Should().BeFalse();
    }

    [Fact]
    public async Task Semana_actual_no_permite_dia_actual_ni_dias_pasados()
    {
        using var ctx = Ctx();
        IRepository<MenuConfiguracion> repoCfg = new EfRepository<MenuConfiguracion>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        var cfgSvc = new MenuConfiguracionService(repoCfg, uow);
        var fechaSvc = new FechaServicio();
        var svc = new MenuEdicionService(cfgSvc, fechaSvc);

        var menu = new Menu { FechaInicio = new DateOnly(2025, 1, 6), FechaTermino = new DateOnly(2025, 1, 10) };
        var opcionMartes = new OpcionMenu { MenuId = menu.Id, DiaSemana = DayOfWeek.Tuesday };
        var opcionLunes = new OpcionMenu { MenuId = menu.Id, DiaSemana = DayOfWeek.Monday };

        var referenciaMartes = new DateTime(2025, 1, 7, 9, 0, 0, DateTimeKind.Utc);
        var resultado = await svc.CalcularVentanaAsync(menu, new[] { opcionMartes, opcionLunes }, referenciaMartes, true);
        resultado.EdicionPorOpcion[opcionMartes.Id].Should().BeFalse();
        resultado.EdicionPorOpcion[opcionLunes.Id].Should().BeFalse();
    }
}
