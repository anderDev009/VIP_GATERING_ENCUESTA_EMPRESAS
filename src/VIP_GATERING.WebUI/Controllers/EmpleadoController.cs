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

    public async Task<IActionResult> MiSemana(string? semana, int? localizacionId, int? sucursalId)
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
        var empleadoNombre = !string.IsNullOrWhiteSpace(empleadoDatos.Nombre)
            ? empleadoDatos.Nombre
            : (!string.IsNullOrWhiteSpace(empleadoDatos.Codigo) ? empleadoDatos.Codigo : "Sin nombre");

        var sucursalPrincipalId = sucursalDependencia.Id;
        var empresaId = sucursalDependencia.EmpresaId;
        var sucursalesEmpresa = await _db.Sucursales
            .AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .Select(s => new { s.Id, s.Nombre })
            .ToListAsync();
        var sucursalesPermitidas = sucursalesEmpresa.Select(s => s.Id).ToHashSet();
        if (sucursalesPermitidas.Count == 0) sucursalesPermitidas.Add(sucursalPrincipalId);

        var localizacionDefectoId = await _db.EmpleadosLocalizaciones
            .AsNoTracking()
            .Where(el => el.EmpleadoId == empleadoId)
            .Select(el => (int?)el.LocalizacionId)
            .FirstOrDefaultAsync();

        var localizacionesAsignadasIds = await _db.EmpleadosLocalizaciones
            .AsNoTracking()
            .Where(el => el.EmpleadoId == empleadoId)
            .Select(el => el.LocalizacionId)
            .ToListAsync();

        var localizacionesQuery = _db.Localizaciones
            .AsNoTracking()
            .Include(l => l.Sucursal)
            .Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaId);
        if (localizacionesAsignadasIds.Count > 0
            && !string.Equals(sucursalDependencia.EmpresaNombre, "GRUPO UNIVERSAL", StringComparison.OrdinalIgnoreCase))
            localizacionesQuery = localizacionesQuery.Where(l => localizacionesAsignadasIds.Contains(l.Id));
        var localizacionesRaw = await localizacionesQuery.ToListAsync();

        var localizacionesEntregaInfo = localizacionesRaw
            .GroupBy(l => l.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var preferida = g.FirstOrDefault(l => l.SucursalId == sucursalPrincipalId);
                var loc = preferida ?? g.OrderBy(l => l.Id).First();
                var sucNombre = loc.Sucursal?.Nombre ?? string.Empty;
                return new
                {
                    loc.Id,
                    loc.Nombre,
                    loc.SucursalId,
                    SucursalNombre = sucNombre,
                    Etiqueta = loc.Nombre
                };
            })
            .ToList();
        localizacionesEntregaInfo = localizacionesEntregaInfo
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .ToList();

        var localizacionesEntregaDisponibles = localizacionesEntregaInfo
            .OrderBy(l => l.Etiqueta)
            .Select(l => new ValueTuple<int, string>(l.Id, l.Etiqueta))
            .ToList();

        int? localizacionEntregaId = localizacionId ?? localizacionDefectoId;
        if (!localizacionEntregaId.HasValue || localizacionEntregaId.Value == 0)
        {
            localizacionEntregaId = localizacionesEntregaInfo
                .OrderBy(l => l.Etiqueta)
                .Select(l => (int?)l.Id)
                .FirstOrDefault();
        }
        if (localizacionEntregaId.HasValue && !localizacionesEntregaInfo.Any(l => l.Id == localizacionEntregaId.Value))
            localizacionEntregaId = localizacionesEntregaInfo.OrderBy(l => l.Etiqueta).Select(l => (int?)l.Id).FirstOrDefault();

        var localizacionEntrega = localizacionesEntregaInfo.FirstOrDefault(l => l.Id == localizacionEntregaId);
        var sucursalEntregaId = localizacionEntrega?.SucursalId ?? empleadoDatos.SucursalPrincipalId;
        if (!sucursalesPermitidas.Contains(sucursalEntregaId))
            return Forbid();
        var sucursalEntregaNombre = localizacionEntrega?.SucursalNombre ?? sucursalDependencia.Nombre;
        TimeOnly? horaInicioAlmuerzo = null;
        TimeOnly? horaFinAlmuerzo = null;

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
                Bloqueado = false,
                BloqueadoPorEstado = noHabilitado,
                MensajeBloqueo = null,
                MensajeSinMenu = "Aun no se ha configurado el menú para esta semana. Contacta con tu administrador para más información.",
                MenuDisponible = false,
                FechaInicio = inicioActual,
                FechaTermino = finActual,
                EmpleadoNombre = empleadoNombre,
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
            if (bloqueoSemana.LocalizacionEntregaId != null && bloqueoSemana.LocalizacionEntregaId.Value != 0)
            {
                if (localizacionEntregaId != bloqueoSemana.LocalizacionEntregaId)
                {
                    var localizacionBloqueada = localizacionesEntregaInfo.FirstOrDefault(l => l.Id == bloqueoSemana.LocalizacionEntregaId.Value);
                    if (localizacionBloqueada != null)
                    {
                        var localizacionBloqueadaNombre = localizacionBloqueada.Nombre;
                        TempData["Info"] = $"Ya tienes selecciones registradas para esta semana en {localizacionBloqueadaNombre}. Borra todas tus selecciones para poder cambiar de localizacion.";
                        return RedirectToAction(nameof(MiSemana), new { semana = semanaClave, localizacionId = bloqueoSemana.LocalizacionEntregaId });
                    }
                }
                localizacionEntregaId = bloqueoSemana.LocalizacionEntregaId;
            }
            else if (bloqueoSemana.SucursalEntregaId != 0)
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
        var horaSeleccionada = respuestas.Select(r => r.HoraAlmuerzo).FirstOrDefault(h => h != null);
        var horaElegida = horaSeleccionada;
        var horarioIds = opciones.Where(o => o.HorarioId != null).Select(o => o.HorarioId!.Value).Distinct().ToList();
        var horariosSlots = await _db.SucursalesHorariosSlots
            .AsNoTracking()
            .Where(sh => sh.SucursalId == sucursalEntregaId && horarioIds.Contains(sh.HorarioId))
            .Select(sh => new { sh.HorarioId, sh.Hora })
            .ToListAsync();
        var slotsMap = horariosSlots
            .GroupBy(h => h.HorarioId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Hora).Distinct().OrderBy(x => x).ToList());
        var horariosHoras = opciones
            .Where(o => o.HorarioId != null)
            .GroupBy(o => o.HorarioId!.Value)
            .Select(g =>
            {
                var horarioNombre = g.First().Horario?.Nombre ?? "Horario";
                slotsMap.TryGetValue(g.Key, out var slots);
                var horaSel = respuestas
                    .Where(r => g.Select(x => x.Id).Contains(r.OpcionMenuId))
                    .Select(r => r.HoraAlmuerzo)
                    .FirstOrDefault(h => h != null);
                return new HorarioHoraVM
                {
                    HorarioId = g.Key,
                    HorarioNombre = horarioNombre,
                    HoraSeleccionada = horaSel?.ToString("HH:mm"),
                    HorasDisponibles = slots?.Select(s => s.ToString("HH:mm")).ToList() ?? new List<string>()
                };
            })
            .OrderBy(h => h.HorarioNombre)
            .ToList();

        const decimal itbisRate = 0.18m;

        // Adicionales fijos del menu (se cobran 100%)
        var adicionales = await _db.MenusAdicionales
            .AsNoTracking()
            .Include(a => a.Opcion)
            .Where(a => a.MenuId == menu.Id)
            .Select(a => new AdicionalDisponibleVM
            {
                Id = a.OpcionId,
                Nombre = a.Opcion != null ? a.Opcion.Nombre : "Sin definir",
                Precio = a.Opcion != null
                    ? Math.Round((a.Opcion.Precio ?? a.Opcion.Costo) + ((a.Opcion.LlevaItbis ? (a.Opcion.Precio ?? a.Opcion.Costo) * itbisRate : 0m)), 2)
                    : 0m
            })
            .OrderBy(a => a.Nombre)
            .ToListAsync();

        var esSemanaActual = semanaClave == "actual";
        var edicion = await _menuEdicion.CalcularVentanaAsync(menu, opciones, DateTime.Now, esSemanaActual);
        var bloqueadoPorTiempo = encuestaCerrada && !edicion.TieneVentanaActiva;
        var bloqueado = bloqueadoPorTiempo || noHabilitado;
        var mensajeBloqueo = bloqueado ? (edicion.MensajeBloqueo ?? $"El menu esta cerrado desde {fechaCierreAuto:dd/MM/yyyy}.") : null;
        var configVentana = await _menuConfig.ObtenerAsync();
        string? notaVentana = esSemanaActual
            ? $"Puedes modificar tu menu hasta las {TimeOnly.FromTimeSpan(configVentana.HoraLimiteEdicion):HH\\:mm} del dia anterior."
            : null;
        if (notaVentana != null && edicion.ProximoLimiteUtc.HasValue)
        {
            var prox = edicion.ProximoLimiteUtc.Value;
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

        decimal? CalcularPrecioEmpleadoConItbis(Opcion? opcion)
        {
            if (opcion == null) return null;
            var basePrecio = opcion.Precio ?? opcion.Costo;
            if (basePrecio <= 0) return 0m;
            var precioEmpleado = CalcularPrecioEmpleado(opcion) ?? basePrecio;
            var itbis = opcion.LlevaItbis ? Math.Round(basePrecio * itbisRate, 2) : 0m;
            var total = basePrecio + itbis;
            var ratio = basePrecio > 0 ? Math.Clamp(precioEmpleado / basePrecio, 0m, 1m) : 1m;
            return Math.Round(total * ratio, 2);
        }

        var totalEmpleado = 0m;
        foreach (var resp in respuestas)
        {
            var om = opciones.FirstOrDefault(x => x.Id == resp.OpcionMenuId);
            var opcion = GetOpcionSeleccionada(om, resp.Seleccion);
            var precio = CalcularPrecioEmpleadoConItbis(opcion);
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
            EmpleadoNombre = empleadoNombre,
            EmpleadoCodigo = empleadoDatos.Codigo,
            EsJefe = empleadoDatos.EsJefe,
            RespuestasCount = respuestas.Count,
            TotalDias = opciones.Count,
            OrigenMenu = menu.SucursalId != null ? "Filial" : "Empresa",
            EmpresaNombre = sucursalDependencia.EmpresaNombre,
            SucursalNombre = sucursalDependencia.Nombre,
            HoraAlmuerzoSeleccionada = horaElegida?.ToString("HH:mm"),
            HoraAlmuerzoInicio = horaInicioAlmuerzo?.ToString("HH:mm"),
            HoraAlmuerzoFin = horaFinAlmuerzo?.ToString("HH:mm"),
            TotalEmpleado = totalEmpleado,
            HorariosHoras = horariosHoras,
            Dias = opciones.OrderBy(o => o.DiaSemana).ThenBy(o => o.Horario!.Orden).Select(o =>
            {
                var fechaDia = _fechas.ObtenerFechaDelDia(menu.FechaInicio, o.DiaSemana);
                var diaFuturo = fechaDia > hoy;
                var puedeEditarPorOpcion = edicion.EdicionPorOpcion.TryGetValue(o.Id, out var ed) ? ed && !noHabilitado : !bloqueadoPorTiempo;
                var editableFinal = diaFuturo && puedeEditarPorOpcion;
                if (o.DiaCerrado) editableFinal = false;
                return new DiaEmpleadoVM
                {
                    OpcionMenuId = o.Id,
                    DiaSemana = o.DiaSemana,
                    HorarioId = o.HorarioId,
                    HorarioNombre = o.Horario?.Nombre,
                    DiaCerrado = o.DiaCerrado,
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
                    PrecioEmpleadoA = CalcularPrecioEmpleadoConItbis(o.OpcionA),
                    PrecioEmpleadoB = CalcularPrecioEmpleadoConItbis(o.OpcionB),
                    PrecioEmpleadoC = CalcularPrecioEmpleadoConItbis(o.OpcionC),
                    PrecioEmpleadoD = CalcularPrecioEmpleadoConItbis(o.OpcionD),
                    PrecioEmpleadoE = CalcularPrecioEmpleadoConItbis(o.OpcionE),
                    OpcionesMaximas = o.OpcionesMaximas == 0 ? 3 : o.OpcionesMaximas,
                    Seleccion = respuestasPorOpcion.TryGetValue(o.Id, out var resp) ? resp.Seleccion : null,
                    AdicionalOpcionId = respuestasPorOpcion.TryGetValue(o.Id, out var resp2) ? resp2.AdicionalOpcionId : null,
                    Editable = editableFinal
                };
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
        var empresaId = await _db.Sucursales
            .AsNoTracking()
            .Where(s => s.Id == sucursalPrincipalIdPost)
            .Select(s => s.EmpresaId)
            .FirstAsync();
        var sucursalesPermitidasSet = (await _db.Sucursales
            .AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .Select(s => s.Id)
            .ToListAsync())
            .ToHashSet();
        if (sucursalesPermitidasSet.Count == 0) sucursalesPermitidasSet.Add(sucursalPrincipalIdPost);

        int? localizacionEntregaId = model.LocalizacionEntregaId;
        var localizacionDefectoId = await _db.EmpleadosLocalizaciones
            .AsNoTracking()
            .Where(el => el.EmpleadoId == empleadoId)
            .Select(el => (int?)el.LocalizacionId)
            .FirstOrDefaultAsync();
        var localizacionesAsignadasIds = await _db.EmpleadosLocalizaciones
            .AsNoTracking()
            .Where(el => el.EmpleadoId == empleadoId)
            .Select(el => el.LocalizacionId)
            .ToListAsync();
        if (!localizacionEntregaId.HasValue || localizacionEntregaId == 0)
        {
            if (localizacionDefectoId.HasValue)
            {
                localizacionEntregaId = localizacionDefectoId;
            }
            else if (localizacionesAsignadasIds.Count > 0)
            {
                localizacionEntregaId = localizacionesAsignadasIds.First();
            }
            else
            {
                localizacionEntregaId = await _db.Localizaciones
                    .AsNoTracking()
                    .Include(l => l.Sucursal)
                    .Where(l => l.Sucursal != null && l.Sucursal.EmpresaId == empresaId)
                    .OrderBy(l => l.Nombre)
                    .Select(l => (int?)l.Id)
                    .FirstOrDefaultAsync();
            }
        }
        Localizacion? localizacionEntrega = null;
        if (localizacionEntregaId.HasValue && localizacionEntregaId.Value != 0)
        {
            var empresaNombre = await _db.Empresas
                .AsNoTracking()
                .Where(e => e.Id == empresaId)
                .Select(e => e.Nombre)
                .FirstOrDefaultAsync();
            var esGrupoUniversal = string.Equals(empresaNombre, "GRUPO UNIVERSAL", StringComparison.OrdinalIgnoreCase);
            if (!esGrupoUniversal && localizacionesAsignadasIds.Count > 0 && !localizacionesAsignadasIds.Contains(localizacionEntregaId.Value))
            {
                TempData["Error"] = "Localizacion de entrega no permitida.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
            }
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
        if (sucursalEntregaId == 0)
            sucursalEntregaId = empleado.SucursalId;
        if (!sucursalesPermitidasSet.Contains(sucursalEntregaId))
        {
            TempData["Error"] = "Filial de entrega no permitida.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }

        if (localizacionEntregaId == null || localizacionEntregaId == 0)
        {
            TempData["Error"] = "Selecciona una localizacion de entrega.";
            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave });
        }
        if (localizacionEntrega != null && localizacionEntrega.Sucursal != null && localizacionEntrega.Sucursal.EmpresaId != empresaId)
        {
            TempData["Error"] = "Localizacion de entrega no pertenece a la empresa.";
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

            if (bloqueoSemana.LocalizacionEntregaId != null && bloqueoSemana.LocalizacionEntregaId.Value != 0)
            {
                if (localizacionEntregaId == null || localizacionEntregaId == 0)
                {
                    localizacionEntregaId = bloqueoSemana.LocalizacionEntregaId;
                }
                else if (localizacionEntregaId != bloqueoSemana.LocalizacionEntregaId)
                {
                    TempData["Error"] = "Ya tienes selecciones registradas para esta semana en otra localizacion. Borra todas tus selecciones antes de cambiar.";
                    return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = bloqueoSemana.LocalizacionEntregaId });
                }
            }
            else if (bloqueoSemana.SucursalEntregaId != 0 && bloqueoSemana.SucursalEntregaId != sucursalEntregaId)
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
        var edicion = await _menuEdicion.CalcularVentanaAsync(menu, opciones, DateTime.Now, esSemanaActual);
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

        var horasPorHorario = new Dictionary<int, TimeOnly?>();
        foreach (var key in Request.Form.Keys)
        {
            if (!key.StartsWith("HorasHorario[", StringComparison.OrdinalIgnoreCase)) continue;
            var idPart = key.Substring("HorasHorario[".Length).TrimEnd(']');
            if (!int.TryParse(idPart, out var horarioId)) continue;
            var valor = Request.Form[key].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(valor))
            {
                horasPorHorario[horarioId] = null;
                continue;
            }
            if (!TimeOnly.TryParse(valor, out var parsedHora))
            {
                TempData["Error"] = "La hora de almuerzo no es valida.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
            }
            horasPorHorario[horarioId] = parsedHora;
        }

        var horariosIds = opciones.Where(o => o.HorarioId != null).Select(o => o.HorarioId!.Value).Distinct().ToList();
        var horariosSlots = await _db.SucursalesHorariosSlots
            .AsNoTracking()
            .Where(sh => sh.SucursalId == sucursalEntregaId && horariosIds.Contains(sh.HorarioId))
            .Select(sh => new { sh.HorarioId, sh.Hora })
            .ToListAsync();
        var slotsMap = horariosSlots
            .GroupBy(h => h.HorarioId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Hora).Distinct().ToHashSet());

        var horariosConSeleccion = new HashSet<int>();
        foreach (var d in model.Dias)
        {
            if (d.Seleccion is not ('A' or 'B' or 'C' or 'D' or 'E')) continue;
            var horarioId = opciones.FirstOrDefault(o => o.Id == d.OpcionMenuId)?.HorarioId;
            if (horarioId.HasValue)
                horariosConSeleccion.Add(horarioId.Value);
        }

        var horaActual = TimeOnly.FromDateTime(DateTime.Now);
        var hoyFecha = DateOnly.FromDateTime(DateTime.Now);
        var esDiaSemana = hoyFecha >= menu.FechaInicio && hoyFecha <= menu.FechaTermino;
        var diaSemanaHoy = hoyFecha.DayOfWeek;

        foreach (var horarioId in horariosConSeleccion)
        {
            if (!horasPorHorario.TryGetValue(horarioId, out var horaTmp) || horaTmp == null)
            {
                TempData["Error"] = "Selecciona la hora antes de guardar tu menu.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
            }
        }

        foreach (var horarioId in horariosConSeleccion)
        {
            horasPorHorario.TryGetValue(horarioId, out var horaSeleccionada);
            slotsMap.TryGetValue(horarioId, out var slots);
            if (slots != null && slots.Count > 0)
            {
                if (horaSeleccionada == null)
                {
                    TempData["Error"] = "Selecciona una hora permitida para el horario.";
                    return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
                }
                if (!slots.Contains(horaSeleccionada.Value))
                {
                    TempData["Error"] = "La hora seleccionada no esta dentro de las opciones permitidas.";
                    return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
                }
            }
            else
            {
                TempData["Error"] = "No hay horarios configurados para este horario.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
            }

            if (esDiaSemana && horaSeleccionada != null && horaSeleccionada <= horaActual)
            {
                TempData["Error"] = "La hora seleccionada debe ser mayor a la hora actual.";
                return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
            }

            if (esDiaSemana)
            {
                var opcionHoy = opciones.FirstOrDefault(o => o.DiaSemana == diaSemanaHoy && o.HorarioId == horarioId);
                if (opcionHoy != null)
                {
                    var horaAsignada = respuestasActuales
                        .Where(r => r.OpcionMenuId == opcionHoy.Id)
                        .Select(r => r.HoraAlmuerzo)
                        .FirstOrDefault(h => h != null);
                    if (horaAsignada != null && horaActual >= horaAsignada.Value)
                    {
                        if (horaSeleccionada != null && horaSeleccionada != horaAsignada)
                        {
                            TempData["Error"] = $"No puedes cambiar tu hora de almuerzo despues de las {horaAsignada:HH\\:mm}.";
                            return RedirectToAction(nameof(MiSemana), new { semana = model.SemanaClave, localizacionId = localizacionEntregaId });
                        }
                        horasPorHorario[horarioId] = horaAsignada;
                    }
                }
            }
        }

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
                var horarioId = opciones.FirstOrDefault(o => o.Id == d.OpcionMenuId)?.HorarioId;
                TimeOnly? horaSel = null;
                if (horarioId.HasValue && horasPorHorario.TryGetValue(horarioId.Value, out var horaTmp))
                    horaSel = horaTmp;
                await _menuService.RegistrarSeleccionAsync(
                    empleadoId.Value,
                    d.OpcionMenuId,
                    d.Seleccion.Value,
                    sucursalEntregaId,
                    localizacionEntregaId,
                    d.AdicionalOpcionId,
                    horaSel);
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























