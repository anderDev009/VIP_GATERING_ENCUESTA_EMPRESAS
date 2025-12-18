using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.WebUI.Controllers;

// Panel simple para armar menú semanal
[Authorize(Roles = "Admin,Empresa")]
public class MenuController : Controller
{
    private readonly AppDbContext _db;
    private readonly IMenuService _menuService;
    private readonly ILogger<MenuController> _logger;
    private readonly IMenuCloneService _cloneService;
    private readonly ICurrentUserService _current;
    private readonly IEncuestaCierreService _cierre;
    public MenuController(AppDbContext db, IMenuService menuService, ILogger<MenuController> logger, IMenuCloneService cloneService, ICurrentUserService current, IEncuestaCierreService cierre)
    { _db = db; _menuService = menuService; _logger = logger; _cloneService = cloneService; _current = current; _cierre = cierre; }

    // Clientes con buscador para administrar menús
    [HttpGet]
    public async Task<IActionResult> Clientes(string? q)
    {
        var query = _db.Empresas.AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _current.EmpresaId;
            if (empresaId != null)
                query = query.Where(e => e.Id == empresaId);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(e => e.Nombre.ToLower().Contains(ql) || (e.Rnc != null && e.Rnc.ToLower().Contains(ql)));
        }
        var items = await query
            .OrderBy(e => e.Nombre)
            .Select(e => new MenuClientesVM.Item
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Rnc = e.Rnc,
                Sucursales = _db.Sucursales.Count(s => s.EmpresaId == e.Id)
            }).ToListAsync();

        var vm = new MenuClientesVM { Q = q, Clientes = items };
        return View(vm);
    }

    // Listado de sucursales agrupadas por cliente (o filtradas por cliente)
    [HttpGet]
    public async Task<IActionResult> Sucursales(Guid? empresaId, string? q)
    {
        var sucQuery = _db.Sucursales.Include(s => s.Empresa).AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _current.EmpresaId;
            sucQuery = sucQuery.Where(s => s.EmpresaId == currentEmpresaId);
            if (empresaId == null) empresaId = currentEmpresaId;
        }
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

    // GET: copiar menú del cliente a múltiples sucursales
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Copiar(Guid empresaId, DateTime? fecha)
    {
        if (User.IsInRole("Empresa") && _current.EmpresaId != empresaId) return Forbid();
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

    // POST: copiar menú del cliente a sucursales seleccionadas
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Copiar(Guid empresaId, DateOnly inicio, DateOnly fin, [FromForm] List<Guid> sucursalIds)
    {
        if (User.IsInRole("Empresa") && _current.EmpresaId != empresaId) return Forbid();
        if (sucursalIds == null || sucursalIds.Count == 0)
        {
            TempData["Info"] = "Debe seleccionar al menos una sucursal.";
            return RedirectToAction(nameof(Copiar), new { empresaId, fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd") });
        }
        var (updated, skipped) = await _cloneService.CloneEmpresaMenuToSucursalesAsync(inicio, fin, empresaId, sucursalIds);
        TempData["Success"] = $"Menú copiado. Actualizadas: {updated}. Omitidas por bloqueo: {skipped}.";
        return RedirectToAction(nameof(Clientes));
    }

    // GET: Administrar menú por rango (selección de semana)
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Administrar(DateTime? fecha, Guid? empresaId, Guid? sucursalId, string? alcance)
    {
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _current.EmpresaId;
            if (empresaId == null) empresaId = currentEmpresaId;
            if (empresaId != currentEmpresaId) return Forbid();
        }
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

        // Cargar catálogos
        var empresas = await _db.Empresas.Select(e => new { e.Id, e.Nombre }).ToListAsync();
        var sucursales = await _db.Sucursales.Select(s => new { s.Id, s.Nombre, s.EmpresaId }).ToListAsync();

        Guid? empresaSel = empresaId ?? empresas.FirstOrDefault()?.Id;
        Guid? sucursalSel = sucursalId;
        if (!string.IsNullOrEmpty(alcance))
        {
            if (alcance.Equals("empresa", StringComparison.OrdinalIgnoreCase)) sucursalSel = null;
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
        var opciones = await _db.Opciones.OrderBy(o => o.Nombre).ToListAsync();
        var adicionalesIds = await _db.MenusAdicionales
            .AsNoTracking()
            .Where(a => a.MenuId == menu.Id)
            .Select(a => a.OpcionId)
            .ToListAsync();
        // Calcular empleados con encuesta completa (toda la semana)
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
            Sucursales = sucursales.Select(s => (s.Id, s.Nombre)),
            EmpresaNombre = empresas.FirstOrDefault(e => e.Id == empresaSel)?.Nombre,
            SucursalNombre = sucursales.FirstOrDefault(s => s.Id == sucursalSel)?.Nombre,
            Dias = diasVm,
            PuedeEliminarEncuesta = sinRespuestas,
            AdicionalesIds = adicionalesIds
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> CrearProxima()
    {
        var menu = await _menuService.GetOrCreateMenuSemanaSiguienteAsync();
        TempData["Success"] = "Se aseguró el menú de la próxima semana.";
        var fecha = menu.FechaInicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
        return RedirectToAction(nameof(Administrar), new { fecha });
    }

    // POST: Guardar todos los días en un solo envío
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(MenuEdicionVM model)
    {
        if (model == null) return RedirectToAction(nameof(Administrar));
        if (User.IsInRole("Empresa") && _current.EmpresaId != model.EmpresaId) return Forbid();

        // Reglas: si ya existe al menos un empleado con 100% de respuestas, bloquear cambios
        var menuDb = await _menuService.GetOrCreateMenuAsync(model.FechaInicio, model.FechaTermino, model.EmpresaId, model.SucursalId);
        if (_cierre.EstaCerrada(menuDb))
        {
            TempData["Error"] = "La encuesta está cerrada. Reábrela para poder modificar el menú.";
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
                var mId = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.OpcionMenuId$");
                var mA = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.A$");
                var mB = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.B$");
                var mC = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.C$");
                var mD = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.D$");
                var mE = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.E$");
                var mMax = Regex.Match(k, "^Dias\\[(\\d+)\\]\\.OpcionesMaximas$");
                if (mId.Success && int.TryParse(mId.Groups[1].Value, out var i1)) idxs.Add(i1);
                if (mA.Success && int.TryParse(mA.Groups[1].Value, out var i2)) idxs.Add(i2);
                if (mB.Success && int.TryParse(mB.Groups[1].Value, out var i3)) idxs.Add(i3);
                if (mC.Success && int.TryParse(mC.Groups[1].Value, out var i4)) idxs.Add(i4);
                if (mD.Success && int.TryParse(mD.Groups[1].Value, out var i5)) idxs.Add(i5);
                if (mE.Success && int.TryParse(mE.Groups[1].Value, out var i6)) idxs.Add(i6);
                if (mMax.Success && int.TryParse(mMax.Groups[1].Value, out var i7)) idxs.Add(i7);
            }
            var dias = new List<DiaEdicion>();
            foreach (var i in idxs.OrderBy(x => x))
            {
                _ = Guid.TryParse(form[$"Dias[{i}].OpcionMenuId"], out var omId);
                Guid? ga = Guid.TryParse(form[$"Dias[{i}].A"], out var a) ? a : null;
                Guid? gb = Guid.TryParse(form[$"Dias[{i}].B"], out var b) ? b : null;
                Guid? gc = Guid.TryParse(form[$"Dias[{i}].C"], out var c) ? c : null;
                Guid? gd = Guid.TryParse(form[$"Dias[{i}].D"], out var d) ? d : null;
                Guid? ge = Guid.TryParse(form[$"Dias[{i}].E"], out var eVal) ? eVal : null;
                int max = 3;
                if (int.TryParse(form[$"Dias[{i}].OpcionesMaximas"], out var maxForm))
                    max = Math.Clamp(maxForm, 1, 5);
                dias.Add(new DiaEdicion { OpcionMenuId = omId, A = ga, B = gb, C = gc, D = gd, E = ge, OpcionesMaximas = max });
                _logger.LogInformation("[POST Guardar] idx={Idx} omId={Om} A={A} B={B} C={C} D={D} E={E} Max={Max}", i, omId, ga, gb, gc, gd, ge, max);
            }
            model.Dias = dias;
            _logger.LogInformation("[POST Guardar] Días reconstruidos: {Dias}", model.Dias.Count);
        }
        if (model.Dias == null || !model.Dias.Any())
        {
            TempData["Info"] = "No hay cambios para guardar.";
            _logger.LogWarning("[POST Guardar] Sin días después de parseo. FormKeys={Count}", form?.Keys.Count ?? 0);
            return RedirectToAction(nameof(Administrar), new { fecha = model.FechaInicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId = model.EmpresaId, sucursalId = model.SucursalId });
        }

        var diasDb = await _db.OpcionesMenu
            .Include(o => o.Horario)
            .Where(x => x.MenuId == menuDb.Id)
            .OrderBy(x => x.DiaSemana)
            .ThenBy(x => x.Horario != null ? x.Horario.Orden : int.MaxValue)
            .ThenBy(x => x.HorarioId)
            .ToListAsync();

        // Si faltan OpcionMenuId, mapeamos por índice contra los días del menú del rango
        if (model.Dias.Any(d => d.OpcionMenuId == Guid.Empty))
        {
            if (diasDb.Count == model.Dias.Count)
            {
                for (int i = 0; i < model.Dias.Count; i++)
                {
                    if (model.Dias[i].OpcionMenuId == Guid.Empty)
                        model.Dias[i].OpcionMenuId = diasDb[i].Id;
                }
                _logger.LogInformation("[POST Guardar] OpcionMenuId completados por índice.");
            }
            else
            {
                _logger.LogWarning("[POST Guardar] No coincide el conteo de días: modelo={ModelCount}, db={DbCount}", model.Dias.Count, diasDb.Count);
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
            }
            // Actualizar adicionales fijos del menú (se cobran 100% al empleado)
            var nuevosIds = (model.AdicionalesIds ?? new List<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToHashSet();
            var actuales = await _db.MenusAdicionales.Where(a => a.MenuId == menuDb.Id).ToListAsync();
            var actualesSet = actuales.Select(a => a.OpcionId).ToHashSet();
            var toRemove = actuales.Where(a => !nuevosIds.Contains(a.OpcionId)).ToList();
            if (toRemove.Count > 0) _db.MenusAdicionales.RemoveRange(toRemove);
            var toAdd = nuevosIds.Except(actualesSet).Select(id => new MenuAdicional { MenuId = menuDb.Id, OpcionId = id }).ToList();
            if (toAdd.Count > 0) await _db.MenusAdicionales.AddRangeAsync(toAdd);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Menú actualizado correctamente.";
            _logger.LogInformation("[POST Guardar] Cambios guardados OK.");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "No se pudo guardar el menú: " + ex.Message;
            _logger.LogError(ex, "[POST Guardar] Error al guardar.");
        }
        // Determinar semana a partir del menú del primer día (redirección estable)
        var fecha = menuDb.FechaInicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
        return RedirectToAction(nameof(Administrar), new { fecha, empresaId = model.EmpresaId, sucursalId = model.SucursalId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarEncuesta(DateOnly inicio, Guid? empresaId, Guid? sucursalId)
    {
        var fin = inicio.AddDays(4);
        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId);
        menu.EncuestaCerradaManualmente = true;
        menu.FechaCierreManual = DateTime.UtcNow;
        menu.EncuestaReabiertaManualmente = false;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Encuesta cerrada manualmente.";
        return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReabrirEncuesta(DateOnly inicio, Guid? empresaId, Guid? sucursalId)
    {
        var fin = inicio.AddDays(4);
        var menu = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId);
        menu.EncuestaCerradaManualmente = false;
        menu.FechaCierreManual = null;
        menu.EncuestaReabiertaManualmente = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Encuesta reabierta.";
        return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
    }

    [Authorize(Roles = "Admin,Empresa")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarEncuesta(DateOnly inicio, Guid? empresaId, Guid? sucursalId)
    {
        var fin = inicio.AddDays(4);
        // Seguridad: empresa solo en su alcance
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _current.EmpresaId;
            if (currentEmpresaId == null || (empresaId != null && currentEmpresaId != empresaId)) return Forbid();
            empresaId ??= currentEmpresaId;
        }

        var menu = await _menuService.FindMenuAsync(inicio, fin, empresaId, sucursalId);
        if (menu == null)
        {
            TempData["Info"] = "No existe encuesta para ese rango.";
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
        TempData["Success"] = "Encuesta eliminada (sin respuestas).";
        return RedirectToAction(nameof(Administrar), new { fecha = inicio.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), empresaId, sucursalId });
    }

    private static List<DiaEdicion> MapDias(IEnumerable<OpcionMenu> diasDb, IEnumerable<DiaEdicion>? overrides)
    {
        var overrideDict = overrides?
            .Where(d => d.OpcionMenuId != Guid.Empty)
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
}
