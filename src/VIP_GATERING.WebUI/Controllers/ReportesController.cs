using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;
using VIP_GATERING.WebUI.Models;
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
    public async Task<IActionResult> ItemsSemana(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var (inicio, fin) = GetDefaultReportRange(_fechas.Hoy());

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursales = await _db.Sucursales
            .OrderBy(s => s.Nombre)
            .ToListAsync();
        var empleados = await ObtenerEmpleadosFiltroAsync(empresaId, sucursalId);

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
        if (empleadoId != null)
            baseQuery = baseQuery.Where(r => r.EmpleadoId == empleadoId);

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
            EmpleadoId = empleadoId,
            Empresas = empresas,
            Sucursales = sucursales,
            Empleados = empleados,
            Items = items
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> Selecciones(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado"))
        {
            if (_current.EmpresaId == null) return Forbid();
            empresaId = _current.EmpresaId;
            sucursalId = _current.SucursalId;
            empleadoId = _current.EmpleadoId;
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
        var empleados = await ObtenerEmpleadosFiltroAsync(empresaId, sucursalId);

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
        if (empleadoId != null)
            baseQuery = baseQuery.Where(r => r.EmpleadoId == empleadoId);

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
            EmpleadoId = empleadoId,
            Empresas = empresas,
            Sucursales = sucursales,
            Empleados = empleados,
            TotalRespuestas = detalle.Sum(d => d.Cantidad),
            TotalCosto = detalle.Sum(d => d.TotalCosto),
            TotalPrecio = detalle.Sum(d => d.TotalPrecio),
            PorSucursal = porSucursal,
            PorEmpleado = detalle,
            SeleccionesEmpleado = seleccionesEmpleado
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleados(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursalesBase = _db.Sucursales.AsQueryable();
        if (empresaId != null)
            sucursalesBase = sucursalesBase.Where(s => s.EmpresaId == empresaId);
        var sucursales = await sucursalesBase.OrderBy(s => s.Nombre).ToListAsync();
        // TotalesEmpleados sigue mostrando el resumen general; no requiere filial obligatoria.
        var empleados = await ObtenerEmpleadosFiltroAsync(empresaId, sucursalId);

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
        if (empleadoId != null)
            baseQuery = baseQuery.Where(r => r.EmpleadoId == empleadoId);

        var hoy = _fechas.Hoy();
        var respuestas = await baseQuery.ToListAsync();
        respuestas = respuestas
            .Where(r => r.OpcionMenu != null && r.OpcionMenu.Menu != null
                && ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana) <= hoy)
            .ToList();

        const decimal itbisRate = 0.18m;
        var rowsRaw = new List<(int EmpleadoId, string EmpleadoNombre, decimal Costo, decimal Itbis, decimal Precio)>();
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
                var itbis = opcion.LlevaItbis ? Math.Round(opcion.Costo * itbisRate, 2) : 0m;
                rowsRaw.Add((r.Empleado.Id, empleadoNombre, opcion.Costo, itbis, precio));
            }

            if (r.AdicionalOpcionId != null && r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var precio = adicional.Precio ?? adicional.Costo;
                var itbis = adicional.LlevaItbis ? Math.Round(adicional.Costo * itbisRate, 2) : 0m;
                rowsRaw.Add((r.Empleado.Id, empleadoNombre, adicional.Costo, itbis, precio));
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
                TotalItbis = g.Sum(i => i.Itbis),
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
            EmpleadoId = empleadoId,
            Empresas = empresas,
            Sucursales = sucursales,
            Empleados = empleados,
            Filas = rows
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> CierreNomina(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        var data = await BuildCierreFacturacionAsync(empresaId, sucursalId, desde, hasta, false);
        data.Vm.Titulo = "Cierre de nomina";
        data.Vm.AccionGenerar = nameof(CierreNominaGenerar);
        data.Vm.AccionLabel = "Cerrar nomina";
        data.Vm.TipoExport = "cierre-nomina";
        data.Vm.EstaCerrado = false;
        data.Vm.EstadoProceso = data.Items.Count == 0 ? "Sin pendientes" : "Abierto";
        return View(data.Vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CierreNominaGuardar(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null || sucursalId == null)
        {
            TempData["ExportMessage"] = "Debe seleccionar empresa y filial para guardar la nomina.";
            return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
        }

        var (inicio, fin) = ResolveRango(desde, hasta);
        var respuestas = await BuildCierreBaseAsync(inicio, fin, empresaId.Value, sucursalId.Value);
        if (respuestas.Count == 0)
        {
            TempData["Success"] = null;
            TempData["ExportMessage"] = "No hay registros para guardar la nomina.";
            return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
        }

        var usuarioProceso = User.Identity?.Name ?? "Sistema";
        var abiertas = respuestas.Where(r => !r.CierreNomina).ToList();
        if (abiertas.Count == 0)
        {
            TempData["Success"] = null;
            TempData["ExportMessage"] = "Nomina sin pendientes.";
            return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
        }
        foreach (var r in abiertas)
        {
            if (string.IsNullOrWhiteSpace(r.UsuarioCierreNomina))
                r.UsuarioCierreNomina = usuarioProceso;
            if (r.CierreNomina)
                r.FechaCierreNomina = r.FechaCierreNomina ?? DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = "Nomina guardada correctamente.";
        return RedirectToAction(nameof(NominasMantenimiento));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CierreNominaGenerar(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null, string? format = null)
    {
        if (sucursalId == null)
        {
            TempData["ExportMessage"] = "Debe seleccionar una filial para cerrar nomina.";
            return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
        }

        var data = await BuildCierreFacturacionAsync(empresaId, sucursalId, desde, hasta, false);
        if (data.Items.Count == 0)
        {
            TempData["ExportMessage"] = "No hay registros disponibles para el cierre de nomina.";
            return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
        }

        var usuarioProceso = User.Identity?.Name ?? "Sistema";
        var timestamp = DateTime.UtcNow;
        EnsureSnapshots(data.Respuestas);
        foreach (var r in data.Respuestas.Where(r => !r.CierreNomina))
        {
            r.CierreNomina = true;
            r.FechaCierreNomina = timestamp;
            r.UsuarioCierreNomina = usuarioProceso;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Nomina creada correctamente.";
        return RedirectToAction(nameof(Nominas), new { empresaId, sucursalId, desde, hasta });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Facturacion(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        var data = await BuildCierreFacturacionAsync(empresaId, sucursalId, desde, hasta, true);
        data.Vm.Titulo = "Facturacion";
        data.Vm.AccionGenerar = nameof(FacturacionGenerar);
        data.Vm.AccionLabel = "Facturar";
        data.Vm.TipoExport = "facturacion";
        if (empresaId != null && sucursalId != null)
        {
            var (inicio, fin) = ResolveRango(desde, hasta);
            var respuestas = await BuildCierreBaseAsync(inicio, fin, empresaId.Value, sucursalId.Value);
            data.Vm.EstaCerrado = respuestas.Any(r => r.Facturado);
        }
        data.Vm.EstadoProceso = data.Vm.EstaCerrado ? "Cerrado" : "Abierto";
        return View(data.Vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReabrirNomina(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null || sucursalId == null)
        {
            TempData["ExportMessage"] = "Debe seleccionar empresa y filial para reabrir nomina.";
            return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
        }

        var (inicio, fin) = ResolveRango(desde, hasta);
        var count = await ClearCierreAsync(false, empresaId.Value, sucursalId.Value, inicio, fin, clearUsuarioNomina: false);
        if (count == 0)
        {
            TempData["ExportMessage"] = "No hay nominas cerradas para reabrir.";
            return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
        }

        TempData["Success"] = "Nomina reabierta correctamente.";
        return RedirectToAction(nameof(CierreNomina), new { empresaId, sucursalId, desde, hasta });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReabrirFacturacion(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null || sucursalId == null)
        {
            TempData["ExportMessage"] = "Debe seleccionar empresa y filial para reabrir facturacion.";
            return RedirectToAction(nameof(Facturacion), new { empresaId, sucursalId, desde, hasta });
        }

        var (inicio, fin) = ResolveRango(desde, hasta);
        var count = await ClearCierreAsync(true, empresaId.Value, sucursalId.Value, inicio, fin);
        if (count == 0)
        {
            TempData["ExportMessage"] = "No hay facturas cerradas para reabrir.";
            return RedirectToAction(nameof(Facturacion), new { empresaId, sucursalId, desde, hasta });
        }

        TempData["Success"] = "Facturacion reabierta correctamente.";
        return RedirectToAction(nameof(Facturacion), new { empresaId, sucursalId, desde, hasta });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FacturacionGenerar(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null, string? format = null)
    {
        if (sucursalId == null)
        {
            TempData["ExportMessage"] = "Debe seleccionar una filial para facturar.";
            return RedirectToAction(nameof(Facturacion), new { empresaId, sucursalId, desde, hasta });
        }

        var data = await BuildCierreFacturacionAsync(empresaId, sucursalId, desde, hasta, true);
        if (data.Items.Count == 0)
        {
            TempData["ExportMessage"] = "No hay registros disponibles para facturacion.";
            return RedirectToAction(nameof(Facturacion), new { empresaId, sucursalId, desde, hasta });
        }

        var usuarioProceso = User.Identity?.Name ?? "Sistema";
        var timestamp = DateTime.UtcNow;
        EnsureSnapshots(data.Respuestas);
        foreach (var r in data.Respuestas)
        {
            r.Facturado = true;
            r.FechaFacturado = timestamp;
            r.UsuarioFacturacion = usuarioProceso;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Facturacion creada correctamente.";
        return RedirectToAction(nameof(Facturas), new { empresaId, sucursalId, desde, hasta });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> NominasMantenimiento(int page = 1, int pageSize = 20)
    {
        var vm = await BuildCierreListadoAsync(false, false, page, pageSize);
        vm.Titulo = "Nominas";
        vm.MostrarCrear = true;
        vm.AccionCrear = nameof(CierreNomina);
        vm.AccionEditar = nameof(CierreNomina);
        vm.AccionDetalle = nameof(Nominas);
        vm.AccionEliminar = nameof(EliminarNomina);
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> FacturacionMantenimiento(int page = 1, int pageSize = 20)
    {
        var vm = await BuildCierreListadoAsync(true, true, page, pageSize);
        vm.Titulo = "Facturacion";
        vm.MostrarCrear = true;
        vm.AccionCrear = nameof(Facturacion);
        vm.AccionEditar = nameof(Facturacion);
        vm.AccionDetalle = nameof(Facturas);
        vm.AccionEliminar = nameof(EliminarFacturacion);
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> NominasHistorico(int page = 1, int pageSize = 20)
    {
        var vm = await BuildCierreListadoAsync(false, true, page, pageSize);
        vm.Titulo = "Historico de nominas";
        vm.MostrarCrear = false;
        vm.AccionDetalle = nameof(Nominas);
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> FacturasHistorico(int page = 1, int pageSize = 20)
    {
        var vm = await BuildCierreListadoAsync(true, true, page, pageSize);
        vm.Titulo = "Historico de facturas";
        vm.MostrarCrear = false;
        vm.AccionDetalle = nameof(Facturas);
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarNomina(int empresaId, int sucursalId, DateOnly desde, DateOnly hasta, int page = 1, int pageSize = 20)
    {
        var count = await ClearCierreAsync(false, empresaId, sucursalId, desde, hasta, clearUsuarioNomina: true);
        if (count > 0)
        {
            TempData["Success"] = "Nomina eliminada correctamente.";
            return RedirectToAction(nameof(NominasMantenimiento), new { page, pageSize });
        }

        var abiertas = await ClearNominaCreadaAsync(empresaId, sucursalId, desde, hasta);
        TempData["Success"] = abiertas > 0 ? "Nomina eliminada correctamente." : "No hay nominas para eliminar.";
        return RedirectToAction(nameof(NominasMantenimiento), new { page, pageSize });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarFacturacion(int empresaId, int sucursalId, DateOnly desde, DateOnly hasta, int page = 1, int pageSize = 20)
    {
        var count = await ClearCierreAsync(true, empresaId, sucursalId, desde, hasta);
        TempData["Success"] = count > 0 ? "Facturacion eliminada correctamente." : "No hay facturas cerradas para eliminar.";
        return RedirectToAction(nameof(FacturacionMantenimiento), new { page, pageSize });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Nominas(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, false);
        vm.Titulo = "Nominas cerradas";
        vm.ExportExcelAction = nameof(NominasExcel);
        vm.ExportCsvAction = nameof(NominasCsv);
        vm.ExportPdfAction = nameof(NominasPdf);
        vm.EstadoProceso = "Cerrado";
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Facturas(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, true);
        vm.Titulo = "Facturas";
        vm.ExportExcelAction = nameof(FacturasExcel);
        vm.ExportCsvAction = nameof(FacturasCsv);
        vm.ExportPdfAction = nameof(FacturasPdf);
        vm.EstadoProceso = "Cerrado";
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> NominasCsv(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null)
            empresaId = await _db.Empresas.OrderBy(e => e.Nombre).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, false);
        var export = BuildDetalleExport(vm);
        return File(ExportHelper.BuildCsv(export.Headers, export.Rows), "text/csv", $"nominas-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> NominasExcel(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null)
            empresaId = await _db.Empresas.OrderBy(e => e.Nombre).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, false);
        var export = BuildDetalleExport(vm);
        return File(ExportHelper.BuildExcel("Nominas cerradas", export.Headers, export.Rows),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"nominas-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> NominasPdf(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null)
            empresaId = await _db.Empresas.OrderBy(e => e.Nombre).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, false);
        var export = BuildDetalleExport(vm);
        return File(ExportHelper.BuildPdf("Nominas cerradas", export.Headers, export.Rows),
            "application/pdf",
            $"nominas-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> FacturasCsv(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null)
            empresaId = await _db.Empresas.OrderBy(e => e.Nombre).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, true);
        var export = BuildDetalleExport(vm);
        return File(ExportHelper.BuildCsv(export.Headers, export.Rows), "text/csv", $"facturas-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> FacturasExcel(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null)
            empresaId = await _db.Empresas.OrderBy(e => e.Nombre).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, true);
        var export = BuildDetalleExport(vm);
        return File(ExportHelper.BuildExcel("Facturas", export.Headers, export.Rows),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"facturas-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> FacturasPdf(int? empresaId = null, int? sucursalId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (empresaId == null)
            empresaId = await _db.Empresas.OrderBy(e => e.Nombre).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        var vm = await BuildCierreFacturacionDetalleAsync(empresaId, sucursalId, desde, hasta, true);
        var export = BuildDetalleExport(vm);
        return File(ExportHelper.BuildPdf("Facturas", export.Headers, export.Rows),
            "application/pdf",
            $"facturas-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
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
        var rawDesde = Request.Query["desde"].FirstOrDefault();
        var rawHasta = Request.Query["hasta"].FirstOrDefault();
        if (desde == null && !string.IsNullOrWhiteSpace(rawDesde))
        {
            if (TryParseDateOnly(rawDesde, out var parsed))
                desde = parsed;
        }
        if (hasta == null && !string.IsNullOrWhiteSpace(rawHasta))
        {
            if (TryParseDateOnly(rawHasta, out var parsed))
                hasta = parsed;
        }
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
        const decimal itbisRate = 0.18m;
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
            var basePrecio = opcion.Precio ?? opcion.Costo;
            var baseEmpleado = _subsidios.CalcularPrecioEmpleado(basePrecio, ctx).PrecioEmpleado;
            var itbis = opcion.LlevaItbis ? Math.Round(basePrecio * itbisRate, 2) : 0m;
            var total = basePrecio + itbis;
            var ratio = basePrecio > 0 ? Math.Clamp(baseEmpleado / basePrecio, 0m, 1m) : 1m;
            var totalEmpleado = Math.Round(total * ratio, 2);
            var itbisEmpleado = Math.Round(totalEmpleado - baseEmpleado, 2);
            movimientos.Add(new EstadoCuentaEmpleadoVM.MovimientoRow
            {
                Fecha = fecha,
                DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                Horario = r.OpcionMenu.Horario != null ? r.OpcionMenu.Horario.Nombre : null,
                Seleccion = r.Seleccion.ToString(),
                OpcionNombre = opcion.Nombre ?? "Sin definir",
                BaseEmpleado = Math.Round(baseEmpleado, 2),
                ItbisEmpleado = itbisEmpleado,
                TotalEmpleado = totalEmpleado
            });

            if (r.AdicionalOpcion != null)
            {
                var adicional = r.AdicionalOpcion;
                var baseAdicional = adicional.Precio ?? adicional.Costo;
                var itbisAdicional = adicional.LlevaItbis ? Math.Round(baseAdicional * itbisRate, 2) : 0m;
                var totalAdicional = baseAdicional + itbisAdicional;
                movimientos.Add(new EstadoCuentaEmpleadoVM.MovimientoRow
                {
                    Fecha = fecha,
                    DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                    Horario = r.OpcionMenu.Horario != null ? r.OpcionMenu.Horario.Nombre : null,
                    Seleccion = r.Seleccion.ToString(),
                    OpcionNombre = $"Adicional: {adicional.Nombre ?? "Sin definir"}",
                    BaseEmpleado = Math.Round(baseAdicional, 2),
                    ItbisEmpleado = itbisAdicional,
                    TotalEmpleado = Math.Round(totalAdicional, 2)
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

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> Seleccionados(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado"))
        {
            if (_current.EmpresaId == null) return Forbid();
            empresaId = _current.EmpresaId;
            sucursalId = _current.SucursalId;
            empleadoId = _current.EmpleadoId;
        }

        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursalesBase = _db.Sucursales.AsQueryable();
        if (empresaId != null)
            sucursalesBase = sucursalesBase.Where(s => s.EmpresaId == empresaId);
        var sucursales = await sucursalesBase.OrderBy(s => s.Nombre).ToListAsync();
        var empleados = await ObtenerEmpleadosFiltroAsync(empresaId, sucursalId);

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
        if (empleadoId != null)
            baseQuery = baseQuery.Where(r => r.EmpleadoId == empleadoId);

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
            EmpleadoId = empleadoId,
            Empresas = empresas,
            Sucursales = sucursales,
            Empleados = empleados,
            TotalRespuestas = detalle.Sum(d => d.Cantidad),
            TotalCosto = detalle.Sum(d => d.TotalCosto),
            TotalPrecio = detalle.Sum(d => d.TotalPrecio),
            PorSucursal = porSucursal,
            PorEmpleado = detalle
        };
        return View("Seleccionados", vm);
    }

    [Authorize(Roles = "Admin,RRHH")]
    [HttpGet]
    public async Task<IActionResult> Distribucion(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, int? localizacionId = null, int? horarioId = null, DateOnly? desde = null, DateOnly? hasta = null, string? horaAlmuerzo = null)
    {
        if (empresaId <= 0) empresaId = null;
        if (sucursalId <= 0) sucursalId = null;
        if (empleadoId <= 0) empleadoId = null;
        if (localizacionId <= 0) localizacionId = null;
        if (horarioId <= 0) horarioId = null;
        TimeOnly? horaAlmuerzoValue = null;
        if (!string.IsNullOrWhiteSpace(horaAlmuerzo)
            && TimeOnly.TryParseExact(horaAlmuerzo, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var horaParsed))
        {
            horaAlmuerzoValue = horaParsed;
        }

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
        List<Sucursal> sucursales;
        if (empresaId != null)
        {
            sucursales = await _db.Sucursales
                .Where(s => s.EmpresaId == empresaId)
                .OrderBy(s => s.Nombre)
                .ToListAsync();
        }
        else
        {
            sucursales = await _db.Sucursales
                .OrderBy(s => s.Nombre)
                .ToListAsync();
        }
        var empleados = await ObtenerEmpleadosFiltroAsync(empresaId, sucursalId);
        var horarios = await ObtenerHorariosFiltroAsync(empresaId, sucursalId, inicio, fin);
        List<Localizacion> localizaciones;
        var localizacionesBase = _db.Localizaciones.AsNoTracking().AsQueryable();
        if (empresaId != null)
            localizacionesBase = localizacionesBase.Where(l => l.EmpresaId == empresaId);
        if (sucursalId != null)
            localizacionesBase = localizacionesBase.Where(l => l.SucursalId == sucursalId);
        var localizacionesRaw = await localizacionesBase
            .OrderBy(l => l.Nombre)
            .ToListAsync();

        if (sucursalId == null)
        {
            localizaciones = localizacionesRaw
                .GroupBy(l => NormalizeLocalizacionKey(l.Nombre))
                .Select(g => g.OrderBy(x => x.Id).First())
                .OrderBy(l => l.Nombre)
                .ToList();
        }
        else
        {
            localizaciones = localizacionesRaw
                .GroupBy(l => (l.EmpresaId, l.SucursalId, Key: NormalizeLocalizacionKey(l.Nombre)))
                .Select(g => g.OrderBy(x => x.Id).First())
                .OrderBy(l => l.Nombre)
                .ToList();
        }

        var localizacionNombreFiltro = localizacionId != null
            ? localizaciones.FirstOrDefault(l => l.Id == localizacionId)?.Nombre
            : null;

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
        if (empleadoId != null)
            baseQuery = baseQuery.Where(r => r.EmpleadoId == empleadoId);
        if (localizacionId != null)
        {
            if (sucursalId == null && !string.IsNullOrWhiteSpace(localizacionNombreFiltro))
                baseQuery = baseQuery.Where(r => r.LocalizacionEntrega != null && r.LocalizacionEntrega.Nombre == localizacionNombreFiltro);
            else
                baseQuery = baseQuery.Where(r => r.LocalizacionEntregaId == localizacionId);
        }
        if (horarioId != null)
            baseQuery = baseQuery.Where(r => r.OpcionMenu != null && r.OpcionMenu.HorarioId == horarioId);

        var horasAlmuerzoSet = new HashSet<TimeOnly>();
        if (horarioId != null && (empresaId != null || sucursalId != null))
        {
            var slotsQuery = _db.SucursalesHorariosSlots.AsNoTracking().AsQueryable();
            if (sucursalId != null)
            {
                slotsQuery = slotsQuery.Where(sh => sh.SucursalId == sucursalId && sh.HorarioId == horarioId);
            }
            else if (empresaId != null)
            {
                slotsQuery = slotsQuery
                    .Include(sh => sh.Sucursal)
                    .Where(sh => sh.Sucursal != null && sh.Sucursal.EmpresaId == empresaId && sh.HorarioId == horarioId);
            }

            var slots = await slotsQuery
                .Select(sh => sh.Hora)
                .Distinct()
                .ToListAsync();

            foreach (var hora in slots)
                horasAlmuerzoSet.Add(hora);
        }

        var horasHistoricas = await baseQuery
            .Where(r => r.HoraAlmuerzo != null)
            .Select(r => r.HoraAlmuerzo!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var hora in horasHistoricas)
            horasAlmuerzoSet.Add(hora);

        var horasAlmuerzo = horasAlmuerzoSet
            .OrderBy(h => h)
            .ToList();

        if (horaAlmuerzoValue != null)
            baseQuery = baseQuery.Where(r => r.HoraAlmuerzo == horaAlmuerzoValue);

        var respuestas = await baseQuery.ToListAsync();
        respuestas = respuestas
            .Where(r =>
            {
                if (r.OpcionMenu == null || r.OpcionMenu.Menu == null) return false;
                var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana);
                return fecha >= inicio && fecha <= fin;
            })
            .ToList();
        if (empleadoId != null)
        {
            respuestas = respuestas.Where(r => r.EmpleadoId == empleadoId.Value).ToList();
        }

        const decimal itbisRate = 0.18m;
        var items = new List<(DateOnly Fecha, int FilialId, string Filial, int EmpleadoId, string Empleado, string EmpleadoCodigo, string Tanda, string Opcion, string Seleccion, string Localizacion, decimal Base, decimal Itbis, decimal Total, decimal EmpresaPaga, decimal EmpleadoPaga, decimal ItbisEmpresa, decimal ItbisEmpleado, bool EsAdicional)>();

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
            var empleadoCodigo = r.Empleado.Codigo ?? string.Empty;
            var empleadoIdRow = r.Empleado.Id;
            var localizacion = r.LocalizacionEntrega?.Nombre ?? "Sin asignar";

            void AddItem(Opcion opcion, string nombre, string seleccionLabel, bool aplicaSubsidio, bool esAdicional)
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

                items.Add((fechaDia, filialId, filial, empleadoIdRow, empleadoNombre, empleadoCodigo, tanda, nombre, seleccionLabel, localizacion, basePrecio, itbis, total, empresaPaga, empleadoPaga, itbisEmpresa, itbisEmpleado, esAdicional));
            }

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
                AddItem(opcion, opcion.Nombre ?? "Sin definir", MapSeleccion(r.Seleccion), true, false);

            if (r.AdicionalOpcion != null)
                AddItem(r.AdicionalOpcion, r.AdicionalOpcion.Nombre ?? "Sin definir", "Adicional", false, true);
        }

        if (empleadoId != null)
        {
            items = items.Where(i => i.EmpleadoId == empleadoId.Value).ToList();
        }

        var resumen = items
            .GroupBy(i => new { i.Fecha, i.FilialId, i.Filial })
            .Select(g => new DistribucionVM.ResumenFilialRow
            {
                Fecha = g.Key.Fecha,
                FilialId = g.Key.FilialId,
                Filial = g.Key.Filial,
                Base = g.Sum(x => x.Base),
                Itbis = g.Sum(x => x.Itbis),
                Total = g.Sum(x => x.Total),
                ItbisEmpresa = g.Sum(x => x.ItbisEmpresa),
                ItbisEmpleado = g.Sum(x => x.ItbisEmpleado),
                MontoAdicional = g.Where(x => x.EsAdicional).Sum(x => x.Base),
                ItbisAdicional = g.Where(x => x.EsAdicional).Sum(x => x.Itbis),
                EmpresaPaga = g.Sum(x => x.EmpresaPaga),
                EmpleadoPaga = g.Sum(x => x.EmpleadoPaga)
            })
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Filial)
            .ToList();

        var detalle = items
            .Select(i => new DistribucionVM.DetalleEmpleadoRow
            {
                Fecha = i.Fecha,
                Filial = i.Filial,
                Localizacion = i.Localizacion,
                Empleado = i.Empleado,
                EmpleadoCodigo = i.EmpleadoCodigo,
                Tanda = i.Tanda,
                Opcion = i.Opcion,
                Seleccion = i.Seleccion,
                Base = i.Base,
                Itbis = i.Itbis,
                Total = i.Total,
                EmpresaPaga = i.EmpresaPaga,
                EmpleadoPaga = i.EmpleadoPaga,
                ItbisEmpresa = i.ItbisEmpresa,
                ItbisEmpleado = i.ItbisEmpleado
            })
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Filial)
            .ThenBy(r => r.Empleado)
            .ThenBy(r => r.Opcion)
            .ToList();

        var porLocalizacion = items
            .GroupBy(i => new { i.Fecha, i.Filial, i.Localizacion, i.Opcion, i.Seleccion })
            .Select(g => new DistribucionVM.DistribucionLocalizacionRow
            {
                Fecha = g.Key.Fecha,
                Filial = g.Key.Filial,
                Localizacion = g.Key.Localizacion,
                Opcion = g.Key.Opcion,
                Seleccion = g.Key.Seleccion,
                Cantidad = g.Count(),
                MontoTotal = g.Sum(x => x.Total),
                EmpresaPaga = g.Sum(x => x.EmpresaPaga),
                EmpleadoPaga = g.Sum(x => x.EmpleadoPaga)
            })
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Filial)
            .ThenBy(r => r.Localizacion)
            .ThenBy(r => r.Opcion)
            .ToList();

        var porLocalizacionCocina = items
            .GroupBy(i => new { i.Fecha, i.Filial, i.Localizacion })
            .Select(g =>
            {
                var row = new DistribucionVM.DistribucionCocinaRow
                {
                    Fecha = g.Key.Fecha,
                    Filial = g.Key.Filial,
                    Localizacion = g.Key.Localizacion
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

                return row;
            })
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Filial)
            .ThenBy(r => r.Localizacion)
            .ToList();

        var porLocalizacionCocinaDetalle = items
            .Select(i => new DistribucionVM.DistribucionCocinaDetalleRow
            {
                Fecha = i.Fecha,
                Filial = i.Filial,
                Localizacion = i.Localizacion,
                EmpleadoCodigo = string.IsNullOrWhiteSpace(i.EmpleadoCodigo) ? i.Empleado : i.EmpleadoCodigo,
                EmpleadoNombre = i.Empleado,
                Seleccion = i.Seleccion,
                Opcion = i.Opcion
            })
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Filial)
            .ThenBy(r => r.Localizacion)
            .ThenBy(r => r.EmpleadoCodigo)
            .ThenBy(r => r.Seleccion)
            .ThenBy(r => r.Opcion)
            .ToList();

        var vm = new DistribucionVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            EmpleadoId = empleadoId,
            LocalizacionId = localizacionId,
            HorarioId = horarioId,
            HoraAlmuerzo = horaAlmuerzoValue?.ToString("HH:mm"),
            Empresas = empresas,
            Sucursales = sucursales,
            Empleados = empleados,
            Localizaciones = localizaciones,
            Horarios = horarios,
            HorasAlmuerzo = horasAlmuerzo.Select(h => h.ToString("HH:mm")).ToList(),
            ResumenFiliales = resumen,
            DetalleEmpleados = detalle,
            PorLocalizacion = porLocalizacion,
            PorLocalizacionCocina = porLocalizacionCocina,
            PorLocalizacionCocinaDetalle = porLocalizacionCocinaDetalle
        };
        return View(vm);
    }

    private static bool TryParseDateOnly(string input, out DateOnly date)
    {
        var formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy" };
        return DateOnly.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParseExact(input, formats, new CultureInfo("es-DO"), DateTimeStyles.None, out date)
            || DateOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParse(input, new CultureInfo("es-DO"), DateTimeStyles.None, out date);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleadosCsv(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var result = await TotalesEmpleados(empresaId, sucursalId, empleadoId) as ViewResult;
        var vm = (TotalesEmpleadosVM)result!.Model!;
        var isAdmin = User.IsInRole("Admin");

        var headers = new List<string> { "Empleado", "Selecciones", "Costo", "ITBIS" };
        if (isAdmin)
        {
            headers.Add("Consumo");
            headers.Add("Beneficio");
        }

        var rows = vm.Filas.Select(r =>
        {
            var row = new List<string>
            {
                r.Empleado,
                r.Cantidad.ToString(),
                r.TotalCosto.ToString("C"),
                r.TotalItbis.ToString("C")
            };
            if (isAdmin)
            {
                row.Add(r.TotalPrecio.ToString("C"));
                row.Add(r.TotalBeneficio.ToString("C"));
            }
            return (IReadOnlyList<string>)row;
        }).ToList();

        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", $"totales-empleados-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleadosExcel(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var result = await TotalesEmpleados(empresaId, sucursalId, empleadoId) as ViewResult;
        var vm = (TotalesEmpleadosVM)result!.Model!;
        var isAdmin = User.IsInRole("Admin");

        var headers = new List<string> { "Empleado", "Selecciones", "Costo", "ITBIS" };
        if (isAdmin)
        {
            headers.Add("Consumo");
            headers.Add("Beneficio");
        }

        var rows = vm.Filas.Select(r =>
        {
            var row = new List<string>
            {
                r.Empleado,
                r.Cantidad.ToString(),
                r.TotalCosto.ToString("C"),
                r.TotalItbis.ToString("C")
            };
            if (isAdmin)
            {
                row.Add(r.TotalPrecio.ToString("C"));
                row.Add(r.TotalBeneficio.ToString("C"));
            }
            return (IReadOnlyList<string>)row;
        }).ToList();

        var bytes = ExportHelper.BuildExcel("Totales empleados", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"totales-empleados-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> TotalesEmpleadosPdf(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var result = await TotalesEmpleados(empresaId, sucursalId, empleadoId) as ViewResult;
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
                        h.Cell().AlignRight().Text("ITBIS").SemiBold();
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
                        t.Cell().AlignRight().Text(r.TotalItbis.ToString("C"));
                        if (isAdmin)
                        {
                            t.Cell().AlignRight().Text(r.TotalPrecio.ToString("C"));
                            t.Cell().AlignRight().Text(r.TotalBeneficio.ToString("C"));
                        }
                    }
                    t.Cell().Text("Total").SemiBold();
                    t.Cell().AlignRight().Text(vm.Filas.Sum(x => x.Cantidad).ToString()).SemiBold();
                    t.Cell().AlignRight().Text(vm.TotalCosto.ToString("C")).SemiBold();
                    t.Cell().AlignRight().Text(vm.TotalItbis.ToString("C")).SemiBold();
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
    public async Task<IActionResult> ItemsSemanaCsv(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var result = await ItemsSemana(empresaId, sucursalId, empleadoId) as ViewResult;
        var vm = (ItemsSemanaVM)result!.Model!;

        var headers = new[] { "Item", "Costo unitario", "Precio unitario", "Beneficio unitario", "Cantidad", "Total costo", "Total precio", "Beneficio total" };
        var rows = vm.Items.Select(r => (IReadOnlyList<string>)new[]
        {
            r.Nombre,
            r.CostoUnitario.ToString("C"),
            r.PrecioUnitario.ToString("C"),
            r.BeneficioUnitario.ToString("C"),
            r.Cantidad.ToString(),
            r.TotalCosto.ToString("C"),
            r.TotalPrecio.ToString("C"),
            r.TotalBeneficio.ToString("C")
        }).ToList();

        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", $"items-semana-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ItemsSemanaExcel(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var result = await ItemsSemana(empresaId, sucursalId, empleadoId) as ViewResult;
        var vm = (ItemsSemanaVM)result!.Model!;

        var headers = new[] { "Item", "Costo unitario", "Precio unitario", "Beneficio unitario", "Cantidad", "Total costo", "Total precio", "Beneficio total" };
        var rows = vm.Items.Select(r => (IReadOnlyList<string>)new[]
        {
            r.Nombre,
            r.CostoUnitario.ToString("C"),
            r.PrecioUnitario.ToString("C"),
            r.BeneficioUnitario.ToString("C"),
            r.Cantidad.ToString(),
            r.TotalCosto.ToString("C"),
            r.TotalPrecio.ToString("C"),
            r.TotalBeneficio.ToString("C")
        }).ToList();

        var bytes = ExportHelper.BuildExcel("Items semana", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"items-semana-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ItemsSemanaPdf(int? empresaId = null, int? sucursalId = null, int? empleadoId = null)
    {
        var result = await ItemsSemana(empresaId, sucursalId, empleadoId) as ViewResult;
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

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> SeleccionesCsv(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await Selecciones(empresaId, sucursalId, empleadoId, desde, hasta) as ViewResult;
        var vm = (SeleccionesVM)result!.Model!;
        var isEmpleado = User.IsInRole("Empleado");
        var isAdmin = User.IsInRole("Admin");

        IReadOnlyList<string> headers;
        List<IReadOnlyList<string>> rows;
        if (isEmpleado)
        {
            headers = new[] { "Fecha", "Dia", "Opcion", "Precio", "Localizacion" };
            rows = vm.SeleccionesEmpleado.Select(r => (IReadOnlyList<string>)new[]
            {
                r.Fecha.ToString("yyyy-MM-dd"),
                r.DiaNombre,
                r.OpcionNombre,
                r.Precio.ToString("C"),
                r.Localizacion
            }).ToList();
        }
        else
        {
            headers = isAdmin
                ? new[] { "Empleado", "Filial", "Selecciones", "Costo", "Consumo", "Beneficio" }
                : new[] { "Empleado", "Filial", "Selecciones", "Costo" };
            rows = vm.PorEmpleado.Select(r =>
            {
                var row = new List<string> { r.Empleado, r.Sucursal, r.Cantidad.ToString(), r.TotalCosto.ToString("C") };
                if (isAdmin)
                {
                    row.Add(r.TotalPrecio.ToString("C"));
                    row.Add(r.TotalBeneficio.ToString("C"));
                }
                return (IReadOnlyList<string>)row;
            }).ToList();
        }

        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", $"selecciones-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> SeleccionesExcel(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await Selecciones(empresaId, sucursalId, empleadoId, desde, hasta) as ViewResult;
        var vm = (SeleccionesVM)result!.Model!;
        var isEmpleado = User.IsInRole("Empleado");
        var isAdmin = User.IsInRole("Admin");

        IReadOnlyList<string> headers;
        List<IReadOnlyList<string>> rows;
        if (isEmpleado)
        {
            headers = new[] { "Fecha", "Dia", "Opcion", "Precio", "Localizacion" };
            rows = vm.SeleccionesEmpleado.Select(r => (IReadOnlyList<string>)new[]
            {
                r.Fecha.ToString("yyyy-MM-dd"),
                r.DiaNombre,
                r.OpcionNombre,
                r.Precio.ToString("C"),
                r.Localizacion
            }).ToList();
        }
        else
        {
            headers = isAdmin
                ? new[] { "Empleado", "Filial", "Selecciones", "Costo", "Consumo", "Beneficio" }
                : new[] { "Empleado", "Filial", "Selecciones", "Costo" };
            rows = vm.PorEmpleado.Select(r =>
            {
                var row = new List<string> { r.Empleado, r.Sucursal, r.Cantidad.ToString(), r.TotalCosto.ToString("C") };
                if (isAdmin)
                {
                    row.Add(r.TotalPrecio.ToString("C"));
                    row.Add(r.TotalBeneficio.ToString("C"));
                }
                return (IReadOnlyList<string>)row;
            }).ToList();
        }

        var bytes = ExportHelper.BuildExcel("Selecciones", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"selecciones-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> SeleccionesPdf(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await Selecciones(empresaId, sucursalId, empleadoId, desde, hasta) as ViewResult;
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

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> SeleccionadosCsv(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await Seleccionados(empresaId, sucursalId, empleadoId, desde, hasta) as ViewResult;
        var vm = (SeleccionesVM)result!.Model!;
        var isEmpleado = User.IsInRole("Empleado");
        var isAdmin = User.IsInRole("Admin");

        IReadOnlyList<string> headers;
        List<IReadOnlyList<string>> rows;
        if (isEmpleado)
        {
            headers = new[] { "Fecha", "Dia", "Opcion", "Precio", "Localizacion" };
            rows = vm.SeleccionesEmpleado.Select(r => (IReadOnlyList<string>)new[]
            {
                r.Fecha.ToString("yyyy-MM-dd"),
                r.DiaNombre,
                r.OpcionNombre,
                r.Precio.ToString("C"),
                r.Localizacion
            }).ToList();
        }
        else
        {
            headers = isAdmin
                ? new[] { "Empleado", "Filial", "Selecciones", "Costo", "Consumo", "Beneficio" }
                : new[] { "Empleado", "Filial", "Selecciones", "Costo" };
            rows = vm.PorEmpleado.Select(r =>
            {
                var row = new List<string> { r.Empleado, r.Sucursal, r.Cantidad.ToString(), r.TotalCosto.ToString("C") };
                if (isAdmin)
                {
                    row.Add(r.TotalPrecio.ToString("C"));
                    row.Add(r.TotalBeneficio.ToString("C"));
                }
                return (IReadOnlyList<string>)row;
            }).ToList();
        }

        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", $"seleccionados-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> SeleccionadosExcel(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await Seleccionados(empresaId, sucursalId, empleadoId, desde, hasta) as ViewResult;
        var vm = (SeleccionesVM)result!.Model!;
        var isEmpleado = User.IsInRole("Empleado");
        var isAdmin = User.IsInRole("Admin");

        IReadOnlyList<string> headers;
        List<IReadOnlyList<string>> rows;
        if (isEmpleado)
        {
            headers = new[] { "Fecha", "Dia", "Opcion", "Precio", "Localizacion" };
            rows = vm.SeleccionesEmpleado.Select(r => (IReadOnlyList<string>)new[]
            {
                r.Fecha.ToString("yyyy-MM-dd"),
                r.DiaNombre,
                r.OpcionNombre,
                r.Precio.ToString("C"),
                r.Localizacion
            }).ToList();
        }
        else
        {
            headers = isAdmin
                ? new[] { "Empleado", "Filial", "Selecciones", "Costo", "Consumo", "Beneficio" }
                : new[] { "Empleado", "Filial", "Selecciones", "Costo" };
            rows = vm.PorEmpleado.Select(r =>
            {
                var row = new List<string> { r.Empleado, r.Sucursal, r.Cantidad.ToString(), r.TotalCosto.ToString("C") };
                if (isAdmin)
                {
                    row.Add(r.TotalPrecio.ToString("C"));
                    row.Add(r.TotalBeneficio.ToString("C"));
                }
                return (IReadOnlyList<string>)row;
            }).ToList();
        }

        var bytes = ExportHelper.BuildExcel("Seleccionados", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"seleccionados-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin,Empleado")]
    [HttpGet]
    public async Task<IActionResult> SeleccionadosPdf(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await Seleccionados(empresaId, sucursalId, empleadoId, desde, hasta) as ViewResult;
        var vm = (SeleccionesVM)result!.Model!;
        var isEmpleado = User.IsInRole("Empleado");
        var isAdmin = User.IsInRole("Admin");

        IReadOnlyList<string> headers;
        List<IReadOnlyList<string>> rows;
        if (isEmpleado)
        {
            headers = new[] { "Fecha", "Dia", "Opcion", "Precio", "Localizacion" };
            rows = vm.SeleccionesEmpleado.Select(r => (IReadOnlyList<string>)new[]
            {
                r.Fecha.ToString("yyyy-MM-dd"),
                r.DiaNombre,
                r.OpcionNombre,
                r.Precio.ToString("C"),
                r.Localizacion
            }).ToList();
        }
        else
        {
            headers = isAdmin
                ? new[] { "Empleado", "Filial", "Selecciones", "Costo", "Consumo", "Beneficio" }
                : new[] { "Empleado", "Filial", "Selecciones", "Costo" };
            rows = vm.PorEmpleado.Select(r =>
            {
                var row = new List<string> { r.Empleado, r.Sucursal, r.Cantidad.ToString(), r.TotalCosto.ToString("C") };
                if (isAdmin)
                {
                    row.Add(r.TotalPrecio.ToString("C"));
                    row.Add(r.TotalBeneficio.ToString("C"));
                }
                return (IReadOnlyList<string>)row;
            }).ToList();
        }

        var pdf = ExportHelper.BuildPdf($"Seleccionados {vm.Inicio:yyyy-MM-dd} a {vm.Fin:yyyy-MM-dd}", headers, rows);
        return File(pdf, "application/pdf", $"seleccionados-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Admin,RRHH")]
    [HttpGet]
    public async Task<IActionResult> DistribucionCsv(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, int? localizacionId = null, int? horarioId = null, DateOnly? desde = null, DateOnly? hasta = null, string? horaAlmuerzo = null, string? vista = null)
    {
        var result = await Distribucion(empresaId, sucursalId, empleadoId, localizacionId, horarioId, desde, hasta, horaAlmuerzo) as ViewResult;
        var vm = (DistribucionVM)result!.Model!;
        var export = BuildDistribucionExport(vm, vista);
        var bytes = ExportHelper.BuildCsv(export.Headers, export.Rows);
        return File(bytes, "text/csv", $"distribucion-{export.Suffix}-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin,RRHH")]
    [HttpGet]
    public async Task<IActionResult> DistribucionExcel(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, int? localizacionId = null, int? horarioId = null, DateOnly? desde = null, DateOnly? hasta = null, string? horaAlmuerzo = null, string? vista = null)
    {
        var result = await Distribucion(empresaId, sucursalId, empleadoId, localizacionId, horarioId, desde, hasta, horaAlmuerzo) as ViewResult;
        var vm = (DistribucionVM)result!.Model!;
        var export = BuildDistribucionExport(vm, vista);
        var bytes = ExportHelper.BuildExcel("Distribucion", export.Headers, export.Rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"distribucion-{export.Suffix}-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin,RRHH")]
    [HttpGet]
    public async Task<IActionResult> DistribucionPdf(int? empresaId = null, int? sucursalId = null, int? empleadoId = null, int? localizacionId = null, int? horarioId = null, DateOnly? desde = null, DateOnly? hasta = null, string? horaAlmuerzo = null, string? vista = null)
    {
        var result = await Distribucion(empresaId, sucursalId, empleadoId, localizacionId, horarioId, desde, hasta, horaAlmuerzo) as ViewResult;
        var vm = (DistribucionVM)result!.Model!;

        var export = BuildDistribucionExport(vm, vista);
        var title = $"{export.Title} {vm.Inicio:yyyy-MM-dd} a {vm.Fin:yyyy-MM-dd}";
        var pdf = ExportHelper.BuildPdf(title, export.Headers, export.Rows);
        return File(pdf, "application/pdf", $"distribucion-{export.Suffix}-{vm.Inicio:yyyyMMdd}-{vm.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ReporteMaestro(DateOnly? desde = null, DateOnly? hasta = null, int? empresaId = null, int? sucursalId = null, int? localizacionId = null, int? empleadoId = null, int? horarioId = null, string? horaAlmuerzo = null, string? tipo = null, string? estado = null, int page = 1, int pageSize = 50)
    {
        if (empresaId <= 0) empresaId = null;
        if (sucursalId <= 0) sucursalId = null;
        if (localizacionId <= 0) localizacionId = null;
        if (empleadoId <= 0) empleadoId = null;
        if (horarioId <= 0) horarioId = null;
        tipo = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim().ToLowerInvariant();
        estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToLowerInvariant();
        TimeOnly? horaAlmuerzoValue = null;
        if (!string.IsNullOrWhiteSpace(horaAlmuerzo)
            && TimeOnly.TryParseExact(horaAlmuerzo, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var horaParsed))
        {
            horaAlmuerzoValue = horaParsed;
        }

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
        List<Sucursal> sucursales;
        if (empresaId != null)
        {
            sucursales = await _db.Sucursales
                .Where(s => s.EmpresaId == empresaId)
                .OrderBy(s => s.Nombre)
                .ToListAsync();
        }
        else
        {
            sucursales = await _db.Sucursales
                .OrderBy(s => s.Nombre)
                .ToListAsync();
        }
        var empleados = await ObtenerEmpleadosFiltroAsync(empresaId, sucursalId);
        var horarios = await ObtenerHorariosFiltroAsync(empresaId, sucursalId, inicio, fin);

        var localizacionesRaw = await BuildLocalizacionesFiltroAsync(empresaId, sucursalId);
        List<Localizacion> localizaciones;
        if (sucursalId == null)
        {
            localizaciones = localizacionesRaw
                .GroupBy(l => l.Nombre, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(x => x.Id).First())
                .OrderBy(l => l.Nombre)
                .ToList();
        }
        else
        {
            localizaciones = localizacionesRaw
                .GroupBy(l => (l.EmpresaId, l.SucursalId, l.Nombre))
                .Select(g => g.OrderBy(x => x.Id).First())
                .OrderBy(l => l.Nombre)
                .ToList();
        }

        var localizacionNombreFiltro = localizacionId != null
            ? localizaciones.FirstOrDefault(l => l.Id == localizacionId)?.Nombre
            : null;

        var horasAlmuerzoSet = new HashSet<TimeOnly>();
        if (horarioId != null && (empresaId != null || sucursalId != null))
        {
            var slotsQuery = _db.SucursalesHorariosSlots.AsNoTracking().AsQueryable();
            if (sucursalId != null)
            {
                slotsQuery = slotsQuery.Where(sh => sh.SucursalId == sucursalId && sh.HorarioId == horarioId);
            }
            else if (empresaId != null)
            {
                slotsQuery = slotsQuery
                    .Include(sh => sh.Sucursal)
                    .Where(sh => sh.Sucursal != null && sh.Sucursal.EmpresaId == empresaId && sh.HorarioId == horarioId);
            }

            var slots = await slotsQuery
                .Select(sh => sh.Hora)
                .Distinct()
                .ToListAsync();

            foreach (var hora in slots)
                horasAlmuerzoSet.Add(hora);
        }

        var horasHistoricasQuery = _db.RespuestasFormulario
            .AsNoTracking()
            .Where(r => r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio <= fin
                && r.OpcionMenu.Menu.FechaTermino >= inicio);
        horasHistoricasQuery = AplicarFiltrosEmpresaSucursal(horasHistoricasQuery, empresaId, sucursalId);
        if (empleadoId != null)
            horasHistoricasQuery = horasHistoricasQuery.Where(r => r.EmpleadoId == empleadoId);
        if (localizacionId != null)
        {
            if (sucursalId == null && !string.IsNullOrWhiteSpace(localizacionNombreFiltro))
                horasHistoricasQuery = horasHistoricasQuery.Where(r => r.LocalizacionEntrega != null && r.LocalizacionEntrega.Nombre == localizacionNombreFiltro);
            else
                horasHistoricasQuery = horasHistoricasQuery.Where(r => r.LocalizacionEntregaId == localizacionId);
        }
        if (!string.IsNullOrWhiteSpace(estado))
        {
            horasHistoricasQuery = estado switch
            {
                "abierto" => horasHistoricasQuery.Where(r => !r.CierreNomina),
                "cerrado" => horasHistoricasQuery.Where(r => r.CierreNomina),
                _ => horasHistoricasQuery
            };
        }
        if (horarioId != null)
            horasHistoricasQuery = horasHistoricasQuery.Where(r => r.OpcionMenu != null && r.OpcionMenu.HorarioId == horarioId);

        var horasHistoricas = await horasHistoricasQuery
            .Where(r => r.HoraAlmuerzo != null)
            .Select(r => r.HoraAlmuerzo!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var hora in horasHistoricas)
            horasAlmuerzoSet.Add(hora);

        var horasAlmuerzo = horasAlmuerzoSet
            .OrderBy(h => h)
            .Select(h => h.ToString("HH:mm"))
            .ToList();

        var rows = await BuildReporteMaestroRowsAsync(inicio, fin, empresaId, sucursalId, localizacionId, empleadoId, horarioId, horaAlmuerzoValue, tipo, estado, localizaciones);
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 50;
        var total = rows.Count;
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var vm = new ReporteMaestroVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            LocalizacionId = localizacionId,
            EmpleadoId = empleadoId,
            HorarioId = horarioId,
            HoraAlmuerzo = horaAlmuerzoValue?.ToString("HH:mm"),
            Tipo = tipo,
            Estado = estado,
            Empresas = empresas,
            Sucursales = sucursales,
            Localizaciones = localizaciones,
            Empleados = empleados,
            Horarios = horarios,
            HorasAlmuerzo = horasAlmuerzo,
            Paginado = new PagedResult<ReporteMaestroVM.Row>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            }
        };

        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ReporteMaestroCsv(DateOnly? desde = null, DateOnly? hasta = null, int? empresaId = null, int? sucursalId = null, int? localizacionId = null, int? empleadoId = null, int? horarioId = null, string? horaAlmuerzo = null, string? tipo = null, string? estado = null)
    {
        var vm = await ReporteMaestro(desde, hasta, empresaId, sucursalId, localizacionId, empleadoId, horarioId, horaAlmuerzo, tipo, estado, 1, int.MaxValue) as ViewResult;
        var model = (ReporteMaestroVM)vm!.Model!;
        var export = BuildReporteMaestroExport(model.Paginado.Items);
        var bytes = ExportHelper.BuildCsv(export.Headers, export.Rows);
        return File(bytes, "text/csv", $"reporte-maestro-{model.Inicio:yyyyMMdd}-{model.Fin:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ReporteMaestroExcel(DateOnly? desde = null, DateOnly? hasta = null, int? empresaId = null, int? sucursalId = null, int? localizacionId = null, int? empleadoId = null, int? horarioId = null, string? horaAlmuerzo = null, string? tipo = null, string? estado = null)
    {
        var vm = await ReporteMaestro(desde, hasta, empresaId, sucursalId, localizacionId, empleadoId, horarioId, horaAlmuerzo, tipo, estado, 1, int.MaxValue) as ViewResult;
        var model = (ReporteMaestroVM)vm!.Model!;
        var export = BuildReporteMaestroExport(model.Paginado.Items);
        var bytes = ExportHelper.BuildExcel("Reporte Maestro", export.Headers, export.Rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"reporte-maestro-{model.Inicio:yyyyMMdd}-{model.Fin:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ReporteMaestroPdf(DateOnly? desde = null, DateOnly? hasta = null, int? empresaId = null, int? sucursalId = null, int? localizacionId = null, int? empleadoId = null, int? horarioId = null, string? horaAlmuerzo = null, string? tipo = null, string? estado = null)
    {
        var vm = await ReporteMaestro(desde, hasta, empresaId, sucursalId, localizacionId, empleadoId, horarioId, horaAlmuerzo, tipo, estado, 1, int.MaxValue) as ViewResult;
        var model = (ReporteMaestroVM)vm!.Model!;
        var export = BuildReporteMaestroExport(model.Paginado.Items);
        var pdf = ExportHelper.BuildPdf($"Reporte Maestro {model.Inicio:yyyy-MM-dd} a {model.Fin:yyyy-MM-dd}", export.Headers, export.Rows);
        return File(pdf, "application/pdf", $"reporte-maestro-{model.Inicio:yyyyMMdd}-{model.Fin:yyyyMMdd}.pdf");
    }

    [Authorize(Roles = "Empleado")]
    [HttpGet]
    public async Task<IActionResult> EstadoCuentaCsv(DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await EstadoCuenta(desde, hasta) as ViewResult;
        var vm = (EstadoCuentaEmpleadoVM)result!.Model!;

        var headers = new[] { "Fecha", "Dia", "Horario", "Seleccion", "Opcion", "Base", "ITBIS", "Total" };
        var rows = vm.Movimientos.Select(r => (IReadOnlyList<string>)new[]
        {
            r.Fecha.ToString("yyyy-MM-dd"),
            r.DiaNombre,
            r.Horario ?? string.Empty,
            r.Seleccion,
            r.OpcionNombre ?? string.Empty,
            r.BaseEmpleado.ToString("C"),
            r.ItbisEmpleado.ToString("C"),
            r.TotalEmpleado.ToString("C")
        }).ToList();

        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", $"estado-cuenta-{vm.Desde:yyyyMMdd}-{vm.Hasta:yyyyMMdd}.csv");
    }

    [Authorize(Roles = "Empleado")]
    [HttpGet]
    public async Task<IActionResult> EstadoCuentaExcel(DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await EstadoCuenta(desde, hasta) as ViewResult;
        var vm = (EstadoCuentaEmpleadoVM)result!.Model!;

        var headers = new[] { "Fecha", "Dia", "Horario", "Seleccion", "Opcion", "Base", "ITBIS", "Total" };
        var rows = vm.Movimientos.Select(r => (IReadOnlyList<string>)new[]
        {
            r.Fecha.ToString("yyyy-MM-dd"),
            r.DiaNombre,
            r.Horario ?? string.Empty,
            r.Seleccion,
            r.OpcionNombre ?? string.Empty,
            r.BaseEmpleado.ToString("C"),
            r.ItbisEmpleado.ToString("C"),
            r.TotalEmpleado.ToString("C")
        }).ToList();

        var bytes = ExportHelper.BuildExcel("Estado cuenta", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"estado-cuenta-{vm.Desde:yyyyMMdd}-{vm.Hasta:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Empleado")]
    [HttpGet]
    public async Task<IActionResult> EstadoCuentaPdf(DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (User.IsInRole("Empleado")) return Forbid();
        var result = await EstadoCuenta(desde, hasta) as ViewResult;
        var vm = (EstadoCuentaEmpleadoVM)result!.Model!;

        var headers = new[] { "Fecha", "Dia", "Horario", "Seleccion", "Opcion", "Base", "ITBIS", "Total" };
        var rows = vm.Movimientos.Select(r => (IReadOnlyList<string>)new[]
        {
            r.Fecha.ToString("yyyy-MM-dd"),
            r.DiaNombre,
            r.Horario ?? string.Empty,
            r.Seleccion,
            r.OpcionNombre ?? string.Empty,
            r.BaseEmpleado.ToString("C"),
            r.ItbisEmpleado.ToString("C"),
            r.TotalEmpleado.ToString("C")
        }).ToList();

        var pdf = ExportHelper.BuildPdf($"Estado de cuenta {vm.Desde:yyyy-MM-dd} a {vm.Hasta:yyyy-MM-dd}", headers, rows);
        return File(pdf, "application/pdf", $"estado-cuenta-{vm.Desde:yyyyMMdd}-{vm.Hasta:yyyyMMdd}.pdf");
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


private IQueryable<RespuestaFormulario> AplicarFiltrosEmpresaSucursales(IQueryable<RespuestaFormulario> query, int? empresaId, IReadOnlyCollection<int> sucursalIds)
{
    if (empresaId != null)
    {
        query = query.Where(r =>
            (r.SucursalEntrega != null && r.SucursalEntrega.EmpresaId == empresaId) ||
            (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.Sucursal != null && r.Empleado.Sucursal.EmpresaId == empresaId));
    }

    if (sucursalIds.Count > 0)
    {
        query = query.Where(r =>
            (r.SucursalEntrega != null && sucursalIds.Contains(r.SucursalEntregaId)) ||
            (r.SucursalEntrega == null && r.Empleado != null && sucursalIds.Contains(r.Empleado.SucursalId)));
    }

    return query;
}

    private async Task<List<Empleado>> ObtenerEmpleadosFiltroAsync(int? empresaId, int? sucursalId)
    {
        var query = _db.Empleados
            .Include(e => e.Sucursal)
            .Where(e => !e.Borrado)
            .AsQueryable();
        if (empresaId != null)
            query = query.Where(e => e.Sucursal != null && e.Sucursal.EmpresaId == empresaId);
        if (sucursalId != null)
            query = query.Where(e => e.SucursalId == sucursalId);
        return await query.OrderBy(e => e.Nombre ?? e.Codigo).ToListAsync();
    }

    private async Task<List<Horario>> ObtenerHorariosFiltroAsync(int? empresaId, int? sucursalId, DateOnly? inicio, DateOnly? fin)
    {
        var ids = new HashSet<int>();

        if (sucursalId != null)
        {
            var configurados = await _db.SucursalesHorarios
                .Where(sh => sh.SucursalId == sucursalId)
                .Select(sh => sh.HorarioId)
                .Distinct()
                .ToListAsync();
            if (configurados.Count == 0)
            {
                configurados = await _db.SucursalesHorariosSlots
                    .Where(sh => sh.SucursalId == sucursalId)
                    .Select(sh => sh.HorarioId)
                    .Distinct()
                    .ToListAsync();
            }
            foreach (var id in configurados)
                ids.Add(id);
        }
        else if (empresaId != null)
        {
            var configurados = await _db.SucursalesHorarios
                .Include(sh => sh.Sucursal)
                .Where(sh => sh.Sucursal != null && sh.Sucursal.EmpresaId == empresaId)
                .Select(sh => sh.HorarioId)
                .Distinct()
                .ToListAsync();
            if (configurados.Count == 0)
            {
                configurados = await _db.SucursalesHorariosSlots
                    .Include(sh => sh.Sucursal)
                    .Where(sh => sh.Sucursal != null && sh.Sucursal.EmpresaId == empresaId)
                    .Select(sh => sh.HorarioId)
                    .Distinct()
                    .ToListAsync();
            }
            foreach (var id in configurados)
                ids.Add(id);
        }

        if (inicio != null && fin != null && (empresaId != null || sucursalId != null))
        {
            var inicioValue = inicio.Value;
            var finValue = fin.Value;
            var historicoQuery = _db.RespuestasFormulario
                .AsNoTracking()
                .Where(r => r.OpcionMenu != null
                    && r.OpcionMenu.HorarioId != null
                    && r.OpcionMenu.Menu != null
                    && r.OpcionMenu.Menu.FechaInicio <= finValue
                    && r.OpcionMenu.Menu.FechaTermino >= inicioValue);

            historicoQuery = AplicarFiltrosEmpresaSucursal(historicoQuery, empresaId, sucursalId);

            var historicos = await historicoQuery
                .Select(r => r.OpcionMenu!.HorarioId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var id in historicos)
                ids.Add(id);
        }

        if (ids.Count == 0)
        {
            return await _db.Horarios
                .Where(h => h.Activo)
                .OrderBy(h => h.Orden)
                .ThenBy(h => h.Nombre)
                .ToListAsync();
        }

        return await _db.Horarios
            .Where(h => ids.Contains(h.Id))
            .OrderBy(h => h.Orden)
            .ThenBy(h => h.Nombre)
            .ToListAsync();
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

    private (decimal Base, decimal Itbis, decimal Total, decimal EmpresaPaga, decimal EmpleadoPaga, decimal ItbisEmpresa, decimal ItbisEmpleado)
        CalcularMontos(Opcion opcion, Empleado empleado, Sucursal sucursal, Empresa empresa, bool aplicaSubsidio)
    {
        const decimal itbisRate = 0.18m;
        var basePrecio = opcion.Precio ?? opcion.Costo;
        if (basePrecio < 0) basePrecio = 0;
        var itbis = opcion.LlevaItbis ? Math.Round(basePrecio * itbisRate, 2) : 0m;
        decimal precioEmpleado = basePrecio;
        if (aplicaSubsidio)
        {
            var ctx = BuildSubsidioContext(opcion.EsSubsidiado, empleado, sucursal, empresa);
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

        return (basePrecio, itbis, total, empresaPaga, empleadoPaga, itbisEmpresa, itbisEmpleado);
    }

    private void EnsureSnapshots(IEnumerable<RespuestaFormulario> respuestas)
    {
        foreach (var r in respuestas)
        {
            if (r.OpcionMenu == null || r.Empleado == null) continue;
            var sucursalEmpleado = r.Empleado.Sucursal;
            var empresaEmpleado = sucursalEmpleado?.Empresa;
            if (sucursalEmpleado == null || empresaEmpleado == null) continue;

            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null && r.BaseSnapshot == null)
            {
                var montos = CalcularMontos(opcion, r.Empleado, sucursalEmpleado, empresaEmpleado, true);
                r.BaseSnapshot = montos.Base;
                r.ItbisSnapshot = montos.Itbis;
                r.TotalSnapshot = montos.Total;
                r.EmpresaPagaSnapshot = montos.EmpresaPaga;
                r.EmpleadoPagaSnapshot = montos.EmpleadoPaga;
                r.ItbisEmpresaSnapshot = montos.ItbisEmpresa;
                r.ItbisEmpleadoSnapshot = montos.ItbisEmpleado;
            }

            if (r.AdicionalOpcion != null && r.AdicionalBaseSnapshot == null)
            {
                var montosAdicional = CalcularMontos(r.AdicionalOpcion, r.Empleado, sucursalEmpleado, empresaEmpleado, false);
                r.AdicionalBaseSnapshot = montosAdicional.Base;
                r.AdicionalItbisSnapshot = montosAdicional.Itbis;
                r.AdicionalTotalSnapshot = montosAdicional.Total;
                r.AdicionalEmpresaPagaSnapshot = montosAdicional.EmpresaPaga;
                r.AdicionalEmpleadoPagaSnapshot = montosAdicional.EmpleadoPaga;
                r.AdicionalItbisEmpresaSnapshot = montosAdicional.ItbisEmpresa;
                r.AdicionalItbisEmpleadoSnapshot = montosAdicional.ItbisEmpleado;
            }
        }
    }

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

    

private sealed record CierreItem(
    string Empresa,
    int FilialId,
    string Filial,
    int EmpleadoId,
    string Empleado,
    string EmpleadoCodigo,
    DateOnly Fecha,
    string Tanda,
    string Localizacion,
    string Opcion,
    string Seleccion,
    decimal Base,
    decimal Itbis,
    decimal Total,
    decimal EmpresaPaga,
    decimal ItbisEmpresa,
    decimal EmpleadoPaga,
    decimal ItbisEmpleado,
    bool CierreNomina,
    DateTime? FechaCierreNomina,
    bool Facturado,
    DateTime? FechaFacturado,
    string? NumeroFactura);

private sealed class CierreFacturacionData
{
    public CierreFacturacionVM Vm { get; init; } = new();
    public List<CierreItem> Items { get; init; } = new();
    public List<RespuestaFormulario> Respuestas { get; init; } = new();
}

private (DateOnly Inicio, DateOnly Fin) ResolveRango(DateOnly? desde, DateOnly? hasta)
{
    if (desde.HasValue && hasta.HasValue)
    {
        var inicio = desde.Value;
        var fin = hasta.Value;
        if (fin < inicio)
            (inicio, fin) = (fin, inicio);
        return (inicio, fin);
    }

    if (desde.HasValue)
    {
        var inicio = desde.Value;
        return (inicio, inicio.AddDays(4));
    }

    if (hasta.HasValue)
    {
        var fin = hasta.Value;
        return (fin.AddDays(-4), fin);
    }

    return GetDefaultReportRange(_fechas.Hoy());
}

private async Task<List<RespuestaFormulario>> BuildCierreBaseAsync(DateOnly inicio, DateOnly fin, int empresaId, int sucursalId)
{
    var baseQuery = _db.RespuestasFormulario
        .Include(r => r.Empleado).ThenInclude(e => e!.Sucursal)
        .Include(r => r.SucursalEntrega)
        .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
        .Where(r => r.OpcionMenu != null
            && r.OpcionMenu.Menu != null
            && r.OpcionMenu.Menu.FechaInicio <= fin
            && r.OpcionMenu.Menu.FechaTermino >= inicio);

    baseQuery = baseQuery.Where(r =>
        (r.SucursalEntrega != null && r.SucursalEntrega.EmpresaId == empresaId) ||
        (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.Sucursal != null && r.Empleado.Sucursal.EmpresaId == empresaId));

    baseQuery = baseQuery.Where(r =>
        (r.SucursalEntrega != null && r.SucursalEntrega.Id == sucursalId) ||
        (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.SucursalId == sucursalId));

    var respuestas = await baseQuery.ToListAsync();
    return respuestas
        .Where(r =>
        {
            if (r.OpcionMenu?.Menu == null) return false;
            var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana);
            return fecha >= inicio && fecha <= fin;
        })
        .ToList();
}

private async Task<int> ClearCierreAsync(bool esFacturacion, int empresaId, int sucursalId, DateOnly inicio, DateOnly fin, bool clearUsuarioNomina = true)
{
    var respuestas = await BuildCierreBaseAsync(inicio, fin, empresaId, sucursalId);
    if (esFacturacion)
        respuestas = respuestas.Where(r => r.Facturado).ToList();
    else
        respuestas = respuestas.Where(r => r.CierreNomina).ToList();

    if (respuestas.Count == 0) return 0;

    foreach (var r in respuestas)
    {
        if (esFacturacion)
        {
            r.Facturado = false;
            r.FechaFacturado = null;
            r.UsuarioFacturacion = null;
        }
        else
        {
            r.CierreNomina = false;
            r.FechaCierreNomina = null;
            if (clearUsuarioNomina)
                r.UsuarioCierreNomina = null;
            r.Facturado = false;
            r.FechaFacturado = null;
            r.UsuarioFacturacion = null;
        }
    }
    await _db.SaveChangesAsync();
    return respuestas.Count;
}

private async Task<int> ClearNominaCreadaAsync(int empresaId, int sucursalId, DateOnly inicio, DateOnly fin)
{
    var respuestas = await BuildCierreBaseAsync(inicio, fin, empresaId, sucursalId);
    var abiertas = respuestas.Where(r => !r.CierreNomina && !string.IsNullOrWhiteSpace(r.UsuarioCierreNomina)).ToList();
    if (abiertas.Count == 0) return 0;

    foreach (var r in abiertas)
    {
        r.UsuarioCierreNomina = null;
        r.FechaCierreNomina = null;
    }
    await _db.SaveChangesAsync();
    return abiertas.Count;
}

private async Task<CierreFacturacionData> BuildCierreFacturacionAsync(int? empresaId, int? sucursalId, DateOnly? desde, DateOnly? hasta, bool esFacturacion)
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

    if (empresaId == null || sucursalId == null)
    {
        var empresasFallback = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursalesFallback = empresaId != null
            ? await _db.Sucursales.Where(s => s.EmpresaId == empresaId).OrderBy(s => s.Nombre).ToListAsync()
            : await _db.Sucursales.OrderBy(s => s.Nombre).ToListAsync();
        return new CierreFacturacionData
        {
            Vm = new CierreFacturacionVM
            {
                Inicio = inicio,
                Fin = fin,
                EmpresaId = empresaId,
                SucursalId = sucursalId,
                Empresas = empresasFallback,
                Sucursales = sucursalesFallback,
                ResumenFiliales = new List<CierreFacturacionVM.ResumenFilialRow>(),
                DetalleEmpleados = new List<CierreFacturacionVM.DetalleEmpleadoRow>()
            },
            Items = new List<CierreItem>(),
            Respuestas = new List<RespuestaFormulario>()
        };
    }

    var empresasQuery = _db.Empresas.AsQueryable();
    if (empresaId != null)
        empresasQuery = empresasQuery.Where(e => e.Id == empresaId);
    var empresas = await empresasQuery.OrderBy(e => e.Nombre).ToListAsync();

    var sucursalesBase = _db.Sucursales.AsQueryable();
    if (empresaId != null)
        sucursalesBase = sucursalesBase.Where(s => s.EmpresaId == empresaId);
    var sucursales = empresaId != null
        ? await sucursalesBase.OrderBy(s => s.Nombre).ToListAsync()
        : new List<Sucursal>();

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

    if (empresaId != null)
        baseQuery = baseQuery.Where(r =>
            (r.SucursalEntrega != null && r.SucursalEntrega.EmpresaId == empresaId) ||
            (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.Sucursal != null && r.Empleado.Sucursal.EmpresaId == empresaId));

    if (sucursalId != null)
        baseQuery = baseQuery.Where(r =>
            (r.SucursalEntrega != null && r.SucursalEntrega.Id == sucursalId) ||
            (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.SucursalId == sucursalId));
    if (esFacturacion)
        baseQuery = baseQuery.Where(r => r.CierreNomina && !r.Facturado);
    else
        baseQuery = baseQuery.Where(r => !r.CierreNomina);

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
    var items = new List<CierreItem>();

    foreach (var r in respuestas)
    {
        if (r.OpcionMenu == null || r.Empleado == null) continue;
        var fechaDia = ObtenerFechaDiaSemana(r.OpcionMenu.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
        var sucEmpleado = r.Empleado.Sucursal;
        var empresaEmpleado = sucEmpleado?.Empresa;
        var sucursalEntrega = r.SucursalEntrega ?? sucEmpleado;
        if (sucEmpleado == null || empresaEmpleado == null || sucursalEntrega == null) continue;

        var empresaNombre = sucursalEntrega.Empresa?.Nombre ?? empresaEmpleado.Nombre;
        var tanda = r.OpcionMenu.Horario?.Nombre ?? "Sin horario";
        var filial = sucursalEntrega.Nombre;
        var filialId = sucursalEntrega.Id;
        var empleadoNombre = GetEmpleadoDisplayName(r.Empleado);
        var empleadoCodigo = r.Empleado.Codigo ?? string.Empty;
        var empleadoIdRow = r.Empleado.Id;
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

            items.Add(new CierreItem(
                empresaNombre,
                filialId,
                filial,
                empleadoIdRow,
                empleadoNombre,
                empleadoCodigo,
                fechaDia,
                tanda,
                localizacion,
                nombre,
                seleccionLabel,
                basePrecio,
                itbis,
                total,
                empresaPaga,
                itbisEmpresa,
                empleadoPaga,
                itbisEmpleado,
                r.CierreNomina,
                r.FechaCierreNomina,
                r.Facturado,
                r.FechaFacturado,
                r.NumeroFactura));
        }

        var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
        if (opcion != null)
            AddItem(opcion, opcion.Nombre ?? "Sin definir", MapSeleccion(r.Seleccion), true);

        if (r.AdicionalOpcion != null)
            AddItem(r.AdicionalOpcion, $"Adicional: {r.AdicionalOpcion.Nombre ?? "Sin definir"}", "Adicional", false);
    }

    var resumen = items
        .GroupBy(i => new { i.FilialId, i.Filial })
        .Select(g => new CierreFacturacionVM.ResumenFilialRow
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
        .GroupBy(i => new { i.EmpleadoId, i.Empleado, i.FilialId, i.Filial })
        .Select(g => new CierreFacturacionVM.DetalleEmpleadoRow
        {
            EmpleadoId = g.Key.EmpleadoId,
            Empleado = g.Key.Empleado,
            Filial = g.Key.Filial,
            Cantidad = g.Count(),
            Base = g.Sum(x => x.Base),
            Itbis = g.Sum(x => x.Itbis),
            Total = g.Sum(x => x.Total),
            EmpresaPaga = g.Sum(x => x.EmpresaPaga),
            EmpleadoPaga = g.Sum(x => x.EmpleadoPaga),
            ItbisEmpresa = g.Sum(x => x.ItbisEmpresa),
            ItbisEmpleado = g.Sum(x => x.ItbisEmpleado)
        })
        .OrderBy(r => r.Filial)
        .ThenBy(r => r.Empleado)
        .ToList();

        var vm = new CierreFacturacionVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId,
        Empresas = empresas,
        Sucursales = sucursales,
        ResumenFiliales = resumen,
        DetalleEmpleados = detalle
    };

    return new CierreFacturacionData
    {
        Vm = vm,
        Items = items,
        Respuestas = respuestas
    };
}

private static (IReadOnlyList<string> Headers, List<IReadOnlyList<string>> Rows) BuildCierreFacturacionExport(List<CierreItem> items)
{
    var headers = new[]
    {
        "Empresa",
        "Filial",
        "Empleado codigo",
        "Empleado",
        "Fecha",
        "Tanda",
        "Localizacion",
        "Opcion",
        "Seleccion",
        "Precio base",
        "ITBIS",
        "Total",
        "Empresa paga",
        "ITBIS empresa",
        "Empleado paga",
        "ITBIS empleado",
        "Cierre nomina",
        "Fecha cierre nomina",
        "Facturado",
        "Fecha facturado",
        "Numero factura"
    };

    var rows = items.Select(i => (IReadOnlyList<string>)new[]
    {
        i.Empresa,
        i.Filial,
        i.EmpleadoCodigo,
        i.Empleado,
        i.Fecha.ToString("yyyy-MM-dd"),
        i.Tanda,
        i.Localizacion,
        i.Opcion,
        i.Seleccion,
        i.Base.ToString("0.00", CultureInfo.InvariantCulture),
        i.Itbis.ToString("0.00", CultureInfo.InvariantCulture),
        i.Total.ToString("0.00", CultureInfo.InvariantCulture),
        i.EmpresaPaga.ToString("0.00", CultureInfo.InvariantCulture),
        i.ItbisEmpresa.ToString("0.00", CultureInfo.InvariantCulture),
        i.EmpleadoPaga.ToString("0.00", CultureInfo.InvariantCulture),
        i.ItbisEmpleado.ToString("0.00", CultureInfo.InvariantCulture),
        i.CierreNomina ? "Si" : "No",
        i.FechaCierreNomina?.ToString("yyyy-MM-dd") ?? string.Empty,
        i.Facturado ? "Si" : "No",
        i.FechaFacturado?.ToString("yyyy-MM-dd") ?? string.Empty,
        i.NumeroFactura ?? string.Empty
    }).ToList();

    return (headers, rows);
}

private async Task<CierreFacturacionDetalleVM> BuildCierreFacturacionDetalleAsync(int? empresaId, int? sucursalId, DateOnly? desde, DateOnly? hasta, bool facturado)
{
    DateOnly inicio;
    DateOnly fin;
    if (desde.HasValue && hasta.HasValue)
    {
        inicio = desde.Value;
        fin = hasta.Value;
        if (fin < inicio)
            (inicio, fin) = (fin, inicio);
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

    var empresasQuery = _db.Empresas.AsQueryable();
    if (empresaId != null)
        empresasQuery = empresasQuery.Where(e => e.Id == empresaId);
    var empresas = await empresasQuery.OrderBy(e => e.Nombre).ToListAsync();

    var sucursalesQuery = _db.Sucursales.AsQueryable();
    if (empresaId != null)
        sucursalesQuery = sucursalesQuery.Where(s => s.EmpresaId == empresaId);
    var sucursales = empresaId != null
        ? await sucursalesQuery.OrderBy(s => s.Nombre).ToListAsync()
        : new List<Sucursal>();

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

    if (empresaId != null)
        baseQuery = baseQuery.Where(r =>
            (r.SucursalEntrega != null && r.SucursalEntrega.EmpresaId == empresaId) ||
            (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.Sucursal != null && r.Empleado.Sucursal.EmpresaId == empresaId));

    if (sucursalId != null)
        baseQuery = baseQuery.Where(r =>
            (r.SucursalEntrega != null && r.SucursalEntrega.Id == sucursalId) ||
            (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.SucursalId == sucursalId));

    baseQuery = facturado ? baseQuery.Where(r => r.Facturado) : baseQuery.Where(r => r.CierreNomina);

    var respuestas = await baseQuery.ToListAsync();
    respuestas = respuestas
        .Where(r =>
        {
            if (r.OpcionMenu == null || r.OpcionMenu.Menu == null) return false;
            var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana);
            return fecha >= inicio && fecha <= fin;
        })
        .ToList();

    var rows = new List<CierreFacturacionDetalleVM.Row>();
    foreach (var r in respuestas)
    {
        if (r.OpcionMenu == null || r.Empleado == null) continue;
        var fechaDia = ObtenerFechaDiaSemana(r.OpcionMenu.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
        var sucursalEntrega = r.SucursalEntrega ?? r.Empleado.Sucursal;
        var empresa = sucursalEntrega?.Empresa ?? r.Empleado.Sucursal?.Empresa;
        if (sucursalEntrega == null || empresa == null) continue;

        var tanda = r.OpcionMenu.Horario?.Nombre ?? "Sin horario";
        var filial = sucursalEntrega.Nombre;
        var empresaNombre = empresa.Nombre ?? "Empresa";
        var empleadoNombre = GetEmpleadoDisplayName(r.Empleado);
        var empleadoCodigo = r.Empleado.Codigo ?? string.Empty;

        var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
        if (opcion != null)
        {
            var montos = r.BaseSnapshot.HasValue
                ? (r.BaseSnapshot.Value, r.ItbisSnapshot ?? 0m, r.TotalSnapshot ?? 0m, r.EmpresaPagaSnapshot ?? 0m, r.EmpleadoPagaSnapshot ?? 0m, r.ItbisEmpresaSnapshot ?? 0m, r.ItbisEmpleadoSnapshot ?? 0m)
                : CalcularMontos(opcion, r.Empleado, r.Empleado.Sucursal!, empresa, true);

            rows.Add(new CierreFacturacionDetalleVM.Row
            {
                Fecha = fechaDia,
                Empresa = empresaNombre,
                Filial = filial,
                EmpleadoCodigo = empleadoCodigo,
                Empleado = empleadoNombre,
                Tanda = tanda,
                Seleccion = MapSeleccion(r.Seleccion),
                Opcion = opcion.Nombre ?? "Sin definir",
                Base = montos.Item1,
                Itbis = montos.Item2,
                Total = montos.Item3,
                EmpresaPaga = montos.Item4,
                EmpleadoPaga = montos.Item5,
                ItbisEmpresa = montos.Item6,
                ItbisEmpleado = montos.Item7,
                EsAdicional = false
            });
        }

        if (r.AdicionalOpcion != null)
        {
            var montosAd = r.AdicionalBaseSnapshot.HasValue
                ? (r.AdicionalBaseSnapshot.Value, r.AdicionalItbisSnapshot ?? 0m, r.AdicionalTotalSnapshot ?? 0m, r.AdicionalEmpresaPagaSnapshot ?? 0m, r.AdicionalEmpleadoPagaSnapshot ?? 0m, r.AdicionalItbisEmpresaSnapshot ?? 0m, r.AdicionalItbisEmpleadoSnapshot ?? 0m)
                : CalcularMontos(r.AdicionalOpcion, r.Empleado, r.Empleado.Sucursal!, empresa, false);

            rows.Add(new CierreFacturacionDetalleVM.Row
            {
                Fecha = fechaDia,
                Empresa = empresaNombre,
                Filial = filial,
                EmpleadoCodigo = empleadoCodigo,
                Empleado = empleadoNombre,
                Tanda = tanda,
                Seleccion = "Adicional",
                Opcion = r.AdicionalOpcion.Nombre ?? "Sin definir",
                Base = montosAd.Item1,
                Itbis = montosAd.Item2,
                Total = montosAd.Item3,
                EmpresaPaga = montosAd.Item4,
                EmpleadoPaga = montosAd.Item5,
                ItbisEmpresa = montosAd.Item6,
                ItbisEmpleado = montosAd.Item7,
                EsAdicional = true
            });
        }
    }

    rows = rows
        .OrderBy(r => r.Filial)
        .ThenBy(r => r.EmpleadoCodigo)
        .ThenBy(r => r.Fecha)
        .ToList();

    DateTime? cierreUtc = null;
    if (respuestas.Count > 0)
    {
        cierreUtc = facturado
            ? respuestas.Select(r => r.FechaFacturado).Where(d => d.HasValue).Max()
            : respuestas.Select(r => r.FechaCierreNomina).Where(d => d.HasValue).Max();
    }
    var puedeReabrir = cierreUtc != null && DateTime.UtcNow - cierreUtc.Value <= TimeSpan.FromMinutes(30);
    if (!facturado && empresaId != null && sucursalId != null)
    {
        var tieneCierrePosterior = await _db.RespuestasFormulario
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Where(r => r.CierreNomina
                && r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio > fin)
            .Where(r =>
                (r.SucursalEntrega != null && r.SucursalEntrega.EmpresaId == empresaId) ||
                (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.Sucursal != null && r.Empleado.Sucursal.EmpresaId == empresaId))
            .Where(r =>
                (r.SucursalEntrega != null && r.SucursalEntrega.Id == sucursalId) ||
                (r.SucursalEntrega == null && r.Empleado != null && r.Empleado.SucursalId == sucursalId))
            .AnyAsync();

        if (tieneCierrePosterior)
            puedeReabrir = false;
    }

    return new CierreFacturacionDetalleVM
    {
        Inicio = inicio,
        Fin = fin,
        EmpresaId = empresaId,
        SucursalId = sucursalId,
        Empresas = empresas,
        Sucursales = sucursales,
        Filas = rows,
        FechaCierreUtc = cierreUtc,
        PuedeReabrir = puedeReabrir
    };
}

private async Task<CierreListadoVM> BuildCierreListadoAsync(bool esFacturacion, bool soloCerrados, int page, int pageSize)
{
    if (page < 1) page = 1;
    if (pageSize <= 0) pageSize = 20;

    var baseQuery = _db.RespuestasFormulario
        .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
        .Include(r => r.Empleado).ThenInclude(e => e!.Sucursal).ThenInclude(s => s!.Empresa)
        .Include(r => r.SucursalEntrega).ThenInclude(s => s!.Empresa)
        .Where(r => r.OpcionMenu != null && r.OpcionMenu.Menu != null);

    if (esFacturacion)
        baseQuery = baseQuery.Where(r => r.CierreNomina);
    else
        baseQuery = baseQuery.Where(r => r.CierreNomina || r.UsuarioCierreNomina != null);
    if (soloCerrados)
        baseQuery = baseQuery.Where(r => esFacturacion ? r.Facturado : r.CierreNomina);

    var respuestas = await baseQuery.ToListAsync();
    var rows = respuestas
        .Select(r =>
        {
            var sucursal = r.SucursalEntrega ?? r.Empleado?.Sucursal;
            var empresa = r.SucursalEntrega?.Empresa ?? r.Empleado?.Sucursal?.Empresa;
            return new
            {
                Empresa = empresa,
                Sucursal = sucursal,
                Inicio = r.OpcionMenu!.Menu!.FechaInicio,
                Fin = r.OpcionMenu.Menu.FechaTermino,
                Cerrado = esFacturacion ? r.Facturado : r.CierreNomina
            };
        })
        .Where(x => x.Empresa != null && x.Sucursal != null)
        .GroupBy(x => new { EmpresaId = x.Empresa!.Id, SucursalId = x.Sucursal!.Id, x.Inicio, x.Fin })
        .Select(g =>
        {
            var first = g.First();
            return new CierreListadoVM.Row
            {
                EmpresaId = g.Key.EmpresaId,
                SucursalId = g.Key.SucursalId,
                Empresa = first.Empresa!.Nombre ?? "Empresa",
                Filial = first.Sucursal!.Nombre,
                Inicio = g.Key.Inicio,
                Fin = g.Key.Fin,
                TotalSelecciones = g.Count(),
                Cerrado = g.All(x => x.Cerrado)
            };
        })
        .ToList();

    if (soloCerrados)
        rows = rows.Where(r => r.Cerrado).ToList();

    rows = rows
        .OrderByDescending(r => r.Inicio)
        .ThenBy(r => r.Empresa)
        .ThenBy(r => r.Filial)
        .ToList();

    var total = rows.Count;
    var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    var paged = new PagedResult<CierreListadoVM.Row>
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalItems = total
    };

    return new CierreListadoVM { Paginado = paged };
}

private static (IReadOnlyList<string> Headers, List<IReadOnlyList<string>> Rows) BuildDetalleExport(CierreFacturacionDetalleVM vm)
{
    var headers = new[]
    {
        "Fecha","Empresa","Filial","Empleado codigo","Empleado","Tanda","Seleccion","Plato/Adicional","Base","ITBIS","Total","Empresa paga","Empleado paga","ITBIS empresa","ITBIS empleado"
    };

    var rows = vm.Filas.Select(r => (IReadOnlyList<string>)new[]
    {
        r.Fecha.ToString("yyyy-MM-dd"),
        r.Empresa,
        r.Filial,
        r.EmpleadoCodigo,
        r.Empleado,
        r.Tanda,
        r.Seleccion,
        r.Opcion,
        r.Base.ToString("0.00", CultureInfo.InvariantCulture),
        r.Itbis.ToString("0.00", CultureInfo.InvariantCulture),
        r.Total.ToString("0.00", CultureInfo.InvariantCulture),
        r.EmpresaPaga.ToString("0.00", CultureInfo.InvariantCulture),
        r.EmpleadoPaga.ToString("0.00", CultureInfo.InvariantCulture),
        r.ItbisEmpresa.ToString("0.00", CultureInfo.InvariantCulture),
        r.ItbisEmpleado.ToString("0.00", CultureInfo.InvariantCulture)
    }).ToList();

    return (headers, rows);
}

private static (string Title, string Suffix, IReadOnlyList<string> Headers, List<IReadOnlyList<string>> Rows) BuildDistribucionExport(DistribucionVM vm, string? vista)
{
    var normalized = string.IsNullOrWhiteSpace(vista) ? "resumen" : vista.Trim().ToLowerInvariant();

    List<IReadOnlyList<string>> AddFilterRows(IReadOnlyList<string> headers, List<IReadOnlyList<string>> dataRows)
    {
        var rows = new List<IReadOnlyList<string>>();
        string empresa = vm.EmpresaId != null ? vm.Empresas.FirstOrDefault(e => e.Id == vm.EmpresaId)?.Nombre ?? "Todas" : "Todas";
        string sucursal = vm.SucursalId != null ? vm.Sucursales.FirstOrDefault(s => s.Id == vm.SucursalId)?.Nombre ?? "Todas" : "Todas";
        string empleado = vm.EmpleadoId != null ? (vm.Empleados.FirstOrDefault(e => e.Id == vm.EmpleadoId)?.Nombre ?? vm.Empleados.FirstOrDefault(e => e.Id == vm.EmpleadoId)?.Codigo ?? "Todos") : "Todos";
        string localizacion = vm.LocalizacionId != null ? vm.Localizaciones.FirstOrDefault(l => l.Id == vm.LocalizacionId)?.Nombre ?? "Todas" : "Todas";
        string tanda = vm.HorarioId != null ? vm.Horarios.FirstOrDefault(h => h.Id == vm.HorarioId)?.Nombre ?? "Todas" : "Todas";
        string horaAlmuerzo = !string.IsNullOrWhiteSpace(vm.HoraAlmuerzo) ? vm.HoraAlmuerzo : "Todas";

        void AddRow(string label, string value)
        {
            var row = new string[headers.Count];
            row[0] = label;
            if (headers.Count > 1) row[1] = value;
            rows.Add(row);
        }

        AddRow("Filtro - Desde", vm.Inicio.ToString("yyyy-MM-dd"));
        AddRow("Filtro - Hasta", vm.Fin.ToString("yyyy-MM-dd"));
        AddRow("Filtro - Empresa", empresa);
        AddRow("Filtro - Filial", sucursal);
        AddRow("Filtro - Empleado", empleado);
        AddRow("Filtro - Localizacion", localizacion);
        AddRow("Filtro - Tanda", tanda);
        AddRow("Filtro - Hora almuerzo", horaAlmuerzo);
        rows.Add(new string[headers.Count]);
        rows.AddRange(dataRows);
        return rows;
    }

    switch (normalized)
    {
        case "detalle":
            {
                var headers = new[]
                {
                    "Fecha","Filial","Localizacion","Empleado","Tanda","Opcion","Seleccion","Base","ITBIS","Total","Empresa paga","Empleado paga","ITBIS empresa","ITBIS empleado"
                };
                var rows = vm.DetalleEmpleados.Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Fecha.ToString("yyyy-MM-dd"),
                    r.Filial,
                    r.Localizacion,
                    string.IsNullOrWhiteSpace(r.EmpleadoCodigo) ? r.Empleado : $"{r.EmpleadoCodigo} - {r.Empleado}",
                    r.Tanda,
                    r.Opcion,
                    r.Seleccion,
                    r.Base.ToString("C"),
                    r.Itbis.ToString("C"),
                    r.Total.ToString("C"),
                    r.EmpresaPaga.ToString("C"),
                    r.EmpleadoPaga.ToString("C"),
                    r.ItbisEmpresa.ToString("C"),
                    r.ItbisEmpleado.ToString("C")
                }).ToList();
                return ("Detalle por empleado", "detalle", headers, AddFilterRows(headers, rows));
            }
        case "localizacion":
            {
                var headers = new[]
                {
                    "Fecha","Filial","Localizacion","Opcion","Seleccion","Cantidad","Monto total","Empresa paga","Empleado paga"
                };
                var rows = vm.PorLocalizacion.Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Fecha.ToString("yyyy-MM-dd"),
                    r.Filial,
                    r.Localizacion,
                    r.Opcion,
                    r.Seleccion,
                    r.Cantidad.ToString(),
                    r.MontoTotal.ToString("C"),
                    r.EmpresaPaga.ToString("C"),
                    r.EmpleadoPaga.ToString("C")
                }).ToList();
                return ("Distribucion por localizacion", "localizacion", headers, AddFilterRows(headers, rows));
            }
        case "distribucion-detalle":
            {
                var headers = new[]
                {
                    "Fecha","Filial","Localizacion","Opcion 1","Opcion 2","Opcion 3","Opcion 4","Opcion 5","Adicional","Total Opcion","Total Adic"
                };
                var rows = new List<IReadOnlyList<string>>();
                foreach (var r in vm.PorLocalizacionCocina)
                {
                    rows.Add(new[]
                    {
                        r.Fecha.ToString("yyyy-MM-dd"),
                        r.Filial,
                        r.Localizacion,
                        r.Opcion1.ToString(),
                        r.Opcion2.ToString(),
                        r.Opcion3.ToString(),
                        r.Opcion4.ToString(),
                        r.Opcion5.ToString(),
                        r.Adicionales.ToString(),
                        r.TotalOpciones.ToString(),
                        r.Adicionales.ToString()
                    });

                    var detalles = vm.PorLocalizacionCocinaDetalle
                        .Where(d => d.Fecha == r.Fecha && d.Filial == r.Filial && d.Localizacion == r.Localizacion)
                        .ToList();
                    if (detalles.Count > 0)
                    {
                        rows.Add(new[]
                        {
                            "Detalle","Filial","Fecha","Codigo","Nombre","Seleccion","Plato/Adicional","","","",""
                        });
                        foreach (var d in detalles)
                        {
                            rows.Add(new[]
                            {
                                string.Empty,
                                d.Filial,
                                d.Fecha.ToString("yyyy-MM-dd"),
                                d.EmpleadoCodigo,
                                d.EmpleadoNombre,
                                d.Seleccion,
                                d.Opcion,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty
                            });
                        }
                    }
                }
                return ("Detalle distribucion", "distribucion-detalle", headers, AddFilterRows(headers, rows));
            }
        case "cocina":
            {
                var headers = new[]
                {
                    "Fecha","Filial","Localizacion","Opcion 1","Opcion 2","Opcion 3","Opcion 4","Opcion 5","Adicional","Total Opcion","Total Adic"
                };
                var rows = vm.PorLocalizacionCocina.Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Fecha.ToString("yyyy-MM-dd"),
                    r.Filial,
                    r.Localizacion,
                    r.Opcion1.ToString(),
                    r.Opcion2.ToString(),
                    r.Opcion3.ToString(),
                    r.Opcion4.ToString(),
                    r.Opcion5.ToString(),
                    r.Adicionales.ToString(),
                    r.TotalOpciones.ToString(),
                    r.Adicionales.ToString()
                }).ToList();
                return ("Cocina (totales)", "cocina", headers, AddFilterRows(headers, rows));
            }
        default:
            {
                var headers = new[]
                {
                    "Fecha","Filial","Base","ITBIS","Total","ITBIS empresa","ITBIS empleado","Monto adicional","ITBIS adicional","Empresa paga","Empleado paga"
                };
                var rows = new List<IReadOnlyList<string>>();
                var grupos = vm.ResumenFiliales
                    .GroupBy(r => r.Fecha)
                    .OrderBy(g => g.Key);
                foreach (var g in grupos)
                {
                    foreach (var r in g.OrderBy(x => x.Filial))
                    {
                        rows.Add(new[]
                        {
                            r.Fecha.ToString("yyyy-MM-dd"),
                            r.Filial,
                            r.Base.ToString("C"),
                            r.Itbis.ToString("C"),
                            r.Total.ToString("C"),
                            r.ItbisEmpresa.ToString("C"),
                            r.ItbisEmpleado.ToString("C"),
                            r.MontoAdicional.ToString("C"),
                            r.ItbisAdicional.ToString("C"),
                            r.EmpresaPaga.ToString("C"),
                            r.EmpleadoPaga.ToString("C")
                        });
                    }
                }

                if (vm.ResumenFiliales.Count > 0)
                {
                    rows.Add(new[]
                    {
                        string.Empty,
                        "Total general",
                        vm.TotalBase.ToString("C"),
                        vm.TotalItbis.ToString("C"),
                        vm.TotalGeneral.ToString("C"),
                        vm.ResumenFiliales.Sum(x => x.ItbisEmpresa).ToString("C"),
                        vm.ResumenFiliales.Sum(x => x.ItbisEmpleado).ToString("C"),
                        vm.ResumenFiliales.Sum(x => x.MontoAdicional).ToString("C"),
                        vm.ResumenFiliales.Sum(x => x.ItbisAdicional).ToString("C"),
                        vm.TotalEmpresa.ToString("C"),
                        vm.TotalEmpleado.ToString("C")
                    });
                }
                return ("Distribucion resumen por filial", "resumen", headers, AddFilterRows(headers, rows));
            }
    }
}

private async Task<List<Localizacion>> BuildLocalizacionesFiltroAsync(int? empresaId, int? sucursalId)
{
    var query = _db.Localizaciones.AsNoTracking().AsQueryable();
    if (empresaId != null)
        query = query.Where(l => l.EmpresaId == empresaId);
    if (sucursalId != null)
        query = query.Where(l => l.SucursalId == sucursalId);
    var items = await query.OrderBy(l => l.Nombre).ToListAsync();
    if (sucursalId == null)
    {
        return items
            .GroupBy(l => NormalizeLocalizacionKey(l.Nombre))
            .Select(g => g.OrderBy(x => x.Id).First())
            .OrderBy(l => l.Nombre)
            .ToList();
    }
    return items
        .GroupBy(l => (l.EmpresaId, l.SucursalId, Key: NormalizeLocalizacionKey(l.Nombre)))
        .Select(g => g.OrderBy(x => x.Id).First())
        .OrderBy(l => l.Nombre)
        .ToList();
}

private async Task<List<ReporteMaestroVM.Row>> BuildReporteMaestroRowsAsync(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId, int? localizacionId, int? empleadoId, int? horarioId, TimeOnly? horaAlmuerzo, string? tipo, string? estado, IReadOnlyList<Localizacion> localizaciones)
{
    var localizacionNombreFiltro = localizacionId != null
        ? localizaciones.FirstOrDefault(l => l.Id == localizacionId)?.Nombre
        : null;

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
    if (empleadoId != null)
        baseQuery = baseQuery.Where(r => r.EmpleadoId == empleadoId);
    if (localizacionId != null)
    {
        if (sucursalId == null && !string.IsNullOrWhiteSpace(localizacionNombreFiltro))
            baseQuery = baseQuery.Where(r => r.LocalizacionEntrega != null && r.LocalizacionEntrega.Nombre == localizacionNombreFiltro);
        else
            baseQuery = baseQuery.Where(r => r.LocalizacionEntregaId == localizacionId);
    }
    if (horarioId != null)
        baseQuery = baseQuery.Where(r => r.OpcionMenu != null && r.OpcionMenu.HorarioId == horarioId);
    if (!string.IsNullOrWhiteSpace(estado))
    {
        baseQuery = estado switch
        {
            "abierto" => baseQuery.Where(r => !r.CierreNomina),
            "cerrado" => baseQuery.Where(r => r.CierreNomina),
            _ => baseQuery
        };
    }
    if (horaAlmuerzo != null)
        baseQuery = baseQuery.Where(r => r.HoraAlmuerzo == horaAlmuerzo);

    var respuestas = await baseQuery.ToListAsync();
    respuestas = respuestas
        .Where(r =>
        {
            if (r.OpcionMenu == null || r.OpcionMenu.Menu == null) return false;
            var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu.FechaInicio, r.OpcionMenu.DiaSemana);
            return fecha >= inicio && fecha <= fin;
        })
        .ToList();

    var sucursalIds = respuestas
        .Select(r => r.SucursalEntregaId)
        .Distinct()
        .ToList();
    var empleadoIds = respuestas
        .Select(r => r.EmpleadoId)
        .Distinct()
        .ToList();
    var usuariosEmpleado = await _db.Set<ApplicationUser>()
        .Where(u => u.EmpleadoId != null && empleadoIds.Contains(u.EmpleadoId.Value))
        .Select(u => new { u.EmpleadoId, u.UserName })
        .ToDictionaryAsync(u => u.EmpleadoId!.Value, u => u.UserName ?? string.Empty);

    var culture = CultureInfo.CurrentCulture;
    var rows = new List<ReporteMaestroVM.Row>();
    foreach (var r in respuestas)
    {
        if (r.OpcionMenu == null || r.Empleado == null) continue;
        var fecha = ObtenerFechaDiaSemana(r.OpcionMenu.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
        var sucEmpleado = r.Empleado.Sucursal;
        var empresaEmpleado = sucEmpleado?.Empresa;
        var sucEntrega = r.SucursalEntrega ?? sucEmpleado;
        if (sucEmpleado == null || empresaEmpleado == null || sucEntrega == null) continue;

        var localizacion = r.LocalizacionEntrega?.Nombre ?? "Sin asignar";
        var empleadoNombre = GetEmpleadoDisplayName(r.Empleado);
        var empleadoCodigo = r.Empleado.Codigo ?? string.Empty;
        var usuarioEmpleado = usuariosEmpleado.TryGetValue(r.Empleado.Id, out var username) ? username : string.Empty;
        var horarioNombre = r.OpcionMenu.Horario?.Nombre ?? "Sin horario";
        var horaAlmuerzoTexto = r.HoraAlmuerzo?.ToString("HH:mm") ?? string.Empty;

        void AddRow(string tipoRegistro, string opcionLabel, string plato, decimal baseValor, decimal itbis, decimal total, decimal empresaPaga, decimal empleadoPaga, decimal itbisEmpresa, decimal itbisEmpleado)
        {
            var subsidioAplicado = empresaPaga > 0 ? "Si" : "No";
            var porcentajeSubsidio = total > 0 ? Math.Round((empresaPaga / total) * 100m, 2) : 0m;
            rows.Add(new ReporteMaestroVM.Row
            {
                Fecha = fecha,
                DiaSemana = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                Hora = r.FechaSeleccion.HasValue ? r.FechaSeleccion.Value.ToLocalTime().ToString("HH:mm") : string.Empty,
                HoraAlmuerzo = horaAlmuerzoTexto,
                Horario = horarioNombre,
                Empresa = empresaEmpleado.Nombre ?? string.Empty,
                Filial = sucEntrega.Nombre ?? string.Empty,
                Localizacion = localizacion,
                EmpleadoCodigo = empleadoCodigo,
                EmpleadoNombre = empleadoNombre,
                UsuarioEmpleado = usuarioEmpleado,
                Tipo = tipoRegistro,
                Opcion = opcionLabel,
                Plato = plato,
                Cantidad = 1,
                PrecioUnitario = baseValor,
                SubtotalBase = baseValor,
                ItbisTotal = itbis,
                Total = total,
                SubsidioAplicado = subsidioAplicado,
                PorcentajeSubsidio = porcentajeSubsidio,
                EmpresaPaga = empresaPaga,
                ItbisEmpresa = itbisEmpresa,
                EmpleadoPaga = empleadoPaga,
                ItbisEmpleado = itbisEmpleado,
                NumeroCierre = r.FechaCierreNomina?.ToString("yyyy-MM-dd") ?? string.Empty,
                EstadoNomina = r.CierreNomina ? "Cerrada" : "Abierta",
                NumeroFactura = r.NumeroFactura ?? string.Empty,
                EstadoFacturacion = r.Facturado ? "Facturada" : "Pendiente",
                UsuarioProceso = r.Facturado
                    ? (r.UsuarioFacturacion ?? string.Empty)
                    : (r.CierreNomina ? (r.UsuarioCierreNomina ?? string.Empty) : string.Empty)
            });
        }

        if (tipo == null || tipo == "menu")
        {
            var opcion = GetOpcionSeleccionada(r.OpcionMenu, r.Seleccion);
            if (opcion != null)
            {
                var montos = r.BaseSnapshot.HasValue
                    ? (r.BaseSnapshot.Value, r.ItbisSnapshot ?? 0m, r.TotalSnapshot ?? 0m, r.EmpresaPagaSnapshot ?? 0m, r.EmpleadoPagaSnapshot ?? 0m, r.ItbisEmpresaSnapshot ?? 0m, r.ItbisEmpleadoSnapshot ?? 0m)
                    : CalcularMontos(opcion, r.Empleado, sucEmpleado, empresaEmpleado, true);
                AddRow("Menu", MapSeleccion(r.Seleccion), opcion.Nombre ?? "Sin definir", montos.Item1, montos.Item2, montos.Item3, montos.Item4, montos.Item5, montos.Item6, montos.Item7);
            }
        }

        if ((tipo == null || tipo == "adicional") && r.AdicionalOpcion != null)
        {
            var adicional = r.AdicionalOpcion;
            var montosAd = r.AdicionalBaseSnapshot.HasValue
                ? (r.AdicionalBaseSnapshot.Value, r.AdicionalItbisSnapshot ?? 0m, r.AdicionalTotalSnapshot ?? 0m, r.AdicionalEmpresaPagaSnapshot ?? 0m, r.AdicionalEmpleadoPagaSnapshot ?? 0m, r.AdicionalItbisEmpresaSnapshot ?? 0m, r.AdicionalItbisEmpleadoSnapshot ?? 0m)
                : CalcularMontos(adicional, r.Empleado, sucEmpleado, empresaEmpleado, false);
            AddRow("Adicional", "Adicional", adicional.Nombre ?? "Sin definir", montosAd.Item1, montosAd.Item2, montosAd.Item3, montosAd.Item4, montosAd.Item5, montosAd.Item6, montosAd.Item7);
        }
    }

    return rows
        .OrderBy(r => r.Fecha)
        .ThenBy(r => r.Empresa)
        .ThenBy(r => r.Filial)
        .ThenBy(r => r.EmpleadoNombre)
        .ThenBy(r => r.Tipo)
        .ToList();
}

    private static string NormalizeLocalizacionKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return value.Trim().ToUpperInvariant();
    }

private static (IReadOnlyList<string> Headers, List<IReadOnlyList<string>> Rows) BuildReporteMaestroExport(IReadOnlyList<ReporteMaestroVM.Row> rows)
{
    var headers = new[]
    {
        "Fecha","Dia","Hora","Hora almuerzo","Horario","Empresa","Filial","Localizacion","Codigo empleado","Empleado","Usuario","Tipo","Opcion","Plato","Cantidad","Precio unitario base","ITBIS","Total","Subsidio aplicado","% subsidio","Monto empresa","ITBIS empresa","Monto empleado","ITBIS empleado","Numero cierre","Estado nomina","Numero factura","Estado facturacion","Usuario proceso"
      };

    var data = rows.Select(r => (IReadOnlyList<string>)new[]
    {
        r.Fecha.ToString("yyyy-MM-dd"),
        r.DiaSemana,
        r.Hora,
        r.HoraAlmuerzo,
        r.Horario,
        r.Empresa,
        r.Filial,
        r.Localizacion,
        r.EmpleadoCodigo,
        r.EmpleadoNombre,
        r.UsuarioEmpleado,
        r.Tipo,
        r.Opcion,
        r.Plato,
        r.Cantidad.ToString(),
        r.PrecioUnitario.ToString("C"),
        r.ItbisTotal.ToString("C"),
        r.Total.ToString("C"),
        r.SubsidioAplicado,
        r.PorcentajeSubsidio.ToString("0.00"),
        r.EmpresaPaga.ToString("C"),
        r.ItbisEmpresa.ToString("C"),
        r.EmpleadoPaga.ToString("C"),
        r.ItbisEmpleado.ToString("C"),
        r.NumeroCierre,
        r.EstadoNomina,
        r.NumeroFactura,
        r.EstadoFacturacion,
        r.UsuarioProceso
    }).ToList();

    return (headers, data);
}
}
