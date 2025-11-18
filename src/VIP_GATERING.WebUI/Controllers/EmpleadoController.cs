using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.WebUI.Services;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Empleado")]
public class EmpleadoController : Controller
{
    private readonly IMenuService _menuService;
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEncuestaCierreService _cierre;
    public EmpleadoController(IMenuService menuService, AppDbContext db, ICurrentUserService current, IEncuestaCierreService cierre)
    { _menuService = menuService; _db = db; _current = current; _cierre = cierre; }

    public async Task<IActionResult> MiSemana()
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return RedirectToAction("Login", "Account");
        var estadoEmpleado = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.Estado).FirstAsync();
        var noHabilitado = estadoEmpleado != VIP_GATERING.Domain.Entities.EmpleadoEstado.Habilitado;
        if (noHabilitado)
        {
            ViewBag.NoHabilitado = true;
            TempData["Error"] = "Tu cuenta no está habilitada para seleccionar opciones.";
        }
        var sucursalEmpresa = await _db.Empleados
            .Include(e => e.Sucursal)!.ThenInclude(s => s!.Empresa)
            .Where(e => e.Id == empleadoId)
            .Select(e => new
            {
                e.Estado,
                e.Nombre,
                e.EsJefe,
                e.SucursalId,
                SucursalNombre = e.Sucursal!.Nombre,
                EmpresaId = e.Sucursal!.EmpresaId,
                EmpresaNombre = e.Sucursal!.Empresa!.Nombre
            })
            .FirstAsync();
        var fechas = new VIP_GATERING.Application.Services.FechaServicio();
        var (inicio, fin) = fechas.RangoSemanaSiguiente();
        var menu = await _menuService.GetEffectiveMenuForSemanaAsync(inicio, fin, sucursalEmpresa.EmpresaId, sucursalEmpresa.SucursalId);
        var fechaCierreAuto = _cierre.GetFechaCierreAutomatica(menu);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        var opciones = await _db.OpcionesMenu
            .Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC).Include(o => o.Horario)
            .Where(o => o.MenuId == menu.Id)
            .ToListAsync();
        if (opciones.Count == 0)
        {
            var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            foreach (var d in dias)
                await _db.OpcionesMenu.AddAsync(new VIP_GATERING.Domain.Entities.OpcionMenu { MenuId = menu.Id, DiaSemana = d });
            await _db.SaveChangesAsync();
            opciones = await _db.OpcionesMenu.Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC)
                .Where(o => o.MenuId == menu.Id).ToListAsync();
        }
        var opcionIds = opciones.Select(o => o.Id).ToList();
        var respuestas = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();

        ViewBag.FechaCierreAuto = fechaCierreAuto;
        var modelo = new VIP_GATERING.WebUI.Models.SemanaEmpleadoVM
        {
            EmpleadoId = empleadoId.Value,
            MenuId = menu.Id,
            FechaInicio = menu.FechaInicio,
            FechaTermino = menu.FechaTermino,
            Bloqueado = encuestaCerrada || noHabilitado,
            BloqueadoPorEstado = noHabilitado,
            MensajeBloqueo = encuestaCerrada ? $"La encuesta está cerrada desde {fechaCierreAuto:dd/MM/yyyy}." : null,
            EmpleadoNombre = sucursalEmpresa.Nombre,
            EsJefe = sucursalEmpresa.EsJefe,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Sucursal" : "Cliente",
            EmpresaNombre = sucursalEmpresa.EmpresaNombre,
            SucursalNombre = sucursalEmpresa.SucursalNombre,
            Dias = opciones.OrderBy(o => o.DiaSemana).ThenBy(o => o.Horario!.Orden).Select(o => new VIP_GATERING.WebUI.Models.DiaEmpleadoVM{ OpcionMenuId = o.Id, DiaSemana = o.DiaSemana, HorarioNombre = o.Horario?.Nombre, A = o.OpcionA?.Nombre, B = o.OpcionB?.Nombre, C = o.OpcionC?.Nombre, ImagenA = o.OpcionA?.ImagenUrl, ImagenB = o.OpcionB?.ImagenUrl, ImagenC = o.OpcionC?.ImagenUrl, Seleccion = respuestas.FirstOrDefault(r => r.OpcionMenuId == o.Id)?.Seleccion }).ToList()
        };
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarSemana(VIP_GATERING.WebUI.Models.SemanaEmpleadoVM model)
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return RedirectToAction("Login", "Account");
        var estadoPost = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.Estado).FirstAsync();
        if (estadoPost != VIP_GATERING.Domain.Entities.EmpleadoEstado.Habilitado)
        {
            TempData["Error"] = "Tu cuenta no está habilitada para seleccionar opciones.";
            return RedirectToAction(nameof(MiSemana));
        }

        var menuId = model.MenuId;
        var menu = await _db.Menus.FirstAsync(m => m.Id == menuId);
        if (_cierre.EstaCerrada(menu))
        {
            TempData["Error"] = "La encuesta ya está cerrada. Contacta a tu administrador para cambios.";
            return RedirectToAction(nameof(MiSemana));
        }

        foreach (var d in model.Dias)
        {
            if (d.Seleccion is 'A' or 'B' or 'C')
            {
                await _menuService.RegistrarSeleccionAsync(empleadoId.Value, d.OpcionMenuId, d.Seleccion.Value);
            }
        }
        TempData["Success"] = "Selecciones guardadas.";
        return RedirectToAction(nameof(MiSemana));
    }


    // Acción legacy de guardado por día (permite override de cierres)
    [HttpPost]
    public async Task<IActionResult> Seleccionar(Guid opcionMenuId, string seleccion)
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return RedirectToAction("Login", "Account");
        var estadoSel = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.Estado).FirstAsync();
        if (estadoSel != VIP_GATERING.Domain.Entities.EmpleadoEstado.Habilitado)
        {
            TempData["Error"] = "Tu cuenta no está habilitada para seleccionar opciones.";
            return RedirectToAction(nameof(MiSemana));
        }
        var menuId = await _db.OpcionesMenu.Where(om => om.Id == opcionMenuId).Select(om => om.MenuId).FirstAsync();
        var menu = await _db.Menus.FirstAsync(m => m.Id == menuId);
        if (_cierre.EstaCerrada(menu))
        {
            TempData["Error"] = "La encuesta ya está cerrada. Contacta a tu administrador para cambios.";
            return RedirectToAction(nameof(MiSemana));
        }
        await _menuService.RegistrarSeleccionAsync(empleadoId.Value, opcionMenuId, seleccion.FirstOrDefault());
        TempData["Success"] = "Selección guardada.";
        return RedirectToAction(nameof(MiSemana));
    }
}
