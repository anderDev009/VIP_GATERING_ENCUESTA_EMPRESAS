using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Repositories;

namespace VIP_GATERING.Tests;

public class EmpleadoMenuNextWeekTests
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
    public async Task Empleado_ve_menu_semana_siguiente_con_5_dias()
    {
        using var ctx = Ctx();
        var empresa = new Empresa { Nombre = "Cliente" };
        var suc = new Sucursal { Nombre = "S1", Empresa = empresa };
        var emp = new Empleado { Nombre = "Juan", Sucursal = suc };
        await ctx.AddRangeAsync(empresa, suc, emp);
        await ctx.SaveChangesAsync();

        IRepository<Menu> repoMenu = new EfRepository<Menu>(ctx);
        IRepository<Opcion> repoOpc = new EfRepository<Opcion>(ctx);
        IRepository<OpcionMenu> repoOpcMenu = new EfRepository<OpcionMenu>(ctx);
        IRepository<RespuestaFormulario> repoResp = new EfRepository<RespuestaFormulario>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        IFechaServicio fechas = new FechaServicio();
        IMenuService menuSvc = new MenuService(repoMenu, repoOpc, repoOpcMenu, new EfRepository<Horario>(ctx), repoResp, new EfRepository<SucursalHorario>(ctx), uow, fechas);

        var (inicio, fin) = fechas.RangoSemanaSiguiente();
        var menu = await menuSvc.GetOrCreateMenuAsync(inicio, fin, empresa.Id, suc.Id);
        var dias = await repoOpcMenu.ListAsync(d => d.MenuId == menu.Id);
        dias.Count.Should().Be(10);
    }
}




