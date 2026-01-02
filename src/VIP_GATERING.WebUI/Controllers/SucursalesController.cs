using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Globalization;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin,Empresa")]
public class SucursalesController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public SucursalesController(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IActionResult> Index(int? empresaId, string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Sucursales.Include(s => s.Empresa).Where(s => !s.Borrado).AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var currentEmpresaId = _current.EmpresaId;
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
        var empresas = _db.Empresas.AsQueryable();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _current.EmpresaId;
            if (empresaId == null) return Forbid();
            empresas = empresas.Where(e => e.Id == empresaId);
        }
        ViewBag.Empresas = await empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        return View(new Sucursal());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Sucursal model)
    {
        ApplySubsidioFromForm(model);
        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Horarios = await _db.Horarios.OrderBy(h => h.Orden).ToListAsync();
            return View(model);
        }
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _current.EmpresaId;
            if (empresaId == null || model.EmpresaId != empresaId) return Forbid();
        }
        await _db.Sucursales.AddAsync(model);
        await _db.SaveChangesAsync();
        // asignar horarios seleccionados o por defecto (Almuerzo)
        var seleccion = Request.Form["horarios"].ToArray();
        IEnumerable<int> horarioIds;
        if (seleccion != null && seleccion.Length > 0)
        {
            horarioIds = seleccion
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .Select(value => int.Parse(value!));
        }
        else
        {
            var almuerzoId = await _db.Horarios.Where(h => h.Nombre == "Almuerzo").Select(h => (int?)h.Id).FirstOrDefaultAsync();
            if (almuerzoId != null)
                horarioIds = new[] { almuerzoId.Value };
            else
                horarioIds = (await _db.Horarios.Where(h => h.Activo).Select(h => h.Id).ToListAsync());
        }
        foreach (var hid in horarioIds)
            await _db.SucursalesHorarios.AddAsync(new SucursalHorario { SucursalId = model.Id, HorarioId = hid });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Filial creado.";
        return RedirectToAction(nameof(Index), new { empresaId = model.EmpresaId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent == null) return NotFound();
        if (User.IsInRole("Empresa") && _current.EmpresaId != ent.EmpresaId) return Forbid();
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        ViewBag.SelHorarios = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).Select(sh => sh.HorarioId).ToListAsync();
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Sucursal model)
    {
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent == null) return NotFound();
        if (User.IsInRole("Empresa"))
        {
            var empresaId = _current.EmpresaId;
            if (empresaId == null || ent.EmpresaId != empresaId || model.EmpresaId != empresaId) return Forbid();
        }
        ApplySubsidioFromForm(model);
        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Horarios = await _db.Horarios.OrderBy(h => h.Orden).ToListAsync();
            ViewBag.SelHorarios = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).Select(sh => sh.HorarioId).ToListAsync();
            return View(model);
        }
        ent.Nombre = model.Nombre;
        ent.Direccion = model.Direccion;
        ent.EmpresaId = model.EmpresaId;
        ent.SubsidiaEmpleados = model.SubsidiaEmpleados;
        ent.SubsidioTipo = model.SubsidioTipo;
        ent.SubsidioValor = model.SubsidioValor;
        await _db.SaveChangesAsync();
        // actualizar asignaciones de horarios segun formulario
        var actuales = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).ToListAsync();
        _db.SucursalesHorarios.RemoveRange(actuales);
        var seleccion = Request.Form["horarios"].ToArray();
        if (seleccion != null && seleccion.Length > 0)
        {
            foreach (var val in seleccion.Distinct())
            {
                if (int.TryParse(val, out var hid))
                    await _db.SucursalesHorarios.AddAsync(new SucursalHorario { SucursalId = ent.Id, HorarioId = hid });
            }
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Filial actualizado.";
        return RedirectToAction(nameof(Index), new { empresaId = model.EmpresaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent != null)
        {
            if (User.IsInRole("Empresa") && (_current.EmpresaId == null || ent.EmpresaId != _current.EmpresaId)) return Forbid();
            var empresaId = ent.EmpresaId;
            ent.Borrado = true;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Filial eliminado.";
            return RedirectToAction(nameof(Index), new { empresaId });
        }
        return RedirectToAction(nameof(Index));
    }

    private void ApplySubsidioFromForm(Sucursal target)
    {
        var scope = Request.Form["subsidioScope"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(scope))
        {
            var tieneCustom = !string.IsNullOrWhiteSpace(Request.Form["CustomSubsidioTipo"].FirstOrDefault())
                || !string.IsNullOrWhiteSpace(Request.Form["CustomSubsidioValor"].FirstOrDefault())
                || !string.IsNullOrWhiteSpace(Request.Form["CustomSubsidia"].FirstOrDefault());
            if (tieneCustom)
                scope = "custom";
        }
        if (string.Equals(scope, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var subsidiaStr = Request.Form["CustomSubsidia"].FirstOrDefault();
            target.SubsidiaEmpleados = string.Equals(subsidiaStr, "true", StringComparison.OrdinalIgnoreCase);

            if (Enum.TryParse<SubsidioTipo>(Request.Form["CustomSubsidioTipo"].FirstOrDefault(), out var tipo))
            {
                target.SubsidioTipo = tipo;
            }
            else
            {
                ModelState.AddModelError("SubsidioTipo", "Selecciona el tipo de subsidio.");
            }

            var valorStr = Request.Form["CustomSubsidioValor"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(valorStr) && decimal.TryParse(valorStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var valorInv))
            {
                target.SubsidioValor = valorInv;
            }
            else if (!string.IsNullOrWhiteSpace(valorStr) && decimal.TryParse(valorStr, NumberStyles.Number, CultureInfo.CurrentCulture, out var valorCult))
            {
                target.SubsidioValor = valorCult;
            }
            else
            {
                ModelState.AddModelError("SubsidioValor", "Valor de subsidio inválido.");
            }
        }
        else
        {
            target.SubsidiaEmpleados = null;
            target.SubsidioTipo = null;
            target.SubsidioValor = null;
        }
    }
}

