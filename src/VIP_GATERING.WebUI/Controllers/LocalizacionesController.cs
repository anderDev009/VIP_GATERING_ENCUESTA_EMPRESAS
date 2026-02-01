using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin,RRHH")]
public class LocalizacionesController : Controller
{
    private readonly AppDbContext _db;

    public LocalizacionesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? empresaId, int? sucursalId, string? q, int page = 1, int pageSize = 10)
    {
        var baseQuery = _db.Localizaciones.AsQueryable();

        if (empresaId != null)
            baseQuery = baseQuery.Where(l => l.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(l => l.SucursalId == sucursalId || l.SucursalId == null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            baseQuery = baseQuery.Where(l =>
                l.Nombre.ToLower().Contains(ql) ||
                (l.Sucursal != null && l.Sucursal.Nombre.ToLower().Contains(ql)) ||
                (l.Empresa != null && l.Empresa.Nombre.ToLower().Contains(ql)) ||
                (l.Sucursal != null && l.Sucursal.Empresa != null && l.Sucursal.Empresa.Nombre.ToLower().Contains(ql)));
        }

        var uniqueIds = await baseQuery
            .GroupBy(l => new { l.EmpresaId, Nombre = l.Nombre.ToLower() })
            .Select(g => g.Min(l => l.Id))
            .ToListAsync();

        var query = _db.Localizaciones
            .Include(l => l.Empresa)
            .Include(l => l.Sucursal).ThenInclude(s => s!.Empresa)
            .Where(l => uniqueIds.Contains(l.Id));

        var paged = await query.OrderBy(l => l.Nombre).ToPagedResultAsync(page, pageSize);
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await _db.Sucursales.OrderBy(s => s.Nombre).ToListAsync();
        ViewBag.EmpresaId = empresaId;
        ViewBag.SucursalId = sucursalId;
        ViewBag.Q = q;
        return View(paged);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int? empresaId, int? sucursalId, string? q)
    {
        var localizaciones = await BuildExportQuery(empresaId, sucursalId, q).OrderBy(l => l.Nombre).ToListAsync();
        var headers = new[] { "Localizacion", "RNC", "Empresa", "Filial", "Direccion", "Notas" };
        var rows = localizaciones.Select(l => (IReadOnlyList<string>)new[]
        {
            l.Nombre,
            l.Rnc ?? string.Empty,
            l.Empresa?.Nombre ?? string.Empty,
            l.Sucursal?.Nombre ?? string.Empty,
            l.Direccion,
            l.IndicacionesEntrega
        }).ToList();
        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", "localizaciones.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(int? empresaId, int? sucursalId, string? q)
    {
        var localizaciones = await BuildExportQuery(empresaId, sucursalId, q).OrderBy(l => l.Nombre).ToListAsync();
        var headers = new[] { "Localizacion", "RNC", "Empresa", "Filial", "Direccion", "Notas" };
        var rows = localizaciones.Select(l => (IReadOnlyList<string>)new[]
        {
            l.Nombre,
            l.Rnc ?? string.Empty,
            l.Empresa?.Nombre ?? string.Empty,
            l.Sucursal?.Nombre ?? string.Empty,
            l.Direccion,
            l.IndicacionesEntrega
        }).ToList();
        var bytes = ExportHelper.BuildExcel("Localizaciones", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "localizaciones.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(int? empresaId, int? sucursalId, string? q)
    {
        var localizaciones = await BuildExportQuery(empresaId, sucursalId, q).OrderBy(l => l.Nombre).ToListAsync();
        var headers = new[] { "Localizacion", "RNC", "Empresa", "Filial", "Direccion", "Notas" };
        var rows = localizaciones.Select(l => (IReadOnlyList<string>)new[]
        {
            l.Nombre,
            l.Rnc ?? string.Empty,
            l.Empresa?.Nombre ?? string.Empty,
            l.Sucursal?.Nombre ?? string.Empty,
            l.Direccion,
            l.IndicacionesEntrega
        }).ToList();
        var pdf = ExportHelper.BuildPdf("Localizaciones", headers, rows);
        return File(pdf, "application/pdf", "localizaciones.pdf");
    }

    private IQueryable<Localizacion> BuildExportQuery(int? empresaId, int? sucursalId, string? q)
    {
        var baseQuery = _db.Localizaciones.AsQueryable();

        if (empresaId != null)
            baseQuery = baseQuery.Where(l => l.EmpresaId == empresaId);
        if (sucursalId != null)
            baseQuery = baseQuery.Where(l => l.SucursalId == sucursalId || l.SucursalId == null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            baseQuery = baseQuery.Where(l =>
                l.Nombre.ToLower().Contains(ql) ||
                (l.Sucursal != null && l.Sucursal.Nombre.ToLower().Contains(ql)) ||
                (l.Empresa != null && l.Empresa.Nombre.ToLower().Contains(ql)) ||
                (l.Sucursal != null && l.Sucursal.Empresa != null && l.Sucursal.Empresa.Nombre.ToLower().Contains(ql)));
        }

        var uniqueIds = baseQuery
            .GroupBy(l => new { l.EmpresaId, Nombre = l.Nombre.ToLower() })
            .Select(g => g.Min(l => l.Id));

        return _db.Localizaciones
            .Include(l => l.Empresa)
            .Include(l => l.Sucursal).ThenInclude(s => s!.Empresa)
            .Where(l => uniqueIds.Contains(l.Id));
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        return View(new Localizacion());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Localizacion model)
    {
        if (string.IsNullOrWhiteSpace(model.Nombre))
            ModelState.AddModelError("Nombre", "Nombre requerido.");
        if (string.IsNullOrWhiteSpace(model.Direccion))
            ModelState.AddModelError("Direccion", "Direccion requerida.");
        if (string.IsNullOrWhiteSpace(model.IndicacionesEntrega))
            ModelState.AddModelError("IndicacionesEntrega", "Indicaciones de entrega requeridas.");

        if (!ModelState.IsValid)
            return await ReturnInvalidAsync(model);

        if (model.EmpresaId == 0)
        {
            ModelState.AddModelError("EmpresaId", "Empresa requerida.");
            return await ReturnInvalidAsync(model);
        }

        var nombre = model.Nombre.Trim();
        var existe = await _db.Localizaciones.AnyAsync(l =>
            l.EmpresaId == model.EmpresaId
            && l.Nombre.ToLower() == nombre.ToLower());
        if (existe)
        {
            ModelState.AddModelError("Nombre", "Ya existe una localizacion con ese nombre en la empresa.");
            return await ReturnInvalidAsync(model);
        }

        model.SucursalId = null;
        model.Nombre = nombre;
        await _db.Localizaciones.AddAsync(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Localizacion creada.";
        return RedirectToAction(nameof(Index), new { empresaId = model.EmpresaId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ent = await _db.Localizaciones.Include(l => l.Sucursal).FirstOrDefaultAsync(l => l.Id == id);
        if (ent == null) return NotFound();
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Localizacion model)
    {
        var ent = await _db.Localizaciones.Include(l => l.Sucursal).FirstOrDefaultAsync(l => l.Id == id);
        if (ent == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Nombre))
            ModelState.AddModelError("Nombre", "Nombre requerido.");
        if (string.IsNullOrWhiteSpace(model.Direccion))
            ModelState.AddModelError("Direccion", "Direccion requerida.");
        if (string.IsNullOrWhiteSpace(model.IndicacionesEntrega))
            ModelState.AddModelError("IndicacionesEntrega", "Indicaciones de entrega requeridas.");

        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            return View(model);
        }

        if (model.EmpresaId == 0)
        {
            ModelState.AddModelError("EmpresaId", "Empresa requerida.");
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            return View(model);
        }

        var nombre = model.Nombre.Trim();
        var nombreCambio = !string.Equals(ent.Nombre, nombre, StringComparison.OrdinalIgnoreCase) || ent.EmpresaId != model.EmpresaId;
        if (nombreCambio)
        {
            var existe = await _db.Localizaciones.AnyAsync(l =>
                l.Id != ent.Id
                && l.EmpresaId == model.EmpresaId
                && l.Nombre.ToLower() == nombre.ToLower());
            if (existe)
            {
                ModelState.AddModelError("Nombre", "Ya existe una localizacion con ese nombre en la empresa.");
                ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
                return View(model);
            }
        }

        ent.Nombre = nombre;
        ent.EmpresaId = model.EmpresaId;
        ent.SucursalId = null;
        ent.Rnc = model.Rnc;
        ent.Direccion = model.Direccion;
        ent.IndicacionesEntrega = model.IndicacionesEntrega;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Localizacion actualizada.";
        return RedirectToAction(nameof(Index), new { empresaId = ent.EmpresaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ent = await _db.Localizaciones.Include(l => l.Sucursal).FirstOrDefaultAsync(l => l.Id == id);
        if (ent == null) return RedirectToAction(nameof(Index));

        var usado = await _db.EmpleadosLocalizaciones.AnyAsync(el => el.LocalizacionId == ent.Id)
            || await _db.RespuestasFormulario.AnyAsync(r => r.LocalizacionEntregaId == ent.Id);
        if (usado)
        {
            TempData["Error"] = "No se puede eliminar la localizacion porque esta asignada a empleados o menus.";
            return RedirectToAction(nameof(Index), new { empresaId = ent.EmpresaId });
        }

        _db.Localizaciones.Remove(ent);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Localizacion eliminada.";
        return RedirectToAction(nameof(Index), new { empresaId = ent.EmpresaId });
    }

    private async Task<IActionResult> ReturnInvalidAsync(Localizacion model)
    {
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        return View(model);
    }
}
