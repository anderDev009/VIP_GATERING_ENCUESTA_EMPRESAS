using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Repositories;

namespace VIP_GATERING.Tests;

public class MenuServiceTests
{
    private static AppDbContext CreateContext()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task Crea_Menu_Semana_Siguiente_Si_No_Existe()
    {
        using var ctx = CreateContext();
        var repoMenu = new EfRepository<Menu>(ctx);
        var repoOpc = new EfRepository<Opcion>(ctx);
        var repoOpcMenu = new EfRepository<OpcionMenu>(ctx);
        var repoResp = new EfRepository<RespuestaFormulario>(ctx);
        var repoEmp = new EfRepository<Empleado>(ctx);
        var repoEmpSuc = new EfRepository<EmpleadoSucursal>(ctx);
        var repoMenuAdi = new EfRepository<MenuAdicional>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        IFechaServicio fechas = new FechaServicio();

        var service = new MenuService(repoMenu, repoOpc, repoOpcMenu, new EfRepository<Horario>(ctx), repoResp, new EfRepository<SucursalHorario>(ctx), repoEmp, repoEmpSuc, repoMenuAdi, uow, fechas);
        var menu = await service.GetOrCreateMenuSemanaSiguienteAsync();

        menu.Id.Should().NotBeEmpty();
        (await ctx.OpcionesMenu.Where(o=>o.MenuId==menu.Id).CountAsync()).Should().Be(10);
    }
}



