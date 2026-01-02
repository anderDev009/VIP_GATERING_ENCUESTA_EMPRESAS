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
    public async Task<IActionResult> ItemsSemana(int? empresaId = null, int? sucursalId = null)
    {
        var (inicio, fin) = GetDefaultReportRange(_fechas.Hoy());

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
                && r.OpcionMenu.Menu.FechaInicio >= inicio
                && r.OpcionMenu.Menu.FechaTermino <= fin);

        baseQuery = AplicarFiltrosEmpresaSucursal(baseQuery, empresaId, sucursalId);

        var hoy = _fechas.Hoy();
        var respuestas = await baseQuery.ToListAsync();
        var itemsRaw = new List<(int OpcionId, string Nombre, decimal Costo, decimal PrecioEmpleado)>();
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

    [Authorize(Roles = "Admin,Empresa,Empleado")]
    [HttpGet]
    public async Task<IActionResult> Selecciones(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        // Empresa solo puede ver su propia data
        if (User.IsInRole("Empresa"))
        {
            if (_current.EmpresaId == null) return Forbid();
            if (empresaId != null && empresaId != _current.EmpresaId) return Forbid();
            empresaId = _current.EmpresaId;
        }
        else if (User.IsInRole("Empleado"))
        {
            if (_current.EmpresaId == null) return Forbid();
            empresaId = _current.EmpresaId;
            sucursalId = _current.SucursalId;
        }

        var isEmpleado = User.IsInRole("Empleado");
        DateOnly inicio;
        DateOnly fin;
        if (desde.HasValue && hasta.HasValue)
        {
            inicio = desde.Value;
            fin = hasta.Value;
            if (fin < inicio)
            {
                (inicio, fin) = (fin, inicio);
            }
        }
        else if (desde.HasValue)
        {
            inicio = desde.Value;
            fin = inicio.AddDays(4);
        }
        else if (hasta.HasValue)
        {
            fin = hasta.Value;
            inicio = fin.AddDays(-4);
        }
        else
        {
            (inicio, fin) = isEmpleado ? _fechas.RangoSemanaActual() : GetDefaultReportRange(_fechas.Hoy());
        }

        var empresasQuery = _db.Empresas.AsQueryable();
        if (User.IsInRole("Empleado") && empresaId != null)
            empresasQuery = empresasQuery.Where(e => e.Id == empresaId);
        var empresas = await empresasQuery.OrderBy(e => e.Nombre).ToListAsync();
        var sucursalesBase = _db.Sucursales.AsQueryable();
        if (empresaId != null)
            sucursalesBase = sucursalesBase.Where(s => s.EmpresaId == empresaId);
        if (isEmpleado && sucursalId != null)
            sucursalesBase = sucursalesBase.Where(s => s.Id == sucursalId);
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
                && r.OpcionMenu.Menu.FechaInicio <= fin
                && r.OpcionMenu.Menu.FechaTermino >= inicio);

        baseQuery = AplicarFiltrosEmpresaSucursal(baseQuery, empresaId, sucursalId);

        var hoy = _fechas.Hoy();
        var respuestas = await baseQuery.ToListAsync();
        var respuestasVisible = respuestas
            .Where(r => r.OpcionMenu != null && r.OpcionMenu.Menu != null)
            .Where(r =>
            {
                var fecha = ObtenerFechaDiaSemana(r.OpcionMenu!.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
                if (fecha < inicio || fecha > fin) return false;
                return isEmpleado ? fecha > hoy : fecha <= hoy;
            })
            .ToList();

        var detalleRaw = new List<(int EmpleadoId, string EmpleadoNombre, int SucursalId, string SucursalNombre, decimal Costo, decimal Precio)>();
        foreach (var r in respuestasVisible)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var empleadoNombre = GetEmpleadoDisplayName(r.Empleado);
            var suc = r.SucursalEntrega ?? r.Empleado.Sucursal;
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            if (suc == null || sucEmpleado == null || empresaEmpleado == null) continue;

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
            {
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, sucEmpleado, empresaEmpleado);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                detalleRaw.Add((r.Empleado.Id, empleadoNombre, suc.Id, suc.Nombre, opcion.Costo, precio));
            }

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var precio = adicional.Precio ?? adicional.Costo;
                detalleRaw.Add((r.Empleado.Id, empleadoNombre, suc.Id, suc.Nombre, adicional.Costo, precio));
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

        var seleccionesEmpleado = new List<SeleccionesVM.SeleccionDetalleEmpleadoRow>();
        if (isEmpleado)
        {
            var culture = CultureInfo.CurrentCulture;
            foreach (var r in respuestasVisible)
            {
                if (r.OpcionMenu == null || r.Empleado == null) continue;
                var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
                var localizacion = r.SucursalEntrega?.Nombre ?? r.Empleado.Sucursal?.Nombre ?? "Sin asignar";

                var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
                if (opcion != null)
                {
                    var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, r.Empleado.Sucursal!, r.Empleado.Sucursal!.Empresa!);
                    var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                    seleccionesEmpleado.Add(new SeleccionesVM.SeleccionDetalleEmpleadoRow
                    {
                        Fecha = fecha,
                        DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                        OpcionNombre = opcion.Nombre ?? "Sin definir",
                        Precio = precio,
                        Localizacion = localizacion
                    });
                }

                if (r.AdicionalOpcion != null)
                {
                    seleccionesEmpleado.Add(new SeleccionesVM.SeleccionDetalleEmpleadoRow
                    {
                        Fecha = fecha,
                        DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                        OpcionNombre = $"Adicional: {r.AdicionalOpcion.Nombre ?? "Sin definir"}",
                        Precio = r.AdicionalOpcion.Precio ?? r.AdicionalOpcion.Costo,
                        Localizacion = localizacion
                    });
                }
            }
        }

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
            PorEmpleado = detalle,
            SeleccionesEmpleado = seleccionesEmpleado
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin,Empresa,Empleado")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleados(int? empresaId = null, int? sucursalId = null)
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
                && r.OpcionMenu.Menu.FechaInicio <= fin
                && r.OpcionMenu.Menu.FechaTermino >= inicio);

        baseQuery = AplicarFiltrosEmpresaSucursal(baseQuery, empresaId, sucursalId);

        var hoy = _fechas.Hoy();
        var respuestas = await baseQuery.ToListAsync();
        respuestas = respuestas
            .Where(r => r.OpcionMenu != null && r.OpcionMenu.Menu != null
                && ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana) <= hoy)
            .ToList();

        var rowsRaw = new List<(int EmpleadoId, string EmpleadoNombre, decimal Costo, decimal Precio)>();
        foreach (var r in respuestas)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var empleadoNombre = GetEmpleadoDisplayName(r.Empleado);
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            if (sucEmpleado == null || empresaEmpleado == null) continue;

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
            {
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, sucEmpleado, empresaEmpleado);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                rowsRaw.Add((r.Empleado.Id, empleadoNombre, opcion.Costo, precio));
            }

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var precio = adicional.Precio ?? adicional.Costo;
                rowsRaw.Add((r.Empleado.Id, empleadoNombre, adicional.Costo, precio));
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
        var empleadoNombre = GetEmpleadoDisplayName(empleadoDatos);
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
        respuestas = respuestas
            .Where(r => r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana) <= hoy)
            .ToList();

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

    [Authorize(Roles = "Admin,Empresa,Empleado")]
    [HttpGet]
    public async Task<IActionResult> Seleccionados(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
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
                && r.OpcionMenu.Menu.FechaInicio <= fin
                && r.OpcionMenu.Menu.FechaTermino >= inicio);

        baseQuery = AplicarFiltrosEmpresaSucursal(baseQuery, empresaId, sucursalId);

        var hoy = _fechas.Hoy();
        var respuestas = await baseQuery.ToListAsync();
        var pendientes = respuestas
            .Where(r =>
            {
                if (r.OpcionMenu == null || r.OpcionMenu.Menu == null) return false;
                var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana);
                return fecha > hoy && fecha >= inicio && fecha <= fin;
            })
            .ToList();

        var detalleRaw = new List<(int EmpleadoId, string EmpleadoNombre, int SucursalId, string SucursalNombre, decimal Costo, decimal Precio)>();
        foreach (var r in pendientes)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var empleadoNombre = GetEmpleadoDisplayName(r.Empleado);
            var sucEntrega = r.SucursalEntrega ?? r.Empleado.Sucursal;
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            if (sucEntrega == null || sucEmpleado == null || empresaEmpleado == null) continue;

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
            {
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, sucEmpleado, empresaEmpleado);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                detalleRaw.Add((r.Empleado.Id, empleadoNombre, sucEntrega.Id, sucEntrega.Nombre, opcion.Costo, precio));
            }

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var precio = adicional.Precio ?? adicional.Costo;
                detalleRaw.Add((r.Empleado.Id, empleadoNombre, sucEntrega.Id, sucEntrega.Nombre, adicional.Costo, precio));
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
        return View("Seleccionados", vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Distribucion(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        DateOnly inicio;
        DateOnly fin;
        if (desde.HasValue && hasta.HasValue)
        {
            inicio = desde.Value;
            fin = hasta.Value;
            if (fin < inicio)
            {
                (inicio, fin) = (fin, inicio);
            }
        }
        else if (desde.HasValue)
        {
            inicio = desde.Value;
            fin = inicio.AddDays(4);
        }
        else if (hasta.HasValue)
        {
            fin = hasta.Value;
            inicio = fin.AddDays(-4);
        }
        else
        {
            (inicio, fin) = GetDefaultReportRange(_fechas.Hoy());
        }

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
                && r.OpcionMenu.Menu.FechaInicio <= fin
                && r.OpcionMenu.Menu.FechaTermino >= inicio);

        baseQuery = AplicarFiltrosEmpresaSucursal(baseQuery, empresaId, sucursalId);

        var respuestas = await baseQuery.ToListAsync();
        respuestas = respuestas
            .Where(r =>
            {
                if (r.OpcionMenu == null || r.OpcionMenu.Menu == null) return false;
                var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana);
                return fecha >= inicio && fecha <= fin;
            })
            .ToList();

        const decimal itbisRate = 0.18m;
        var items = new List<(DateOnly Fecha, int FilialId, string Filial, int EmpleadoId, string Empleado, string Tanda, string Opcion, string Seleccion, string Localizacion, decimal Base, decimal Itbis, decimal Total, decimal EmpresaPaga, decimal EmpleadoPaga, decimal ItbisEmpresa, decimal ItbisEmpleado)>();

        foreach (var r in respuestas)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var fechaDia = ObtenerFechaDiaSemana(r.OpcionMenu.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
            var sucEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucEmpleado?.Empresa;
            var sucursalEntrega = r.SucursalEntrega ?? sucEmpleado;
            if (sucEmpleado == null || empresaEmpleado == null || sucursalEntrega == null) continue;

            var tanda = r.OpcionMenu.Horario?.Nombre ?? "Sin horario";
            var filial = sucursalEntrega.Nombre;
            var filialId = sucursalEntrega.Id;
            var empleadoNombre = GetEmpleadoDisplayName(r.Empleado);
            var empleadoId = r.Empleado.Id;
            var localizacion = r.LocalizacionEntrega?.Nombre ?? "Sin asignar";

            void AddItem(Opcion opcion, string nombre, string seleccionLabel, bool aplicaSubsidio)
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

                items.Add((fechaDia, filialId, filial, empleadoId, empleadoNombre, tanda, nombre, seleccionLabel, localizacion, basePrecio, itbis, total, empresaPaga, empleadoPaga, itbisEmpresa, itbisEmpleado));
            }

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
                AddItem(opcion, opcion.Nombre ?? "Sin definir", MapSeleccion(r.Seleccion), true);

            if (r.AdicionalOpcion != null)
                AddItem(r.AdicionalOpcion, $"Adicional: {r.AdicionalOpcion.Nombre ?? "Sin definir"}", "Adicional", false);
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
            .GroupBy(i => new { i.Fecha, i.FilialId, i.Filial, i.EmpleadoId, i.Empleado, i.Tanda, i.Opcion, i.Seleccion })
            .Select(g => new DistribucionVM.DetalleEmpleadoRow
            {
                Fecha = g.Key.Fecha,
                Filial = g.Key.Filial,
                Empleado = g.Key.Empleado,
                Tanda = g.Key.Tanda,
                Opcion = g.Key.Opcion,
                Seleccion = g.Key.Seleccion,
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
            .GroupBy(i => new { i.Localizacion, i.Opcion, i.Seleccion })
            .Select(g => new DistribucionVM.DistribucionLocalizacionRow
            {
                Localizacion = g.Key.Localizacion,
                Opcion = g.Key.Opcion,
                Seleccion = g.Key.Seleccion,
                Cantidad = g.Count(),
                MontoTotal = g.Sum(x => x.Total),
                EmpresaPaga = g.Sum(x => x.EmpresaPaga),
                EmpleadoPaga = g.Sum(x => x.EmpleadoPaga)
            })
            .OrderBy(r => r.Localizacion)
            .ThenBy(r => r.Opcion)
            .ToList();

        var porLocalizacionCocina = items
            .GroupBy(i => i.Localizacion)
            .Select(g =>
            {
                var adicionalesDetalle = g
                    .Where(i => i.Seleccion == "Adicional")
                    .Select(i => i.Opcion.StartsWith("Adicional:", StringComparison.OrdinalIgnoreCase)
                        ? i.Opcion.Replace("Adicional:", string.Empty).Trim()
                        : i.Opcion)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                var row = new DistribucionVM.DistribucionCocinaRow
                {
                    Localizacion = g.Key
                };

                foreach (var item in g)
                {
                    if (item.Seleccion == "Adicional")
                    {
                        row.Adicionales += 1;
                        continue;
                    }

                    if (item.Seleccion.StartsWith("Opcion", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = item.Seleccion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && int.TryParse(parts[1], out var idx))
                        {
                            switch (idx)
                            {
                                case 1: row.Opcion1 += 1; break;
                                case 2: row.Opcion2 += 1; break;
                                case 3: row.Opcion3 += 1; break;
                                case 4: row.Opcion4 += 1; break;
                                case 5: row.Opcion5 += 1; break;
                            }
                        }
                    }
                }

                if (adicionalesDetalle.Count > 0)
                    row.AdicionalesDetalle = string.Join(", ", adicionalesDetalle);

                return row;
            })
            .OrderBy(r => r.Localizacion)
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
            PorLocalizacion = porLocalizacion,
            PorLocalizacionCocina = porLocalizacionCocina
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin,Empresa")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleadosPdf(int? empresaId = null, int? sucursalId = null)
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
                page.Footer().AlignCenter().Text(x => x.Span("Generado por VIP CATERING").FontSize(9).Light());
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"totales-empleados-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ItemsSemanaPdf(int? empresaId = null, int? sucursalId = null)
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
                page.Footer().AlignCenter().Text(x => x.Span("Generado por VIP CATERING").FontSize(9).Light());
            });
        }).GeneratePdf();

        return File(pdf, "application/pdf", $"items-semana-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Admin,Empresa,Empleado")]
    [HttpGet]
    public async Task<IActionResult> SeleccionesPdf(int? empresaId = null, int? sucursalId = null)
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
                page.Footer().AlignCenter().Text(x => x.Span("Generado por VIP CATERING").FontSize(9).Light());
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

    private IQueryable<RespuestaFormulario> AplicarFiltrosEmpresaSucursal(IQueryable<RespuestaFormulario> query, int? empresaId, int? sucursalId)
    {
        if (empresaId != null)
        {
            query = query.Where(r =>
                (r.SucursalEntrega != null && r.SucursalEntrega.EmpresaId == empresaId) ||
                (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.Sucursal != null && r.Empleado.Sucursal.EmpresaId == empresaId));
        }

        if (sucursalId != null)
        {
            query = query.Where(r =>
                (r.SucursalEntrega != null && r.SucursalEntrega.Id == sucursalId) ||
                (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.SucursalId == sucursalId));
        }

        return query;
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

    private static (DateOnly inicio, DateOnly fin) GetDefaultReportRange(DateOnly hoy)
    {
        var inicio = new DateOnly(hoy.Year, hoy.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        return (inicio, fin);
    }

    private static string MapSeleccion(char seleccion) => seleccion switch
    {
        'A' => "Opcion 1",
        'B' => "Opcion 2",
        'C' => "Opcion 3",
        'D' => "Opcion 4",
        'E' => "Opcion 5",
        _ => "Opcion"
    };

    private static string GetEmpleadoDisplayName(Empleado empleado)
    {
        if (!string.IsNullOrWhiteSpace(empleado.Nombre)) return empleado.Nombre;
        if (!string.IsNullOrWhiteSpace(empleado.Codigo)) return empleado.Codigo;
        return "Sin nombre";
    }
}
