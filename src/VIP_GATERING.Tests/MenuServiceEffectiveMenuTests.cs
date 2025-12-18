using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Repositories;

namespace VIP_GATERING.Tests;

public class MenuServiceEffectiveMenuTests
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
    public async Task Usa_menu_de_empresa_si_sucursal_vacio()
    {
        using var ctx = Ctx();
        var emp = new Empresa { Nombre = "Cliente" };
        var suc = new Sucursal { Nombre = "S1", Empresa = emp };
        await ctx.AddRangeAsync(emp, suc);
        var op = new Opcion { Nombre = "A" }; await ctx.AddAsync(op);
        await ctx.SaveChangesAsync();

        IRepository<Menu> repoMenu = new EfRepository<Menu>(ctx);
        IRepository<Opcion> repoOpc = new EfRepository<Opcion>(ctx);
        IRepository<OpcionMenu> repoOpcMenu = new EfRepository<OpcionMenu>(ctx);
        IRepository<RespuestaFormulario> repoResp = new EfRepository<RespuestaFormulario>(ctx);
        IRepository<Empleado> repoEmp = new EfRepository<Empleado>(ctx);
        IRepository<EmpleadoSucursal> repoEmpSuc = new EfRepository<EmpleadoSucursal>(ctx);
        IRepository<MenuAdicional> repoMenuAdi = new EfRepository<MenuAdicional>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        var fechas = new FechaServicio();
        var svc = new MenuService(repoMenu, repoOpc, repoOpcMenu, new EfRepository<Horario>(ctx), repoResp, new EfRepository<SucursalHorario>(ctx), repoEmp, repoEmpSuc, repoMenuAdi, uow, fechas);

        var (inicio, fin) = fechas.RangoSemanaSiguiente();
        // Crear menú empresa con opciones
        var mEmp = await svc.GetOrCreateMenuAsync(inicio, fin, emp.Id, null);
        var diasEmp = await repoOpcMenu.ListAsync(d => d.MenuId == mEmp.Id);
        foreach (var d in diasEmp) { d.OpcionIdA = op.Id; }
        await uow.SaveChangesAsync();

        // Crear menú sucursal vacío
        var mSuc = await svc.GetOrCreateMenuAsync(inicio, fin, emp.Id, suc.Id);
        var diasSuc = await repoOpcMenu.ListAsync(d => d.MenuId == mSuc.Id);
        diasSuc.Count.Should().Be(10);

        var effective = await svc.GetEffectiveMenuForSemanaAsync(inicio, fin, emp.Id, suc.Id);
        effective.Id.Should().Be(mEmp.Id);
    }
}



