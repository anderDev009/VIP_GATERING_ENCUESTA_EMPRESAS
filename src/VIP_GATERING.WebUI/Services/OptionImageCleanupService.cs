using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.WebUI.Services;

public class OptionImageCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<OptionImageCleanupService> _logger;

    public OptionImageCleanupService(IServiceScopeFactory scopeFactory, IWebHostEnvironment env, ILogger<OptionImageCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _env = env;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);
        var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken token)
    {
        try
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads", "opciones");
            if (!Directory.Exists(folder)) return;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var referenced = await db.Opciones.Where(o => o.ImagenUrl != null && o.ImagenUrl != "")
                .Select(o => Normalize(o.ImagenUrl!))
                .ToListAsync(token);
            var referencedSet = referenced.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(folder))
            {
                token.ThrowIfCancellationRequested();
                var rel = Normalize("/uploads/opciones/" + Path.GetFileName(file));
                if (!referencedSet.Contains(rel))
                {
                    try { File.Delete(file); }
                    catch (Exception ex) { _logger.LogWarning(ex, "No se pudo eliminar imagen huérfana {File}", file); }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar imágenes huérfanas");
        }
    }

    private static string Normalize(string path) => path.Trim().Replace('\\', '/');
}
