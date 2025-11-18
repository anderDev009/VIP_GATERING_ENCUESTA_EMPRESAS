using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin")]
public class OpcionesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IOptionImageService _imageService;
    public OpcionesController(AppDbContext db, IOptionImageService imageService)
    {
        _db = db;
        _imageService = imageService;
    }

    [Authorize(Roles = "Admin,Monitor")]
    public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Opciones.Where(o => !o.Borrado).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(o =>
                o.Nombre.ToLower().Contains(ql)
                || (o.Descripcion != null && o.Descripcion.ToLower().Contains(ql))
                || (o.Codigo != null && o.Codigo.ToLower().Contains(ql))
            );
        }
        var paged = await query.OrderBy(o => o.Nombre).ToPagedResultAsync(page, pageSize);
        ViewBag.Q = q;
        return View("IndexClean", paged);
    }

    public IActionResult Create() => View(new Opcion());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opcion model, IFormFile? imagen)
    {
        var errorImagen = _imageService.Validate(imagen);
        if (errorImagen != null)
        {
            ModelState.AddModelError("Imagen", errorImagen);
        }
        if (!ModelState.IsValid) return View(model);
        model.ImagenUrl = await _imageService.SaveAsync(imagen, null);
        await _db.Opciones.AddAsync(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var ent = await _db.Opciones.FindAsync(id);
        if (ent == null) return NotFound();
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Opcion model, IFormFile? imagen, bool eliminarImagen = false)
    {
        var ent = await _db.Opciones.FindAsync(id);
        if (ent == null) return NotFound();
        var errorImagen = _imageService.Validate(imagen);
        if (errorImagen != null)
        {
            ModelState.AddModelError("Imagen", errorImagen);
        }
        if (!ModelState.IsValid)
        {
            model.ImagenUrl = ent.ImagenUrl;
            return View(model);
        }
        if (eliminarImagen)
        {
            await _imageService.DeleteAsync(ent.ImagenUrl);
            ent.ImagenUrl = null;
        }
        if (imagen != null && imagen.Length > 0)
        {
            ent.ImagenUrl = await _imageService.SaveAsync(imagen, ent.ImagenUrl);
        }
        ent.Codigo = model.Codigo; ent.Nombre = model.Nombre; ent.Descripcion = model.Descripcion; ent.Costo = model.Costo;
        ent.Precio = model.Precio; ent.EsSubsidiado = model.EsSubsidiado; ent.LlevaItbis = model.LlevaItbis;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ent = await _db.Opciones.FindAsync(id);
        if (ent != null)
        {
            await _imageService.DeleteAsync(ent.ImagenUrl);
            ent.ImagenUrl = null;
            ent.Borrado = true;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
