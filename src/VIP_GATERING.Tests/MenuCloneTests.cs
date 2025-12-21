using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Repositories;

namespace VIP_GATERING.Tests;

public class MenuCloneTests
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
    public async Task Clona_menu_de_cliente_a_sucursales_y_respeta_bloqueo()
    {
        using var ctx = Ctx();
        var empresa = new Empresa { Nombre = "Cliente" };
        var s1 = new Sucursal { Nombre = "S1", Empresa = empresa };
        var s2 = new Sucursal { Nombre = "S2", Empresa = empresa };
        await ctx.AddRangeAsync(empresa, s1, s2);
        var op1 = new Opcion { Nombre = "A" }; var op2 = new Opcion { Nombre = "B" }; var op3 = new Opcion { Nombre = "C" };
        await ctx.AddRangeAsync(op1, op2, op3);
        await ctx.SaveChangesAsync();

        var inicio = new DateOnly(2025,1,6); var fin = inicio.AddDays(4);
        // Menú cliente con opciones
        var menuCliente = new Menu { FechaInicio = inicio, FechaTermino = fin, EmpresaId = empresa.Id };
        await ctx.Menus.AddAsync(menuCliente);
        foreach (var d in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            await ctx.OpcionesMenu.AddAsync(new OpcionMenu { Menu = menuCliente, MenuId = menuCliente.Id, DiaSemana = d, OpcionIdA = op1.Id, OpcionIdB = op2.Id, OpcionIdC = op3.Id });
        await ctx.SaveChangesAsync();

        // Infra + servicios
        IRepository<Menu> repoMenu = new EfRepository<Menu>(ctx);
        IRepository<Opcion> repoOpc = new EfRepository<Opcion>(ctx);
        IRepository<OpcionMenu> repoOpcMenu = new EfRepository<OpcionMenu>(ctx);
        IRepository<RespuestaFormulario> repoResp = new EfRepository<RespuestaFormulario>(ctx);
        IRepository<Empleado> repoEmp = new EfRepository<Empleado>(ctx);
        IRepository<EmpleadoSucursal> repoEmpSuc = new EfRepository<EmpleadoSucursal>(ctx);
        IRepository<MenuAdicional> repoMenuAdi = new EfRepository<MenuAdicional>(ctx);
        IRepository<Localizacion> repoLoc = new EfRepository<Localizacion>(ctx);
        IRepository<EmpleadoLocalizacion> repoEmpLoc = new EfRepository<EmpleadoLocalizacion>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        IFechaServicio fechas = new FechaServicio();
        IMenuService menuSvc = new MenuService(repoMenu, repoOpc, repoOpcMenu, new EfRepository<Horario>(ctx), repoResp, new EfRepository<SucursalHorario>(ctx), repoEmp, repoEmpSuc, repoMenuAdi, repoLoc, repoEmpLoc, uow, fechas);
        var cloneSvc = new MenuCloneService(menuSvc, repoMenu, repoOpcMenu, repoResp, uow);

        // Clonar a dos sucursales
        var result1 = await cloneSvc.CloneEmpresaMenuToSucursalesAsync(inicio, fin, empresa.Id, new[] { s1.Id, s2.Id });
        result1.updated.Should().Be(2);
        result1.skipped.Should().Be(0);

        // Marcar s2 como bloqueada (5 respuestas)
        var menuS2 = await repoMenu.ListAsync(m => m.SucursalId == s2.Id && m.FechaInicio == inicio && m.FechaTermino == fin);
        var menuS2Id = menuS2.Single().Id;
        var diasS2 = await repoOpcMenu.ListAsync(om => om.MenuId == menuS2Id);
        var emp = new Empleado { Nombre = "E", SucursalId = s2.Id };
        await ctx.Empleados.AddAsync(emp);
        await ctx.SaveChangesAsync();
        foreach (var d in diasS2)
            await ctx.RespuestasFormulario.AddAsync(new RespuestaFormulario { EmpleadoId = emp.Id, OpcionMenuId = d.Id, Seleccion = 'A', SucursalEntregaId = s2.Id });
        await ctx.SaveChangesAsync();

        // Intentar clonar nuevamente: s1 actualiza, s2 se omite
        var result2 = await cloneSvc.CloneEmpresaMenuToSucursalesAsync(inicio, fin, empresa.Id, new[] { s1.Id, s2.Id });
        result2.updated.Should().Be(1);
        result2.skipped.Should().Be(1);
    }
}

