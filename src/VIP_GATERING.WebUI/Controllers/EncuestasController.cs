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
            var sucActual = _current.SucursalId;
            if (sucActual == null) return Forbid();
            var principal = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.SucursalId).FirstOrDefaultAsync();
            var extra = await _db.EmpleadosSucursales.Where(es => es.EmpleadoId == empleadoId).Select(es => es.SucursalId).ToListAsync();
            if (principal != sucActual && !extra.Contains(sucActual.Value)) return Forbid();
        }

        // Determinar semana siguiente y menú efectivo del empleado
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();
        var existeEmpleado = await _db.Empleados.AnyAsync(e => e.Id == empleadoId);
        if (!existeEmpleado) return NotFound();

        var respuestas = await _db.RespuestasFormulario
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Where(r => r.EmpleadoId == empleadoId && r.OpcionMenu!.Menu!.FechaInicio == inicio && r.OpcionMenu.Menu.FechaTermino == fin)
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
