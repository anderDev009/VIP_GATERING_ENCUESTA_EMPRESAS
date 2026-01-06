using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Application.Services;

public interface IMenuService
{
    Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(CancellationToken ct = default);
    Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(int? empresaId, int? sucursalId, CancellationToken ct = default);
    Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, CancellationToken ct = default);
    Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId, CancellationToken ct = default);
    Task<Menu?> FindMenuAsync(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId, CancellationToken ct = default);
    Task<Menu> GetEffectiveMenuForSemanaAsync(DateOnly inicio, DateOnly fin, int empresaId, int? sucursalId, CancellationToken ct = default);
    Task<Menu?> FindEffectiveMenuForSemanaAsync(DateOnly inicio, DateOnly fin, int empresaId, int? sucursalId, bool requireOpcionesConfiguradas = false, CancellationToken ct = default);
    Task<IReadOnlyList<OpcionMenu>> ObtenerOpcionesEmpleadoAsync(int empleadoId, CancellationToken ct = default);
    Task RegistrarSeleccionAsync(int empleadoId, int opcionMenuId, char seleccion, CancellationToken ct = default);
    Task RegistrarSeleccionAsync(int empleadoId, int opcionMenuId, char seleccion, int sucursalEntregaId, int? adicionalOpcionId, CancellationToken ct = default);
    Task RegistrarSeleccionAsync(int empleadoId, int opcionMenuId, char seleccion, int sucursalEntregaId, int? localizacionEntregaId, int? adicionalOpcionId, CancellationToken ct = default);
}

public class MenuService : IMenuService
{
    private readonly IRepository<Menu> _menus;
    private readonly IRepository<Opcion> _opciones;
    private readonly IRepository<OpcionMenu> _opcionesMenu;
    private readonly IRepository<Horario> _horarios;
    private readonly IRepository<RespuestaFormulario> _respuestas;
    private readonly IRepository<SucursalHorario> _sucursalHorarios;
    private readonly IRepository<Empleado> _empleados;
    private readonly IRepository<EmpleadoSucursal> _empleadosSucursales;
    private readonly IRepository<Localizacion> _localizaciones;
    private readonly IRepository<EmpleadoLocalizacion> _empleadosLocalizaciones;
    private readonly IRepository<MenuAdicional> _menusAdicionales;
    private readonly IUnitOfWork _uow;
    private readonly IFechaServicio _fechaSvc;

    public MenuService(IRepository<Menu> menus,
        IRepository<Opcion> opciones,
        IRepository<OpcionMenu> opcionesMenu,
        IRepository<Horario> horarios,
        IRepository<RespuestaFormulario> respuestas,
        IRepository<SucursalHorario> sucursalHorarios,
        IRepository<Empleado> empleados,
        IRepository<EmpleadoSucursal> empleadosSucursales,
        IRepository<MenuAdicional> menusAdicionales,
        IRepository<Localizacion> localizaciones,
        IRepository<EmpleadoLocalizacion> empleadosLocalizaciones,
        IUnitOfWork uow,
        IFechaServicio fechaSvc)
    {
        _menus = menus;
        _opciones = opciones;
        _opcionesMenu = opcionesMenu;
        _horarios = horarios;
        _respuestas = respuestas;
        _sucursalHorarios = sucursalHorarios;
        _empleados = empleados;
        _empleadosSucursales = empleadosSucursales;
        _menusAdicionales = menusAdicionales;
        _localizaciones = localizaciones;
        _empleadosLocalizaciones = empleadosLocalizaciones;
        _uow = uow;
        _fechaSvc = fechaSvc;
    }

    public async Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(CancellationToken ct = default)
    {
        var (inicio, fin) = _fechaSvc.RangoSemanaSiguiente();
        return await GetOrCreateMenuAsync(inicio, fin, ct);
    }

