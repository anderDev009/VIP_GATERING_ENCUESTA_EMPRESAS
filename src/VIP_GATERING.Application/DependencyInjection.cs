using Microsoft.Extensions.DependencyInjection;
using VIP_GATERING.Application.Services;

namespace VIP_GATERING.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IMenuCloneService, MenuCloneService>();
        services.AddSingleton<IFechaServicio, FechaServicio>();
        services.AddSingleton<ISubsidioService, SubsidioService>();
        services.AddScoped<IMenuConfiguracionService, MenuConfiguracionService>();
        services.AddScoped<IMenuEdicionService, MenuEdicionService>();
        return services;
    }
}
