using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin")]
public class EmpresasController : Controller
{
    private readonly AppDbContext _db;
    public EmpresasController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Empresas.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(e => e.Nombre.ToLower().Contains(ql) || (e.Rnc != null && e.Rnc.ToLower().Contains(ql)));
        }
        var paged = await query.OrderBy(e => e.Nombre).Select(e=>e).ToPagedResultAsync(page, pageSize);
        ViewBag.Q = q;
        return View(paged);
    }

    public IActionResult Create() => View(new Empresa());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Empresa model)
    {
        if (!ModelState.IsValid) return View(model);
        await _db.Empresas.AddAsync(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Empresa creada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var ent = await _db.Empresas.FindAsync(id);
        if (ent == null) return NotFound();
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Empresa model)
    {
        if (!ModelState.IsValid) return View(model);
        var ent = await _db.Empresas.FindAsync(id);
        if (ent == null) return NotFound();
        ent.Nombre = model.Nombre;
        ent.Rnc = model.Rnc;
        ent.SubsidiaEmpleados = model.SubsidiaEmpleados;
        ent.SubsidioTipo = model.SubsidioTipo;
        ent.SubsidioValor = model.SubsidioValor;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Empresa actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ent = await _db.Empresas.FindAsync(id);
        if (ent != null)
        {
            var tieneSucursales = await _db.Sucursales.AnyAsync(s => s.EmpresaId == id);
            var tieneMenus = await _db.Menus.AnyAsync(m => m.EmpresaId == id);
            if (tieneSucursales || tieneMenus)
            {
                TempData["Error"] = "No se puede eliminar la empresa porque tiene filiales o menus asociados.";
                return RedirectToAction(nameof(Index));
            }
            _db.Empresas.Remove(ent);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Empresa eliminada.";
        }
        return RedirectToAction(nameof(Index));
    }
}

