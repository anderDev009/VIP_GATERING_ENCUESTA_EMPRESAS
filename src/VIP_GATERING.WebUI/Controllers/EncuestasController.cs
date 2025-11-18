using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Admin,Sucursal")]
public class EncuestasController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFechaServicio _fechas;
    private readonly ICurrentUserService _current;
    private readonly IMenuService _menus;

    public EncuestasController(AppDbContext db, IFechaServicio fechas, ICurrentUserService current, IMenuService menus)
    { _db = db; _fechas = fechas; _current = current; _menus = menus; }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(Guid empleadoId)
    {
        // Seguridad: si es Sucursal, solo sobre su propia sucursal
        if (User.IsInRole("Sucursal"))
        {
            var sucEmpleado = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.SucursalId).FirstOrDefaultAsync();
            if (_current.SucursalId == null || sucEmpleado != _current.SucursalId) return Forbid();
        }

        // Determinar semana siguiente y menú efectivo del empleado
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();
        var info = await _db.Empleados.Include(e => e.Sucursal)!.ThenInclude(s => s!.Empresa)
            .Where(e => e.Id == empleadoId)
            .Select(e => new { e.SucursalId, EmpresaId = e.Sucursal!.EmpresaId })
            .FirstOrDefaultAsync();
        if (info == null) return NotFound();

        var menu = await _menus.GetEffectiveMenuForSemanaAsync(inicio, fin, info.EmpresaId, info.SucursalId);
        var opcionIds = await _db.OpcionesMenu.Where(om => om.MenuId == menu.Id).Select(om => om.Id).ToListAsync();

        var respuestas = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();
        if (respuestas.Count == 0)
        {
            TempData["Info"] = "El empleado no tiene respuestas para anular.";
            return RedirectToAction("Semana", "Empleados", new { id = empleadoId });
        }

        _db.RespuestasFormulario.RemoveRange(respuestas);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Encuesta anulada. El empleado puede volver a responder.";
        return RedirectToAction("Semana", "Empleados", new { id = empleadoId });
    }
}

