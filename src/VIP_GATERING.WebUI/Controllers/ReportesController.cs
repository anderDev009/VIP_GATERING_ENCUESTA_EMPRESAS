using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models.Reportes;
using VIP_GATERING.WebUI.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize]
public class ReportesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFechaServicio _fechas;
    private readonly ICurrentUserService _current;
    private readonly ISubsidioService _subsidios;

    public ReportesController(AppDbContext db, IFechaServicio fechas, ICurrentUserService current, ISubsidioService subsidios)
    { _db = db; _fechas = fechas; _current = current; _subsidios = subsidios; }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ItemsSemana(Guid? empresaId = null, Guid? sucursalId = null)
    {
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursales = await _db.Sucursales
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        var baseQuery = _db.RespuestasFormulario
            .Include(r => r.Empleado).ThenInclude(e => e!.Sucursal).ThenInclude(s => s!.Empresa)
            .Include(r => r.SucursalEntrega).ThenInclude(s => s!.Empresa)
            .Include(r => r.AdicionalOpcion)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionA)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionB)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionC)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionD)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionE)
            .Where(r => r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio == inicio
                && r.OpcionMenu.Menu.FechaTermino == fin);

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntrega != null && x.SucursalEntrega.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntregaId == sucursalId);

        var respuestas = await baseQuery.ToListAsync();

        var itemsRaw = new List<(Guid OpcionId, string Nombre, decimal Costo, decimal PrecioEmpleado)>();
        foreach (var r in respuestas)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            if (sucEmpleado == null || empresaEmpleado == null) continue;

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
            {
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, sucEmpleado, empresaEmpleado);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                itemsRaw.Add((opcion.Id, opcion.Nombre ?? "Sin definir", opcion.Costo, precio));
            }

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var precio = adicional.Precio ?? adicional.Costo;
                itemsRaw.Add((adicional.Id, adicional.Nombre ?? "Sin definir", adicional.Costo, precio));
            }
        }

        var items = itemsRaw
            .GroupBy(x => new { x.OpcionId, x.Nombre, x.Costo, x.PrecioEmpleado })
            .Select(g => new ItemsSemanaVM.ItemRow
            {
                OpcionId = g.Key.OpcionId,
                Nombre = g.Key.Nombre ?? "Sin definir",
                CostoUnitario = g.Key.Costo,
                PrecioUnitario = g.Key.PrecioEmpleado,
                Cantidad = g.Count()
            })
            .OrderByDescending(r => r.Cantidad)
            .ThenBy(r => r.Nombre)
            .ToList();

        var vm = new ItemsSemanaVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            Empresas = empresas,
            Sucursales = sucursales,
            Items = items
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin,Empresa")]
    [HttpGet]
    public async Task<IActionResult> Selecciones(Guid? empresaId = null, Guid? sucursalId = null)
    {
        // Empresa solo puede ver su propia data
        if (User.IsInRole("Empresa"))
        {
            if (_current.EmpresaId == null) return Forbid();
            if (empresaId != null && empresaId != _current.EmpresaId) return Forbid();
            empresaId = _current.EmpresaId;
        }

        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursalesBase = _db.Sucursales.AsQueryable();
        if (empresaId != null)
            sucursalesBase = sucursalesBase.Where(s => s.EmpresaId == empresaId);
        var sucursales = await sucursalesBase.OrderBy(s => s.Nombre).ToListAsync();

        var baseQuery = _db.RespuestasFormulario
            .Include(r => r.Empleado).ThenInclude(e => e!.Sucursal).ThenInclude(s => s!.Empresa)
            .Include(r => r.SucursalEntrega).ThenInclude(s => s!.Empresa)
            .Include(r => r.AdicionalOpcion)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionA)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionB)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionC)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionD)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionE)
            .Where(r => r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio == inicio
                && r.OpcionMenu.Menu.FechaTermino == fin);

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntrega != null && x.SucursalEntrega.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntregaId == sucursalId);

        var respuestas = await baseQuery.ToListAsync();

        var detalleRaw = new List<(Guid EmpleadoId, string EmpleadoNombre, Guid SucursalId, string SucursalNombre, decimal Costo, decimal Precio)>();
        foreach (var r in respuestas)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var suc = r.SucursalEntrega;
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            if (suc == null || sucEmpleado == null || empresaEmpleado == null) continue;

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
            {
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, sucEmpleado, empresaEmpleado);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                detalleRaw.Add((r.Empleado.Id, r.Empleado.Nombre, suc.Id, suc.Nombre, opcion.Costo, precio));
            }

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var precio = adicional.Precio ?? adicional.Costo;
                detalleRaw.Add((r.Empleado.Id, r.Empleado.Nombre, suc.Id, suc.Nombre, adicional.Costo, precio));
            }
        }

        var detalle = detalleRaw
            .GroupBy(x => new { x.EmpleadoId, x.EmpleadoNombre, x.SucursalId, x.SucursalNombre })
            .Select(g => new SeleccionesVM.EmpleadoResumen
            {
                EmpleadoId = g.Key.EmpleadoId,
                Empleado = g.Key.EmpleadoNombre,
                SucursalId = g.Key.SucursalId,
                Sucursal = g.Key.SucursalNombre,
                Cantidad = g.Count(),
                TotalCosto = g.Sum(i => i.Costo),
                TotalPrecio = g.Sum(i => i.Precio)
            })
            .OrderBy(r => r.Sucursal)
            .ThenBy(r => r.Empleado)
            .ToList();

        var porSucursal = detalle
            .GroupBy(d => new { d.SucursalId, d.Sucursal })
            .Select(g => new SeleccionesVM.SucursalResumen
            {
                SucursalId = g.Key.SucursalId,
                Sucursal = g.Key.Sucursal,
                Cantidad = g.Sum(x => x.Cantidad),
                TotalCosto = g.Sum(x => x.TotalCosto),
                TotalPrecio = g.Sum(x => x.TotalPrecio)
            })
            .OrderBy(g => g.Sucursal)
            .ToList();

        var vm = new SeleccionesVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            Empresas = empresas,
            Sucursales = sucursales,
            TotalRespuestas = detalle.Sum(d => d.Cantidad),
            TotalCosto = detalle.Sum(d => d.TotalCosto),
            TotalPrecio = detalle.Sum(d => d.TotalPrecio),
            PorSucursal = porSucursal,
            PorEmpleado = detalle
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin,Empresa")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleados(Guid? empresaId = null, Guid? sucursalId = null)
    {
        if (User.IsInRole("Empresa"))
        {
            if (_current.EmpresaId == null) return Forbid();
            if (empresaId != null && empresaId != _current.EmpresaId) return Forbid();
            empresaId = _current.EmpresaId;
        }

        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursalesBase = _db.Sucursales.AsQueryable();
        if (empresaId != null)
            sucursalesBase = sucursalesBase.Where(s => s.EmpresaId == empresaId);
        var sucursales = await sucursalesBase.OrderBy(s => s.Nombre).ToListAsync();

        var baseQuery = _db.RespuestasFormulario
            .Include(r => r.Empleado).ThenInclude(e => e!.Sucursal).ThenInclude(s => s!.Empresa)
            .Include(r => r.SucursalEntrega).ThenInclude(s => s!.Empresa)
            .Include(r => r.AdicionalOpcion)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionA)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionB)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionC)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionD)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionE)
            .Where(r => r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio == inicio
                && r.OpcionMenu.Menu.FechaTermino == fin);

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntrega != null && x.SucursalEntrega.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntregaId == sucursalId);

        var respuestas = await baseQuery.ToListAsync();

        var rowsRaw = new List<(Guid EmpleadoId, string EmpleadoNombre, decimal Costo, decimal Precio)>();
        foreach (var r in respuestas)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            if (sucEmpleado == null || empresaEmpleado == null) continue;

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
            {
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, sucEmpleado, empresaEmpleado);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                rowsRaw.Add((r.Empleado.Id, r.Empleado.Nombre, opcion.Costo, precio));
            }

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var precio = adicional.Precio ?? adicional.Costo;
                rowsRaw.Add((r.Empleado.Id, r.Empleado.Nombre, adicional.Costo, precio));
            }
        }

        var rows = rowsRaw
            .GroupBy(x => new { x.EmpleadoId, x.EmpleadoNombre })
            .Select(g => new TotalesEmpleadosVM.Row
            {
                EmpleadoId = g.Key.EmpleadoId,
                Empleado = g.Key.EmpleadoNombre,
                Cantidad = g.Count(),
                TotalCosto = g.Sum(i => i.Costo),
                TotalPrecio = g.Sum(i => i.Precio)
            })
            .OrderBy(r => r.Empleado)
            .ToList();

        var vm = new TotalesEmpleadosVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            Empresas = empresas,
            Sucursales = sucursales,
            Filas = rows
        };
        return View(vm);
    }

    [Authorize(Roles = "Empleado")]
    [HttpGet]
    public async Task<IActionResult> EstadoCuenta(DateOnly? desde = null, DateOnly? hasta = null)
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return Forbid();

        var empleadoDatos = await _db.Empleados
            .Include(e => e.Sucursal).ThenInclude(s => s!.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleadoDatos == null || empleadoDatos.Sucursal?.Empresa == null) return Forbid();
        var empleadoNombre = empleadoDatos.Nombre;
        var empleadoCodigo = empleadoDatos.Codigo;

        var hoy = _fechas.Hoy();
        hasta ??= hoy;
        desde ??= hasta.Value.AddDays(-30);
        if (hasta < desde)
        {
            (desde, hasta) = (hasta, desde);
        }

        var desdeValue = desde.Value;
        var hastaValue = hasta.Value;

        var respuestas = await _db.RespuestasFormulario
            .Include(r => r.SucursalEntrega).ThenInclude(s => s!.Empresa)
            .Include(r => r.AdicionalOpcion)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Horario)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionA)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionB)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionC)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionD)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionE)
            .Where(r => r.EmpleadoId == empleadoId)
            .Where(r => r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio <= hastaValue
                && r.OpcionMenu.Menu.FechaTermino >= desdeValue)
            .ToListAsync();

        var culture = CultureInfo.CurrentCulture;
        var movimientos = new List<EstadoCuentaEmpleadoVM.MovimientoRow>();
        foreach (var r in respuestas)
        {
            var fecha = ObtenerFechaDiaSemana(r.OpcionMenu!.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
            if (fecha < desdeValue || fecha > hastaValue) continue;
            var opcion = GetOpcionSeleccionada(r.OpcionMenu!, r.Seleccion);
            if (opcion == null) continue;
            var suc = empleadoDatos.Sucursal!;
            var emp = suc.Empresa!;
            var ctx = BuildSubsidioContext(opcion.EsSubsidiado, empleadoDatos, suc, emp);
            var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
            movimientos.Add(new EstadoCuentaEmpleadoVM.MovimientoRow
            {
                Fecha = fecha,
                DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                Horario = r.OpcionMenu.Horario != null ? r.OpcionMenu.Horario.Nombre : null,
                Seleccion = r.Seleccion.ToString(),
                OpcionNombre = opcion.Nombre ?? "Sin definir",
                PrecioEmpleado = precio
            });

            if (r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                movimientos.Add(new EstadoCuentaEmpleadoVM.MovimientoRow
                {
                    Fecha = fecha,
                    DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                    Horario = r.OpcionMenu.Horario != null ? r.OpcionMenu.Horario.Nombre : null,
                    Seleccion = r.Seleccion.ToString(),
                    OpcionNombre = $"Adicional: {adicional.Nombre ?? "Sin definir"}",
                    PrecioEmpleado = adicional.Precio ?? adicional.Costo
                });
            }
        }

        movimientos = movimientos
            .OrderByDescending(m => m.Fecha)
            .ThenBy(m => m.Horario)
            .ToList();

        var vm = new EstadoCuentaEmpleadoVM
        {
            EmpleadoId = empleadoId.Value,
            EmpleadoNombre = empleadoNombre,
            EmpleadoCodigo = empleadoCodigo,
            Desde = desdeValue,
            Hasta = hastaValue,
            Movimientos = movimientos
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Distribucion(Guid? empresaId = null, Guid? sucursalId = null)
    {
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursalesBase = _db.Sucursales.AsQueryable();
        if (empresaId != null)
            sucursalesBase = sucursalesBase.Where(s => s.EmpresaId == empresaId);
        var sucursales = await sucursalesBase.OrderBy(s => s.Nombre).ToListAsync();

        var baseQuery = _db.RespuestasFormulario
            .Include(r => r.Empleado).ThenInclude(e => e!.Sucursal).ThenInclude(s => s!.Empresa)
            .Include(r => r.SucursalEntrega).ThenInclude(s => s!.Empresa)
            .Include(r => r.LocalizacionEntrega)
            .Include(r => r.AdicionalOpcion)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Horario)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionA)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionB)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionC)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionD)
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.OpcionE)
            .Where(r => r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio == inicio
                && r.OpcionMenu.Menu.FechaTermino == fin);

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntrega != null && x.SucursalEntrega.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.SucursalEntregaId == sucursalId);

        var respuestas = await baseQuery.ToListAsync();

        const decimal itbisRate = 0.18m;
        var items = new List<(DateOnly Fecha, Guid FilialId, string Filial, Guid EmpleadoId, string Empleado, string Tanda, string Opcion, string Localizacion, decimal Base, decimal Itbis, decimal Total, decimal EmpresaPaga, decimal EmpleadoPaga, decimal ItbisEmpresa, decimal ItbisEmpleado)>();

        foreach (var r in respuestas)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            if (r.SucursalEntrega == null) continue;
            var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            if (sucEmpleado == null || empresaEmpleado == null) continue;

            var tanda = r.OpcionMenu.Horario?.Nombre ?? "Sin horario";
            var filial = r.SucursalEntrega.Nombre;
            var filialId = r.SucursalEntrega.Id;
            var empleadoNombre = r.Empleado.Nombre;
            var empleadoId = r.Empleado.Id;
            var localizacion = r.LocalizacionEntrega?.Nombre ?? "Sin asignar";

            void AddItem(Opcion opcion, string nombre, bool aplicaSubsidio)
            {
                var basePrecio = opcion.Precio ?? opcion.Costo;
                if (basePrecio < 0) basePrecio = 0;
                var itbis = opcion.LlevaItbis ? Math.Round(basePrecio * itbisRate, 2) : 0m;
                decimal precioEmpleado = basePrecio;
                if (aplicaSubsidio)
                {
                    var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, sucEmpleado, empresaEmpleado);
                    precioEmpleado = _subsidios.CalcularPrecioEmpleado(basePrecio, ctx).PrecioEmpleado;
                }
                var ratio = basePrecio > 0 ? precioEmpleado / basePrecio : 1m;
                if (ratio < 0) ratio = 0;
                if (ratio > 1) ratio = 1;
                var total = basePrecio + itbis;
                var empleadoPaga = Math.Round(total * ratio, 2);
                var empresaPaga = total - empleadoPaga;
                var itbisEmpleado = Math.Round(itbis * ratio, 2);
                var itbisEmpresa = itbis - itbisEmpleado;

                items.Add((fecha, filialId, filial, empleadoId, empleadoNombre, tanda, nombre, localizacion, basePrecio, itbis, total, empresaPaga, empleadoPaga, itbisEmpresa, itbisEmpleado));
            }

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
                AddItem(opcion, opcion.Nombre ?? "Sin definir", true);

            if (r.AdicionalOpcion != null)
                AddItem(r.AdicionalOpcion, $"Adicional: {r.AdicionalOpcion.Nombre ?? "Sin definir"}", false);
        }

        var resumen = items
            .GroupBy(i => new { i.FilialId, i.Filial })
            .Select(g => new DistribucionVM.ResumenFilialRow
            {
                FilialId = g.Key.FilialId,
                Filial = g.Key.Filial,
                Base = g.Sum(x => x.Base),
                Itbis = g.Sum(x => x.Itbis),
                Total = g.Sum(x => x.Total),
                EmpresaPaga = g.Sum(x => x.EmpresaPaga),
                EmpleadoPaga = g.Sum(x => x.EmpleadoPaga)
            })
            .OrderBy(r => r.Filial)
            .ToList();

        var detalle = items
            .GroupBy(i => new { i.Fecha, i.FilialId, i.Filial, i.EmpleadoId, i.Empleado, i.Tanda, i.Opcion })
            .Select(g => new DistribucionVM.DetalleEmpleadoRow
            {
                Fecha = g.Key.Fecha,
                Filial = g.Key.Filial,
                Empleado = g.Key.Empleado,
                Tanda = g.Key.Tanda,
                Opcion = g.Key.Opcion,
                Cantidad = g.Count(),
                MontoTotal = g.Sum(x => x.Total),
                EmpresaPaga = g.Sum(x => x.EmpresaPaga),
                EmpleadoPaga = g.Sum(x => x.EmpleadoPaga),
                ItbisEmpresa = g.Sum(x => x.ItbisEmpresa),
                ItbisEmpleado = g.Sum(x => x.ItbisEmpleado)
            })
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Filial)
            .ThenBy(r => r.Empleado)
            .ThenBy(r => r.Opcion)
            .ToList();

        var porLocalizacion = items
            .GroupBy(i => new { i.Localizacion, i.Opcion })
            .Select(g => new DistribucionVM.DistribucionLocalizacionRow
            {
                Localizacion = g.Key.Localizacion,
                Opcion = g.Key.Opcion,
                Cantidad = g.Count(),
                MontoTotal = g.Sum(x => x.Total),
                EmpresaPaga = g.Sum(x => x.EmpresaPaga),
                EmpleadoPaga = g.Sum(x => x.EmpleadoPaga)
            })
            .OrderBy(r => r.Localizacion)
            .ThenBy(r => r.Opcion)
            .ToList();

        var vm = new DistribucionVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            Empresas = empresas,
            Sucursales = sucursales,
            ResumenFiliales = resumen,
            DetalleEmpleados = detalle,
            PorLocalizacion = porLocalizacion
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin,Empresa")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleadosPdf(Guid? empresaId = null, Guid? sucursalId = null)
    {
        var result = await TotalesEmpleados(empresaId, sucursalId) as ViewResult;
        var vm = (TotalesEmpleadosVM)result!.Model!;
        var isAdmin = User.IsInRole("Admin");

        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"Totales por empleado {vm.Inicio:yyyy-MM-dd} a {vm.Fin:yyyy-MM-dd}").SemiBold().FontSize(16);
                page.Content().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(6);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        if (isAdmin)
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        }
                    });
                    t.Header(h =>
                    {
                        h.Cell().Text("Empleado").SemiBold();
                        h.Cell().AlignRight().Text("Selecciones").SemiBold();
                        h.Cell().AlignRight().Text("Costo").SemiBold();
                        if (isAdmin)
                        {
                            h.Cell().AlignRight().Text("Consumo").SemiBold();
                            h.Cell().AlignRight().Text("Beneficio").SemiBold();
                        }
                    });
                    foreach (var r in vm.Filas)
                    {
                        t.Cell().Text(r.Empleado);
                        t.Cell().AlignRight().Text(r.Cantidad.ToString());
                        t.Cell().AlignRight().Text(r.TotalCosto.ToString("C"));
                        if (isAdmin)
                        {
                            t.Cell().AlignRight().Text(r.TotalPrecio.ToString("C"));
                            t.Cell().AlignRight().Text(r.TotalBeneficio.ToString("C"));
                        }
                    }
                    t.Cell().Text("Total").SemiBold();
                    t.Cell().AlignRight().Text(vm.Filas.Sum(x => x.Cantidad).ToString()).SemiBold();
                    t.Cell().AlignRight().Text(vm.TotalCosto.ToString("C")).SemiBold();
                    if (isAdmin)
                    {
                        t.Cell().AlignRight().Text(vm.TotalPrecio.ToString("C")).SemiBold();
                        t.Cell().AlignRight().Text(vm.TotalBeneficio.ToString("C")).SemiBold();
                    }
                });
                page.Footer().AlignCenter().Text(x => x.Span("Generado por VIP GATERING").FontSize(9).Light());
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"totales-empleados-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ItemsSemanaPdf(Guid? empresaId = null, Guid? sucursalId = null)
    {
        var result = await ItemsSemana(empresaId, sucursalId) as ViewResult;
        var vm = (ItemsSemanaVM)result!.Model!;

        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"Items de la semana {vm.Inicio:yyyy-MM-dd} a {vm.Fin:yyyy-MM-dd}").SemiBold().FontSize(16);
                page.Content().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        if (vm.EmpresaId != null) text.Span("Empresa filtrada").SemiBold().FontSize(10);
                        if (vm.SucursalId != null) text.Span("  | Filial filtrada").SemiBold().FontSize(10);
                    });
                    col.Spacing(5);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(6);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Text("Item").SemiBold();
                            h.Cell().AlignRight().Text("Costo unit.").SemiBold();
                            h.Cell().AlignRight().Text("Precio unit.").SemiBold();
                            h.Cell().AlignRight().Text("Cant.").SemiBold();
                            h.Cell().AlignRight().Text("Total costo").SemiBold();
                            h.Cell().AlignRight().Text("Total consumo").SemiBold();
                            h.Cell().AlignRight().Text("Beneficio total").SemiBold();
                        });
                        foreach (var r in vm.Items)
                        {
                            t.Cell().Text(r.Nombre);
                            t.Cell().AlignRight().Text(r.CostoUnitario.ToString("C"));
                            t.Cell().AlignRight().Text(r.PrecioUnitario.ToString("C"));
                            t.Cell().AlignRight().Text(r.Cantidad.ToString());
                            t.Cell().AlignRight().Text(r.TotalCosto.ToString("C"));
                            t.Cell().AlignRight().Text(r.TotalPrecio.ToString("C"));
                            t.Cell().AlignRight().Text(r.TotalBeneficio.ToString("C"));
                        }
                        t.Cell().ColumnSpan(4).AlignRight().Text("Total").SemiBold();
                        t.Cell().AlignRight().Text(vm.TotalCosto.ToString("C")).SemiBold();
                        t.Cell().AlignRight().Text(vm.TotalPrecio.ToString("C")).SemiBold();
                        t.Cell().AlignRight().Text(vm.TotalBeneficio.ToString("C")).SemiBold();
                    });
                });
                page.Footer().AlignCenter().Text(x => x.Span("Generado por VIP GATERING").FontSize(9).Light());
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"items-semana-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Admin,Empresa")]
    [HttpGet]
    public async Task<IActionResult> SeleccionesPdf(Guid? empresaId = null, Guid? sucursalId = null)
    {
        var result = await Selecciones(empresaId, sucursalId) as ViewResult;
        var vm = (SeleccionesVM)result!.Model!;
        var isAdmin = User.IsInRole("Admin");

        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"Selecciones {vm.Inicio:yyyy-MM-dd} a {vm.Fin:yyyy-MM-dd}").SemiBold().FontSize(16);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Total costo: {vm.TotalCosto:C} | Selecciones: {vm.TotalRespuestas}").FontSize(10);
                    if (isAdmin)
                    {
                        col.Item().Text($"Consumo empleados: {vm.TotalPrecio:C} | Beneficio: {vm.TotalBeneficio:C}").FontSize(10);
                    }
                    col.Spacing(10);
                    col.Item().Text("Subtotales por filial").SemiBold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(6);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            if (isAdmin)
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            }
                        });
                        t.Header(h =>
                        {
                            h.Cell().Text("Filial").SemiBold();
                            h.Cell().AlignRight().Text("Selecciones").SemiBold();
                            h.Cell().AlignRight().Text("Costo").SemiBold();
                            if (isAdmin)
                            {
                                h.Cell().AlignRight().Text("Consumo").SemiBold();
                                h.Cell().AlignRight().Text("Beneficio").SemiBold();
                            }
                        });
                        foreach (var s in vm.PorSucursal)
                        {
                            t.Cell().Text(s.Sucursal);
                            t.Cell().AlignRight().Text(s.Cantidad.ToString());
                            t.Cell().AlignRight().Text(s.TotalCosto.ToString("C"));
                            if (isAdmin)
                            {
                                t.Cell().AlignRight().Text(s.TotalPrecio.ToString("C"));
                                t.Cell().AlignRight().Text(s.TotalBeneficio.ToString("C"));
                            }
                        }
                        t.Cell().Text("Total").SemiBold();
                        t.Cell().AlignRight().Text(vm.PorSucursal.Sum(x => x.Cantidad).ToString()).SemiBold();
                        t.Cell().AlignRight().Text(vm.PorSucursal.Sum(x => x.TotalCosto).ToString("C")).SemiBold();
                        if (isAdmin)
                        {
                            t.Cell().AlignRight().Text(vm.PorSucursal.Sum(x => x.TotalPrecio).ToString("C")).SemiBold();
                            t.Cell().AlignRight().Text(vm.PorSucursal.Sum(x => x.TotalBeneficio).ToString("C")).SemiBold();
                        }
                    });
                    col.Spacing(10);
                    col.Item().Text("Detalle por empleado").SemiBold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(4);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            if (isAdmin)
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            }
                        });
                        t.Header(h =>
                        {
                            h.Cell().Text("Empleado").SemiBold();
                            h.Cell().Text("Filial").SemiBold();
                            h.Cell().AlignRight().Text("Selecciones").SemiBold();
                            h.Cell().AlignRight().Text("Costo").SemiBold();
                            if (isAdmin)
                            {
                                h.Cell().AlignRight().Text("Consumo").SemiBold();
                                h.Cell().AlignRight().Text("Beneficio").SemiBold();
                            }
                        });
                        foreach (var e in vm.PorEmpleado)
                        {
                            t.Cell().Text(e.Empleado);
                            t.Cell().Text(e.Sucursal);
                            t.Cell().AlignRight().Text(e.Cantidad.ToString());
                            t.Cell().AlignRight().Text(e.TotalCosto.ToString("C"));
                            if (isAdmin)
                            {
                                t.Cell().AlignRight().Text(e.TotalPrecio.ToString("C"));
                                t.Cell().AlignRight().Text(e.TotalBeneficio.ToString("C"));
                            }
                        }
                        t.Cell().ColumnSpan(2).AlignRight().Text("Total").SemiBold();
                        t.Cell().AlignRight().Text(vm.PorEmpleado.Sum(x => x.Cantidad).ToString()).SemiBold();
                        t.Cell().AlignRight().Text(vm.PorEmpleado.Sum(x => x.TotalCosto).ToString("C")).SemiBold();
                        if (isAdmin)
                        {
                            t.Cell().AlignRight().Text(vm.PorEmpleado.Sum(x => x.TotalPrecio).ToString("C")).SemiBold();
                            t.Cell().AlignRight().Text(vm.PorEmpleado.Sum(x => x.TotalBeneficio).ToString("C")).SemiBold();
                        }
                    });
                });
                page.Footer().AlignCenter().Text(x => x.Span("Generado por VIP GATERING").FontSize(9).Light());
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"selecciones-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    private static Opcion? GetOpcionSeleccionada(OpcionMenu opcionMenu, char seleccion)
    {
        var max = opcionMenu.OpcionesMaximas == 0 ? 3 : Math.Clamp(opcionMenu.OpcionesMaximas, 1, 5);
        return seleccion switch
        {
            'A' when max >= 1 => opcionMenu.OpcionA,
            'B' when max >= 2 => opcionMenu.OpcionB,
            'C' when max >= 3 => opcionMenu.OpcionC,
            'D' when max >= 4 => opcionMenu.OpcionD,
            'E' when max >= 5 => opcionMenu.OpcionE,
            _ => null
        };
    }

    private SubsidioContext BuildSubsidioContext(bool opcionSubsidiada, Empleado empleado, Sucursal sucursal, Empresa empresa) =>
        new(opcionSubsidiada,
            empleado.EsSubsidiado,
            empresa.SubsidiaEmpleados,
            empresa.SubsidioTipo,
            empresa.SubsidioValor,
            sucursal.SubsidiaEmpleados,
            sucursal.SubsidioTipo,
            sucursal.SubsidioValor,
            empleado.SubsidioTipo,
            empleado.SubsidioValor);

    private static DateOnly ObtenerFechaDiaSemana(DateOnly inicioSemana, DayOfWeek dia)
    {
        var offset = ((int)dia - (int)DayOfWeek.Monday + 7) % 7;
        return inicioSemana.AddDays(offset);
    }
}
