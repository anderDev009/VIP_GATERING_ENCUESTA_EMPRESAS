using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin")]
public class HorariosController : Controller
{
    private readonly AppDbContext _db;
    public HorariosController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        var list = await _db.Horarios.Where(h => !h.Borrado).OrderBy(h => h.Orden).ToListAsync();
        return View(list);
    }

    public IActionResult Create() => View(new Horario { Activo = true, Orden = 1 });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Horario model)
    {
        if (!ModelState.IsValid) return View(model);
        await _db.Horarios.AddAsync(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ent = await _db.Horarios.FindAsync(id);
        if (ent == null) return NotFound();
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Horario model)
    {
        if (!ModelState.IsValid) return View(model);
        var ent = await _db.Horarios.FindAsync(id);
        if (ent == null) return NotFound();
        ent.Nombre = model.Nombre; ent.Orden = model.Orden; ent.Activo = model.Activo;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ent = await _db.Horarios.FindAsync(id);
        if (ent != null)
        {
            ent.Borrado = true;
            ent.Activo = false;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
