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

    public async Task<IActionResult> Create()
    {
        await LoadHorariosAsync();
        return View(new Opcion());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opcion model, IFormFile? imagen)
    {
        var selectedHorarios = ParseHorarioIds(Request.Form);
        var errorImagen = _imageService.Validate(imagen);
        if (errorImagen != null)
        {
            ModelState.AddModelError("Imagen", errorImagen);
        }
        if (!ModelState.IsValid)
        {
            await LoadHorariosAsync(selectedHorarios);
            return View(model);
        }
        model.EsSubsidiado = true;
        model.ImagenUrl = await _imageService.SaveAsync(imagen, null);
        await _db.Opciones.AddAsync(model);
        foreach (var horarioId in selectedHorarios)
        {
            _db.OpcionesHorarios.Add(new OpcionHorario { OpcionId = model.Id, HorarioId = horarioId });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var ent = await _db.Opciones.Include(o => o.Horarios).FirstOrDefaultAsync(o => o.Id == id);
        if (ent == null) return NotFound();
        await LoadHorariosAsync(ent.Horarios.Select(h => h.HorarioId));
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Opcion model, IFormFile? imagen, bool eliminarImagen = false)
    {
        var selectedHorarios = ParseHorarioIds(Request.Form);
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
            await LoadHorariosAsync(selectedHorarios);
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
        ent.Precio = model.Precio; ent.EsSubsidiado = true; ent.LlevaItbis = model.LlevaItbis;

        var actuales = await _db.OpcionesHorarios.Where(oh => oh.OpcionId == ent.Id).ToListAsync();
        var actualesSet = actuales.Select(oh => oh.HorarioId).ToHashSet();
        var seleccionSet = selectedHorarios.ToHashSet();
        var toRemove = actuales.Where(oh => !seleccionSet.Contains(oh.HorarioId)).ToList();
        if (toRemove.Count > 0)
            _db.OpcionesHorarios.RemoveRange(toRemove);
        var toAdd = seleccionSet.Except(actualesSet).Select(h => new OpcionHorario { OpcionId = ent.Id, HorarioId = h }).ToList();
        if (toAdd.Count > 0)
            await _db.OpcionesHorarios.AddRangeAsync(toAdd);

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    private async Task LoadHorariosAsync(IEnumerable<Guid>? selected = null)
    {
        var horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        ViewBag.Horarios = horarios;
        ViewBag.HorarioIds = selected?.ToList() ?? new List<Guid>();
    }

    private static List<Guid> ParseHorarioIds(IFormCollection form)
    {
        var values = form["HorarioIds"];
        var ids = new List<Guid>();
        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var id))
                ids.Add(id);
        }
        return ids.Distinct().ToList();
    }
}
