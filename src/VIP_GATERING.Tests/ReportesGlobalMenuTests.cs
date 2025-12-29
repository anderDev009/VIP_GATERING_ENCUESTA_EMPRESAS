using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Controllers;
using VIP_GATERING.WebUI.Models.Reportes;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.Tests;

public class ReportesGlobalMenuTests
{
    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task ItemsSemana_muestra_menu_global_de_empresa()
    {
        using var ctx = CreateContext();
        var inicio = new DateOnly(2025, 1, 6);
        var fin = inicio.AddDays(4);

        var empresa = new Empresa
        {
            Nombre = "Empresa Demo",
            SubsidiaEmpleados = true,
            SubsidioTipo = SubsidioTipo.Porcentaje,
            SubsidioValor = 75m
        };
        var sucursal = new Sucursal
        {
            Nombre = "Filial Demo",
            Empresa = empresa,
            SubsidiaEmpleados = true,
            SubsidioTipo = SubsidioTipo.Porcentaje,
            SubsidioValor = 75m
        };
        var empleado = new Empleado
        {
            Nombre = "Empleado Demo",
            Codigo = "EMPDEMO",
            Sucursal = sucursal
        };
        var horario = new Horario
        {
            Nombre = "Almuerzo",
            Orden = 1,
            Activo = true
        };
        var opcion = new Opcion
        {
            Nombre = "Opción global",
            Costo = 190m,
            Precio = 230m,
            EsSubsidiado = true
        };
        var menu = new Menu
        {
            Empresa = empresa,
            FechaInicio = inicio,
            FechaTermino = fin
        };
        var opcionMenu = new OpcionMenu
        {
            Menu = menu,
            Horario = horario,
            DiaSemana = DayOfWeek.Monday,
            OpcionesMaximas = 1,
            OpcionA = opcion,
            OpcionIdA = opcion.Id
        };
        var respuesta = new RespuestaFormulario
        {
            Empleado = empleado,
            EmpleadoId = empleado.Id,
            OpcionMenu = opcionMenu,
            OpcionMenuId = opcionMenu.Id,
            SucursalEntrega = sucursal,
            SucursalEntregaId = sucursal.Id,
            Seleccion = 'A'
        };

        ctx.AddRange(empresa, sucursal, empleado, horario, opcion, menu, opcionMenu, respuesta);
        await ctx.SaveChangesAsync();

        var controller = new ReportesController(ctx, new FixedFechaServicio(inicio, fin), new EmptyCurrentUserService(), new SubsidioService());
        var result = await controller.ItemsSemana();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeAssignableTo<ItemsSemanaVM>().Subject;
        vm.Items.Should().ContainSingle(i => i.Nombre == "Opción global");
    }

    private sealed class FixedFechaServicio : IFechaServicio
    {
        private readonly DateOnly _inicio;
        private readonly DateOnly _fin;

        public FixedFechaServicio(DateOnly inicio, DateOnly fin)
        {
            _inicio = inicio;
            _fin = fin;
        }

        public DateOnly Hoy() => _inicio;

        public (DateOnly inicio, DateOnly fin) RangoSemanaActual() => (_inicio, _fin);

        public (DateOnly inicio, DateOnly fin) RangoSemanaSiguiente() => (_inicio, _fin);

        public DateOnly ObtenerFechaDelDia(DateOnly inicioSemana, DayOfWeek dia)
        {
            var offset = ((int)dia - (int)DayOfWeek.Monday + 7) % 7;
            return inicioSemana.AddDays(offset);
        }
    }

    private sealed class EmptyCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public Guid? EmpleadoId => null;
        public Guid? EmpresaId => null;
        public Guid? SucursalId => null;
        public Task SetUsuarioAsync(Guid usuarioId) => Task.CompletedTask;
    }
}
