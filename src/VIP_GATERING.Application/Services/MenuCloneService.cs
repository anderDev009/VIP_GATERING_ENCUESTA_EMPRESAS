using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Application.Services;

public interface IMenuCloneService
{
    Task<(int updated, int skipped)> CloneEmpresaMenuToSucursalesAsync(DateOnly inicio, DateOnly fin, int empresaId, IEnumerable<int> sucursalIds, CancellationToken ct = default);
}

public class MenuCloneService : IMenuCloneService
{
    private readonly IMenuService _menuService;
    private readonly IRepository<Menu> _menus;
    private readonly IRepository<OpcionMenu> _opcionesMenu;
    private readonly IRepository<MenuAdicional> _menusAdicionales;
    private readonly IRepository<RespuestaFormulario> _respuestas;
    private readonly IUnitOfWork _uow;

    public MenuCloneService(IMenuService menuService,
        IRepository<Menu> menus,
        IRepository<OpcionMenu> opcionesMenu,
        IRepository<MenuAdicional> menusAdicionales,
        IRepository<RespuestaFormulario> respuestas,
        IUnitOfWork uow)
    {
        _menuService = menuService;
        _menus = menus;
        _opcionesMenu = opcionesMenu;
        _menusAdicionales = menusAdicionales;
        _respuestas = respuestas;
        _uow = uow;
    }

    public async Task<(int updated, int skipped)> CloneEmpresaMenuToSucursalesAsync(DateOnly inicio, DateOnly fin, int empresaId, IEnumerable<int> sucursalIds, CancellationToken ct = default)
    {
        var menuEmpresa = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, null, ct);
        var diasEmpresa = (await _opcionesMenu.ListAsync(om => om.MenuId == menuEmpresa.Id, ct))
            .OrderBy(d => d.DiaSemana)
            .ThenBy(d => d.HorarioId)
            .ToList();
        var diasEmpresaMap = diasEmpresa.ToDictionary(d => (d.DiaSemana, d.HorarioId));

        var adicionalesEmpresa = await _menusAdicionales.ListAsync(a => a.MenuId == menuEmpresa.Id, ct);
        var adicionalesEmpresaIds = adicionalesEmpresa.Select(a => a.OpcionId).ToHashSet();

        int updated = 0, skipped = 0;
        foreach (var sucursalId in sucursalIds)
        {
            var menuSucursal = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, sucursalId, ct);
            var diasSucursal = (await _opcionesMenu.ListAsync(om => om.MenuId == menuSucursal.Id, ct)).OrderBy(d => d.DiaSemana).ToList();

            // Bloqueo: si algún empleado completó la semana de este menú
            var idsSucursal = diasSucursal.Select(d => d.Id).ToList();
            var respuestas = await _respuestas.ListAsync(r => idsSucursal.Contains(r.OpcionMenuId), ct);
            var completos = respuestas.GroupBy(r => r.EmpleadoId).Any(g => g.Count() >= diasSucursal.Count);
            if (completos)
            {
                skipped++;
                continue;
            }

            foreach (var dSuc in diasSucursal)
            {
                if (!diasEmpresaMap.TryGetValue((dSuc.DiaSemana, dSuc.HorarioId), out var dEmp))
                    continue;
                if (dEmp == null) continue;
                dSuc.OpcionIdA = dEmp.OpcionIdA;
                dSuc.OpcionIdB = dEmp.OpcionIdB;
                dSuc.OpcionIdC = dEmp.OpcionIdC;
                dSuc.OpcionIdD = dEmp.OpcionIdD;
                dSuc.OpcionIdE = dEmp.OpcionIdE;
                dSuc.OpcionesMaximas = dEmp.OpcionesMaximas;
                dSuc.DiaCerrado = dEmp.DiaCerrado;
                if (dEmp.DiaCerrado)
                {
                    var respuestasCerrar = await _respuestas.ListAsync(r => r.OpcionMenuId == dSuc.Id, ct);
                    foreach (var r in respuestasCerrar)
                        _respuestas.Remove(r);
                }
            }

            var adicionalesSucursal = await _menusAdicionales.ListAsync(a => a.MenuId == menuSucursal.Id, ct);
            foreach (var existente in adicionalesSucursal.Where(a => !adicionalesEmpresaIds.Contains(a.OpcionId)))
            {
                _menusAdicionales.Remove(existente);
            }
            var actualesIds = adicionalesSucursal.Select(a => a.OpcionId).ToHashSet();
            var nuevos = adicionalesEmpresaIds.Except(actualesIds)
                .Select(id => new MenuAdicional { MenuId = menuSucursal.Id, OpcionId = id })
                .ToList();
            if (nuevos.Count > 0)
                await _menusAdicionales.AddRangeAsync(nuevos, ct);

            updated++;
        }

        await _uow.SaveChangesAsync(ct);
        return (updated, skipped);
    }
}

