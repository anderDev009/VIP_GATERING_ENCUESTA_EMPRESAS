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

[Authorize(Roles = "Admin,RRHH")]
public class SucursalesController : Controller
{
    private readonly AppDbContext _db;

    public SucursalesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? empresaId, string? q, int page = 1, int pageSize = 10)
    {
        var query = _db.Sucursales.Include(s => s.Empresa).Where(s => !s.Borrado).AsQueryable();
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

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int? empresaId, string? q)
    {
        var sucursales = await BuildExportQuery(empresaId, q).OrderBy(s => s.Nombre).ToListAsync();
        var headers = new[] { "Filial", "RNC", "Empresa", "Direccion" };
        var rows = sucursales.Select(s => (IReadOnlyList<string>)new[]
        {
            s.Nombre,
            s.Rnc ?? string.Empty,
            s.Empresa?.Nombre ?? string.Empty,
            s.Direccion ?? string.Empty
        }).ToList();
        var bytes = ExportHelper.BuildCsv(headers, rows);
        return File(bytes, "text/csv", "filiales.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(int? empresaId, string? q)
    {
        var sucursales = await BuildExportQuery(empresaId, q).OrderBy(s => s.Nombre).ToListAsync();
        var headers = new[] { "Filial", "RNC", "Empresa", "Direccion" };
        var rows = sucursales.Select(s => (IReadOnlyList<string>)new[]
        {
            s.Nombre,
            s.Rnc ?? string.Empty,
            s.Empresa?.Nombre ?? string.Empty,
            s.Direccion ?? string.Empty
        }).ToList();
        var bytes = ExportHelper.BuildExcel("Filiales", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "filiales.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(int? empresaId, string? q)
    {
        var sucursales = await BuildExportQuery(empresaId, q).OrderBy(s => s.Nombre).ToListAsync();
        var headers = new[] { "Filial", "RNC", "Empresa", "Direccion" };
        var rows = sucursales.Select(s => (IReadOnlyList<string>)new[]
        {
            s.Nombre,
            s.Rnc ?? string.Empty,
            s.Empresa?.Nombre ?? string.Empty,
            s.Direccion ?? string.Empty
        }).ToList();
        var pdf = ExportHelper.BuildPdf("Filiales", headers, rows);
        return File(pdf, "application/pdf", "filiales.pdf");
    }

    private IQueryable<Sucursal> BuildExportQuery(int? empresaId, string? q)
    {
        var query = _db.Sucursales.Include(s => s.Empresa).Where(s => !s.Borrado).AsQueryable();
        if (empresaId != null) query = query.Where(s => s.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(s => s.Nombre.ToLower().Contains(ql) || s.Empresa!.Nombre.ToLower().Contains(ql));
        }
        return query;
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        ViewBag.HorarioSlots = new Dictionary<int, string>();
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
            ViewBag.HorarioSlots = new Dictionary<int, string>();
            return View(model);
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
        {
            await _db.SucursalesHorarios.AddAsync(new SucursalHorario
            {
                SucursalId = model.Id,
                HorarioId = hid
            });
            var slots = ParseHorarioSlots(Request.Form[$"horario_slots_{hid}"]);
            foreach (var slot in slots)
            {
                await _db.SucursalesHorariosSlots.AddAsync(new SucursalHorarioSlot
                {
                    SucursalId = model.Id,
                    HorarioId = hid,
                    Hora = slot
                });
            }
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Filial creado.";
        return RedirectToAction(nameof(Index), new { empresaId = model.EmpresaId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent == null) return NotFound();
        ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        ViewBag.Horarios = await _db.Horarios.Where(h => h.Activo).OrderBy(h => h.Orden).ToListAsync();
        ViewBag.SelHorarios = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).Select(sh => sh.HorarioId).ToListAsync();
        ViewBag.HorarioSlots = await _db.SucursalesHorariosSlots
            .Where(sh => sh.SucursalId == ent.Id)
            .GroupBy(sh => sh.HorarioId)
            .ToDictionaryAsync(
                g => g.Key,
                g => string.Join(", ", g.OrderBy(x => x.Hora)
                    .Select(x => x.Hora.ToString("HH:mm"))
                    .Distinct()));
        return View(ent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Sucursal model)
    {
        var ent = await _db.Sucursales.FindAsync(id);
        if (ent == null) return NotFound();
        ApplySubsidioFromForm(model);
        if (!ModelState.IsValid)
        {
            ViewBag.Empresas = await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Horarios = await _db.Horarios.OrderBy(h => h.Orden).ToListAsync();
            ViewBag.SelHorarios = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).Select(sh => sh.HorarioId).ToListAsync();
            ViewBag.HorarioSlots = await _db.SucursalesHorariosSlots
                .Where(sh => sh.SucursalId == ent.Id)
                .GroupBy(sh => sh.HorarioId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => string.Join(", ", g.OrderBy(x => x.Hora)
                        .Select(x => x.Hora.ToString("HH:mm"))
                        .Distinct()));
            return View(model);
        }
        ent.Nombre = model.Nombre;
        ent.Direccion = model.Direccion;
        ent.EmpresaId = model.EmpresaId;
        ent.SubsidiaEmpleados = model.SubsidiaEmpleados;
        ent.SubsidioTipo = model.SubsidioTipo;
        ent.SubsidioValor = model.SubsidioValor;
        ent.HorariosEspecificos = model.HorariosEspecificos;
        await _db.SaveChangesAsync();
        // actualizar asignaciones de horarios segun formulario
        var actuales = await _db.SucursalesHorarios.Where(sh => sh.SucursalId == ent.Id).ToListAsync();
        _db.SucursalesHorarios.RemoveRange(actuales);
        var actualesSlots = await _db.SucursalesHorariosSlots.Where(sh => sh.SucursalId == ent.Id).ToListAsync();
        _db.SucursalesHorariosSlots.RemoveRange(actualesSlots);
        var seleccion = Request.Form["horarios"].ToArray();
        if (seleccion != null && seleccion.Length > 0)
        {
            foreach (var val in seleccion.Distinct())
            {
                if (int.TryParse(val, out var hid))
                {
                    await _db.SucursalesHorarios.AddAsync(new SucursalHorario
                    {
                        SucursalId = ent.Id,
                        HorarioId = hid
                    });
                    var slots = ParseHorarioSlots(Request.Form[$"horario_slots_{hid}"]);
                    foreach (var slot in slots)
                    {
                        await _db.SucursalesHorariosSlots.AddAsync(new SucursalHorarioSlot
                        {
                            SucursalId = ent.Id,
                            HorarioId = hid,
                            Hora = slot
                        });
                    }
                }
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

    private static List<TimeOnly> ParseHorarioSlots(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<TimeOnly>();
        var tokens = value.Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var slots = new List<TimeOnly>();
        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (TimeOnly.TryParse(trimmed, out var parsed))
            {
                if (!slots.Contains(parsed))
                    slots.Add(parsed);
            }
        }
        return slots.OrderBy(s => s).ToList();
    }
}

