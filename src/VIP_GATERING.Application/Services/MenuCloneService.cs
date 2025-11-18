using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Application.Services;

public interface IMenuCloneService
{
    Task<(int updated, int skipped)> CloneEmpresaMenuToSucursalesAsync(DateOnly inicio, DateOnly fin, Guid empresaId, IEnumerable<Guid> sucursalIds, CancellationToken ct = default);
}

public class MenuCloneService : IMenuCloneService
{
    private readonly IMenuService _menuService;
    private readonly IRepository<Menu> _menus;
    private readonly IRepository<OpcionMenu> _opcionesMenu;
    private readonly IRepository<RespuestaFormulario> _respuestas;
    private readonly IUnitOfWork _uow;

    public MenuCloneService(IMenuService menuService,
        IRepository<Menu> menus,
        IRepository<OpcionMenu> opcionesMenu,
        IRepository<RespuestaFormulario> respuestas,
        IUnitOfWork uow)
    {
        _menuService = menuService;
        _menus = menus;
        _opcionesMenu = opcionesMenu;
        _respuestas = respuestas;
        _uow = uow;
    }

    public async Task<(int updated, int skipped)> CloneEmpresaMenuToSucursalesAsync(DateOnly inicio, DateOnly fin, Guid empresaId, IEnumerable<Guid> sucursalIds, CancellationToken ct = default)
    {
        var menuEmpresa = await _menuService.GetOrCreateMenuAsync(inicio, fin, empresaId, null, ct);
        var diasEmpresa = (await _opcionesMenu.ListAsync(om => om.MenuId == menuEmpresa.Id, ct)).OrderBy(d => d.DiaSemana).ToList();

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
                var dEmp = diasEmpresa.FirstOrDefault(x => x.DiaSemana == dSuc.DiaSemana);
                if (dEmp == null) continue;
                dSuc.OpcionIdA = dEmp.OpcionIdA;
                dSuc.OpcionIdB = dEmp.OpcionIdB;
                dSuc.OpcionIdC = dEmp.OpcionIdC;
            }
            updated++;
        }

        await _uow.SaveChangesAsync(ct);
        return (updated, skipped);
    }
}