    public async Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(int? empresaId, int? sucursalId, CancellationToken ct = default)
    {
        var (inicio, fin) = _fechaSvc.RangoSemanaSiguiente();
        return await GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId, ct);
    }

    private async Task<IReadOnlyList<Horario>> EnsureHorariosAsync(CancellationToken ct)
    {
        var list = await _horarios.ListAsync(h => h.Activo, ct);
        if (list.Count == 0)
        {
            await _horarios.AddAsync(new Horario { Nombre = "Desayuno", Orden = 1, Activo = true }, ct);
            await _horarios.AddAsync(new Horario { Nombre = "Almuerzo", Orden = 2, Activo = true }, ct);
            await _uow.SaveChangesAsync(ct);
            list = await _horarios.ListAsync(h => h.Activo, ct);
        }
        return list.OrderBy(h => h.Orden).ToList();
    }

    public async Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, CancellationToken ct = default)
    {
        var existentes = await _menus.ListAsync(m => m.FechaInicio == inicio && m.FechaTermino == fin, ct);
        var menu = existentes.FirstOrDefault();
        if (menu != null) return menu;

        var horarios = await EnsureHorariosAsync(ct);

        // Create menu with empty options per working day and horario
        menu = new Menu { FechaInicio = inicio, FechaTermino = fin };
        await _menus.AddAsync(menu, ct);

        var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var dia in dias)
            foreach (var h in horarios)
                await _opcionesMenu.AddAsync(new OpcionMenu { Menu = menu, MenuId = menu.Id, DiaSemana = dia, HorarioId = h.Id, OpcionesMaximas = 3 }, ct);

        await _uow.SaveChangesAsync(ct);
        return menu;
    }

    private async Task<IReadOnlyList<Horario>> GetHorariosForScopeAsync(int? sucursalId, CancellationToken ct)
    {
        if (sucursalId == null)
            return await EnsureHorariosAsync(ct);
        var asignados = await _sucursalHorarios.ListAsync(sh => sh.SucursalId == sucursalId, ct);
        if (asignados.Count == 0)
            return await EnsureHorariosAsync(ct);
        var activos = await EnsureHorariosAsync(ct);
        var set = asignados.Select(a => a.HorarioId).ToHashSet();
        return activos.Where(h => set.Contains(h.Id)).OrderBy(h => h.Orden).ToList();
    }

    public async Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId, CancellationToken ct = default)
    {
        // If sucursal specified, search ONLY by sucursal; else, ONLY by empresa
        Menu? menu = null;
        if (sucursalId != null)
        {
            var list = await _menus.ListAsync(m => m.FechaInicio == inicio && m.FechaTermino == fin && m.SucursalId == sucursalId, ct);
            menu = list.FirstOrDefault();
        }
        else if (empresaId != null)
        {
            var list = await _menus.ListAsync(m => m.FechaInicio == inicio && m.FechaTermino == fin && m.EmpresaId == empresaId && m.SucursalId == null, ct);
            menu = list.FirstOrDefault();
        }
        if (menu != null) return menu;

        var horarios = await GetHorariosForScopeAsync(sucursalId, ct);

        menu = new Menu { FechaInicio = inicio, FechaTermino = fin, SucursalId = sucursalId, EmpresaId = sucursalId == null ? empresaId : null };
        await _menus.AddAsync(menu, ct);
        var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var dia in dias)
            foreach (var h in horarios)
                await _opcionesMenu.AddAsync(new OpcionMenu { Menu = menu, MenuId = menu.Id, DiaSemana = dia, HorarioId = h.Id, OpcionesMaximas = 3 }, ct);
        await _uow.SaveChangesAsync(ct);
        return menu;
    }

    public async Task<Menu?> FindMenuAsync(DateOnly inicio, DateOnly fin, int? empresaId, int? sucursalId, CancellationToken ct = default)
    {
        // Prefer sucursal menu; else fallback to empresa menu
        if (sucursalId != null)
        {
            var bySuc = await _menus.ListAsync(m => m.FechaInicio == inicio && m.FechaTermino == fin && m.SucursalId == sucursalId, ct);
            var found = bySuc.FirstOrDefault();
            if (found != null) return found;
        }
        if (empresaId != null)
        {
            var byEmp = await _menus.ListAsync(m => m.FechaInicio == inicio && m.FechaTermino == fin && m.EmpresaId == empresaId && m.SucursalId == null, ct);
            return byEmp.FirstOrDefault();
        }
        return null;
    }

    public async Task<Menu> GetEffectiveMenuForSemanaAsync(DateOnly inicio, DateOnly fin, int empresaId, int? sucursalId, CancellationToken ct = default)
    {
        // 1) If sucursal menu exists and has options, use it
        if (sucursalId != null)
        {
            var menuSuc = await FindMenuAsync(inicio, fin, empresaId, sucursalId, ct);
            if (menuSuc != null)
            {
                var diasSuc = await _opcionesMenu.ListAsync(d => d.MenuId == menuSuc.Id, ct);
                var tieneOpciones = diasSuc.Any(d =>
                    d.OpcionIdA != null || d.OpcionIdB != null || d.OpcionIdC != null || d.OpcionIdD != null || d.OpcionIdE != null);
                if (tieneOpciones)
                    return menuSuc;
            }
        }

        // 2) Else, if empresa menu exists, use it
        var menuEmp = await FindMenuAsync(inicio, fin, empresaId, null, ct);
        if (menuEmp != null)
            return menuEmp;

        // 3) Otherwise create empresa menu
        return await GetOrCreateMenuAsync(inicio, fin, empresaId, null, ct);
    }

    public async Task<Menu?> FindEffectiveMenuForSemanaAsync(DateOnly inicio, DateOnly fin, int empresaId, int? sucursalId, bool requireOpcionesConfiguradas = false, CancellationToken ct = default)
    {
        if (sucursalId != null)
        {
            var menuSuc = await FindMenuAsync(inicio, fin, empresaId, sucursalId, ct);
            if (menuSuc != null && (!requireOpcionesConfiguradas || await TieneOpcionesConfiguradasAsync(menuSuc.Id, ct)))
                return menuSuc;
        }

        var menuEmp = await FindMenuAsync(inicio, fin, empresaId, null, ct);
        if (menuEmp != null && (!requireOpcionesConfiguradas || await TieneOpcionesConfiguradasAsync(menuEmp.Id, ct)))
            return menuEmp;

        return null;
    }

    public async Task<IReadOnlyList<OpcionMenu>> ObtenerOpcionesEmpleadoAsync(int empleadoId, CancellationToken ct = default)
    {
        var menu = await GetOrCreateMenuSemanaSiguienteAsync(ct);
        var lista = await _opcionesMenu.ListAsync(om => om.MenuId == menu.Id, ct);
        return lista.OrderBy(o => o.DiaSemana).ThenBy(o => o.HorarioId).ToList();
    }

    public async Task RegistrarSeleccionAsync(int empleadoId, int opcionMenuId, char seleccion, CancellationToken ct = default)
    {
        var empleado = await _empleados.GetByIdAsync(empleadoId, ct);
        if (empleado == null) throw new ArgumentException("Empleado no existe", nameof(empleadoId));

        await RegistrarSeleccionAsync(empleadoId, opcionMenuId, seleccion, empleado.SucursalId, null, null, ct);
    }

    public async Task RegistrarSeleccionAsync(int empleadoId, int opcionMenuId, char seleccion, int sucursalEntregaId, int? adicionalOpcionId, CancellationToken ct = default)
    {
        await RegistrarSeleccionAsync(empleadoId, opcionMenuId, seleccion, sucursalEntregaId, null, adicionalOpcionId, ct);
    }

    public async Task RegistrarSeleccionAsync(int empleadoId, int opcionMenuId, char seleccion, int sucursalEntregaId, int? localizacionEntregaId, int? adicionalOpcionId, CancellationToken ct = default)
    {
        var opcionMenu = await _opcionesMenu.GetByIdAsync(opcionMenuId, ct);
        if (opcionMenu == null) throw new ArgumentException("OpcionMenu no existe", nameof(opcionMenuId));

        Localizacion? localizacion = null;
        if (localizacionEntregaId.HasValue && localizacionEntregaId.Value != 0)
        {
            localizacion = await _localizaciones.GetByIdAsync(localizacionEntregaId.Value, ct);
            if (localizacion == null)
                throw new ArgumentException("Localizacion de entrega invalida", nameof(localizacionEntregaId));
        }

        if (sucursalEntregaId == 0)
            throw new ArgumentException("Filial de entrega invalida", nameof(sucursalEntregaId));

        // Validar que la filial de entrega esta asignada al empleado (principal o adicional)
        var empleado = await _empleados.GetByIdAsync(empleadoId, ct);
        if (empleado == null) throw new ArgumentException("Empleado no existe", nameof(empleadoId));
        var asignadas = await _empleadosSucursales.ListAsync(es => es.EmpleadoId == empleadoId, ct);
        var sucursalesPermitidas = asignadas.Select(a => a.SucursalId).ToHashSet();
        sucursalesPermitidas.Add(empleado.SucursalId);
        if (sucursalEntregaId == 0 || !sucursalesPermitidas.Contains(sucursalEntregaId))
            sucursalEntregaId = empleado.SucursalId;

        if (localizacion != null)
        {
            var asignadasLoc = await _empleadosLocalizaciones.ListAsync(el => el.EmpleadoId == empleadoId, ct);
            if (asignadasLoc.Count > 0 && !asignadasLoc.Any(a => a.LocalizacionId == localizacion.Id))
            {
                // La validacion de pertenencia a empresa se realiza en el controller.
                // Aqui no bloqueamos si la localizacion no esta asignada al empleado.
            }
        }

        // Validar adicional: debe estar configurado como adicional fijo para el menu
        if (adicionalOpcionId != null)
        {
            var adicionalesMenu = await _menusAdicionales.ListAsync(a => a.MenuId == opcionMenu.MenuId, ct);
            var set = adicionalesMenu.Select(a => a.OpcionId).ToHashSet();
            if (!set.Contains(adicionalOpcionId.Value))
                throw new InvalidOperationException("El adicional seleccionado no esta disponible para este menu.");
        }

        var max = opcionMenu.OpcionesMaximas <= 0 ? 3 : Math.Clamp(opcionMenu.OpcionesMaximas, 1, 5);
        var slot = seleccion switch
        {
            'A' => 1,
            'B' => 2,
            'C' => 3,
            'D' => 4,
            'E' => 5,
            _ => 0
        };
        if (slot == 0 || slot > max)
            throw new ArgumentException("Seleccion invalida", nameof(seleccion));

        bool slotTieneOpcion = slot switch
        {
            1 => opcionMenu.OpcionIdA != null,
            2 => opcionMenu.OpcionIdB != null,
            3 => opcionMenu.OpcionIdC != null,
            4 => opcionMenu.OpcionIdD != null,
            5 => opcionMenu.OpcionIdE != null,
            _ => false
        };
        if (!slotTieneOpcion)
            throw new ArgumentException("Seleccion no disponible para este dia/horario", nameof(seleccion));

        var existentes = await _respuestas.ListAsync(r => r.EmpleadoId == empleadoId && r.OpcionMenuId == opcionMenuId, ct);
        var actual = existentes.FirstOrDefault();
        if (actual == null)
        {
            await _respuestas.AddAsync(new RespuestaFormulario
            {
                EmpleadoId = empleadoId,
                OpcionMenuId = opcionMenuId,
                Seleccion = seleccion,
                SucursalEntregaId = sucursalEntregaId,
                LocalizacionEntregaId = localizacion?.Id,
                AdicionalOpcionId = adicionalOpcionId
            }, ct);
        }
        else
        {
            actual.Seleccion = seleccion;
            actual.SucursalEntregaId = sucursalEntregaId;
            actual.LocalizacionEntregaId = localizacion?.Id;
            actual.AdicionalOpcionId = adicionalOpcionId;
        }
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<bool> TieneOpcionesConfiguradasAsync(int menuId, CancellationToken ct)
    {
        var dias = await _opcionesMenu.ListAsync(d => d.MenuId == menuId, ct);
        if (dias.Count == 0) return false;
        return dias.Any(d => d.OpcionIdA != null || d.OpcionIdB != null || d.OpcionIdC != null || d.OpcionIdD != null || d.OpcionIdE != null);
    }
}


