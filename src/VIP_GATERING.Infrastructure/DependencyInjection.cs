using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Application.Services;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Repositories;
using VIP_GATERING.Infrastructure.Services;

namespace VIP_GATERING.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db";
        services.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseSqlite(connectionString);
        });

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IFechaServicio, FechaServicio>();
        services.AddScoped<IEmpleadoUsuarioService, EmpleadoUsuarioService>();

        return services;
    }
}
