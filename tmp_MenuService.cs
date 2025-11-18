using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Application.Services;

public interface IMenuService
{
    Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(CancellationToken ct = default);
    Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(Guid? empresaId, Guid? sucursalId, CancellationToken ct = default);
    Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, CancellationToken ct = default);
    Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, Guid? empresaId, Guid? sucursalId, CancellationToken ct = default);
    Task<Menu?> FindMenuAsync(DateOnly inicio, DateOnly fin, Guid? empresaId, Guid? sucursalId, CancellationToken ct = default);
    Task<Menu> GetEffectiveMenuForSemanaAsync(DateOnly inicio, DateOnly fin, Guid empresaId, Guid? sucursalId, CancellationToken ct = default);
    Task<IReadOnlyList<OpcionMenu>> ObtenerOpcionesEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
    Task RegistrarSeleccionAsync(Guid empleadoId, Guid opcionMenuId, char seleccion, CancellationToken ct = default);
}

public class MenuService : IMenuService
{
    private readonly IRepository<Menu> _menus;
    private readonly IRepository<Opcion> _opciones;
    private readonly IRepository<OpcionMenu> _opcionesMenu;
    private readonly IRepository<RespuestaFormulario> _respuestas;
    private readonly IUnitOfWork _uow;
    private readonly IFechaServicio _fechaSvc;

    public MenuService(IRepository<Menu> menus,
        IRepository<Opcion> opciones,
        IRepository<OpcionMenu> opcionesMenu,
        IRepository<RespuestaFormulario> respuestas,
        IUnitOfWork uow,
        IFechaServicio fechaSvc)
    {
        _menus = menus;
        _opciones = opciones;
        _opcionesMenu = opcionesMenu;
        _respuestas = respuestas;
        _uow = uow;
        _fechaSvc = fechaSvc;
    }

    public async Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(CancellationToken ct = default)
    {
        var (inicio, fin) = _fechaSvc.RangoSemanaSiguiente();
        return await GetOrCreateMenuAsync(inicio, fin, ct);
    }

    public async Task<Menu> GetOrCreateMenuSemanaSiguienteAsync(Guid? empresaId, Guid? sucursalId, CancellationToken ct = default)
    {
        var (inicio, fin) = _fechaSvc.RangoSemanaSiguiente();
        return await GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId, ct);
    }

    public async Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, CancellationToken ct = default)
    {
        var existentes = await _menus.ListAsync(m => m.FechaInicio == inicio && m.FechaTermino == fin, ct);
        var menu = existentes.FirstOrDefault();
        if (menu != null) return menu;

        // Crear menú con opciones vacías por día laboral
        menu = new Menu { FechaInicio = inicio, FechaTermino = fin };
        await _menus.AddAsync(menu, ct);

        var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var dia in dias)
        {
            await _opcionesMenu.AddAsync(new OpcionMenu { Menu = menu, MenuId = menu.Id, DiaSemana = dia }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return menu;
    }

    public async Task<Menu> GetOrCreateMenuAsync(DateOnly inicio, DateOnly fin, Guid? empresaId, Guid? sucursalId, CancellationToken ct = default)
    {
        // Si se especifica sucursal, buscar SOLO por sucursal; si no, buscar SOLO por empresa (alcance cliente)
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

        menu = new Menu { FechaInicio = inicio, FechaTermino = fin, SucursalId = sucursalId, EmpresaId = sucursalId == null ? empresaId : null };
        await _menus.AddAsync(menu, ct);
        var dias = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var dia in dias)
            await _opcionesMenu.AddAsync(new OpcionMenu { Menu = menu, MenuId = menu.Id, DiaSemana = dia }, ct);
        await _uow.SaveChangesAsync(ct);
        return menu;
    }

    public async Task<Menu?> FindMenuAsync(DateOnly inicio, DateOnly fin, Guid? empresaId, Guid? sucursalId, CancellationToken ct = default)
    {
        // Preferir menÃº por sucursal; si no existe, caer al menÃº por empresa
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

    public async Task<Menu> GetEffectiveMenuForSemanaAsync(DateOnly inicio, DateOnly fin, Guid empresaId, Guid? sucursalId, CancellationToken ct = default)
    {
        // 1) Si hay menÃº de sucursal y tiene al menos una opciÃ³n configurada, usarlo
        if (sucursalId != null)
        {
            var menuSuc = await FindMenuAsync(inicio, fin, empresaId, sucursalId, ct);
            if (menuSuc != null)
            {
                var diasSuc = await _opcionesMenu.ListAsync(d => d.MenuId == menuSuc.Id, ct);
                var tieneOpciones = diasSuc.Any(d => d.OpcionIdA != null || d.OpcionIdB != null || d.OpcionIdC != null);
                if (tieneOpciones)
                    return menuSuc;
            }
        }

        // 2) Caso contrario, si hay menÃº de empresa, usarlo
        var menuEmp = await FindMenuAsync(inicio, fin, empresaId, null, ct);
        if (menuEmp != null)
            return menuEmp;

        // 3) Si no existe, crear menÃº de empresa
        return await GetOrCreateMenuAsync(inicio, fin, empresaId, null, ct);
    }

    public async Task<IReadOnlyList<OpcionMenu>> ObtenerOpcionesEmpleadoAsync(Guid empleadoId, CancellationToken ct = default)
    {
        var menu = await GetOrCreateMenuSemanaSiguienteAsync(ct);
        // No filtramos por empleado aquÃ­; se retorna la colecciÃ³n de dÃ­as
        var lista = await _opcionesMenu.ListAsync(om => om.MenuId == menu.Id, ct);
        return lista.OrderBy(o => o.DiaSemana).ToList();
    }

    public async Task RegistrarSeleccionAsync(Guid empleadoId, Guid opcionMenuId, char seleccion, CancellationToken ct = default)
    {
        if (seleccion is not ('A' or 'B' or 'C'))
            throw new ArgumentException("SelecciÃ³n invÃ¡lida", nameof(seleccion));

        var existentes = await _respuestas.ListAsync(r => r.EmpleadoId == empleadoId && r.OpcionMenuId == opcionMenuId, ct);
        var actual = existentes.FirstOrDefault();
        if (actual == null)
        {
            await _respuestas.AddAsync(new RespuestaFormulario
            {
                EmpleadoId = empleadoId,
                OpcionMenuId = opcionMenuId,
                Seleccion = seleccion
            }, ct);
        }
        else
        {
            actual.Seleccion = seleccion;
        }
        await _uow.SaveChangesAsync(ct);
    }
}


