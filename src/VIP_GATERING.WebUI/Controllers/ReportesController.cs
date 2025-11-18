using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using VIP_GATERING.Application.Services;
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

    public ReportesController(AppDbContext db, IFechaServicio fechas, ICurrentUserService current)
    { _db = db; _fechas = fechas; _current = current; }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ItemsSemana(Guid? empresaId = null, Guid? sucursalId = null)
    {
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        var sucursales = await _db.Sucursales
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        var query = from r in _db.RespuestasFormulario
                    join e in _db.Empleados on r.EmpleadoId equals e.Id
                    join s in _db.Sucursales on e.SucursalId equals s.Id
                    join om in _db.OpcionesMenu on r.OpcionMenuId equals om.Id
                    join m in _db.Menus on om.MenuId equals m.Id
                    where m.FechaInicio == inicio && m.FechaTermino == fin
                    select new { r.Seleccion, OpcionMenu = om, Sucursal = s };

        if (empresaId != null)
            query = query.Where(x => x.Sucursal.EmpresaId == empresaId);
        if (sucursalId != null)
            query = query.Where(x => x.Sucursal.Id == sucursalId);

        var itemsRaw = await query
            .Select(x => new
            {
                OpcionId = x.Seleccion == 'A' ? x.OpcionMenu.OpcionIdA!
                         : x.Seleccion == 'B' ? x.OpcionMenu.OpcionIdB!
                         : x.OpcionMenu.OpcionIdC!,
                Nombre = x.Seleccion == 'A' ? x.OpcionMenu.OpcionA!.Nombre
                        : x.Seleccion == 'B' ? x.OpcionMenu.OpcionB!.Nombre
                        : x.OpcionMenu.OpcionC!.Nombre,
                Costo = x.Seleccion == 'A' ? x.OpcionMenu.OpcionA!.Costo
                      : x.Seleccion == 'B' ? x.OpcionMenu.OpcionB!.Costo
                      : x.OpcionMenu.OpcionC!.Costo,
                Precio = x.Seleccion == 'A'
                    ? (x.OpcionMenu.OpcionA!.Precio ?? x.OpcionMenu.OpcionA!.Costo)
                    : x.Seleccion == 'B'
                        ? (x.OpcionMenu.OpcionB!.Precio ?? x.OpcionMenu.OpcionB!.Costo)
                        : (x.OpcionMenu.OpcionC!.Precio ?? x.OpcionMenu.OpcionC!.Costo)
            })
            .ToListAsync();

        var items = itemsRaw
            .GroupBy(x => new { x.OpcionId, x.Nombre, x.Costo, x.Precio })
            .Select(g => new ItemsSemanaVM.ItemRow
            {
                OpcionId = g.Key.OpcionId,
                Nombre = g.Key.Nombre,
                CostoUnitario = g.Key.Costo,
                PrecioUnitario = g.Key.Precio,
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

        var baseQuery = from r in _db.RespuestasFormulario
                        join e in _db.Empleados on r.EmpleadoId equals e.Id
                        join s in _db.Sucursales on e.SucursalId equals s.Id
                        join om in _db.OpcionesMenu on r.OpcionMenuId equals om.Id
                        join m in _db.Menus on om.MenuId equals m.Id
                        where m.FechaInicio == inicio && m.FechaTermino == fin
                        select new { r.Seleccion, Empleado = e, Sucursal = s, OpcionMenu = om };

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.Sucursal.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.Sucursal.Id == sucursalId);

        var detalleRaw = await baseQuery
            .Select(x => new
            {
                x.Empleado.Id,
                EmpleadoNombre = x.Empleado.Nombre,
                SucursalId = x.Sucursal.Id,
                SucursalNombre = x.Sucursal.Nombre,
                Costo = x.Seleccion == 'A' ? x.OpcionMenu.OpcionA!.Costo
                      : x.Seleccion == 'B' ? x.OpcionMenu.OpcionB!.Costo
                      : x.Seleccion == 'C' ? x.OpcionMenu.OpcionC!.Costo
                      : 0m,
                Precio = x.Seleccion == 'A'
                    ? (x.OpcionMenu.OpcionA!.Precio ?? x.OpcionMenu.OpcionA!.Costo)
                    : x.Seleccion == 'B'
                        ? (x.OpcionMenu.OpcionB!.Precio ?? x.OpcionMenu.OpcionB!.Costo)
                        : (x.OpcionMenu.OpcionC!.Precio ?? x.OpcionMenu.OpcionC!.Costo)
            })
            .ToListAsync();

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

        var baseQuery = from r in _db.RespuestasFormulario
                        join e in _db.Empleados on r.EmpleadoId equals e.Id
                        join s in _db.Sucursales on e.SucursalId equals s.Id
                        join om in _db.OpcionesMenu on r.OpcionMenuId equals om.Id
                        join m in _db.Menus on om.MenuId equals m.Id
                        where m.FechaInicio == inicio && m.FechaTermino == fin
                        select new { r.Seleccion, Empleado = e, Sucursal = s, OpcionMenu = om };

        if (empresaId != null)
            baseQuery = baseQuery.Where(x => x.Sucursal.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(x => x.Sucursal.Id == sucursalId);

        var rowsRaw = await baseQuery
            .Select(x => new
            {
                x.Empleado.Id,
                EmpleadoNombre = x.Empleado.Nombre,
                Costo = x.Seleccion == 'A' ? x.OpcionMenu.OpcionA!.Costo
                      : x.Seleccion == 'B' ? x.OpcionMenu.OpcionB!.Costo
                      : x.Seleccion == 'C' ? x.OpcionMenu.OpcionC!.Costo
                      : 0m,
                Precio = x.Seleccion == 'A'
                    ? (x.OpcionMenu.OpcionA!.Precio ?? x.OpcionMenu.OpcionA!.Costo)
                    : x.Seleccion == 'B'
                        ? (x.OpcionMenu.OpcionB!.Precio ?? x.OpcionMenu.OpcionB!.Costo)
                        : (x.OpcionMenu.OpcionC!.Precio ?? x.OpcionMenu.OpcionC!.Costo)
            })
            .ToListAsync();

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

        var empleadoNombre = await _db.Empleados
            .Where(e => e.Id == empleadoId)
            .Select(e => e.Nombre)
            .FirstOrDefaultAsync() ?? "Empleado";

        var hoy = _fechas.Hoy();
        hasta ??= hoy;
        desde ??= hasta.Value.AddDays(-30);
        if (hasta < desde)
        {
            (desde, hasta) = (hasta, desde);
        }

        var desdeValue = desde.Value;
        var hastaValue = hasta.Value;

        var rawMovimientos = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId)
            .Where(r => r.OpcionMenu!.Menu!.FechaInicio <= hastaValue && r.OpcionMenu.Menu!.FechaTermino >= desdeValue)
            .Select(r => new
            {
                r.Seleccion,
                Dia = r.OpcionMenu!.DiaSemana,
                Horario = r.OpcionMenu.Horario != null ? r.OpcionMenu.Horario.Nombre : null,
                MenuInicio = r.OpcionMenu.Menu!.FechaInicio,
                NombreA = r.OpcionMenu.OpcionA != null ? r.OpcionMenu.OpcionA.Nombre : null,
                PrecioA = r.OpcionMenu.OpcionA != null ? (r.OpcionMenu.OpcionA.Precio ?? r.OpcionMenu.OpcionA.Costo) : (decimal?)null,
                NombreB = r.OpcionMenu.OpcionB != null ? r.OpcionMenu.OpcionB.Nombre : null,
                PrecioB = r.OpcionMenu.OpcionB != null ? (r.OpcionMenu.OpcionB.Precio ?? r.OpcionMenu.OpcionB.Costo) : (decimal?)null,
                NombreC = r.OpcionMenu.OpcionC != null ? r.OpcionMenu.OpcionC.Nombre : null,
                PrecioC = r.OpcionMenu.OpcionC != null ? (r.OpcionMenu.OpcionC.Precio ?? r.OpcionMenu.OpcionC.Costo) : (decimal?)null
            })
            .ToListAsync();

        var culture = CultureInfo.CurrentCulture;
        var movimientos = rawMovimientos
            .Select(r =>
            {
                var fecha = ObtenerFechaDiaSemana(r.MenuInicio, r.Dia);
                if (fecha < desdeValue || fecha > hastaValue) return null;
                (string? nombre, decimal? precio) detalle = r.Seleccion switch
                {
                    'A' => (r.NombreA, r.PrecioA),
                    'B' => (r.NombreB, r.PrecioB),
                    'C' => (r.NombreC, r.PrecioC),
                    _ => (null, null)
                };
                return new EstadoCuentaEmpleadoVM.MovimientoRow
                {
                    Fecha = fecha,
                    DiaNombre = culture.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                    Horario = r.Horario,
                    Seleccion = r.Seleccion.ToString(),
                    OpcionNombre = detalle.nombre ?? "Sin definir",
                    PrecioEmpleado = detalle.precio ?? 0m
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

    private static DateOnly ObtenerFechaDiaSemana(DateOnly inicioSemana, DayOfWeek dia)
    {
        var offset = ((int)dia - (int)DayOfWeek.Monday + 7) % 7;
        return inicioSemana.AddDays(offset);
    }
}

