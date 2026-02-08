using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;
using VIP_GATERING.WebUI.Models;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VIP_GATERING.WebUI.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using System.IO;
using System.Text;

namespace VIP_GATERING.WebUI.Controllers;

// Panel simple para armar menu semanal
[Authorize(Roles = "Admin")]
public class MenuController : Controller
{
    private readonly AppDbContext _db;
    private readonly IMenuService _menuService;
    private readonly ILogger<MenuController> _logger;
    private readonly IMenuCloneService _cloneService;
    private readonly IEncuestaCierreService _cierre;
    private readonly UserManager<ApplicationUser> _userManager;
    public MenuController(AppDbContext db, IMenuService menuService, ILogger<MenuController> logger, IMenuCloneService cloneService, IEncuestaCierreService cierre, UserManager<ApplicationUser> userManager)
    { _db = db; _menuService = menuService; _logger = logger; _cloneService = cloneService; _cierre = cierre; _userManager = userManager; }

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
    private const int ImportErrorLimit = 50;
    private const int ImportWarningLimit = 50;

    // Sucursales con buscador para administrar menus
    [HttpGet]
    public async Task<IActionResult> Clientes(string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Empresas.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(e => e.Nombre.ToLower().Contains(ql) || (e.Rnc != null && e.Rnc.ToLower().Contains(ql)));
        }
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 10;
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(e => e.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new MenuClientesVM.Item
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Rnc = e.Rnc,
                Sucursales = _db.Sucursales.Count(s => s.EmpresaId == e.Id)
            }).ToListAsync();

        var vm = new MenuClientesVM
        {
            Q = q,
            Sucursales = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
        return View("Clientes", vm);
    }

    // Listado de sucursales agrupadas por Sucursal (o filtradas por Sucursal)
    [HttpGet]
    public async Task<IActionResult> Sucursales(int? empresaId, string? q)
    {
        var sucQuery = _db.Sucursales.Include(s => s.Empresa).AsQueryable();
        if (empresaId != null) sucQuery = sucQuery.Where(s => s.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            sucQuery = sucQuery.Where(s => s.Nombre.ToLower().Contains(ql) || s.Empresa!.Nombre.ToLower().Contains(ql));
        }
        var list = await sucQuery.OrderBy(s => s.Empresa!.Nombre).ThenBy(s => s.Nombre).ToListAsync();
        var grupos = list.GroupBy(s => new { s.EmpresaId, s.Empresa!.Nombre })
            .Select(g => new MenuSucursalesVM.Grupo
            {
                EmpresaId = g.Key.EmpresaId,
                Empresa = g.Key.Nombre,
                Sucursales = g.Select(s => new MenuSucursalesVM.SucItem { Id = s.Id, Nombre = s.Nombre }).ToList()
            }).ToList();
        var vm2 = new MenuSucursalesVM { EmpresaId = empresaId, Q = q, Grupos = grupos };
        return View(vm2);
    }

    // GET: copiar menu del cliente a multiples filiales
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Copiar(int empresaId, DateTime? fecha)
    {
        var empresa = await _db.Empresas.FirstAsync(e => e.Id == empresaId);
        var baseDate = fecha?.Date ?? DateTime.UtcNow.Date;
        int diff = ((int)baseDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var lunes = baseDate.AddDays(-diff);
        var inicio = DateOnly.FromDateTime(lunes);
        var fin = inicio.AddDays(4);
        var sucsRaw = await _db.Sucursales.Where(s => s.EmpresaId == empresaId)
            .OrderBy(s => s.Nombre)
            .Select(s => new { s.Id, s.Nombre })
            .ToListAsync();
        var sucs = sucsRaw.Select(x => (x.Id, x.Nombre)).ToList();
        return View((empresa, inicio, fin, sucs));
    }

    // POST: copiar menu del cliente a filiales seleccionadas
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Copiar(int empresaId, DateOnly inicio, DateOnly fin, [FromForm] List<int> sucursalIds)
    {
        if (sucursalIds == null || sucursalIds.Count == 0)
        {
            TempData["Info"] = "Debe seleccionar al menos una filial.";
            return RedirectToAction(nameof(Copiar), new { empresaId, fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd") });
        }
        var (updated, skipped) = await _cloneService.CloneEmpresaMenuToSucursalesAsync(inicio, fin, empresaId, sucursalIds);
        TempData["Success"] = $"Menu copiado. Actualizadas: {updated}. Omitidas por bloqueo: {skipped}.";
        return RedirectToAction(nameof(Sucursales));
    }

    // GET: Administrar menu por rango (seleccion de semana)
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Administrar(DateTime? fecha, int? empresaId, int? sucursalId, string? alcance)
    {
        var baseDate = fecha?.Date ?? DateTime.UtcNow.Date;
        // Si no se especifica fecha y hoy es domingo, movemos a lunes siguiente como semana por defecto
        if (fecha == null && baseDate.DayOfWeek == DayOfWeek.Sunday)
            baseDate = baseDate.AddDays(1);

        // Calcular lunes de la semana del baseDate (lunes como inicio)
        int diff = ((int)baseDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var lunes = baseDate.AddDays(-diff);
        var inicio = DateOnly.FromDateTime(lunes);
        var fin = inicio.AddDays(4);

        _logger.LogInformation("[GET Administrar] baseDate={Base}, lunes={Lunes}, rango={Inicio}-{Fin}", baseDate.ToString("yyyy-MM-dd"), lunes.ToString("yyyy-MM-dd"), inicio, fin);

        // Cargar catÃ¡logos
        var empresas = await _db.Empresas.Select(e => new { e.Id, e.Nombre }).ToListAsync();
        int? empresaSel = empresaId;
        if (empresaSel == null && sucursalId != null)
        {
            empresaSel = await _db.Sucursales
                .Where(s => s.Id == sucursalId)
                .Select(s => (int?)s.EmpresaId)
                .FirstOrDefaultAsync();
        }
        empresaSel ??= empresas.FirstOrDefault()?.Id;
        if (empresaSel != null && empresas.All(e => e.Id != empresaSel))
            empresaSel = empresas.FirstOrDefault()?.Id;

        var sucursalesAll = await _db.Sucursales
            .Select(s => new { s.Id, s.Nombre, s.EmpresaId })
            .ToListAsync();
        var sucursalesEmpresa = empresaSel != null
            ? sucursalesAll.Where(s => s.EmpresaId == empresaSel).ToList()
            : sucursalesAll;

        int? sucursalSel = sucursalId;
        if (!string.IsNullOrEmpty(alcance))
        {
            if (alcance.Equals("empresa", StringComparison.OrdinalIgnoreCase)) sucursalSel = null;
        }
        if (sucursalSel != null && !sucursalesEmpresa.Any(s => s.Id == sucursalSel))
            sucursalSel = null;
        if (sucursalSel != null)
        {
            var horariosCount = await _db.SucursalesHorarios
                .Where(sh => sh.SucursalId == sucursalSel)
                .Select(sh => sh.HorarioId)
                .Distinct()
                .CountAsync();
            if (horariosCount == 0)
            {
                TempData["Error"] = "La filial seleccionada no tiene horarios configurados. Configura horarios en Filiales.";
                return RedirectToAction(nameof(Sucursales));
            }
        }

        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaSel, sucursalSel);
        var fechaCierreAuto = _cierre.GetFechaCierreAutomatica(menu);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        var diasDb = await _db.OpcionesMenu
            .Include(o => o.OpcionA)
            .Include(o => o.OpcionB)
            .Include(o => o.OpcionC)
            .Include(o => o.OpcionD)
            .Include(o => o.OpcionE)
            .Include(o => o.Horario)
            .Where(o => o.MenuId == menu.Id)
            .OrderBy(o => o.DiaSemana)
            .ThenBy(o => o.Horario != null ? o.Horario.Orden : int.MaxValue)
            .ThenBy(o => o.HorarioId)
            .ToListAsync();
        var diasVm = MapDias(diasDb, null);
        var opciones = await _db.Opciones.Include(o => o.Horarios).OrderBy(o => o.Nombre).ToListAsync();
        var horariosActivos = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        var horarioIdsPermitidos = new HashSet<int>();
        if (sucursalSel != null)
        {
            var ids = await _db.SucursalesHorarios
                .Where(sh => sh.SucursalId == sucursalSel)
                .Select(sh => sh.HorarioId)
                .Distinct()
                .ToListAsync();
            horarioIdsPermitidos = ids.ToHashSet();
        }
        else if (empresaSel != null)
        {
            var ids = await _db.SucursalesHorarios
                .Include(sh => sh.Sucursal)
                .Where(sh => sh.Sucursal != null && sh.Sucursal.EmpresaId == empresaSel)
                .Select(sh => sh.HorarioId)
                .Distinct()
                .ToListAsync();
            horarioIdsPermitidos = ids.ToHashSet();
        }
        var horariosPermitidos = horariosActivos
            .Where(h => horarioIdsPermitidos.Contains(h.Id))
            .ToList();
        var adicionalesIds = await _db.MenusAdicionales
            .AsNoTracking()
            .Where(a => a.MenuId == menu.Id)
            .Select(a => a.OpcionId)
            .ToListAsync();
        // Calcular empleados con menÃº completo (toda la semana)
        var opcionIds = diasDb.Select(d => d.Id).ToList();
        var completos = await _db.RespuestasFormulario
            .Where(r => opcionIds.Contains(r.OpcionMenuId))
            .GroupBy(r => r.EmpleadoId)
            .Where(g => g.Count() >= diasVm.Count)
            .CountAsync();
        var sinRespuestas = !await _db.RespuestasFormulario.AnyAsync(r => opcionIds.Contains(r.OpcionMenuId));

        var vm = new MenuEdicionVM
        {
            MenuId = menu.Id,
            FechaInicio = inicio,
            FechaTermino = fin,
            Opciones = opciones,
            EncuestaCerrada = encuestaCerrada,
            FechaCierreAutomatica = fechaCierreAuto,
            FechaCierreManual = menu.FechaCierreManual,
            EmpleadosCompletos = completos,
            EmpresaId = empresaSel,
            SucursalId = sucursalSel,
            Empresas = empresas.Select(e => (e.Id, e.Nombre)),
            Sucursales = sucursalesAll.Select(s => (id: s.Id, nombre: s.Nombre, empresaId: s.EmpresaId)),
            EmpresaNombre = empresas.FirstOrDefault(e => e.Id == empresaSel)?.Nombre,
            SucursalNombre = sucursalesAll.FirstOrDefault(s => s.Id == sucursalSel)?.Nombre,
            Dias = diasVm,
            PuedeEliminarEncuesta = sinRespuestas,
            AdicionalesIds = adicionalesIds,
            HorariosPermitidos = horariosPermitidos
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> VistaPrevia(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId)
    {
        var vm = await BuildMenuPreviewVmAsync(inicio, fin, empresaId, sucursalId);
        return View("VistaPrevia", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarDiasVistaPrevia(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId, [FromForm] List<int>? opcionMenuIds)
    {
        if (opcionMenuIds == null || opcionMenuIds.Count == 0)
        {
            TempData["Info"] = "Selecciona al menos un dia para quitar.";
            return RedirectToAction(nameof(VistaPrevia), new { inicio, fin, empresaId, sucursalId });
        }

        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId);
        var ids = opcionMenuIds.Distinct().ToList();
        var dias = await _db.OpcionesMenu
            .Where(o => o.MenuId == menu.Id && ids.Contains(o.Id))
            .ToListAsync();

        if (dias.Count == 0)
        {
            TempData["Info"] = "No se encontraron dias validos para quitar.";
            return RedirectToAction(nameof(VistaPrevia), new { inicio, fin, empresaId, sucursalId });
        }

        foreach (var dia in dias)
        {
            dia.DiaCerrado = true;
            dia.OpcionIdA = null;
            dia.OpcionIdB = null;
            dia.OpcionIdC = null;
            dia.OpcionIdD = null;
            dia.OpcionIdE = null;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Se quitaron {dias.Count} dia(s) del menu.";
        return RedirectToAction(nameof(VistaPrevia), new { inicio, fin, empresaId, sucursalId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RehabilitarDiasVistaPrevia(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId, [FromForm] List<int>? opcionMenuIds)
    {
        if (opcionMenuIds == null || opcionMenuIds.Count == 0)
        {
            TempData["Info"] = "Selecciona al menos un dia para re-habilitar.";
            return RedirectToAction(nameof(VistaPrevia), new { inicio, fin, empresaId, sucursalId });
        }

        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId);
        var ids = opcionMenuIds.Distinct().ToList();
        var dias = await _db.OpcionesMenu
            .Where(o => o.MenuId == menu.Id && ids.Contains(o.Id))
            .ToListAsync();

        if (dias.Count == 0)
        {
            TempData["Info"] = "No se encontraron dias validos para re-habilitar.";
            return RedirectToAction(nameof(VistaPrevia), new { inicio, fin, empresaId, sucursalId });
        }

        foreach (var dia in dias)
            dia.DiaCerrado = false;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Se re-habilitaron {dias.Count} dia(s).";
        return RedirectToAction(nameof(VistaPrevia), new { inicio, fin, empresaId, sucursalId });
    }

    [HttpGet]
    public async Task<IActionResult> ExportMenuCsv(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId)
    {
        var vm = await BuildMenuPreviewVmAsync(inicio, fin, empresaId, sucursalId);
        var export = BuildMenuPreviewExport(vm);
        var bytes = ExportHelper.BuildCsv(export.Headers, export.Rows);
        return File(bytes, "text/csv", $"menu-{inicio:yyyyMMdd}-{fin:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportMenuExcel(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId)
    {
        var vm = await BuildMenuPreviewVmAsync(inicio, fin, empresaId, sucursalId);
        var export = BuildMenuPreviewExport(vm);
        var bytes = ExportHelper.BuildExcel("Menu", export.Headers, export.Rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"menu-{inicio:yyyyMMdd}-{fin:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportMenuPdf(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId)
    {
        var vm = await BuildMenuPreviewVmAsync(inicio, fin, empresaId, sucursalId);
        var export = BuildMenuPreviewExport(vm);
        var pdf = ExportHelper.BuildPdf("Menu", export.Headers, export.Rows);
        return File(pdf, "application/pdf", $"menu-{inicio:yyyyMMdd}-{fin:yyyyMMdd}.pdf");
    }

    private async Task<MenuPreviewVM> BuildMenuPreviewVmAsync(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId)
    {
        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId);
        var opciones = await _db.OpcionesMenu
            .Include(o => o.OpcionA)
            .Include(o => o.OpcionB)
            .Include(o => o.OpcionC)
            .Include(o => o.OpcionD)
            .Include(o => o.OpcionE)
            .Include(o => o.Horario)
            .Where(o => o.MenuId == menu.Id)
            .OrderBy(o => o.DiaSemana)
            .ThenBy(o => o.Horario != null ? o.Horario.Orden : int.MaxValue)
            .ToListAsync();

        var empresaLookupId = empresaId ?? menu.EmpresaId;
        if (empresaLookupId == null && menu.SucursalId != null)
            empresaLookupId = await _db.Sucursales.Where(s => s.Id == menu.SucursalId).Select(s => (int?)s.EmpresaId).FirstOrDefaultAsync();

        var empresaNombre = empresaLookupId != null
            ? await _db.Empresas.Where(e => e.Id == empresaLookupId).Select(e => e.Nombre).FirstOrDefaultAsync()
            : null;

        var sucursalLookupId = sucursalId ?? menu.SucursalId;
        var sucursalNombre = sucursalLookupId != null
            ? await _db.Sucursales.Where(s => s.Id == sucursalLookupId).Select(s => s.Nombre).FirstOrDefaultAsync()
            : null;

        var dias = opciones.Select(o => new MenuPreviewDiaVM
        {
            OpcionMenuId = o.Id,
            DiaSemana = o.DiaSemana,
            HorarioNombre = o.Horario?.Nombre ?? "General",
            HorarioOrden = o.Horario?.Orden ?? int.MaxValue,
            DiaCerrado = o.DiaCerrado,
            A = o.OpcionA?.Nombre,
            B = o.OpcionB?.Nombre,
            C = o.OpcionC?.Nombre,
            D = o.OpcionD?.Nombre,
            E = o.OpcionE?.Nombre,
            OpcionesMaximas = o.OpcionesMaximas == 0 ? 3 : o.OpcionesMaximas
        }).ToList();

        return new MenuPreviewVM
        {
            FechaInicio = inicio,
            FechaTermino = fin,
            EmpresaId = empresaLookupId,
            SucursalId = sucursalLookupId,
            EmpresaNombre = empresaNombre,
            FilialNombre = sucursalNombre,
            OrigenScope = sucursalLookupId != null ? "Filial" : "Empresa",
            Dias = dias.Where(d => !d.DiaCerrado).ToList(),
            DiasCerrados = dias.Where(d => d.DiaCerrado).ToList()
        };
    }

    private static (IReadOnlyList<string> Headers, List<IReadOnlyList<string>> Rows) BuildMenuPreviewExport(MenuPreviewVM vm)
    {
        var max = vm.Dias.Count == 0 ? 3 : vm.Dias.Max(d => d.OpcionesMaximas <= 0 ? 3 : d.OpcionesMaximas);
        var headers = new List<string> { "Dia", "Horario" };
        for (var i = 0; i < max; i++)
        {
            headers.Add($"Plato {(char)('A' + i)}");
        }

        var rows = vm.Dias
            .OrderBy(d => d.HorarioOrden)
            .ThenBy(d => d.DiaSemana)
            .Select(d =>
            {
                var row = new List<string>
                {
                    System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(d.DiaSemana),
                    d.HorarioNombre ?? "General"
                };
                var opciones = new[] { d.A, d.B, d.C, d.D, d.E };
                for (var i = 0; i < max; i++)
                {
                    row.Add(opciones.Length > i ? (opciones[i] ?? string.Empty) : string.Empty);
                }
                return (IReadOnlyList<string>)row;
            }).ToList();

        return (headers, rows);
    }

    [HttpGet]
    public async Task<IActionResult> CrearProxima()
    {
        var menu = await _menuService.GetOrCreateMenuSemanaSiguienteAsync();
        TempData["Success"] = "Se aseguro el menu de la proxima semana.";
        var fecha = menu.FechaInicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
        return RedirectToAction(nameof(Administrar), new { fecha });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ImportarSelecciones(DateTime? fecha, int? empresaId)
    {
        var baseDate = fecha?.Date ?? DateTime.UtcNow.Date;
        if (fecha == null && baseDate.DayOfWeek == DayOfWeek.Sunday)
            baseDate = baseDate.AddDays(1);
        int diff = ((int)baseDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var lunes = baseDate.AddDays(-diff);
        var inicio = DateOnly.FromDateTime(lunes);
        var fin = inicio.AddDays(4);

        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        return View(new MenuImportVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ImportarMenu(DateTime? fecha, int? empresaId, int? sucursalId)
    {
        var baseDate = fecha?.Date ?? DateTime.UtcNow.Date;
        if (fecha == null && baseDate.DayOfWeek == DayOfWeek.Sunday)
            baseDate = baseDate.AddDays(1);
        int diff = ((int)baseDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var lunes = baseDate.AddDays(-diff);
        var inicio = DateOnly.FromDateTime(lunes);
        var fin = inicio.AddDays(4);

        return View(new MenuSemanalImportVM
        {
            Inicio = inicio,
            Fin = fin,
            EmpresaId = empresaId,
            SucursalId = sucursalId
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult PlantillaMenuExcel()
    {
        using var workbook = new XLWorkbook();
        var menuSheet = workbook.Worksheets.Add("Menu");
        menuSheet.Cell(1, 1).Value = "Dia";
        menuSheet.Cell(1, 2).Value = "Horario";
        menuSheet.Cell(1, 3).Value = "OpcionesMaximas";
        menuSheet.Cell(1, 4).Value = "Plato A";
        menuSheet.Cell(1, 5).Value = "Plato B";
        menuSheet.Cell(1, 6).Value = "Plato C";
        menuSheet.Cell(1, 7).Value = "Plato D";
        menuSheet.Cell(1, 8).Value = "Plato E";
        menuSheet.Cell(1, 9).Value = "DiaCerrado";
        menuSheet.Cell(2, 1).Value = "Lunes";
        menuSheet.Cell(2, 2).Value = "Almuerzo";
        menuSheet.Cell(2, 3).Value = 3;
        menuSheet.Cell(2, 4).Value = "P0001";
        menuSheet.Cell(2, 5).Value = "P0002";
        menuSheet.Cell(2, 6).Value = "P0003";
        menuSheet.Cell(2, 9).Value = "No";
        menuSheet.Columns().AdjustToContents();

        var adicionalesSheet = workbook.Worksheets.Add("Adicionales");
        adicionalesSheet.Cell(1, 1).Value = "Codigo";
        adicionalesSheet.Cell(1, 2).Value = "Nombre";
        adicionalesSheet.Cell(2, 1).Value = "A0001";
        adicionalesSheet.Cell(2, 2).Value = "Refresco 16 onz";
        adicionalesSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plantilla-menu.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportarMenu(MenuSemanalImportVM model)
    {
        if (model.Archivo == null || model.Archivo.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Debes seleccionar un archivo Excel.");
            return View(model);
        }

        if (model.Inicio > model.Fin)
        {
            ModelState.AddModelError(string.Empty, "El rango de fechas es invalido.");
            return View(model);
        }

        var errores = new List<string>();
        var advertencias = new List<string>();
        var filasProcesadas = 0;
        var diasActualizados = 0;
        var adicionalesActualizados = 0;

        var menu = await _menuService.GetOrCreateMenuAsync(model.Inicio, model.Fin, model.EmpresaId, model.SucursalId);
        if (_cierre.EstaCerrada(menu))
        {
            ModelState.AddModelError(string.Empty, "No se puede importar: el menu esta cerrado.");
            return View(model);
        }

        var horariosActivos = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        var horarioIdsPermitidos = new HashSet<int>();
        if (model.SucursalId != null)
        {
            var ids = await _db.SucursalesHorarios
                .Where(sh => sh.SucursalId == model.SucursalId)
                .Select(sh => sh.HorarioId)
                .Distinct()
                .ToListAsync();
            horarioIdsPermitidos = ids.ToHashSet();
        }
        else if (model.EmpresaId != null)
        {
            var ids = await _db.SucursalesHorarios
                .Include(sh => sh.Sucursal)
                .Where(sh => sh.Sucursal != null && sh.Sucursal.EmpresaId == model.EmpresaId)
                .Select(sh => sh.HorarioId)
                .Distinct()
                .ToListAsync();
            horarioIdsPermitidos = ids.ToHashSet();
        }
        var horariosPermitidos = horariosActivos.Where(h => horarioIdsPermitidos.Contains(h.Id)).ToList();
        if (horariosPermitidos.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No hay horarios configurados para esta empresa o filial.");
            return View(model);
        }

        var opciones = await _db.Opciones.Where(o => !o.Borrado).ToListAsync();
        var opcionesPorCodigo = opciones
            .Where(o => !string.IsNullOrWhiteSpace(o.Codigo))
            .ToDictionary(o => NormalizeKey(o.Codigo!), o => o);
        var opcionesPorNombre = opciones
            .Where(o => !string.IsNullOrWhiteSpace(o.Nombre))
            .ToDictionary(o => NormalizeKey(o.Nombre), o => o);

        var opcionesMenu = await _db.OpcionesMenu
            .Where(o => o.MenuId == menu.Id)
            .ToListAsync();

        var opcionesPorDiaHorario = opcionesMenu
            .GroupBy(o => new { o.DiaSemana, o.HorarioId })
            .ToDictionary(g => (g.Key.DiaSemana, g.Key.HorarioId), g => g.First());

        using var stream = model.Archivo.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var menuSheet = workbook.Worksheets.FirstOrDefault(ws => NormalizeKey(ws.Name) == "menu") ?? workbook.Worksheets.FirstOrDefault();
        if (menuSheet == null)
        {
            ModelState.AddModelError(string.Empty, "El archivo Excel no contiene hojas.");
            return View(model);
        }

        var headerRow = menuSheet.FirstRowUsed();
        if (headerRow == null)
        {
            ModelState.AddModelError(string.Empty, "No se encontro la fila de encabezados.");
            return View(model);
        }

        var headerMap = BuildHeaderMap(headerRow);
        if (!TryGetColumn(headerMap, out var colDia, "dia")
            || !TryGetColumn(headerMap, out var colHorario, "horario"))
        {
            ModelState.AddModelError(string.Empty, "Faltan columnas requeridas: Dia, Horario.");
            return View(model);
        }

        if (!TryGetColumn(headerMap, out var colMax, "opcionesmaximas")
            && !TryGetColumn(headerMap, out colMax, "cantidad", "opcion"))
        {
            ModelState.AddModelError(string.Empty, "Falta la columna OpcionesMaximas.");
            return View(model);
        }

        if (!TryGetOptionColumn(headerMap, out var colA, 1)
            || !TryGetOptionColumn(headerMap, out var colB, 2)
            || !TryGetOptionColumn(headerMap, out var colC, 3)
            || !TryGetOptionColumn(headerMap, out var colD, 4)
            || !TryGetOptionColumn(headerMap, out var colE, 5))
        {
            ModelState.AddModelError(string.Empty, "Faltan columnas de platos: Plato A, Plato B, Plato C, Plato D, Plato E.");
            return View(model);
        }

        TryGetColumn(headerMap, out var colCerrado, "diacerrado");

        var firstDataRow = headerRow.RowBelow();
        var lastRow = menuSheet.LastRowUsed()?.RowNumber() ?? firstDataRow.RowNumber();
        var filas = new List<MenuImportRow>();
        var seenKeys = new HashSet<string>();

        for (var rowNumber = firstDataRow.RowNumber(); rowNumber <= lastRow; rowNumber++)
        {
            var row = menuSheet.Row(rowNumber);
            if (row.IsEmpty()) continue;
            filasProcesadas++;
            var errorCountBefore = errores.Count;

            var diaRaw = row.Cell(colDia).GetString().Trim();
            var horarioRaw = row.Cell(colHorario).GetString().Trim();
            if (string.IsNullOrWhiteSpace(diaRaw) && string.IsNullOrWhiteSpace(horarioRaw))
                continue;

            if (!TryParseDia(diaRaw, out var dia))
            {
                AddLimitedError(errores, $"Fila {rowNumber}: dia '{diaRaw}' no reconocido.");
                continue;
            }

            var horarioKey = NormalizeKey(horarioRaw);
            var horario = horariosPermitidos.FirstOrDefault(h => NormalizeKey(h.Nombre) == horarioKey);
            if (horario == null)
            {
                AddLimitedError(errores, $"Fila {rowNumber}: horario '{horarioRaw}' no disponible para este alcance.");
                continue;
            }

            var rowKey = $"{dia}-{horario.Id}";
            if (!seenKeys.Add(rowKey))
            {
                AddLimitedError(errores, $"Fila {rowNumber}: dia/horario duplicado.");
                continue;
            }

            var maxRaw = row.Cell(colMax).GetString().Trim();
            if (!TryParseOpcionesMaximas(maxRaw, out var max))
            {
                AddLimitedError(errores, $"Fila {rowNumber}: OpcionesMaximas invalido ('{maxRaw}').");
                continue;
            }

            var rawA = row.Cell(colA).GetString().Trim();
            var rawB = row.Cell(colB).GetString().Trim();
            var rawC = row.Cell(colC).GetString().Trim();
            var rawD = row.Cell(colD).GetString().Trim();
            var rawE = row.Cell(colE).GetString().Trim();

            var diaCerrado = colCerrado > 0 && TryParseBool(row.Cell(colCerrado).GetString(), out var closed) && closed;
            if (!diaCerrado && AnyTokenFeriado(rawA, rawB, rawC, rawD, rawE))
                diaCerrado = true;

            if (diaCerrado && AnyTokenNoVacio(rawA, rawB, rawC, rawD, rawE))
            {
                AddLimitedError(errores, $"Fila {rowNumber}: dia cerrado no puede tener platos asignados.");
                continue;
            }

            var optionIds = new int?[5];
            var rawValues = new[] { rawA, rawB, rawC, rawD, rawE };
            for (var i = 0; i < rawValues.Length; i++)
            {
                var raw = rawValues[i];
                if (IsNullToken(raw))
                {
                    optionIds[i] = null;
                    continue;
                }

                var opcion = FindOpcionFromCell(opcionesPorCodigo, opcionesPorNombre, raw);
                if (opcion == null)
                {
                    AddLimitedError(errores, $"Fila {rowNumber}: plato '{raw}' no existe.");
                    break;
                }
                if (opcion.EsAdicional)
                {
                    AddLimitedError(errores, $"Fila {rowNumber}: '{raw}' es adicional y no puede ir en menu.");
                    break;
                }
                optionIds[i] = opcion.Id;
            }

            if (errores.Count > errorCountBefore)
                continue;

            if (!opcionesPorDiaHorario.TryGetValue((dia, horario.Id), out var opcionMenu))
            {
                AddLimitedError(errores, $"Fila {rowNumber}: no existe configuracion para {diaRaw} / {horarioRaw}.");
                continue;
            }

            filas.Add(new MenuImportRow
            {
                RowNumber = rowNumber,
                DiaSemana = dia,
                HorarioId = horario.Id,
                OpcionesMaximas = max,
                DiaCerrado = diaCerrado,
                Opciones = optionIds,
                OpcionMenuId = opcionMenu.Id
            });
        }

        var adicionalesSheet = workbook.Worksheets.FirstOrDefault(ws =>
            NormalizeKey(ws.Name) == "adicionales" || NormalizeKey(ws.Name) == "opcionales");
        var adicionalesIds = new List<int>();
        if (adicionalesSheet != null)
        {
            var adicionalesHeader = adicionalesSheet.FirstRowUsed();
            if (adicionalesHeader != null)
            {
                var map = BuildHeaderMap(adicionalesHeader);
                if (TryGetColumn(map, out var colCodigo, "codigo")
                    || TryGetColumn(map, out colCodigo, "cod"))
                {
                    TryGetColumn(map, out var colNombre, "nombre");
                    var first = adicionalesHeader.RowBelow();
                    var last = adicionalesSheet.LastRowUsed()?.RowNumber() ?? first.RowNumber();
                    for (var rowNumber = first.RowNumber(); rowNumber <= last; rowNumber++)
                    {
                        var row = adicionalesSheet.Row(rowNumber);
                        if (row.IsEmpty()) continue;
                        var codigoRaw = colCodigo > 0 ? row.Cell(colCodigo).GetString().Trim() : string.Empty;
                        var nombreRaw = colNombre > 0 ? row.Cell(colNombre).GetString().Trim() : string.Empty;
                        if (string.IsNullOrWhiteSpace(codigoRaw) && string.IsNullOrWhiteSpace(nombreRaw))
                            continue;

                        var opcion = FindOpcionFromCell(opcionesPorCodigo, opcionesPorNombre, !string.IsNullOrWhiteSpace(codigoRaw) ? codigoRaw : nombreRaw);
                        if (opcion == null)
                        {
                            AddLimitedError(errores, $"Adicional fila {rowNumber}: no existe '{codigoRaw} {nombreRaw}'.");
                            continue;
                        }
                        if (!opcion.EsAdicional)
                        {
                            AddLimitedError(errores, $"Adicional fila {rowNumber}: '{opcion.Nombre}' no esta marcado como adicional.");
                            continue;
                        }
                        if (!adicionalesIds.Contains(opcion.Id))
                            adicionalesIds.Add(opcion.Id);
                    }
                }
            }
        }

        if (errores.Count > 0)
        {
            model.Errores = errores;
            model.Advertencias = advertencias;
            return View(model);
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var cerrarIds = new List<int>();
            foreach (var row in filas)
            {
                var diaMenu = opcionesMenu.First(x => x.Id == row.OpcionMenuId);
                diaMenu.OpcionesMaximas = Math.Clamp(row.OpcionesMaximas, 1, 5);
                diaMenu.DiaCerrado = row.DiaCerrado;

                if (row.DiaCerrado)
                {
                    diaMenu.OpcionIdA = null;
                    diaMenu.OpcionIdB = null;
                    diaMenu.OpcionIdC = null;
                    diaMenu.OpcionIdD = null;
                    diaMenu.OpcionIdE = null;
                    cerrarIds.Add(diaMenu.Id);
                }
                else
                {
                    diaMenu.OpcionIdA = row.Opciones.ElementAtOrDefault(0);
                    diaMenu.OpcionIdB = row.Opciones.ElementAtOrDefault(1);
                    diaMenu.OpcionIdC = row.Opciones.ElementAtOrDefault(2);
                    diaMenu.OpcionIdD = row.Opciones.ElementAtOrDefault(3);
                    diaMenu.OpcionIdE = row.Opciones.ElementAtOrDefault(4);
                }
            }

            if (cerrarIds.Count > 0)
            {
                var respuestas = await _db.RespuestasFormulario.Where(r => cerrarIds.Contains(r.OpcionMenuId)).ToListAsync();
                if (respuestas.Count > 0)
                    _db.RespuestasFormulario.RemoveRange(respuestas);
            }

            if (adicionalesSheet != null)
            {
                var actuales = await _db.MenusAdicionales.Where(a => a.MenuId == menu.Id).ToListAsync();
                if (actuales.Count > 0)
                    _db.MenusAdicionales.RemoveRange(actuales);
                var toAdd = adicionalesIds.Select(id => new MenuAdicional { MenuId = menu.Id, OpcionId = id }).ToList();
                if (toAdd.Count > 0)
                    await _db.MenusAdicionales.AddRangeAsync(toAdd);
                adicionalesActualizados = toAdd.Count;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            diasActualizados = filas.Count;
            model.FilasProcesadas = filasProcesadas;
            model.DiasActualizados = diasActualizados;
            model.AdicionalesActualizados = adicionalesActualizados;
            TempData["Success"] = $"Menu importado. Dias actualizados: {diasActualizados}.";
            return RedirectToAction(nameof(Administrar), new { fecha = model.Inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId = model.EmpresaId, sucursalId = model.SucursalId });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            model.Errores = new List<string> { "No se pudo importar el menu: " + ex.Message };
            model.Advertencias = advertencias;
            return View(model);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportarSelecciones(MenuImportVM model)
    {
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        if (model.Archivo == null || model.Archivo.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Debes seleccionar un archivo Excel.");
            return View(model);
        }
        if (model.Inicio > model.Fin)
        {
            ModelState.AddModelError(string.Empty, "El rango de fechas es invalido.");
            return View(model);
        }

        var errores = new List<string>();
        var advertencias = new List<string>();
        int totalFilas = 0;
        int filasProcesadas = 0;
        int empleadosCreados = 0;
        int usuariosCreados = 0;
        int seleccionesGuardadas = 0;
        int seleccionesSaltadas = 0;

        var empresaFiltro = model.EmpresaId;
        var empleados = await _db.Empleados
            .Include(e => e.Sucursal)
            .Where(e => !e.Borrado && (empresaFiltro == null || (e.Sucursal != null && e.Sucursal.EmpresaId == empresaFiltro)))
            .ToListAsync();
        var empleadosPorCodigo = empleados
            .Where(e => !string.IsNullOrWhiteSpace(e.Codigo))
            .ToDictionary(e => NormalizeKey(e.Codigo!), e => e);
        var empleadosPorNombre = empleados
            .Where(e => !string.IsNullOrWhiteSpace(e.Nombre))
            .ToDictionary(e => NormalizeKey(e.Nombre!), e => e);

        var localizaciones = await _db.Localizaciones
            .Include(l => l.Sucursal).ThenInclude(s => s!.Empresa)
            .Where(l => empresaFiltro == null || l.EmpresaId == empresaFiltro)
            .ToListAsync();
        var localizacionesPorNombre = localizaciones
            .GroupBy(l => NormalizeKey(l.Nombre))
            .ToDictionary(g => g.Key, g => g.ToList());

        var sucursales = await _db.Sucursales
            .Include(s => s.Empresa)
            .Where(s => empresaFiltro == null || s.EmpresaId == empresaFiltro)
            .ToListAsync();
        var empresasIds = await _db.Empresas.Select(e => e.Id).ToListAsync();
        var empresasSet = empresasIds.ToHashSet();
        var sucursalesPorNombre = sucursales
            .GroupBy(s => NormalizeKey(s.Nombre))
            .ToDictionary(g => g.Key, g => g.ToList());

        var horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        var horarioAlmuerzo = horarios.FirstOrDefault(h => NormalizeKey(h.Nombre).Contains("almuerzo")) ?? horarios.FirstOrDefault();
        if (horarioAlmuerzo == null)
        {
            ModelState.AddModelError(string.Empty, "No hay horarios activos para asignar el almuerzo.");
            return View(model);
        }

        var menuCache = new Dictionary<(int EmpresaId, int? SucursalId), Menu>();
        var opcionesCache = new Dictionary<int, Dictionary<DayOfWeek, OpcionMenu>>();

        using var stream = model.Archivo.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            ModelState.AddModelError(string.Empty, "El archivo Excel no contiene hojas.");
            return View(model);
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow == null)
        {
            ModelState.AddModelError(string.Empty, "No se encontro la fila de encabezados.");
            return View(model);
        }

        var headerMap = BuildHeaderMap(headerRow);
        if (!TryGetColumn(headerMap, out var colNombre, "nombre", "completo")
            || !TryGetColumn(headerMap, out var colCodigo, "codigo")
            || !TryGetColumn(headerMap, out var colLocalidad, "localidad")
            || !TryGetColumn(headerMap, out var colLunes, "opcion", "lunes")
            || !TryGetColumn(headerMap, out var colMartes, "opcion", "martes")
            || !TryGetColumn(headerMap, out var colMiercoles, "opcion", "miercoles")
            || !TryGetColumn(headerMap, out var colJueves, "opcion", "jueves")
            || !TryGetColumn(headerMap, out var colViernes, "opcion", "viernes"))
        {
            ModelState.AddModelError(string.Empty, "Faltan columnas requeridas. Verifica los encabezados del archivo.");
            return View(model);
        }
        TryGetColumn(headerMap, out var colCodigoUni, "codigouni");
        TryGetColumn(headerMap, out var colContrasena, "contrasena");
        TryGetColumn(headerMap, out var colHoraAlmuerzo, "hora", "almuerzo");
        if (colHoraAlmuerzo == 0)
            TryGetColumn(headerMap, out colHoraAlmuerzo, "turno", "almuerzo");
        if (colHoraAlmuerzo == 0)
            TryGetColumn(headerMap, out colHoraAlmuerzo, "hora", "turno");

        var firstDataRow = headerRow.RowBelow();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRow.RowNumber();

        for (var rowNumber = firstDataRow.RowNumber(); rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty()) continue;
            totalFilas++;

            var nombre = row.Cell(colNombre).GetString().Trim();
            var codigo = row.Cell(colCodigo).GetString().Trim();
            var localidad = row.Cell(colLocalidad).GetString().Trim();
            var codigoUni = colCodigoUni > 0 ? row.Cell(colCodigoUni).GetString().Trim() : string.Empty;
            var contrasena = colContrasena > 0 ? row.Cell(colContrasena).GetString().Trim() : string.Empty;
            TimeOnly? horaAlmuerzo = null;
            if (colHoraAlmuerzo > 0)
            {
                var horaCell = row.Cell(colHoraAlmuerzo);
                var rawHora = horaCell.GetString().Trim();
                if (TryParseHoraAlmuerzoCell(horaCell, out var horaParsed, out var hadRange, out var rawText))
                {
                    horaAlmuerzo = horaParsed;
                    if (hadRange)
                        AddLimitedWarning(advertencias, $"Fila {rowNumber}: turno de almuerzo '{rawText}' interpretado como {horaParsed:HH\\:mm} (hora inicial).");
                }
                else if (!string.IsNullOrWhiteSpace(rawHora))
                {
                    AddLimitedWarning(advertencias, $"Fila {rowNumber}: turno de almuerzo '{rawHora}' no es valido; se usara la hora por defecto.");
                }
            }

            if (string.IsNullOrWhiteSpace(nombre) && string.IsNullOrWhiteSpace(codigo))
            {
                seleccionesSaltadas++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(localidad))
            {
                AddLimitedError(errores, $"Fila {rowNumber}: falta la localidad.");
                seleccionesSaltadas++;
                continue;
            }

            var localizacion = FindLocalizacion(localizacionesPorNombre, localidad, advertencias, rowNumber);
            var sucursal = localizacion?.Sucursal ?? FindSucursal(sucursalesPorNombre, localidad, advertencias, rowNumber);
            if (localizacion == null)
            {
                var locKey = NormalizeKey(localidad);
                if (locKey == NormalizeKey("Torre Universal"))
                {
                    sucursal = sucursales.FirstOrDefault(s =>
                        NormalizeKey(s.Nombre) == NormalizeKey("SEGUROS UNIVERSAL"));
                }
            }
            if (localizacion != null && sucursal == null)
            {
                var empresaIdLoc = localizacion.EmpresaId;
                sucursal = sucursales
                    .Where(s => s.EmpresaId == empresaIdLoc)
                    .OrderBy(s => s.Nombre)
                    .FirstOrDefault();
            }
            if (localizacion == null && sucursal != null)
            {
                if (!empresasSet.Contains(sucursal.EmpresaId))
                {
                    AddLimitedError(errores, $"Fila {rowNumber}: la filial '{sucursal.Nombre}' no tiene empresa valida.");
                    seleccionesSaltadas++;
                    continue;
                }
                localizacion = new Localizacion
                {
                    Nombre = localidad,
                    SucursalId = null,
                    EmpresaId = sucursal.EmpresaId
                };
                await _db.Localizaciones.AddAsync(localizacion);
                await _db.SaveChangesAsync();
                localizaciones.Add(localizacion);
                var key = NormalizeKey(localidad);
                if (!localizacionesPorNombre.TryGetValue(key, out var list))
                {
                    list = new List<Localizacion>();
                    localizacionesPorNombre[key] = list;
                }
                list.Add(localizacion);
            }
            if (sucursal == null)
            {
                AddLimitedError(errores, $"Fila {rowNumber}: no se encontro filial para la localidad '{localidad}'.");
                seleccionesSaltadas++;
                continue;
            }

            var empresaId = sucursal.EmpresaId;
            var empleado = FindEmpleado(empleadosPorCodigo, empleadosPorNombre, codigo, nombre);
            if (empleado == null)
            {
                empleado = new Empleado
                {
                    Nombre = nombre,
                    Codigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo,
                    SucursalId = sucursal.Id,
                    EsSubsidiado = true,
                    Estado = EmpleadoEstado.Habilitado
                };
                await _db.Empleados.AddAsync(empleado);
                await _db.SaveChangesAsync();
                empleadosCreados++;
                if (!string.IsNullOrWhiteSpace(empleado.Codigo))
                    empleadosPorCodigo[NormalizeKey(empleado.Codigo)] = empleado;
                if (!string.IsNullOrWhiteSpace(empleado.Nombre))
                    empleadosPorNombre[NormalizeKey(empleado.Nombre)] = empleado;
            }
            else if (empleado.SucursalId == 0)
            {
                empleado.SucursalId = sucursal.Id;
                await _db.SaveChangesAsync();
            }
            else if (empleado.SucursalId != sucursal.Id)
            {
                var hasSucursal = await _db.EmpleadosSucursales
                    .AnyAsync(es => es.EmpleadoId == empleado.Id && es.SucursalId == sucursal.Id);
                if (!hasSucursal)
                {
                    await _db.EmpleadosSucursales.AddAsync(new EmpleadoSucursal
                    {
                        EmpleadoId = empleado.Id,
                        SucursalId = sucursal.Id
                    });
                    await _db.SaveChangesAsync();
                    AddLimitedWarning(advertencias, $"Fila {rowNumber}: empleado '{empleado.Nombre ?? empleado.Codigo}' agregado a filial {sucursal.Nombre}.");
                }
            }

            if (localizacion != null)
            {
                var existsLoc = await _db.EmpleadosLocalizaciones
                    .AnyAsync(el => el.EmpleadoId == empleado.Id && el.LocalizacionId == localizacion.Id);
                if (!existsLoc)
                {
                    await _db.EmpleadosLocalizaciones.AddAsync(new EmpleadoLocalizacion
                    {
                        EmpleadoId = empleado.Id,
                        LocalizacionId = localizacion.Id
                    });
                    await _db.SaveChangesAsync();
                }
            }

            if (!string.IsNullOrWhiteSpace(codigo) || !string.IsNullOrWhiteSpace(codigoUni) || !string.IsNullOrWhiteSpace(contrasena))
            {
                var userResult = await EnsureIdentityUserAsync(empleado, codigo, codigoUni, contrasena);
                if (userResult.Created)
                    usuariosCreados++;
                else if (!string.IsNullOrWhiteSpace(userResult.Error))
                    AddLimitedWarning(advertencias, $"Fila {rowNumber}: {userResult.Error}");
            }

            var menuKey = (empresaId, (int?)sucursal.Id);
            if (!menuCache.TryGetValue(menuKey, out var menu))
            {
                menu = await _menuService.GetEffectiveMenuForSemanaAsync(model.Inicio, model.Fin, empresaId, sucursal.Id);
                menuCache[menuKey] = menu;
            }

            if (!opcionesCache.TryGetValue(menu.Id, out var opcionesDia))
            {
                var opcionesMenu = await EnsureOpcionesAlmuerzoAsync(menu.Id, horarioAlmuerzo.Id);
                opcionesDia = BuildOpcionesPorDia(opcionesMenu, horarioAlmuerzo.Id);
                opcionesCache[menu.Id] = opcionesDia;
            }

            var selections = new (int Col, DayOfWeek Dia)[]
            {
                (colLunes, DayOfWeek.Monday),
                (colMartes, DayOfWeek.Tuesday),
                (colMiercoles, DayOfWeek.Wednesday),
                (colJueves, DayOfWeek.Thursday),
                (colViernes, DayOfWeek.Friday)
            };

            foreach (var (col, dia) in selections)
            {
                var valor = row.Cell(col).GetString().Trim();
                var seleccion = ParseSeleccion(valor);
                if (seleccion == null)
                {
                    if (!string.IsNullOrWhiteSpace(valor))
                        seleccionesSaltadas++;
                    continue;
                }

                if (!opcionesDia.TryGetValue(dia, out var opcionMenu))
                {
                    AddLimitedError(errores, $"Fila {rowNumber}: no existe menu para {dia}.");
                    seleccionesSaltadas++;
                    continue;
                }

                if (!SeleccionDisponible(opcionMenu, seleccion.Value))
                {
                    var fallback = GetFallbackSeleccion(opcionMenu);
                    if (fallback == null)
                    {
                        AddLimitedError(errores, $"Fila {rowNumber}: no hay opciones disponibles para {dia}.");
                        seleccionesSaltadas++;
                        continue;
                    }
                    seleccion = fallback;
                }

                try
                {
                    await _menuService.RegistrarSeleccionAsync(
                        empleado.Id,
                        opcionMenu.Id,
                        seleccion.Value,
                        sucursal.Id,
                        localizacion?.Id,
                        null,
                        horaAlmuerzo);
                    seleccionesGuardadas++;
                }
                catch (Exception ex)
                {
                    AddLimitedError(errores, $"Fila {rowNumber}: {ex.Message}");
                    seleccionesSaltadas++;
                }
            }

            filasProcesadas++;
        }

        model.TotalFilas = totalFilas;
        model.FilasProcesadas = filasProcesadas;
        model.EmpleadosCreados = empleadosCreados;
        model.UsuariosCreados = usuariosCreados;
        model.SeleccionesGuardadas = seleccionesGuardadas;
        model.SeleccionesSaltadas = seleccionesSaltadas;
        model.Errores = errores;
        model.Advertencias = advertencias;

        if (errores.Count == 0)
            TempData["Success"] = $"Importacion completada: {seleccionesGuardadas} selecciones guardadas.";
        else
            TempData["Error"] = $"Importacion completada con {errores.Count} errores.";
        return View(model);
    }

    // POST: Guardar todos los dias en un solo envio
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(MenuEdicionVM model)
    {
        if (model == null) return RedirectToAction(nameof(Administrar));

        // Reglas: si ya existe al menos un empleado con 100% de respuestas, bloquear cambios
        var menuDb = await _menuService.GetOrCreateMenuAsync(model.FechaInicio, model.FechaTermino, model.EmpresaId, model.SucursalId);
        if (_cierre.EstaCerrada(menuDb))
        {
            TempData["Error"] = "El menu esta cerrado. Reabrelo para poder modificarlo.";
            var fechaBlock = model.FechaInicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            return RedirectToAction(nameof(Administrar), new { fecha = fechaBlock, empresaId = model.EmpresaId, sucursalId = model.SucursalId });
        }
        // Parseo manual confiable desde el formulario para evitar problemas de binding
        var form = HttpContext.Request?.Form;
        if (form != null)
        {
            _logger.LogInformation("[POST Guardar] Keys recibidas: {Count} -> {Keys}", form.Keys.Count, string.Join(", ", form.Keys));
            var idxs = new HashSet<int>();
            foreach (var k in form.Keys)
            {
                var mId = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.OpcionMenuId$", RegexOptions.None, RegexTimeout);
                var mA = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.A$", RegexOptions.None, RegexTimeout);
                var mB = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.B$", RegexOptions.None, RegexTimeout);
                var mC = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.C$", RegexOptions.None, RegexTimeout);
                var mD = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.D$", RegexOptions.None, RegexTimeout);
                var mE = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.E$", RegexOptions.None, RegexTimeout);
                var mMax = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.OpcionesMaximas$", RegexOptions.None, RegexTimeout);
                var mCerrado = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.DiaCerrado$", RegexOptions.None, RegexTimeout);
                if (mId.Success && int.TryParse(mId.Groups[1].Value, out var i1)) idxs.Add(i1);
                if (mA.Success && int.TryParse(mA.Groups[1].Value, out var i2)) idxs.Add(i2);
                if (mB.Success && int.TryParse(mB.Groups[1].Value, out var i3)) idxs.Add(i3);
                if (mC.Success && int.TryParse(mC.Groups[1].Value, out var i4)) idxs.Add(i4);
                if (mD.Success && int.TryParse(mD.Groups[1].Value, out var i5)) idxs.Add(i5);
                if (mE.Success && int.TryParse(mE.Groups[1].Value, out var i6)) idxs.Add(i6);
                if (mMax.Success && int.TryParse(mMax.Groups[1].Value, out var i7)) idxs.Add(i7);
                if (mCerrado.Success && int.TryParse(mCerrado.Groups[1].Value, out var i8)) idxs.Add(i8);
            }
            var dias = new List<DiaEdicion>();
            foreach (var i in idxs.OrderBy(x => x))
            {
                _ = int.TryParse(form[$"Dias[{i}].OpcionMenuId"], out var omId);
                int? ga = int.TryParse(form[$"Dias[{i}].A"], out var a) ? a : null;
                int? gb = int.TryParse(form[$"Dias[{i}].B"], out var b) ? b : null;
                int? gc = int.TryParse(form[$"Dias[{i}].C"], out var c) ? c : null;
                int? gd = int.TryParse(form[$"Dias[{i}].D"], out var d) ? d : null;
                int? ge = int.TryParse(form[$"Dias[{i}].E"], out var eVal) ? eVal : null;
                int max = 3;
                if (int.TryParse(form[$"Dias[{i}].OpcionesMaximas"], out var maxForm))
                    max = Math.Clamp(maxForm, 1, 5);
                var cerradoVal = form[$"Dias[{i}].DiaCerrado"].FirstOrDefault();
                var diaCerrado = !string.IsNullOrWhiteSpace(cerradoVal)
                    && (string.Equals(cerradoVal, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(cerradoVal, "on", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(cerradoVal, "1", StringComparison.OrdinalIgnoreCase));
                dias.Add(new DiaEdicion { OpcionMenuId = omId, A = ga, B = gb, C = gc, D = gd, E = ge, OpcionesMaximas = max, DiaCerrado = diaCerrado });
                _logger.LogInformation("[POST Guardar] idx={Idx} omId={Om} A={A} B={B} C={C} D={D} E={E} Max={Max}", i, omId, ga, gb, gc, gd, ge, max);
            }
            model.Dias = dias;
            _logger.LogInformation("[POST Guardar] DÃ­as reconstruidos: {Dias}", model.Dias.Count);
        }
        if (model.Dias == null || !model.Dias.Any())
        {
            TempData["Info"] = "No hay cambios para guardar.";
            _logger.LogWarning("[POST Guardar] Sin dÃ­as despuÃ©s de parseo. FormKeys={Count}", form?.Keys.Count ?? 0);
            return RedirectToAction(nameof(Administrar), new { fecha = model.FechaInicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId = model.EmpresaId, sucursalId = model.SucursalId });
        }

        var diasDb = await _db.OpcionesMenu
            .Include(o => o.Horario)
            .Where(x => x.MenuId == menuDb.Id)
            .OrderBy(x => x.DiaSemana)
            .ThenBy(x => x.Horario != null ? x.Horario.Orden : int.MaxValue)
            .ThenBy(x => x.HorarioId)
            .ToListAsync();

        // Si faltan OpcionMenuId, mapeamos por Ã­ndice contra los dÃ­as del menÃº del rango
        if (model.Dias.Any(d => d.OpcionMenuId == 0))
        {
            if (diasDb.Count == model.Dias.Count)
            {
                for (int i = 0; i < model.Dias.Count; i++)
                {
                    if (model.Dias[i].OpcionMenuId == 0)
                        model.Dias[i].OpcionMenuId = diasDb[i].Id;
                }
                _logger.LogInformation("[POST Guardar] OpcionMenuId completados por Ã­ndice.");
            }
            else
            {
                _logger.LogWarning("[POST Guardar] No coincide el conteo de dÃ­as: modelo={ModelCount}, db={DbCount}", model.Dias.Count, diasDb.Count);
            }
        }

        var overrides = model.Dias;
        model.Dias = MapDias(diasDb, overrides);

        try
        {
            var diasDict = diasDb.ToDictionary(x => x.Id);
            foreach (var d in model.Dias)
            {
                if (!diasDict.TryGetValue(d.OpcionMenuId, out var dia)) continue;
                var max = d.OpcionesMaximas <= 0 ? 3 : Math.Clamp(d.OpcionesMaximas, 1, 5);
                dia.OpcionesMaximas = max;
                dia.OpcionIdA = max >= 1 ? d.A : null;
                dia.OpcionIdB = max >= 2 ? d.B : null;
                dia.OpcionIdC = max >= 3 ? d.C : null;
                dia.OpcionIdD = max >= 4 ? d.D : null;
                dia.OpcionIdE = max >= 5 ? d.E : null;
                dia.DiaCerrado = d.DiaCerrado;
            }
            var diasCerrados = model.Dias.Where(d => d.DiaCerrado).Select(d => d.OpcionMenuId).ToList();
            if (diasCerrados.Count > 0)
            {
                var respuestasCerrar = await _db.RespuestasFormulario
                    .Where(r => diasCerrados.Contains(r.OpcionMenuId))
                    .ToListAsync();
                if (respuestasCerrar.Count > 0)
                    _db.RespuestasFormulario.RemoveRange(respuestasCerrar);
            }
            // Actualizar adicionales fijos del menÃº (se cobran 100% al empleado)
            var nuevosIds = (model.AdicionalesIds ?? new List<int>())
                .Where(x => x != 0)
                .Distinct()
                .ToHashSet();
            var actuales = await _db.MenusAdicionales.Where(a => a.MenuId == menuDb.Id).ToListAsync();
            var actualesSet = actuales.Select(a => a.OpcionId).ToHashSet();
            var toRemove = actuales.Where(a => !nuevosIds.Contains(a.OpcionId)).ToList();
            if (toRemove.Count > 0) _db.MenusAdicionales.RemoveRange(toRemove);
            var toAdd = nuevosIds.Except(actualesSet).Select(id => new MenuAdicional { MenuId = menuDb.Id, OpcionId = id }).ToList();
            if (toAdd.Count > 0) await _db.MenusAdicionales.AddRangeAsync(toAdd);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Menu actualizado correctamente.";

            if (model.AplicarATodasFiliales && model.EmpresaId != null && model.SucursalId == null)
            {
                var sucursalIds = await _db.Sucursales
                    .Where(s => s.EmpresaId == model.EmpresaId)
                    .Select(s => s.Id)
                    .ToListAsync();
                if (sucursalIds.Count > 0)
                {
                    var (updated, skipped) = await _cloneService.CloneEmpresaMenuToSucursalesAsync(
                        model.FechaInicio,
                        model.FechaTermino,
                        model.EmpresaId.Value,
                        sucursalIds);
                    TempData["Success"] = $"Menu actualizado. Filiales actualizadas: {updated}. Omitidas por bloqueo: {skipped}.";
                }
            }
            _logger.LogInformation("[POST Guardar] Cambios guardados OK.");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "No se pudo guardar el menu: " + ex.Message;
            _logger.LogError(ex, "[POST Guardar] Error al guardar.");
        }
        // Determinar semana a partir del menÃº del primer dÃ­a (redirecciÃ³n estable)
        var fecha = menuDb.FechaInicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
        return RedirectToAction(nameof(Administrar), new { fecha, empresaId = model.EmpresaId, sucursalId = model.SucursalId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarEncuesta(DateOnly inicio, int? empresaId, int? sucursalId)
    {
        var fin = inicio.AddDays(4);
        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId);
        menu.EncuestaCerradaManualmente = true;
        menu.FechaCierreManual = DateTime.UtcNow;
        menu.EncuestaReabiertaManualmente = false;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Menu cerrado manualmente.";
        return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReabrirEncuesta(DateOnly inicio, int? empresaId, int? sucursalId)
    {
        var fin = inicio.AddDays(4);
        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId);
        menu.EncuestaCerradaManualmente = false;
        menu.FechaCierreManual = null;
        menu.EncuestaReabiertaManualmente = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Menu reabierto.";
        return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarEncuesta(DateOnly inicio, int? empresaId, int? sucursalId)
    {
        var fin = inicio.AddDays(4);
        var menu = await _menuService.FindMenuAsync(inicio, fin, empresaId, sucursalId);
        if (menu == null)
        {
            TempData["Info"] = "No existe menu para ese rango.";
            return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
        }

        var opcionIds = await _db.OpcionesMenu.Where(o => o.MenuId == menu.Id).Select(o => o.Id).ToListAsync();
        var tieneRespuestas = await _db.RespuestasFormulario.AnyAsync(r => opcionIds.Contains(r.OpcionMenuId));
        if (tieneRespuestas)
        {
            TempData["Error"] = "No se puede eliminar: ya existen respuestas registradas.";
            return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
        }

        var opciones = await _db.OpcionesMenu.Where(o => o.MenuId == menu.Id).ToListAsync();
        _db.OpcionesMenu.RemoveRange(opciones);
        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Menu eliminado (sin respuestas).";
        return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
    }

    private static List<DiaEdicion> MapDias(IEnumerable<OpcionMenu> diasDb, IEnumerable<DiaEdicion>? overrides)
    {
        var overrideDict = overrides?
            .Where(d => d.OpcionMenuId != 0)
            .ToDictionary(d => d.OpcionMenuId, d => d);

        var list = new List<DiaEdicion>();
        foreach (var dia in diasDb)
        {
            DiaEdicion? custom = null;
            if (overrideDict != null)
            {
                overrideDict.TryGetValue(dia.Id, out custom);
            }
            var nombreHorario = dia.Horario?.Nombre;
            list.Add(new DiaEdicion
            {
                OpcionMenuId = dia.Id,
                DiaSemana = dia.DiaSemana,
                HorarioId = dia.HorarioId,
                HorarioNombre = string.IsNullOrWhiteSpace(nombreHorario) ? "Horario general" : nombreHorario!,
                HorarioOrden = dia.Horario?.Orden ?? int.MaxValue,
                DiaCerrado = custom?.DiaCerrado ?? dia.DiaCerrado,
                A = custom?.A ?? dia.OpcionIdA,
                B = custom?.B ?? dia.OpcionIdB,
                C = custom?.C ?? dia.OpcionIdC,
                D = custom?.D ?? dia.OpcionIdD,
                E = custom?.E ?? dia.OpcionIdE,
                OpcionesMaximas = Math.Clamp(custom?.OpcionesMaximas ?? (dia.OpcionesMaximas == 0 ? 3 : dia.OpcionesMaximas), 1, 5)
            });
        }
        return list;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>();
        var lastCell = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var col = 1; col <= lastCell; col++)
        {
            var raw = headerRow.Cell(col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var key = NormalizeKey(raw);
            map[key] = col;
        }
        return map;
    }

    private static bool TryGetColumn(Dictionary<string, int> headerMap, out int col, params string[] tokens)
    {
        if (tokens.Length == 0)
        {
            col = 0;
            return false;
        }
        foreach (var entry in headerMap)
        {
            var match = true;
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (!entry.Key.Contains(token))
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                col = entry.Value;
                return true;
            }
        }
        col = 0;
        return false;
    }

    private static string NormalizeKey(string value)
    {
        var cleaned = RemoveDiacritics(value ?? string.Empty).ToLowerInvariant();
        var sb = new StringBuilder(cleaned.Length);
        foreach (var ch in cleaned)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Empleado? FindEmpleado(Dictionary<string, Empleado> porCodigo, Dictionary<string, Empleado> porNombre, string codigo, string nombre)
    {
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var key = NormalizeKey(codigo);
            if (porCodigo.TryGetValue(key, out var encontrado))
                return encontrado;
        }
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var key = NormalizeKey(nombre);
            if (porNombre.TryGetValue(key, out var encontrado))
                return encontrado;
        }
        return null;
    }

    private static Localizacion? FindLocalizacion(Dictionary<string, List<Localizacion>> lookup, string nombre, List<string> advertencias, int rowNumber)
    {
        var key = NormalizeKey(nombre);
        if (!lookup.TryGetValue(key, out var list) || list.Count == 0)
            return null;
        if (list.Count > 1)
            AddLimitedWarning(advertencias, $"Fila {rowNumber}: hay {list.Count} localizaciones con nombre '{nombre}', se usara la primera.");
        return list[0];
    }

    private static Sucursal? FindSucursal(Dictionary<string, List<Sucursal>> lookup, string nombre, List<string> advertencias, int rowNumber)
    {
        var key = NormalizeKey(nombre);
        if (!lookup.TryGetValue(key, out var list) || list.Count == 0)
            return null;
        if (list.Count > 1)
            AddLimitedWarning(advertencias, $"Fila {rowNumber}: hay {list.Count} filiales con nombre '{nombre}', se usara la primera.");
        return list[0];
    }

    private static Dictionary<DayOfWeek, OpcionMenu> BuildOpcionesPorDia(IEnumerable<OpcionMenu> opciones, int horarioPreferidoId)
    {
        var result = new Dictionary<DayOfWeek, OpcionMenu>();
        foreach (var dia in opciones.GroupBy(o => o.DiaSemana))
        {
            var preferido = dia.FirstOrDefault(o => o.HorarioId == horarioPreferidoId);
            result[dia.Key] = preferido ?? dia.OrderBy(o => o.Horario?.Orden ?? int.MaxValue).First();
        }
        return result;
    }

    private static char? ParseSeleccion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = NormalizeKey(value);
        if (normalized.Contains("ninguno")
            || normalized == "no"
            || normalized.StartsWith("no ")
            || normalized.Contains("feriado"))
            return null;
        if (normalized.Contains("opcion"))
            normalized = normalized.Replace("opcion", string.Empty);
        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var slot))
        {
            return slot switch
            {
                1 => 'A',
                2 => 'B',
                3 => 'C',
                4 => 'D',
                5 => 'E',
                _ => null
            };
        }
        return normalized switch
        {
            "a" => 'A',
            "b" => 'B',
            "c" => 'C',
            "d" => 'D',
            "e" => 'E',
            _ => null
        };
    }

    private static bool TryParseHoraAlmuerzoCell(IXLCell cell, out TimeOnly hora, out bool hadRange, out string rawText)
    {
        hadRange = false;
        rawText = cell.GetString().Trim();
        if (cell.TryGetValue<DateTime>(out var dt))
        {
            hora = TimeOnly.FromDateTime(dt);
            return true;
        }
        if (cell.TryGetValue<TimeSpan>(out var ts))
        {
            hora = TimeOnly.FromTimeSpan(ts);
            return true;
        }
        if (string.IsNullOrWhiteSpace(rawText))
        {
            hora = default;
            return false;
        }
        return TryParseHoraAlmuerzo(rawText, out hora, out hadRange);
    }

    private static bool TryParseHoraAlmuerzo(string raw, out TimeOnly hora, out bool hadRange)
    {
        hora = default;
        hadRange = false;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var matches = Regex.Matches(raw, @"(?<!\d)(\d{1,2})(?:[:.](\d{2}))?\s*([ap]\.?m\.?|m\.?m\.?)?(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        if (matches.Count == 0) return false;

        if (matches.Count > 1 && Regex.IsMatch(raw, @"\b(a|hasta|al|–|-|—)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            hadRange = true;

        var match = matches[0];
        if (!int.TryParse(match.Groups[1].Value, out var hours)) return false;
        var minutes = 0;
        if (match.Groups[2].Success && !int.TryParse(match.Groups[2].Value, out minutes))
            return false;

        if (minutes < 0 || minutes > 59) return false;
        var meridian = match.Groups[3].Success ? match.Groups[3].Value.Replace(".", string.Empty).ToLowerInvariant() : string.Empty;
        if (meridian.StartsWith("p") && hours < 12) hours += 12;
        if (meridian.StartsWith("a") && hours == 12) hours = 0;
        if (meridian.StartsWith("m") && hours < 12) hours += 12;
        if (hours < 0 || hours > 23) return false;

        hora = new TimeOnly(hours, minutes);
        return true;
    }

    private static readonly DayOfWeek[] WorkingDays = new[]
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    };

    private async Task<List<OpcionMenu>> EnsureOpcionesAlmuerzoAsync(int menuId, int horarioId)
    {
        var opcionesMenu = await _db.OpcionesMenu
            .Where(o => o.MenuId == menuId && o.HorarioId == horarioId)
            .ToListAsync();

        if (opcionesMenu.Count == 0)
        {
            foreach (var dia in WorkingDays)
            {
                _db.OpcionesMenu.Add(new OpcionMenu
                {
                    MenuId = menuId,
                    DiaSemana = dia,
                    HorarioId = horarioId,
                    OpcionesMaximas = 3
                });
            }
            await _db.SaveChangesAsync();

            opcionesMenu = await _db.OpcionesMenu
                .Where(o => o.MenuId == menuId && o.HorarioId == horarioId)
                .ToListAsync();
        }

        var yaConfigurado = opcionesMenu.Any(o =>
            o.OpcionIdA != null || o.OpcionIdB != null || o.OpcionIdC != null || o.OpcionIdD != null || o.OpcionIdE != null);
        if (yaConfigurado)
            return opcionesMenu;

        var opciones = await GetOrCreateOpcionesParaHorarioAsync(horarioId);
        if (opciones.Count == 0)
            return opcionesMenu;

        foreach (var opcionMenu in opcionesMenu)
        {
            opcionMenu.OpcionIdA = opciones.ElementAtOrDefault(0)?.Id;
            opcionMenu.OpcionIdB = opciones.ElementAtOrDefault(1)?.Id;
            opcionMenu.OpcionIdC = opciones.ElementAtOrDefault(2)?.Id;
            opcionMenu.OpcionIdD = opciones.ElementAtOrDefault(3)?.Id;
            opcionMenu.OpcionIdE = opciones.ElementAtOrDefault(4)?.Id;
            opcionMenu.OpcionesMaximas = opciones.Count;
        }

        await _db.SaveChangesAsync();
        return opcionesMenu;
    }

    private async Task<List<Opcion>> GetOrCreateOpcionesParaHorarioAsync(int horarioId)
    {
        var opcionIds = await _db.OpcionesHorarios
            .Where(oh => oh.HorarioId == horarioId)
            .Select(oh => oh.OpcionId)
            .Distinct()
            .ToListAsync();

        var query = _db.Opciones.Where(o => !o.Borrado);
        if (opcionIds.Count > 0)
            query = query.Where(o => opcionIds.Contains(o.Id));

        var opciones = await query.OrderBy(o => o.Nombre).Take(5).ToListAsync();
        if (opciones.Count > 0)
            return opciones;

        var placeholders = new List<Opcion>();
        for (var i = 1; i <= 5; i++)
        {
            var nombre = $"Opcion {i}";
            var existente = await _db.Opciones.FirstOrDefaultAsync(o => o.Nombre == nombre);
            if (existente == null)
            {
                existente = new Opcion
                {
                    Nombre = nombre,
                    Descripcion = nombre,
                    EsSubsidiado = true,
                    LlevaItbis = true,
                    Costo = 0m,
                    Precio = 0m
                };
                _db.Opciones.Add(existente);
                await _db.SaveChangesAsync();
            }

            var horarioExiste = await _db.OpcionesHorarios
                .AnyAsync(oh => oh.OpcionId == existente.Id && oh.HorarioId == horarioId);
            if (!horarioExiste)
            {
                _db.OpcionesHorarios.Add(new OpcionHorario
                {
                    OpcionId = existente.Id,
                    HorarioId = horarioId
                });
                await _db.SaveChangesAsync();
            }

            placeholders.Add(existente);
        }

        return placeholders;
    }

    private static bool SeleccionDisponible(OpcionMenu opcionMenu, char seleccion)
    {
        return seleccion switch
        {
            'A' => opcionMenu.OpcionIdA != null || opcionMenu.OpcionA != null,
            'B' => opcionMenu.OpcionIdB != null || opcionMenu.OpcionB != null,
            'C' => opcionMenu.OpcionIdC != null || opcionMenu.OpcionC != null,
            'D' => opcionMenu.OpcionIdD != null || opcionMenu.OpcionD != null,
            'E' => opcionMenu.OpcionIdE != null || opcionMenu.OpcionE != null,
            _ => false
        };
    }

    private static char? GetFallbackSeleccion(OpcionMenu opcionMenu)
    {
        if (opcionMenu.OpcionIdA != null || opcionMenu.OpcionA != null) return 'A';
        if (opcionMenu.OpcionIdB != null || opcionMenu.OpcionB != null) return 'B';
        if (opcionMenu.OpcionIdC != null || opcionMenu.OpcionC != null) return 'C';
        if (opcionMenu.OpcionIdD != null || opcionMenu.OpcionD != null) return 'D';
        if (opcionMenu.OpcionIdE != null || opcionMenu.OpcionE != null) return 'E';
        return null;
    }

    private sealed class MenuImportRow
    {
        public int RowNumber { get; set; }
        public DayOfWeek DiaSemana { get; set; }
        public int HorarioId { get; set; }
        public int OpcionesMaximas { get; set; }
        public bool DiaCerrado { get; set; }
        public int OpcionMenuId { get; set; }
        public int?[] Opciones { get; set; } = new int?[5];
    }

    private static bool TryParseDia(string value, out DayOfWeek day)
    {
        var key = NormalizeKey(value);
        switch (key)
        {
            case "lunes":
            case "monday":
                day = DayOfWeek.Monday; return true;
            case "martes":
            case "tuesday":
                day = DayOfWeek.Tuesday; return true;
            case "miercoles":
            case "wednesday":
                day = DayOfWeek.Wednesday; return true;
            case "jueves":
            case "thursday":
                day = DayOfWeek.Thursday; return true;
            case "viernes":
            case "friday":
                day = DayOfWeek.Friday; return true;
        }
        day = default;
        return false;
    }

    private static bool TryGetOptionColumn(Dictionary<string, int> headerMap, out int col, int index)
    {
        var letter = ((char)('a' + (index - 1))).ToString();
        var targets = new[]
        {
            $"opcion{index}",
            $"opcion{letter}",
            $"plato{index}",
            $"plato{letter}"
        };
        foreach (var entry in headerMap)
        {
            foreach (var target in targets)
            {
                if (entry.Key.Contains(target))
                {
                    col = entry.Value;
                    return true;
                }
            }
        }
        col = 0;
        return false;
    }

    private static bool TryParseOpcionesMaximas(string raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!int.TryParse(raw, out var parsed)) return false;
        if (parsed < 1 || parsed > 5) return false;
        value = parsed;
        return true;
    }

    private static bool TryParseBool(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var key = NormalizeKey(raw);
        if (key == "1" || key == "si" || key == "true" || key == "cerrado" || key == "feriado")
        {
            value = true;
            return true;
        }
        if (key == "0" || key == "no" || key == "false")
        {
            value = false;
            return true;
        }
        return false;
    }

    private static bool IsNullToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var key = NormalizeKey(raw);
        return key == "ninguno" || key == "no" || key == "sinplato" || key == "feriado";
    }

    private static bool AnyTokenFeriado(params string[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (NormalizeKey(value) == "feriado")
                return true;
        }
        return false;
    }

    private static bool AnyTokenNoVacio(params string[] values)
    {
        foreach (var value in values)
        {
            if (!IsNullToken(value))
                return true;
        }
        return false;
    }

    private static Opcion? FindOpcionFromCell(Dictionary<string, Opcion> porCodigo, Dictionary<string, Opcion> porNombre, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var key = NormalizeKey(raw);
        if (porCodigo.TryGetValue(key, out var encontrado))
            return encontrado;
        if (porNombre.TryGetValue(key, out encontrado))
            return encontrado;
        if (key.All(char.IsDigit) && porCodigo.TryGetValue($"p{key}", out encontrado))
            return encontrado;
        return null;
    }

    private async Task<(bool Created, string? Error)> EnsureIdentityUserAsync(Empleado empleado, string codigo, string codigoUni, string contrasena)
    {
        var username = BuildUsernameFromCodigo(codigo, codigoUni, empleado.Codigo);

        if (string.IsNullOrWhiteSpace(username))
            return (false, "Usuario sin codigo para crear credenciales.");

        var existing = await _userManager.FindByNameAsync(username);
        if (existing != null)
        {
            if (existing.EmpleadoId != null && existing.EmpleadoId != empleado.Id)
                return (false, $"El usuario {username} ya esta asignado a otro empleado.");

            if (existing.EmpleadoId == null)
            {
                if (await _db.Set<ApplicationUser>().AnyAsync(u => u.EmpleadoId == empleado.Id && u.Id != existing.Id))
                    return (false, "El empleado ya tiene usuario asignado.");

                var empresaId = await _db.Sucursales
                    .Where(s => s.Id == empleado.SucursalId)
                    .Select(s => (int?)s.EmpresaId)
                    .FirstOrDefaultAsync();
                existing.EmpleadoId = empleado.Id;
                existing.EmpresaId = empresaId;
                await _userManager.UpdateAsync(existing);
            }

            if (!await _userManager.IsInRoleAsync(existing, "Empleado"))
                await _userManager.AddToRoleAsync(existing, "Empleado");
            await EnsureMustChangePasswordClaimAsync(existing);

            return (false, null);
        }

        if (await _db.Set<ApplicationUser>().AnyAsync(u => u.EmpleadoId == empleado.Id))
            return (false, "El empleado ya tiene usuario asignado.");

        var sucursalData = await _db.Sucursales
            .Where(s => s.Id == empleado.SucursalId)
            .Select(s => new { s.EmpresaId })
            .FirstOrDefaultAsync();

        var usuario = new ApplicationUser
        {
            UserName = username,
            EmailConfirmed = true,
            EmpleadoId = empleado.Id,
            EmpresaId = sucursalData?.EmpresaId
        };

        var password = !string.IsNullOrWhiteSpace(contrasena)
            ? contrasena.Trim()
            : BuildDefaultPassword(username, empleado.Id);

        var result = await _userManager.CreateAsync(usuario, password);
        if (!result.Succeeded)
            return (false, $"No se pudo crear el usuario: {string.Join("; ", result.Errors.Select(e => e.Description))}");

        if (!await _userManager.IsInRoleAsync(usuario, "Empleado"))
            await _userManager.AddToRoleAsync(usuario, "Empleado");
        await EnsureMustChangePasswordClaimAsync(usuario);

        return (true, null);
    }

    private async Task EnsureMustChangePasswordClaimAsync(ApplicationUser user)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        if (!claims.Any(c => c.Type == "must_change_password" && c.Value == "1"))
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("must_change_password", "1"));
    }

    private static string BuildDefaultPassword(string codigo, int empleadoId)
    {
        var codigoToken = NormalizeKey(codigo);
        if (string.IsNullOrWhiteSpace(codigoToken))
            codigoToken = empleadoId.ToString().PadLeft(6, '0');
        var password = $"UNI{codigoToken}@";
        if (password.Length < 6)
            password = password.PadRight(6, '0');
        return password;
    }

    private static string BuildUsernameFromCodigo(string codigo, string codigoUni, string? codigoEmpleado)
    {
        var candidate = !string.IsNullOrWhiteSpace(codigo)
            ? codigo.Trim()
            : (!string.IsNullOrWhiteSpace(codigoEmpleado) ? codigoEmpleado.Trim() : string.Empty);
        if (string.IsNullOrWhiteSpace(candidate) && !string.IsNullOrWhiteSpace(codigoUni))
            candidate = codigoUni.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;
        var normalized = NormalizeKey(candidate);
        if (normalized.StartsWith("uni"))
            normalized = normalized.Substring(3);
        if (normalized.StartsWith("u"))
            normalized = normalized.Substring(1);
        return normalized.ToUpperInvariant();
    }

    private static void AddLimitedError(List<string> errores, string mensaje)
    {
        if (errores.Count < ImportErrorLimit)
            errores.Add(mensaje);
    }

    private static void AddLimitedWarning(List<string> advertencias, string mensaje)
    {
        if (advertencias.Count < ImportWarningLimit)
            advertencias.Add(mensaje);
    }
}




