using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.Tests;

public class ClienteSucursalTests
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
    public async Task Crea_cliente_y_sucursal()
    {
        using var ctx = Ctx();
        var empresa = new Empresa { Nombre = "Cliente A", Rnc = "RNC123" };
        await ctx.Empresas.AddAsync(empresa);
        await ctx.Sucursales.AddAsync(new Sucursal { Nombre = "Sucursal 1", Empresa = empresa });
        await ctx.SaveChangesAsync();

        var dbEmpresa = await ctx.Empresas.SingleAsync();
        var dbSucursal = await ctx.Sucursales.Include(s=>s.Empresa).SingleAsync();

        dbEmpresa.Nombre.Should().Be("Cliente A");
        dbSucursal.EmpresaId.Should().Be(dbEmpresa.Id);
    }
}

