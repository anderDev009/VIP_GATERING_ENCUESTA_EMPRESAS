using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Repositories;

namespace VIP_GATERING.Tests;

public class MenuServiceFallbackTests
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
    public async Task Fallback_a_menu_de_empresa_si_no_hay_de_sucursal()
    {
        using var ctx = Ctx();
        var emp = new Empresa { Nombre = "Cliente" };
        var suc = new Sucursal { Nombre = "S1", Empresa = emp };
        await ctx.AddRangeAsync(emp, suc);
        await ctx.SaveChangesAsync();

        IRepository<Menu> repoMenu = new EfRepository<Menu>(ctx);
        IRepository<Opcion> repoOpc = new EfRepository<Opcion>(ctx);
        IRepository<OpcionMenu> repoOpcMenu = new EfRepository<OpcionMenu>(ctx);
        IRepository<RespuestaFormulario> repoResp = new EfRepository<RespuestaFormulario>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        var fechas = new FechaServicio();
        var svc = new MenuService(repoMenu, repoOpc, repoOpcMenu, new EfRepository<Horario>(ctx), repoResp, new EfRepository<SucursalHorario>(ctx), uow, fechas);

        var (inicio, fin) = fechas.RangoSemanaSiguiente();
        // solo crear menú de empresa
        var mEmp = await svc.GetOrCreateMenuAsync(inicio, fin, emp.Id, null);
        var found = await svc.FindMenuAsync(inicio, fin, emp.Id, suc.Id);
        found!.Id.Should().Be(mEmp.Id);
    }
}



