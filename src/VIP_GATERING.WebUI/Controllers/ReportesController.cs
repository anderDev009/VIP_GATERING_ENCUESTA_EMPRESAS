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
            .Include(r => r.Empleado)!.ThenInclude(e => e.Sucursal)!.ThenInclude(s => s.Empresa)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionA)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionB)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionC)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionD)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionE)
            .Where(r => r.OpcionMenu!.Menu!.FechaInicio == inicio && r.OpcionMenu.Menu!.FechaTermino == fin);
        baseQuery = baseQuery.Where(x => x.Empleado != null && x.Empleado.Sucursal != null && x.Empleado.Sucursal.Empresa != null && x.OpcionMenu != null && x.OpcionMenu.Menu != null);
        baseQuery = baseQuery.Where(x => x.Empleado != null && x.Empleado.Sucursal != null && x.Empleado.Sucursal.Empresa != null && x.OpcionMenu != null && x.OpcionMenu.Menu != null);
        baseQuery = baseQuery.Where(x => x.Empleado != null && x.Empleado.Sucursal != null && x.Empleado.Sucursal.Empresa != null && x.OpcionMenu != null && x.OpcionMenu.Menu != null);

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.Empleado!.Sucursal!.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.Empleado!.SucursalId == sucursalId);

        var respuestas = await baseQuery.ToListAsync();

        var itemsRaw = respuestas
            .Select(r =>
            {
                var opcion = GetOpcionSeleccionada(r.OpcionMenu!, r.Seleccion);
                if (opcion == null || r.Empleado?.Sucursal?.Empresa == null) return null;
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, r.Empleado.Sucursal, r.Empleado.Sucursal.Empresa);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                return new
                {
                    OpcionId = opcion.Id,
                    Nombre = opcion.Nombre,
                    Costo = opcion.Costo,
                    PrecioEmpleado = precio
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

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
            .Include(r => r.Empleado)!.ThenInclude(e => e.Sucursal)!.ThenInclude(s => s.Empresa)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionA)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionB)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionC)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionD)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionE)
            .Where(r => r.OpcionMenu!.Menu!.FechaInicio == inicio && r.OpcionMenu.Menu!.FechaTermino == fin);

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.Empleado!.Sucursal!.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.Empleado!.SucursalId == sucursalId);

        var respuestas = await baseQuery.ToListAsync();

        var detalleRaw = respuestas
            .Select(r =>
            {
                var opcion = GetOpcionSeleccionada(r.OpcionMenu!, r.Seleccion);
                if (opcion == null || r.Empleado?.Sucursal?.Empresa == null) return null;
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, r.Empleado.Sucursal, r.Empleado.Sucursal.Empresa);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                return new
                {
                    r.Empleado.Id,
                    EmpleadoNombre = r.Empleado.Nombre,
                    SucursalId = r.Empleado.SucursalId,
                    SucursalNombre = r.Empleado.Sucursal!.Nombre,
                    Costo = opcion.Costo,
                    Precio = precio
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        var detalle = detalleRaw
            .GroupBy(x => new { x.Id, x.EmpleadoNombre, x.SucursalId, x.SucursalNombre })
            .Select(g => new SeleccionesVM.EmpleadoResumen
            {
                EmpleadoId = g.Key.Id,
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
            .Include(r => r.Empleado)!.ThenInclude(e => e.Sucursal)!.ThenInclude(s => s.Empresa)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionA)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionB)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionC)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionD)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionE)
            .Where(r => r.OpcionMenu!.Menu!.FechaInicio == inicio && r.OpcionMenu.Menu!.FechaTermino == fin);

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.Empleado!.Sucursal!.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.Empleado!.SucursalId == sucursalId);

        var respuestas = await baseQuery.ToListAsync();

        var rowsRaw = respuestas
            .Select(r =>
            {
                var opcion = GetOpcionSeleccionada(r.OpcionMenu!, r.Seleccion);
                if (opcion == null || r.Empleado?.Sucursal?.Empresa == null) return null;
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, r.Empleado, r.Empleado.Sucursal, r.Empleado.Sucursal.Empresa);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                return new
                {
                    r.Empleado.Id,
                    EmpleadoNombre = r.Empleado.Nombre,
                    Costo = opcion.Costo,
                    Precio = precio
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        var rows = rowsRaw
            .GroupBy(x => new { x.Id, x.EmpleadoNombre })
            .Select(g => new TotalesEmpleadosVM.Row
            {
                EmpleadoId = g.Key.Id,
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
            .Include(e => e.Sucursal)!.ThenInclude(s => s.Empresa)
            .FirstOrDefaultAsync(e => e.Id == empleadoId);
        if (empleadoDatos == null || empleadoDatos.Sucursal?.Empresa == null) return Forbid();
        var empleadoNombre = empleadoDatos.Nombre;

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
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Horario)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionA)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionB)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionC)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionD)
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.OpcionE)
            .Where(r => r.EmpleadoId == empleadoId)
            .Where(r => r.OpcionMenu!.Menu!.FechaInicio <= hastaValue && r.OpcionMenu.Menu!.FechaTermino >= desdeValue)
            .ToListAsync();

        var culture = CultureInfo.CurrentCulture;
        var movimientos = respuestas
            .Select(r =>
            {
                var fecha = ObtenerFechaDiaSemana(r.OpcionMenu!.Menu!.FechaInicio, r.OpcionMenu.DiaSemana);
                if (fecha < desdeValue || fecha > hastaValue) return null;
                var opcion = GetOpcionSeleccionada(r.OpcionMenu!, r.Seleccion);
                if (opcion == null) return null;
                var ctx = BuildSubsidioContext(opcion.EsSubsidiado, empleadoDatos, empleadoDatos.Sucursal!, empleadoDatos.Sucursal!.Empresa!);
                var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
                return new EstadoCuentaEmpleadoVM.MovimientoRow
                {
                    Fecha = fecha,
                    DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                    Horario = r.OpcionMenu.Horario != null ? r.OpcionMenu.Horario.Nombre : null,
                    Seleccion = r.Seleccion.ToString(),
                    OpcionNombre = opcion.Nombre ?? "Sin definir",
                    PrecioEmpleado = precio
                };
            })
            .Where(m => m != null)
            .Select(m => m!)
            .OrderByDescending(m => m.Fecha)
            .ThenBy(m => m.Horario)
            .ToList();

        var vm = new EstadoCuentaEmpleadoVM
        {
            EmpleadoId = empleadoId.Value,
            EmpleadoNombre = empleadoNombre,
            Desde = desdeValue,
            Hasta = hastaValue,
            Movimientos = movimientos
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
                        if (vm.SucursalId != null) text.Span("  | Sucursal filtrada").SemiBold().FontSize(10);
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
                    col.Item().Text("Subtotales por sucursal").SemiBold();
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
                            h.Cell().Text("Sucursal").SemiBold();
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
                            h.Cell().Text("Sucursal").SemiBold();
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
            sucursal.SubsidioValor);

    private static DateOnly ObtenerFechaDiaSemana(DateOnly inicioSemana, DayOfWeek dia)
    {
        var offset = ((int)dia - (int)DayOfWeek.Monday + 7) % 7;
        return inicioSemana.AddDays(offset);
    }
}
