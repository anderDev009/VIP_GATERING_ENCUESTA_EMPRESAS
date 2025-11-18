using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.Tests;

public class MenuBlockingTests
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
    public async Task Detecta_empleado_con_encuesta_completa()
    {
        using var ctx = Ctx();
        var menu = new Menu { FechaInicio = new DateOnly(2025, 1, 6), FechaTermino = new DateOnly(2025, 1, 10) };
        await ctx.Menus.AddAsync(menu);
        var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var d in dias)
            await ctx.OpcionesMenu.AddAsync(new OpcionMenu { Menu = menu, MenuId = menu.Id, DiaSemana = d });
        var emp = new Empleado { Nombre = "Emp", Sucursal = new Sucursal { Nombre = "S", Empresa = new Empresa { Nombre = "E" } } };
        await ctx.Empleados.AddAsync(emp);
        await ctx.SaveChangesAsync();

        var opcionIds = await ctx.OpcionesMenu.Where(x => x.MenuId == menu.Id).Select(x => x.Id).ToListAsync();
        foreach (var id in opcionIds)
            await ctx.RespuestasFormulario.AddAsync(new RespuestaFormulario { EmpleadoId = emp.Id, OpcionMenuId = id, Seleccion = 'A' });
        await ctx.SaveChangesAsync();

        var completos = await ctx.RespuestasFormulario.Where(r => opcionIds.Contains(r.OpcionMenuId))
            .GroupBy(r => r.EmpleadoId).Where(g => g.Count() >= opcionIds.Count).CountAsync();

        completos.Should().Be(1);
    }
}
