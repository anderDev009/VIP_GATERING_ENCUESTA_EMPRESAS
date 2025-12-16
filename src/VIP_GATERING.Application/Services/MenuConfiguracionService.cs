using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Application.Services;

public record MenuConfiguracionUpdate(bool PermitirEdicionSemanaActual, int DiasAnticipoSemanaActual, TimeSpan HoraLimiteEdicion);

public interface IMenuConfiguracionService
{
    Task<MenuConfiguracion> ObtenerAsync(CancellationToken ct = default);
    Task<MenuConfiguracion> ActualizarAsync(MenuConfiguracionUpdate update, CancellationToken ct = default);
}

public class MenuConfiguracionService : IMenuConfiguracionService
{
    private readonly IRepository<MenuConfiguracion> _configRepo;
    private readonly IUnitOfWork _uow;

    public MenuConfiguracionService(IRepository<MenuConfiguracion> configRepo, IUnitOfWork uow)
    {
        _configRepo = configRepo;
        _uow = uow;
    }

    public async Task<MenuConfiguracion> ObtenerAsync(CancellationToken ct = default)
    {
        return await EnsureConfigAsync(ct);
    }

    public async Task<MenuConfiguracion> ActualizarAsync(MenuConfiguracionUpdate update, CancellationToken ct = default)
    {
        var cfg = await EnsureConfigAsync(ct);
        cfg.PermitirEdicionSemanaActual = update.PermitirEdicionSemanaActual;
        cfg.DiasAnticipoSemanaActual = Math.Clamp(update.DiasAnticipoSemanaActual, 0, 7);

        var hora = update.HoraLimiteEdicion;
        if (hora < TimeSpan.Zero) hora = TimeSpan.Zero;
        if (hora > new TimeSpan(23, 59, 59)) hora = new TimeSpan(23, 59, 59);
        cfg.HoraLimiteEdicion = hora;
        cfg.ActualizadoUtc = DateTime.UtcNow;
        _configRepo.Update(cfg);
        await _uow.SaveChangesAsync(ct);
        return cfg;
    }

    private async Task<MenuConfiguracion> EnsureConfigAsync(CancellationToken ct)
    {
        var list = await _configRepo.ListAsync(null, ct);
        var cfg = list.FirstOrDefault();
        if (cfg != null) return cfg;

        var nuevo = new MenuConfiguracion();
        await _configRepo.AddAsync(nuevo, ct);
        await _uow.SaveChangesAsync(ct);
        return nuevo;
    }
}
