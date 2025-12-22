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

    public async Task<IActionResult> MiSemana(string? semana, Guid? localizacionId, Guid? sucursalId)
    {
        var empleadoId = _current.EmpleadoId;
        if (empleadoId == null) return RedirectToAction("Login", "Account");
        var estadoEmpleado = await _db.Empleados.Where(e => e.Id == empleadoId).Select(e => e.Estado).FirstAsync();
        var noHabilitado = estadoEmpleado != EmpleadoEstado.Habilitado;
        if (noHabilitado)
        {
            ViewBag.NoHabilitado = true;
            TempData["Error"] = "Tu cuenta no esta habilitada para seleccionar platos.";
        }

        var empleadoDatos = await _db.Empleados
            .AsNoTracking()
            .Where(e => e.Id == empleadoId)
            .Select(e => new
            {
                e.Estado,
                e.Nombre,
                e.Codigo,
                e.EsJefe,
                e.EsSubsidiado,
                e.SubsidioTipo,
                e.SubsidioValor,
                SucursalPrincipalId = e.SucursalId
            })
            .FirstAsync();

        // La "dependencia" del empleado es su sucursal principal: de ahi sale el menu y las reglas de subsidio.
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

        var sucursalPrincipalId = sucursalDependencia.Id;
        var sucursalesPermitidas = new HashSet<Guid> { sucursalPrincipalId };
        var localizacionesAsignadasIds = await _db.EmpleadosLocalizaciones
            .AsNoTracking()
            .Where(el => el.EmpleadoId == empleadoId)
            .Select(el => el.LocalizacionId)
            .ToListAsync();
        var tieneLocalizacionesAsignadas = localizacionesAsignadasIds.Count > 0;

        var localizacionesRaw = await _db.Localizaciones
            .AsNoTracking()
            .Where(l => l.SucursalId == sucursalPrincipalId)
            .Select(l => new { l.Id, l.Nombre, l.SucursalId })
            .ToListAsync();
        if (tieneLocalizacionesAsignadas)
            localizacionesRaw = localizacionesRaw.Where(l => localizacionesAsignadasIds.Contains(l.Id)).ToList();

        var sucursalesEntregaMap = new Dictionary<Guid, string>
        {
            [sucursalPrincipalId] = sucursalDependencia.Nombre
        };

        var localizacionesEntregaInfo = localizacionesRaw
            .GroupBy(l => l.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(l => new
            {
                l.Id,
                l.Nombre,
                l.SucursalId,
                SucursalNombre = sucursalesEntregaMap.TryGetValue(l.SucursalId, out var sucNombre) ? sucNombre : string.Empty,
                Etiqueta = l.Nombre
            })
            .ToList();

        var localizacionesEntregaDisponibles = localizacionesEntregaInfo
            .OrderBy(l => l.Nombre)
            .Select(l => new ValueTuple<Guid, string>(l.Id, l.Etiqueta))
            .ToList();

        Guid? localizacionEntregaId = localizacionId;
        if (!localizacionEntregaId.HasValue || localizacionEntregaId.Value == Guid.Empty)
        {
            localizacionEntregaId = localizacionesEntregaInfo
                .OrderBy(l => l.Nombre)
                .Select(l => (Guid?)l.Id)
                .FirstOrDefault();
        }
        if (localizacionEntregaId.HasValue && !localizacionesEntregaInfo.Any(l => l.Id == localizacionEntregaId.Value))
            localizacionEntregaId = localizacionesEntregaInfo.OrderBy(l => l.Nombre).Select(l => (Guid?)l.Id).FirstOrDefault();

        var localizacionEntrega = localizacionesEntregaInfo.FirstOrDefault(l => l.Id == localizacionEntregaId);
        var sucursalEntregaId = localizacionEntrega?.SucursalId ?? empleadoDatos.SucursalPrincipalId;
        if (!sucursalesPermitidas.Contains(sucursalEntregaId))
            return Forbid();
        var sucursalEntregaNombre = localizacionEntrega?.SucursalNombre ?? sucursalDependencia.Nombre;

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
                EmpleadoCodigo = empleadoDatos.Codigo,
                EmpresaNombre = sucursalDependencia.EmpresaNombre,
                SucursalNombre = sucursalDependencia.Nombre,
                SucursalEntregaId = sucursalEntregaId,
                SucursalEntregaNombre = sucursalEntregaNombre,
                LocalizacionEntregaId = localizacionEntregaId,
                LocalizacionEntregaNombre = localizacionEntrega?.Nombre,
                LocalizacionesEntregaDisponibles = localizacionesEntregaDisponibles
            };
            return View(vmSinMenu);
        }

        var semanaSeleccionada = semanasDisponibles.First(s => s.Clave == semanaClave);

        // Regla de negocio: si el empleado ya tiene al menos una seleccion para esta semana,
        // queda bloqueado a esa sucursal de entrega y no puede seleccionar en otra hasta borrar todo.
        var bloqueoSemana = await _db.RespuestasFormulario
            .AsNoTracking()
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Where(r => r.EmpleadoId == empleadoId
                && r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio == semanaSeleccionada.Inicio
                && r.OpcionMenu.Menu.FechaTermino == semanaSeleccionada.Fin)
            .OrderBy(r => r.OpcionMenuId)
            .Select(r => new { r.LocalizacionEntregaId, r.SucursalEntregaId, MenuId = r.OpcionMenu!.MenuId })
            .FirstOrDefaultAsync();

        if (bloqueoSemana != null)
        {
            if (bloqueoSemana.LocalizacionEntregaId != null && bloqueoSemana.LocalizacionEntregaId.Value != Guid.Empty)
            {
                if (localizacionEntregaId != bloqueoSemana.LocalizacionEntregaId)
                {
                    var localizacionBloqueada = localizacionesEntregaInfo.FirstOrDefault(l => l.Id == bloqueoSemana.LocalizacionEntregaId.Value);
                    var localizacionBloqueadaNombre = localizacionBloqueada?.Nombre ?? "otra localizacion";
                    TempData["Info"] = $"Ya tienes selecciones registradas para esta semana en {localizacionBloqueadaNombre}. Borra todas tus selecciones para poder cambiar de localizacion.";
                    return RedirectToAction(nameof(MiSemana), new { semana = semanaClave, localizacionId = bloqueoSemana.LocalizacionEntregaId });
                }
                localizacionEntregaId = bloqueoSemana.LocalizacionEntregaId;
            }
            else if (bloqueoSemana.SucursalEntregaId != Guid.Empty)
            {
                var localizacionBloqueada = localizacionesEntregaInfo.FirstOrDefault(l => l.SucursalId == bloqueoSemana.SucursalEntregaId);
                if (localizacionBloqueada != null && localizacionEntregaId != localizacionBloqueada.Id)
                {
                    var filialNombreBloqueada = string.IsNullOrWhiteSpace(localizacionBloqueada.SucursalNombre) ? "otra filial" : localizacionBloqueada.SucursalNombre;
                    TempData["Info"] = $"Ya tienes selecciones registradas para esta semana en {filialNombreBloqueada}. Borra todas tus selecciones para poder cambiar de localizacion.";
                    return RedirectToAction(nameof(MiSemana), new { semana = semanaClave, localizacionId = localizacionBloqueada.Id });
                }
                if (localizacionBloqueada != null)
                    localizacionEntregaId = localizacionBloqueada.Id;
            }
        }

        localizacionEntrega = localizacionesEntregaInfo.FirstOrDefault(l => l.Id == localizacionEntregaId);
        sucursalEntregaId = localizacionEntrega?.SucursalId ?? empleadoDatos.SucursalPrincipalId;
        if (!sucursalesPermitidas.Contains(sucursalEntregaId))
            return Forbid();
        sucursalEntregaNombre = localizacionEntrega?.SucursalNombre ?? sucursalDependencia.Nombre;

        var menu = semanaClave == "actual" ? menuActual : menuSiguiente;
        if (menu == null)
            menu = await _menuService.GetEffectiveMenuForSemanaAsync(semanaSeleccionada.Inicio, semanaSeleccionada.Fin, sucursalDependencia.EmpresaId, sucursalDependencia.Id);

        // Si ya existen respuestas, usar el menu asociado a esas respuestas para evitar dobles selecciones
        // en menus distintos durante la misma semana.
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

        // Adicionales fijos del menu (se cobran 100%)
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
        var mensajeBloqueo = bloqueado ? (edicion.MensajeBloqueo ?? $"El menu esta cerrado desde {fechaCierreAuto:dd/MM/yyyy}.") : null;
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

        decimal? CalcularPrecioEmpleado(Opcion? opcion)
        {
            if (opcion == null) return null;
            var ctx = new SubsidioContext(
                opcion.EsSubsidiado,
                empleadoDatos.EsSubsidiado,
                sucursalDependencia.EmpresaSubsidia,
                sucursalDependencia.EmpresaTipo,
                sucursalDependencia.EmpresaValor,
                sucursalDependencia.SucursalSubsidia,
                sucursalDependencia.SucursalTipo,
                sucursalDependencia.SucursalValor,
                empleadoDatos.SubsidioTipo,
                empleadoDatos.SubsidioValor);
            var basePrecio = opcion.Precio ?? opcion.Costo;
            return _subsidios.CalcularPrecioEmpleado(basePrecio, ctx).PrecioEmpleado;
        }

        var totalEmpleado = 0m;
        foreach (var resp in respuestas)
        {
            var om = opciones.FirstOrDefault(x => x.Id == resp.OpcionMenuId);
            var opcion = GetOpcionSeleccionada(om, resp.Seleccion);
            var precio = CalcularPrecioEmpleado(opcion);
            if (precio != null)
                totalEmpleado += precio.Value;

            if (resp.AdicionalOpcionId != null && adicionalesPrecio.TryGetValue(resp.AdicionalOpcionId.Value, out var precioAd))
                totalEmpleado += precioAd;
        }

        ViewBag.FechaCierreAuto = fechaCierreAuto;
        var modelo = new SemanaEmpleadoVM
        {
            EmpleadoId = empleadoId.Value,
            MenuId = menu.Id,
            SucursalEntregaId = sucursalEntregaId,
            SucursalEntregaNombre = sucursalEntregaNombre,
            LocalizacionEntregaId = localizacionEntregaId,
            LocalizacionEntregaNombre = localizacionEntrega?.Nombre,
            LocalizacionesEntregaDisponibles = localizacionesEntregaDisponibles,
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
            EmpleadoCodigo = empleadoDatos.Codigo,
            EsJefe = empleadoDatos.EsJefe,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Filial" : "Empresa",
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
                PrecioEmpleadoA = CalcularPrecioEmpleado(o.OpcionA),
                PrecioEmpleadoB = CalcularPrecioEmpleado(o.OpcionB),
                PrecioEmpleadoC = CalcularPrecioEmpleado(o.OpcionC),
                PrecioEmpleadoD = CalcularPrecioEmpleado(o.OpcionD),
                PrecioEmpleadoE = CalcularPrecioEmpleado(o.OpcionE),
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
            TempData["Error"] = "Tu cuenta no esta habilitada para seleccionar platos.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        if (model.Dias == null || model.Dias.Count == 0)
        {
            TempData["Info"] = "No se recibieron cambios.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = model.LocalizacionEntregaId });
        }

        var empleado = await _db.Empleados
            .AsNoTracking()
            .Where(e => e.Id == empleadoId)
            .Select(e => new { e.SucursalId })
            .FirstAsync();
        var sucursalPrincipalIdPost = empleado.SucursalId;
        var sucursalesPermitidasSet = new HashSet<Guid> { sucursalPrincipalIdPost };

        Guid? localizacionEntregaId = model.LocalizacionEntregaId;
        var localizacionesAsignadas = await _db.EmpleadosLocalizaciones
            .AsNoTracking()
            .Where(el => el.EmpleadoId == empleadoId)
            .Select(el => el.LocalizacionId)
            .ToListAsync();
        if (!localizacionEntregaId.HasValue || localizacionEntregaId == Guid.Empty)
        {
            var baseQuery = _db.Localizaciones
                .AsNoTracking()
                .Where(l => l.SucursalId == sucursalPrincipalIdPost);
            if (localizacionesAsignadas.Count > 0)
                baseQuery = baseQuery.Where(l => localizacionesAsignadas.Contains(l.Id));
            localizacionEntregaId = await baseQuery
                .OrderBy(l => l.Nombre)
                .Select(l => (Guid?)l.Id)
                .FirstOrDefaultAsync();
        }
        Localizacion? localizacionEntrega = null;
        if (localizacionEntregaId.HasValue && localizacionEntregaId.Value != Guid.Empty)
        {
            localizacionEntrega = await _db.Localizaciones
                .AsNoTracking()
                .Include(l => l.Sucursal)
                .FirstOrDefaultAsync(l => l.Id == localizacionEntregaId.Value);
            if (localizacionEntrega == null)
            {
                TempData["Error"] = "Localizacion de entrega no valida.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
            }
        }

        var sucursalEntregaId = localizacionEntrega?.SucursalId ?? model.SucursalEntregaId;
        if (sucursalEntregaId == Guid.Empty)
            sucursalEntregaId = empleado.SucursalId;
        if (!sucursalesPermitidasSet.Contains(sucursalEntregaId))
        {
            TempData["Error"] = "Filial de entrega no permitida.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        if (localizacionesAsignadas.Count > 0)
        {
            if (localizacionEntregaId == null || localizacionEntregaId == Guid.Empty)
            {
                TempData["Error"] = "Selecciona una localizacion de entrega.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
            }
            if (!localizacionesAsignadas.Contains(localizacionEntregaId.Value))
            {
                TempData["Error"] = "Localizacion de entrega no permitida.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
            }
        }
        if (localizacionEntrega != null && localizacionEntrega.SucursalId != sucursalPrincipalIdPost)
        {
            TempData["Error"] = "Localizacion de entrega no pertenece a la filial.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        var menu = await _db.Menus.FirstAsync(m => m.Id == model.MenuId);

        var bloqueoSemana = await _db.RespuestasFormulario
            .AsNoTracking()
            .Include(r => r.OpcionMenu).ThenInclude(om => om!.Menu)
            .Where(r => r.EmpleadoId == empleadoId
                && r.OpcionMenu != null
                && r.OpcionMenu.Menu != null
                && r.OpcionMenu.Menu.FechaInicio == menu.FechaInicio
                && r.OpcionMenu.Menu.FechaTermino == menu.FechaTermino)
            .OrderBy(r => r.OpcionMenuId)
            .Select(r => new { r.LocalizacionEntregaId, r.SucursalEntregaId, MenuId = r.OpcionMenu!.MenuId })
            .FirstOrDefaultAsync();

        if (bloqueoSemana != null)
        {
            if (bloqueoSemana.MenuId != menu.Id)
            {
                TempData["Error"] = "Ya tienes selecciones registradas para esta semana en otro menu. Borra todas tus selecciones antes de cambiar.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = bloqueoSemana.LocalizacionEntregaId });
            }

            if (bloqueoSemana.LocalizacionEntregaId != null && bloqueoSemana.LocalizacionEntregaId.Value != Guid.Empty)
            {
                if (localizacionEntregaId == null || localizacionEntregaId == Guid.Empty)
                {
                    localizacionEntregaId = bloqueoSemana.LocalizacionEntregaId;
                }
                else if (localizacionEntregaId != bloqueoSemana.LocalizacionEntregaId)
                {
                    TempData["Error"] = "Ya tienes selecciones registradas para esta semana en otra localizacion. Borra todas tus selecciones antes de cambiar.";
                    return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = bloqueoSemana.LocalizacionEntregaId });
                }
            }
            else if (bloqueoSemana.SucursalEntregaId != Guid.Empty && bloqueoSemana.SucursalEntregaId != sucursalEntregaId)
            {
                TempData["Error"] = "Ya tienes selecciones registradas para esta semana en otra filial. Borra todas tus selecciones antes de cambiar.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
            }
        }

        if (localizacionEntregaId.HasValue && (localizacionEntrega == null || localizacionEntrega.Id != localizacionEntregaId.Value))
        {
            localizacionEntrega = await _db.Localizaciones
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == localizacionEntregaId.Value);
            if (localizacionEntrega != null)
                sucursalEntregaId = localizacionEntrega.SucursalId;
        }

        var opcionIds = model.Dias.Select(d => d.OpcionMenuId).Distinct().ToList();
        var opciones = await _db.OpcionesMenu
            .AsNoTracking()
            .Where(o => opcionIds.Contains(o.Id))
            .ToListAsync();

        var hoy = _fechas.Hoy();
        var esSemanaActual = hoy >= menu.FechaInicio && hoy <= menu.FechaTermino;
        var edicion = await _menuEdicion.CalcularVentanaAsync(menu, opciones, DateTime.UtcNow, esSemanaActual);
        var encuestaCerrada = _cierre.EstaCerrada(menu);
        if ((encuestaCerrada && !edicion.TieneVentanaActiva) || edicion.Cerrado)
        {
            TempData["Error"] = edicion.MensajeBloqueo ?? "El menu ya esta cerrado. Contacta a tu administrador para cambios.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
        }

        var respuestasActuales = await _db.RespuestasFormulario
            .Where(r => r.EmpleadoId == empleadoId && opcionIds.Contains(r.OpcionMenuId))
            .ToListAsync();
        var respuestasMap = respuestasActuales.ToDictionary(r => r.OpcionMenuId, r => r);

        foreach (var d in model.Dias)
        {
            respuestasMap.TryGetValue(d.OpcionMenuId, out var actual);
            var seleccion = d.Seleccion;
            var adicional = d.AdicionalOpcionId;
            var changed = actual == null
                ? (seleccion != null || adicional != null)
                : (actual.Seleccion != seleccion || actual.AdicionalOpcionId != adicional);

            if (changed && edicion.EdicionPorOpcion.TryGetValue(d.OpcionMenuId, out var puede) && !puede)
            {
                TempData["Error"] = "Fuera de horario para modificar este plato.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
            }
        }

        var removals = false;
        foreach (var d in model.Dias)
        {
            if (d.Seleccion is not ('A' or 'B' or 'C' or 'D' or 'E'))
            {
                if (respuestasMap.TryGetValue(d.OpcionMenuId, out var existente))
                {
                    _db.RespuestasFormulario.Remove(existente);
                    removals = true;
                }
            }
        }
        if (removals)
            await _db.SaveChangesAsync();

        foreach (var d in model.Dias)
        {
            if (d.Seleccion is 'A' or 'B' or 'C' or 'D' or 'E')
            {
                await _menuService.RegistrarSeleccionAsync(
                    empleadoId.Value,
                    d.OpcionMenuId,
                    d.Seleccion.Value,
                    sucursalEntregaId,
                    localizacionEntregaId,
                    d.AdicionalOpcionId);
            }
        }

        TempData["Success"] = "Menu actualizado.";
        return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
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























