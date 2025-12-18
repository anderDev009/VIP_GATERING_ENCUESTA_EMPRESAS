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

    public async Task<IActionResult> MiSemana(string? semana, Guid? sucursalId)
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

        var empleadoDatos = await _db.Empleados
            .AsNoTracking()
            .Where(e => e.Id == empleadoId)
            .Select(e => new
            {
                e.Estado,
                e.Nombre,
                e.EsJefe,
                e.EsSubsidiado,
                SucursalPrincipalId = e.SucursalId
            })
            .FirstAsync();

        var sucursalesExtraIds = await _db.EmpleadosSucursales
            .AsNoTracking()
            .Where(es => es.EmpleadoId == empleadoId)
            .Select(es => es.SucursalId)
            .ToListAsync();
        var sucursalesPermitidas = sucursalesExtraIds.ToHashSet();
        sucursalesPermitidas.Add(empleadoDatos.SucursalPrincipalId);

        // La "dependencia" del empleado es su sucursal principal: de ahA- sale el menA§ y las reglas de subsidio.
        var sucursalDependencia = await _db.Sucursales
            .AsNoTracking()
            .Include(s => s.Empresa)
            .Where(s => s.Id == empleadoDatos.SucursalPrincipalId)
            .Select(s => new
            {
                s.Id,
                s.Nombre,
                s.EmpresaId,
                EmpresaNombre = s.Empresa!.Nombre,
                SucursalSubsidia = s.SubsidiaEmpleados,
                SucursalTipo = s.SubsidioTipo,
                SucursalValor = s.SubsidioValor,
                EmpresaSubsidia = s.Empresa!.SubsidiaEmpleados,
                EmpresaTipo = s.Empresa!.SubsidioTipo,
                EmpresaValor = s.Empresa!.SubsidioValor
            })
            .FirstAsync();

        var sucursalEntregaId = sucursalId ?? empleadoDatos.SucursalPrincipalId;
        if (!sucursalesPermitidas.Contains(sucursalEntregaId))
            return Forbid();

        // La sucursal seleccionada solo define la entrega (no cambia el menA§).
        var sucursalEntrega = await _db.Sucursales
            .AsNoTracking()
            .Where(s => s.Id == sucursalEntregaId && s.EmpresaId == sucursalDependencia.EmpresaId)
            .Select(s => new { s.Id, s.Nombre })
            .FirstOrDefaultAsync();
        if (sucursalEntrega == null)
            return Forbid();

        var sucursalesEntregaDisponibles = await _db.Sucursales
            .AsNoTracking()
            .Where(s => sucursalesPermitidas.Contains(s.Id))
            .OrderBy(s => s.Nombre)
            .Select(s => new ValueTuple<Guid, string>(s.Id, s.Nombre))
            .ToListAsync();

        var hoy = _fechas.Hoy();
        var (inicioActual, finActual) = _fechas.RangoSemanaActual();
        var (inicio, fin) = _fechas.RangoSemanaSiguiente();

        var menuActual = await _menuService.FindEffectiveMenuForSemanaAsync(inicioActual, finActual, sucursalDependencia.EmpresaId, sucursalDependencia.Id, true);
        var menuSiguiente = await _menuService.FindEffectiveMenuForSemanaAsync(inicio, fin, sucursalDependencia.EmpresaId, sucursalDependencia.Id, true);

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
                EmpleadoNombre = empleadoDatos.Nombre,
                EmpresaNombre = sucursalDependencia.EmpresaNombre,
                SucursalNombre = sucursalDependencia.Nombre,
                SucursalEntregaId = sucursalEntrega.Id,
                SucursalEntregaNombre = sucursalEntrega.Nombre,
                SucursalesEntregaDisponibles = sucursalesEntregaDisponibles
            };
            return View(vmSinMenu);
        }

        var semanaSeleccionada = semanasDisponibles.First(s => s.Clave == semanaClave);

        // Regla de negocio: si el empleado ya tiene al menos una selecciA3n para esta semana,
        // queda bloqueado a esa sucursal de entrega y no puede seleccionar en otra hasta borrar todo.
        var bloqueoSemana = await _db.RespuestasFormulario
            .AsNoTracking()
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Where(r => r.EmpleadoId == empleadoId && r.OpcionMenu!.Menu!.FechaInicio == semanaSeleccionada.Inicio && r.OpcionMenu.Menu!.FechaTermino == semanaSeleccionada.Fin)
            .OrderBy(r => r.OpcionMenuId)
            .Select(r => new { r.SucursalEntregaId, MenuId = r.OpcionMenu!.MenuId })
            .FirstOrDefaultAsync();

        if (bloqueoSemana != null && bloqueoSemana.SucursalEntregaId != sucursalEntrega.Id)
        {
            var sucursalBloqueadaNombre = await _db.Sucursales
                .AsNoTracking()
                .Where(s => s.Id == bloqueoSemana.SucursalEntregaId)
                .Select(s => s.Nombre)
                .FirstOrDefaultAsync();
            TempData["Info"] = $"Ya tienes selecciones registradas para esta semana en {(string.IsNullOrWhiteSpace(sucursalBloqueadaNombre) ? "otra sucursal" : sucursalBloqueadaNombre)}. Borra todas tus selecciones para poder cambiar de sucursal.";
            return RedirectToAction(nameof(MiSemana), new { semana = semanaClave, sucursalId = bloqueoSemana.SucursalEntregaId });
        }

        var menu = semanaClave == "actual" ? menuActual : menuSiguiente;
        if (menu == null)
            menu = await _menuService.GetEffectiveMenuForSemanaAsync(semanaSeleccionada.Inicio, semanaSeleccionada.Fin, sucursalDependencia.EmpresaId, sucursalDependencia.Id);

        // Si ya existen respuestas, usar el menA§ asociado a esas respuestas para evitar dobles selecciones
        // en menA§s distintos durante la misma semana.
        if (bloqueoSemana != null && menu.Id != bloqueoSemana.MenuId)
            menu = await _db.Menus.FirstAsync(m => m.Id == bloqueoSemana.MenuId);

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

        // Adicionales fijos del menú (se cobran 100%)
        var adicionales = await _db.MenusAdicionales
            .AsNoTracking()
            .Include(a => a.Opcion)
            .Where(a => a.MenuId == menu.Id)
            .Select(a => new AdicionalDisponibleVM
            {
                Id = a.OpcionId,
                Nombre = a.Opcion != null ? a.Opcion.Nombre : "Sin definir",
                Precio = a.Opcion != null ? (a.Opcion.Precio ?? a.Opcion.Costo) : 0m
            })
            .OrderBy(a => a.Nombre)
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

        var respuestasPorOpcion = respuestas
            .GroupBy(r => r.OpcionMenuId)
            .ToDictionary(g => g.Key, g => g.First());
        var adicionalesPrecio = adicionales.ToDictionary(a => a.Id, a => a.Precio);

        var totalEmpleado = 0m;
        foreach (var resp in respuestas)
        {
            var om = opciones.FirstOrDefault(x => x.Id == resp.OpcionMenuId);
            var opcion = GetOpcionSeleccionada(om, resp.Seleccion);
            if (opcion == null) continue;
            var ctx = new SubsidioContext(opcion.EsSubsidiado, empleadoDatos.EsSubsidiado, sucursalDependencia.EmpresaSubsidia, sucursalDependencia.EmpresaTipo, sucursalDependencia.EmpresaValor, sucursalDependencia.SucursalSubsidia, sucursalDependencia.SucursalTipo, sucursalDependencia.SucursalValor);
            var precio = _subsidios.CalcularPrecioEmpleado(opcion.Precio ?? opcion.Costo, ctx).PrecioEmpleado;
            totalEmpleado += precio;

            if (resp.AdicionalOpcionId != null && adicionalesPrecio.TryGetValue(resp.AdicionalOpcionId.Value, out var precioAd))
                totalEmpleado += precioAd;
        }

        ViewBag.FechaCierreAuto = fechaCierreAuto;
        var modelo = new SemanaEmpleadoVM
        {
            EmpleadoId = empleadoId.Value,
            MenuId = menu.Id,
            SucursalEntregaId = sucursalEntrega.Id,
            SucursalEntregaNombre = sucursalEntrega.Nombre,
            SucursalesEntregaDisponibles = sucursalesEntregaDisponibles,
            AdicionalesDisponibles = adicionales,
            SemanaClave = semanaClave,
            SemanasDisponibles = semanasDisponibles,
            FechaInicio = menu.FechaInicio,
            FechaTermino = menu.FechaTermino,
            Bloqueado = bloqueado,
            BloqueadoPorEstado = noHabilitado,
            MensajeBloqueo = mensajeBloqueo,
            NotaVentana = notaVentana,
            EmpleadoNombre = empleadoDatos.Nombre,
            EsJefe = empleadoDatos.EsJefe,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Dependiente" : "Cliente",
            EmpresaNombre = sucursalDependencia.EmpresaNombre,
            SucursalNombre = sucursalDependencia.Nombre,
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
                Seleccion = respuestasPorOpcion.TryGetValue(o.Id, out var resp) ? resp.Seleccion : null,
                AdicionalOpcionId = respuestasPorOpcion.TryGetValue(o.Id, out var resp2) ? resp2.AdicionalOpcionId : null,
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
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, sucursalId = model.SucursalEntregaId });
        }

        var menu = await _db.Menus.FirstAsync(m => m.Id == model.MenuId);

        // Regla de negocio: si ya hay selecciones en una sucursal para esta semana, no permitir guardar en otra.
        var bloqueoSemana = await _db.RespuestasFormulario
            .AsNoTracking()
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Where(r => r.EmpleadoId == empleadoId && r.OpcionMenu!.Menu!.FechaInicio == menu.FechaInicio && r.OpcionMenu.Menu!.FechaTermino == menu.FechaTermino)
            .OrderBy(r => r.OpcionMenuId)
            .Select(r => new { r.SucursalEntregaId, MenuId = r.OpcionMenu!.MenuId })
            .FirstOrDefaultAsync();
        if (bloqueoSemana != null && (bloqueoSemana.SucursalEntregaId != model.SucursalEntregaId || bloqueoSemana.MenuId != model.MenuId))
        {
            var sucursalBloqueadaNombre = await _db.Sucursales
                .AsNoTracking()
                .Where(s => s.Id == bloqueoSemana.SucursalEntregaId)
                .Select(s => s.Nombre)
                .FirstOrDefaultAsync();
            TempData["Error"] = $"Ya tienes al menos una selecciA3n registrada para esta semana en {(string.IsNullOrWhiteSpace(sucursalBloqueadaNombre) ? "otra sucursal" : sucursalBloqueadaNombre)}. Borra todas tus selecciones antes de cambiar de sucursal.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, sucursalId = bloqueoSemana.SucursalEntregaId });
        }
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
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, sucursalId = model.SucursalEntregaId });
        }

        // Validar sucursal de entrega contra asignaciones del empleado
        var emp = await _db.Empleados.AsNoTracking().FirstAsync(e => e.Id == empleadoId);
        var asignadas = await _db.EmpleadosSucursales.AsNoTracking()
            .Where(es => es.EmpleadoId == empleadoId)
            .Select(es => es.SucursalId)
            .ToListAsync();
        var permitidas = asignadas.ToHashSet();
        permitidas.Add(emp.SucursalId);
        if (!permitidas.Contains(model.SucursalEntregaId))
        {
            TempData["Error"] = "Sucursal de entrega no permitida.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        // Si el empleado tenía respuestas en otros menús de la misma semana, eliminarlas (evita doble pedido)
        var otrosOpcionIds = await _db.OpcionesMenu
            .Include(om => om.Menu)
            .Where(om => om.Menu!.FechaInicio == menu.FechaInicio && om.Menu.FechaTermino == menu.FechaTermino && om.MenuId != menu.Id)
            .Select(om => om.Id)
            .ToListAsync();
        if (otrosOpcionIds.Count > 0)
        {
            var otras = await _db.RespuestasFormulario
                .Where(r => r.EmpleadoId == empleadoId && otrosOpcionIds.Contains(r.OpcionMenuId))
                .ToListAsync();
            if (otras.Count > 0)
            {
                _db.RespuestasFormulario.RemoveRange(otras);
                await _db.SaveChangesAsync();
            }
        }

        // Validar adicionales disponibles para este menú
        var adicionalesPermitidos = await _db.MenusAdicionales
            .AsNoTracking()
            .Where(a => a.MenuId == menu.Id)
            .Select(a => a.OpcionId)
            .ToListAsync();
        var setAdicionales = adicionalesPermitidos.ToHashSet();

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
                Guid? adicional = d.AdicionalOpcionId;
                if (adicional != null && !setAdicionales.Contains(adicional.Value))
                    adicional = null;

                await _menuService.RegistrarSeleccionAsync(empleadoId.Value, d.OpcionMenuId, d.Seleccion.Value, model.SucursalEntregaId, adicional);
            }
        }

        if (fueraDeVentana)
            TempData["Info"] = "Algunas selecciones no se guardaron por estar fuera de la ventana permitida.";
        TempData["Success"] = "Selecciones guardadas.";
        return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, sucursalId = model.SucursalEntregaId });
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

        // Regla: mientras haya al menos una respuesta en la semana, queda bloqueado a esa sucursal de entrega.
        var sucursalPrincipalId = await _db.Empleados
            .AsNoTracking()
            .Where(e => e.Id == empleadoId)
            .Select(e => e.SucursalId)
            .FirstAsync();
        var bloqueoSemana = await _db.RespuestasFormulario
            .AsNoTracking()
            .Include(r => r.OpcionMenu)!.ThenInclude(om => om.Menu)
            .Where(r => r.EmpleadoId == empleadoId && r.OpcionMenu!.Menu!.FechaInicio == menu.FechaInicio && r.OpcionMenu.Menu!.FechaTermino == menu.FechaTermino)
            .OrderBy(r => r.OpcionMenuId)
            .Select(r => new { r.SucursalEntregaId, MenuId = r.OpcionMenu!.MenuId })
            .FirstOrDefaultAsync();
        if (bloqueoSemana != null && bloqueoSemana.MenuId != menu.Id)
        {
            TempData["Error"] = "Ya tienes selecciones registradas para esta semana en otro menA§. Borra todas tus selecciones antes de cambiar.";
            return RedirectToAction(nameof(MiSemana), new { semana = esSemanaActual ? "actual" : "siguiente", sucursalId = bloqueoSemana.SucursalEntregaId });
        }

        var sucursalEntregaId = bloqueoSemana?.SucursalEntregaId ?? sucursalPrincipalId;
        var sucursalesExtraIds = await _db.EmpleadosSucursales
            .AsNoTracking()
            .Where(es => es.EmpleadoId == empleadoId)
            .Select(es => es.SucursalId)
            .ToListAsync();
        var permitidas = sucursalesExtraIds.ToHashSet();
        permitidas.Add(sucursalPrincipalId);
        if (!permitidas.Contains(sucursalEntregaId))
        {
            TempData["Error"] = "Sucursal de entrega no permitida.";
            return RedirectToAction(nameof(MiSemana), new { semana = esSemanaActual ? "actual" : "siguiente" });
        }

        await _menuService.RegistrarSeleccionAsync(empleadoId.Value, opcionMenuId, seleccion.FirstOrDefault(), sucursalEntregaId, null);
        TempData["Success"] = "Seleccion guardada.";
        return RedirectToAction(nameof(MiSemana), new { semana = esSemanaActual ? "actual" : "siguiente", sucursalId = sucursalEntregaId });
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
