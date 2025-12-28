using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin,Empresa")]
public class LocalizacionesController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public LocalizacionesController(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IActionResult> Index(Guid? empresaId, Guid? sucursalId, string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Localizaciones
            .Include(l => l.Empresa)
            .Include(l => l.Sucursal).ThenInclude(s => s!.Empresa)
            .AsQueryable();

        if (User.IsInRole("Empresa") && _current.EmpresaId != null)
        {
            var currentEmpresaId = _current.EmpresaId;
            query = query.Where(l => l.EmpresaId == currentEmpresaId);
        }

        if (empresaId != null)
            query = query.Where(l => l.EmpresaId == empresaId);
        if (sucursalId != null)
            query = query.Where(l => l.SucursalId == sucursalId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(l =>
                l.Nombre.ToLower().Contains(ql) ||
                (l.Sucursal != null && l.Sucursal.Nombre.ToLower().Contains(ql)) ||
                (l.Sucursal != null && l.Sucursal.Empresa != null && l.Sucursal.Empresa.Nombre.ToLower().Contains(ql)));
        }

        var paged = await query.OrderBy(l => l.Nombre).ToPagedResultAsync(page, pageSize);
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Sucursales = await _db.Sucursales.OrderBy(s => s.Nombre).ToListAsync();
        ViewBag.EmpresaId = empresaId;
        ViewBag.SucursalId = sucursalId;
        ViewBag.Q = q;
        return View(paged);
    }

    public async Task<IActionResult> Create()
    {
        var empresas = _db.Empresas.AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _current.EmpresaId;
            if (empresaId == null) return Forbid();
            empresas = empresas.Where(e => e.Id == empresaId);
        }
        ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
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

        if (model.EmpresaId == Guid.Empty)
        {
            ModelState.AddModelError("EmpresaId", "Empresa requerida.");
            return await ReturnInvalidAsync(model);
        }

        if (User.IsInRole("Empresa") && _current.EmpresaId != null && _current.EmpresaId != model.EmpresaId)
            return Forbid();

        var sucursal = await _db.Sucursales
            .Where(s => s.EmpresaId == model.EmpresaId)
            .OrderBy(s => s.Nombre)
            .FirstOrDefaultAsync();
        if (sucursal == null)
        {
            ModelState.AddModelError("EmpresaId", "La empresa seleccionada no tiene filiales.");
            return await ReturnInvalidAsync(model);
        }

        model.SucursalId = sucursal.Id;
        await _db.Localizaciones.AddAsync(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Localizacion creada.";
        return RedirectToAction(nameof(Index), new { empresaId = model.EmpresaId });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var ent = await _db.Localizaciones.Include(l => l.Sucursal).FirstOrDefaultAsync(l => l.Id == id);
        if (ent == null) return NotFound();
        if (User.IsInRole("Empresa") && _current.EmpresaId != null && ent.EmpresaId != _current.EmpresaId)
            return Forbid();
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Localizacion model)
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

        if (model.EmpresaId == Guid.Empty)
        {
            ModelState.AddModelError("EmpresaId", "Empresa requerida.");
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            return View(model);
        }

        if (User.IsInRole("Empresa") && _current.EmpresaId != null && _current.EmpresaId != model.EmpresaId)
            return Forbid();

        var sucursal = await _db.Sucursales
            .Where(s => s.EmpresaId == model.EmpresaId)
            .OrderBy(s => s.Nombre)
            .FirstOrDefaultAsync();
        if (sucursal == null)
        {
            ModelState.AddModelError("EmpresaId", "La empresa seleccionada no tiene filiales.");
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            return View(model);
        }

        ent.Nombre = model.Nombre;
        ent.EmpresaId = model.EmpresaId;
        ent.SucursalId = sucursal.Id;
        ent.Direccion = model.Direccion;
        ent.IndicacionesEntrega = model.IndicacionesEntrega;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Localizacion actualizada.";
        return RedirectToAction(nameof(Index), new { empresaId = sucursal.EmpresaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ent = await _db.Localizaciones.Include(l => l.Sucursal).FirstOrDefaultAsync(l => l.Id == id);
        if (ent == null) return RedirectToAction(nameof(Index));

        if (User.IsInRole("Empresa") && _current.EmpresaId != null && ent.EmpresaId != _current.EmpresaId)
            return Forbid();

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
        var empresas = _db.Empresas.AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _current.EmpresaId;
            empresas = empresas.Where(e => e.Id == empresaId);
        }
        ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
        return View(model);
    }
}
