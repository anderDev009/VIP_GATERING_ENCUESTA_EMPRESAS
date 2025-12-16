using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.WebUI.Models;
using VIP_GATERING.WebUI.Services;

namespace VIP_GATERING.WebUI.Controllers;

[Authorize(Roles = "Empleado")]
public class EmpleadoController : Controller
{
    private readonly IMenuService _menuService;
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEncuestaCierreService _cierre;
    private readonly ISubsidioService _subsidios;
    private readonly IMenuEdicionService _menuEdicion;
    private readonly IMenuConfiguracionService _menuConfig;
    private readonly IFechaServicio _fechas;
    public EmpleadoController(IMenuService menuService, AppDbContext db, ICurrentUserService current, IEncuestaCierreService cierre, ISubsidioService subsidios, IMenuEdicionService menuEdicion, IMenuConfiguracionService menuConfig, IFechaServicio fechas)
    { _menuService = menuService; _db = db; _current = current; _cierre = cierre; _subsidios = subsidios; _menuEdicion = menuEdicion; _menuConfig = menuConfig; _fechas = fechas; }

    public async Task<IActionResult> MiSemana(string? semana)
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return RedirectToAction("Login", "Account");
        var estadoEmpleado = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.Estado).FirstAsync();
        var noHabilitado = estadoEmpleado != EmpleadoEstado.Habilitado;
        if (noHabilitado)
        {
            ViewBag.NoHabilitado = true;
            TempData["Error"] = "Tu cuenta no esta habilitada para seleccionar opciones.";
        }
        var sucursalEmpresa = await _db.Empleados
            .Include(e => e.Sucursal)!.ThenInclude(s => s!.Empresa)
            .Where(e => e.Id == empleadoId)
            .Select(e => new
            {
                e.Estado,
                e.Nombre,
                e.EsJefe,
                e.EsSubsidiado,
                e.SucursalId,
                SucursalNombre = e.Sucursal!.Nombre,
                EmpresaId = e.Sucursal!.EmpresaId,
                EmpresaNombre = e.Sucursal!.Empresa!.Nombre,
                SucursalSubsidia = e.Sucursal!.SubsidiaEmpleados,
                SucursalTipo = e.Sucursal!.SubsidioTipo,
                SucursalValor = e.Sucursal!.SubsidioValor,
                EmpresaSubsidia = e.Sucursal!.Empresa!.SubsidiaEmpleados,
                EmpresaTipo = e.Sucursal!.Empresa!.SubsidioTipo,
                EmpresaValor = e.Sucursal!.Empresa!.SubsidioValor
            })
            .FirstAsync();

        var hoy = _fechas.Hoy();
        var (inicioActual, finActual) = _fechas.RangoSemanaActual();
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var menuActual = await _menuService.FindEffectiveMenuForSemanaAsync(inicioActual, finActual, sucursalEmpresa.EmpresaId, sucursalEmpresa.SucursalId, true);
        var menuSiguiente = await _menuService.FindEffectiveMenuForSemanaAsync(inicio, fin, sucursalEmpresa.EmpresaId, sucursalEmpresa.SucursalId, true);

        var semanasDisponibles = new List<SemanaOpcionVM>();
        if (menuActual != null && hoy <= finActual)
            semanasDisponibles.Add(new SemanaOpcionVM { Clave = "actual", Etiqueta = $"Semana actual ({inicioActual:dd/MM} - {finActual:dd/MM})", Inicio = inicioActual, Fin = finActual });
        if (menuSiguiente != null)
            semanasDisponibles.Add(new SemanaOpcionVM { Clave = "siguiente", Etiqueta = $"Semana siguiente ({inicio:dd/MM} - {fin:dd/MM})", Inicio = inicio, Fin = fin });

        var semanaClave = (semana ?? string.Empty).ToLowerInvariant();
        if (!semanasDisponibles.Any(s => s.Clave == semanaClave))
            semanaClave = semanasDisponibles.FirstOrDefault()?.Clave ?? "siguiente";

        if (!semanasDisponibles.Any())
        {
            var vmSinMenu = new SemanaEmpleadoVM
            {
                EmpleadoId = empleadoId.Value,
                SemanaClave = semanaClave,
                SemanasDisponibles = semanasDisponibles,
                Bloqueado = true,
                BloqueadoPorEstado = noHabilitado,
                MensajeBloqueo = "No hay menu configurado para la semana actual ni la siguiente.",
                EmpleadoNombre = sucursalEmpresa.Nombre,
                EmpresaNombre = sucursalEmpresa.EmpresaNombre,
                SucursalNombre = sucursalEmpresa.SucursalNombre
            };
            return View(vmSinMenu);
        }

        var semanaSeleccionada = semanasDisponibles.First(s => s.Clave == semanaClave);
        var menu = semanaClave == "actual" ? menuActual : menuSiguiente;
        if (menu == null)
            menu = await _menuService.GetEffectiveMenuForSemanaAsync(semanaSeleccionada.Inicio, semanaSeleccionada.Fin, sucursalEmpresa.EmpresaId, sucursalEmpresa.SucursalId);

        var fechaCierreAuto = _cierre.GetFechaCierreAutomatica(menu);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        var opciones = await _db.OpcionesMenu
            .Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC)
            .Include(o => o.OpcionD).Include(o => o.OpcionE)
            .Include(o => o.Horario)
            .Where(o => o.MenuId == menu.Id)
            .ToListAsync();
        if (opciones.Count == 0)
        {
            var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            foreach (var d in dias)
                await _db.OpcionesMenu.AddAsync(new OpcionMenu { MenuId = menu.Id, DiaSemana = d });
            await _db.SaveChangesAsync();
            opciones = await _db.OpcionesMenu
                .Include(o => o.OpcionA).Include(o => o.OpcionB).Include(o => o.OpcionC)
                .Include(o => o.OpcionD).Include(o => o.OpcionE)
                .Include(o => o.Horario)
                .Where(o => o.MenuId == menu.Id).ToListAsync();
        }
        var opcionIds = opciones.Select(o => o.Id).ToList();
        var respuestas = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();

        var esSemanaActual = semanaClave == "actual";
        var edicion = await _menuEdicion.CalcularVentanaAsync(menu, opciones, DateTime.UtcNow, esSemanaActual);
        var bloqueadoPorTiempo = encuestaCerrada && !edicion.TieneVentanaActiva;
        var bloqueado = bloqueadoPorTiempo || noHabilitado;
        var mensajeBloqueo = bloqueado ? (edicion.MensajeBloqueo ?? $"La encuesta esta cerrada desde {fechaCierreAuto:dd/MM/yyyy}.") : null;
        var configVentana = await _menuConfig.ObtenerAsync();
        string? notaVentana = esSemanaActual
            ? $"Puedes modificar tu menu hasta las {TimeOnly.FromTimeSpan(configVentana.HoraLimiteEdicion):HH\\:mm} del dia anterior."
            : null;
        if (notaVentana != null && edicion.ProximoLimiteUtc.HasValue)
        {
            var prox = DateTime.SpecifyKind(edicion.ProximoLimiteUtc.Value, DateTimeKind.Utc).ToLocalTime();
            notaVentana += $" Proximo corte: {prox:dd/MM HH:mm}.";
        }

        var totalEmpleado = 0m;
        foreach (var resp in respuestas)
        {
            var om = opciones.FirstOrDefault(x => x.Id == resp.OpcionMenuId);
            var opcion = GetOpcionSeleccionada(om, resp.Seleccion);
            if (opcion == null) continue;
            var ctx = new SubsidioContext(opcion.EsSubsidiado, sucursalEmpresa.EsSubsidiado, sucursalEmpresa.EmpresaSubsidia, sucursalEmpresa.EmpresaTipo, sucursalEmpresa.EmpresaValor, sucursalEmpresa.SucursalSubsidia, sucursalEmpresa.SucursalTipo, sucursalEmpresa.SucursalValor);
            var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
            totalEmpleado += precio;
        }

        ViewBag.FechaCierreAuto = fechaCierreAuto;
        var modelo = new SemanaEmpleadoVM
        {
            EmpleadoId = empleadoId.Value,
            MenuId = menu.Id,
            SemanaClave = semanaClave,
            SemanasDisponibles = semanasDisponibles,
            FechaInicio = menu.FechaInicio,
            FechaTermino = menu.FechaTermino,
            Bloqueado = bloqueado,
            BloqueadoPorEstado = noHabilitado,
            MensajeBloqueo = mensajeBloqueo,
            NotaVentana = notaVentana,
            EmpleadoNombre = sucursalEmpresa.Nombre,
            EsJefe = sucursalEmpresa.EsJefe,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Dependiente" : "Cliente",
            EmpresaNombre = sucursalEmpresa.EmpresaNombre,
            SucursalNombre = sucursalEmpresa.SucursalNombre,
            TotalEmpleado = totalEmpleado,
            Dias = opciones.OrderBy(o => o.DiaSemana).ThenBy(o => o.Horario!.Orden).Select(o => new DiaEmpleadoVM
            {
                OpcionMenuId = o.Id,
                DiaSemana = o.DiaSemana,
                HorarioNombre = o.Horario?.Nombre,
                A = o.OpcionA?.Nombre,
                B = o.OpcionB?.Nombre,
                C = o.OpcionC?.Nombre,
                D = o.OpcionD?.Nombre,
                E = o.OpcionE?.Nombre,
                ImagenA = o.OpcionA?.ImagenUrl,
                ImagenB = o.OpcionB?.ImagenUrl,
                ImagenC = o.OpcionC?.ImagenUrl,
                ImagenD = o.OpcionD?.ImagenUrl,
                ImagenE = o.OpcionE?.ImagenUrl,
                OpcionesMaximas = o.OpcionesMaximas == 0 ? 3 : o.OpcionesMaximas,
                Seleccion = respuestas.FirstOrDefault(r => r.OpcionMenuId == o.Id)?.Seleccion,
                Editable = edicion.EdicionPorOpcion.TryGetValue(o.Id, out var ed) ? ed && !noHabilitado : !bloqueadoPorTiempo
            }).ToList()
        };
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarSemana(SemanaEmpleadoVM model)
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return RedirectToAction("Login", "Account");
        var estadoPost = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.Estado).FirstAsync();
        if (estadoPost != EmpleadoEstado.Habilitado)
        {
            TempData["Error"] = "Tu cuenta no esta habilitada para seleccionar opciones.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        if (model.Dias == null || model.Dias.Count == 0)
        {
            TempData["Info"] = "No se recibieron cambios.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        var menu = await _db.Menus.FirstAsync(m => m.Id == model.MenuId);
        var hoy = _fechas.Hoy();
        var esSemanaActual = hoy >= menu.FechaInicio && hoy <= menu.FechaTermino;

        var opciones = await _db.OpcionesMenu
            .Where(o => o.MenuId == menu.Id)
            .ToListAsync();
        var edicion = await _menuEdicion.CalcularVentanaAsync(menu, opciones, DateTime.UtcNow, esSemanaActual);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        if ((encuestaCerrada && !edicion.TieneVentanaActiva) || edicion.Cerrado)
        {
            TempData["Error"] = edicion.MensajeBloqueo ?? "La encuesta ya esta cerrada. Contacta a tu administrador para cambios.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        var opcionIds = model.Dias.Select(d => d.OpcionMenuId).ToList();
        var respuestasActuales = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();

        bool removals = false;
        bool fueraDeVentana = false;
        foreach (var d in model.Dias)
        {
            var editable = edicion.EdicionPorOpcion.TryGetValue(d.OpcionMenuId, out var ed) ? ed : !encuestaCerrada;
            if (!editable)
            {
                if (d.Seleccion is 'A' or 'B' or 'C' or 'D' or 'E') fueraDeVentana = true;
                continue;
            }
            if (d.Seleccion is not ('A' or 'B' or 'C' or 'D' or 'E'))
            {
                var existente = respuestasActuales.FirstOrDefault(r => r.OpcionMenuId == d.OpcionMenuId);
                if (existente != null)
                {
                    _db.RespuestasFormulario.Remove(existente);
                    removals = true;
                }
            }
        }
        if (removals) await _db.SaveChangesAsync();

        foreach (var d in model.Dias)
        {
            var editable = edicion.EdicionPorOpcion.TryGetValue(d.OpcionMenuId, out var ed) ? ed : !encuestaCerrada;
            if (!editable)
            {
                if (d.Seleccion is 'A' or 'B' or 'C' or 'D' or 'E') fueraDeVentana = true;
                continue;
            }
            if (d.Seleccion is 'A' or 'B' or 'C' or 'D' or 'E')
            {
                await _menuService.RegistrarSeleccionAsync(empleadoId.Value, d.OpcionMenuId, d.Seleccion.Value);
            }
        }

        if (fueraDeVentana)
            TempData["Info"] = "Algunas selecciones no se guardaron por estar fuera de la ventana permitida.";
        TempData["Success"] = "Selecciones guardadas.";
        return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
    }

    // Acción legacy de guardado por día (permite override de cierres)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Seleccionar(Guid opcionMenuId, string seleccion)
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return RedirectToAction("Login", "Account");
        var estadoSel = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.Estado).FirstAsync();
        if (estadoSel != EmpleadoEstado.Habilitado)
        {
            TempData["Error"] = "Tu cuenta no esta habilitada para seleccionar opciones.";
            return RedirectToAction(nameof(MiSemana));
        }
        var opcionMenu = await _db.OpcionesMenu.FirstAsync(om => om.Id == opcionMenuId);
        var menu = await _db.Menus.FirstAsync(m => m.Id == opcionMenu.MenuId);
        var hoy = _fechas.Hoy();
        var esSemanaActual = hoy >= menu.FechaInicio && hoy <= menu.FechaTermino;
        var edicion = await _menuEdicion.CalcularVentanaAsync(menu, new[] { opcionMenu }, DateTime.UtcNow, esSemanaActual);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        var permitido = edicion.EdicionPorOpcion.TryGetValue(opcionMenuId, out var puede) ? puede : !encuestaCerrada;
        if ((encuestaCerrada && !edicion.TieneVentanaActiva) || edicion.Cerrado || !permitido)
        {
            TempData["Error"] = edicion.MensajeBloqueo ?? "La encuesta ya esta cerrada. Contacta a tu administrador para cambios.";
            return RedirectToAction(nameof(MiSemana), new { semana = esSemanaActual ? "actual" : "siguiente" });
        }
        await _menuService.RegistrarSeleccionAsync(empleadoId.Value, opcionMenuId, seleccion.FirstOrDefault());
        TempData["Success"] = "Seleccion guardada.";
        return RedirectToAction(nameof(MiSemana), new { semana = esSemanaActual ? "actual" : "siguiente" });
    }

    private static Opcion? GetOpcionSeleccionada(OpcionMenu? opcionMenu, char seleccion)
    {
        if (opcionMenu == null) return null;
        var max = opcionMenu.OpcionesMaximas == 0 ? 3 : Math.Clamp(opcionMenu.OpcionesMaximas, 1, 5);
        return seleccion switch
        {
            'A' when max >= 1 => opcionMenu.OpcionA,
            'B' when max >= 2 => opcionMenu.OpcionB,
            'C' when max >= 3 => opcionMenu.OpcionC,
            'D' when max >= 4 => opcionMenu.OpcionD,
            'E' when max >= 5 => opcionMenu.OpcionE,
            _ => null
        };
    }
}
