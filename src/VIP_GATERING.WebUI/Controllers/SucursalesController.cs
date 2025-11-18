using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin,Empresa")]
public class SucursalesController : Controller
{
    private readonly AppDbContext _db;
    public SucursalesController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index(Guid? empresaId, string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Sucursales.Include(s => s.Empresa).Where(s => !s.Borrado).AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            // limit to current empresa
            var currentEmpresaId = HttpContext?.RequestServices.GetService<VIP_GATERING.WebUI.Services.ICurrentUserService>()?.EmpresaId;
            if (currentEmpresaId != null)
                query = query.Where(s => s.EmpresaId == currentEmpresaId);
        }
        if (empresaId != null) query = query.Where(s => s.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(s => s.Nombre.ToLower().Contains(ql) || s.Empresa!.Nombre.ToLower().Contains(ql));
        }
        var paged = await query.OrderBy(s => s.Nombre).ToPagedResultAsync(page, pageSize);
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.EmpresaId = empresaId; ViewBag.Q = q;
        return View(paged);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Horarios = await _db.Horarios.OrderBy(h => h.Orden).ToListAsync();
        return View(new Sucursal());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Sucursal model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Horarios = await _db.Horarios.OrderBy(h => h.Orden).ToListAsync();
            return View(model);
        }
        await _db.Sucursales.AddAsync(model);
        await _db.SaveChangesAsync();
        // asignar horarios activos por defecto
        var horariosActivos = await _db.Horarios.Where(h => h.Activo).ToListAsync();
        foreach (var h in horariosActivos)
            await _db.SucursalesHorarios.AddAsync(new SucursalHorario { SucursalId = model.Id, HorarioId = h.Id });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Sucursal creada.";
        return RedirectToAction(nameof(Index), new { empresaId = model.EmpresaId });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent == null) return NotFound();
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Horarios = await _db.Horarios.OrderBy(h => h.Orden).ToListAsync();
        ViewBag.SelHorarios = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).Select(sh => sh.HorarioId).ToListAsync();
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Sucursal model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            return View(model);
        }
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent == null) return NotFound();
        ent.Nombre = model.Nombre;
        ent.Direccion = model.Direccion;
        ent.EmpresaId = model.EmpresaId;
        await _db.SaveChangesAsync();
        // actualizar asignaciones de horarios segun formulario
        var actuales = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).ToListAsync();
        _db.SucursalesHorarios.RemoveRange(actuales);
        var seleccion = Request.Form["horarios"].ToArray();
        if (seleccion != null && seleccion.Length > 0)
        {
            foreach (var val in seleccion.Distinct())
            {
                if (Guid.TryParse(val, out var hid))
                    await _db.SucursalesHorarios.AddAsync(new SucursalHorario { SucursalId = ent.Id, HorarioId = hid });
            }
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Sucursal actualizada.";
        return RedirectToAction(nameof(Index), new { empresaId = model.EmpresaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent != null)
        {
            var empresaId = ent.EmpresaId;
            ent.Borrado = true;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Sucursal eliminada.";
            return RedirectToAction(nameof(Index), new { empresaId });
        }
        return RedirectToAction(nameof(Index));
    }
}
