using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.Tests;

public class MenuPersistenceTests
{
    private static AppDbContext CreateContext()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var ctx = new AppDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task Crear_y_actualizar_opciones_por_dia_persiste()
    {
        using var ctx = CreateContext();

        var menu = new Menu { FechaInicio = new DateOnly(2025, 1, 6), FechaTermino = new DateOnly(2025, 1, 10) };
        await ctx.Menus.AddAsync(menu);
        var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var d in dias)
            await ctx.OpcionesMenu.AddAsync(new OpcionMenu { Menu = menu, MenuId = menu.Id, DiaSemana = d });
        var opA = new Opcion { Nombre = "A" };
        var opB = new Opcion { Nombre = "B" };
        await ctx.Opciones.AddRangeAsync(opA, opB);
        await ctx.SaveChangesAsync();

        var lunes = await ctx.OpcionesMenu.FirstAsync(o => o.MenuId == menu.Id && o.DiaSemana == DayOfWeek.Monday);
        lunes.OpcionIdA = opA.Id; lunes.OpcionIdB = opB.Id; lunes.OpcionIdC = null;
        await ctx.SaveChangesAsync();

        var check = await ctx.OpcionesMenu.AsNoTracking().FirstAsync(o => o.Id == lunes.Id);
        check.OpcionIdA.Should().Be(opA.Id);
        check.OpcionIdB.Should().Be(opB.Id);
        check.OpcionIdC.Should().BeNull();
    }

    [Fact]
    public async Task Obtener_mismo_menu_para_misma_semana()
    {
        using var ctx = CreateContext();
        var m1 = new Menu { FechaInicio = new DateOnly(2025, 1, 6), FechaTermino = new DateOnly(2025, 1, 10) };
        await ctx.Menus.AddAsync(m1);
        await ctx.SaveChangesAsync();

        // Simula una consulta que busca el menú para ese rango
        var m2 = await ctx.Menus.FirstOrDefaultAsync(m => m.FechaInicio == m1.FechaInicio && m.FechaTermino == m1.FechaTermino);
        m2.Should().NotBeNull();
        m2!.Id.Should().Be(m1.Id);
    }
}

