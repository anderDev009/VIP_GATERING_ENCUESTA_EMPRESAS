using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;

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

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? q)
    {
        var empresas = await BuildExportQuery(q).OrderBy(e => e.Nombre).ToListAsync();
        var headers = new[] { "Empresa", "RNC", "Contacto", "Telefono", "Direccion" };
        var rows = empresas.Select(e => (IReadOnlyList<string>)new[]
        {
            e.Nombre,
            e.Rnc ?? string.Empty,
            e.ContactoNombre,
            e.ContactoTelefono,
            e.Direccion
        }).ToList();
        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", "empresas.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? q)
    {
        var empresas = await BuildExportQuery(q).OrderBy(e => e.Nombre).ToListAsync();
        var headers = new[] { "Empresa", "RNC", "Contacto", "Telefono", "Direccion" };
        var rows = empresas.Select(e => (IReadOnlyList<string>)new[]
        {
            e.Nombre,
            e.Rnc ?? string.Empty,
            e.ContactoNombre,
            e.ContactoTelefono,
            e.Direccion
        }).ToList();
        var bytes = ExportHelper.BuildExcel("Empresas", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "empresas.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(string? q)
    {
        var empresas = await BuildExportQuery(q).OrderBy(e => e.Nombre).ToListAsync();
        var headers = new[] { "Empresa", "RNC", "Contacto", "Telefono", "Direccion" };
        var rows = empresas.Select(e => (IReadOnlyList<string>)new[]
        {
            e.Nombre,
            e.Rnc ?? string.Empty,
            e.ContactoNombre,
            e.ContactoTelefono,
            e.Direccion
        }).ToList();
        var pdf = ExportHelper.BuildPdf("Empresas", headers, rows);
        return File(pdf, "application/pdf", "empresas.pdf");
    }

    private IQueryable<Empresa> BuildExportQuery(string? q)
    {
        var query = _db.Empresas.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(e => e.Nombre.ToLower().Contains(ql) || (e.Rnc != null && e.Rnc.ToLower().Contains(ql)));
        }
        return query;
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

    public async Task<IActionResult> Edit(int id)
    {
        var ent = await _db.Empresas.FindAsync(id);
        if (ent == null) return NotFound();
        ViewBag.Horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        var sucursalIds = await _db.Sucursales.Where(s => s.EmpresaId == id).Select(s => s.Id).ToListAsync();
        var horariosEmpresa = await _db.SucursalesHorarios
            .Where(sh => sucursalIds.Contains(sh.SucursalId))
            .ToListAsync();
        ViewBag.EmpresaSelHorarios = horariosEmpresa
            .Select(h => h.HorarioId)
            .Distinct()
            .ToList();
        ViewBag.EmpresaHorarioTimes = horariosEmpresa
            .GroupBy(h => h.HorarioId)
            .ToDictionary(
                g => g.Key,
                g => (Inicio: g.First().HoraInicio?.ToString("HH:mm"), Fin: g.First().HoraFin?.ToString("HH:mm")));
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Empresa model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
            ViewBag.EmpresaSelHorarios = new List<int>();
            ViewBag.EmpresaHorarioTimes = new Dictionary<int, (string? Inicio, string? Fin)>();
            return View(model);
        }
        var ent = await _db.Empresas.FindAsync(id);
        if (ent == null) return NotFound();
        ent.Nombre = model.Nombre;
        ent.Rnc = model.Rnc;
        ent.ContactoNombre = model.ContactoNombre;
        ent.ContactoTelefono = model.ContactoTelefono;
        ent.Direccion = model.Direccion;
        ent.SubsidiaEmpleados = model.SubsidiaEmpleados;
        ent.SubsidioTipo = model.SubsidioTipo;
        ent.SubsidioValor = model.SubsidioValor;
        await _db.SaveChangesAsync();
        await UpsertEmpresaHorariosAsync(ent.Id);
        TempData["Success"] = "Empresa actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
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

    private async Task UpsertEmpresaHorariosAsync(int empresaId)
    {
        var seleccion = Request.Form["empresa_horarios"].ToArray();
        if (seleccion == null) return;

        var sucursalIds = await _db.Sucursales.Where(s => s.EmpresaId == empresaId).Select(s => s.Id).ToListAsync();
        if (sucursalIds.Count == 0) return;

        var actuales = await _db.SucursalesHorarios
            .Where(sh => sucursalIds.Contains(sh.SucursalId))
            .ToListAsync();
        _db.SucursalesHorarios.RemoveRange(actuales);

        var horarioIds = seleccion
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .Select(value => int.Parse(value!))
            .ToList();

        foreach (var sucursalId in sucursalIds)
        {
            foreach (var hid in horarioIds)
            {
                _db.SucursalesHorarios.Add(new SucursalHorario
                {
                    SucursalId = sucursalId,
                    HorarioId = hid,
                    HoraInicio = ParseHora(Request.Form[$"empresa_horario_inicio_{hid}"]),
                    HoraFin = ParseHora(Request.Form[$"empresa_horario_fin_{hid}"])
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private static TimeOnly? ParseHora(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return TimeOnly.TryParse(value, out var parsed) ? parsed : null;
    }
}

