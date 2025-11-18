using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace VIP_GATERING.WebUI.Services;

public interface IOptionImageService
{
    string? Validate(IFormFile? file);
    Task<string?> SaveAsync(IFormFile? file, string? currentPath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? relativePath);
}

public class OptionImageService : IOptionImageService
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<OptionImageService> _logger;

    public OptionImageService(IWebHostEnvironment env, ILogger<OptionImageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public string? Validate(IFormFile? file)
    {
        if (file == null || file.Length == 0) return null;
        if (file.Length > MaxBytes) return "La imagen supera el límite de 2MB.";
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            return "Formato no soportado. Usa JPG, PNG, GIF o WEBP.";
        try
        {
            using var stream = file.OpenReadStream();
            Image.DetectFormat(stream);
        }
        catch (UnknownImageFormatException)
        {
            return "El archivo no es una imagen válida.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al validar imagen");
            return "No se pudo validar la imagen. Inténtalo nuevamente.";
        }
        return null;
    }

    public async Task<string?> SaveAsync(IFormFile? file, string? currentPath, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0) return currentPath;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folder = Path.Combine(_env.WebRootPath, "uploads", "opciones");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{ext}";
        var dest = Path.Combine(folder, name);
        await using (var stream = new FileStream(dest, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            await DeleteAsync(currentPath);
        }
        return $"/uploads/opciones/{name}";
    }

    public Task DeleteAsync(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Task.CompletedTask;
        var rel = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var physical = Path.Combine(_env.WebRootPath, rel);
        if (File.Exists(physical))
        {
            try { File.Delete(physical); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo eliminar la imagen {Imagen}", relativePath); }
        }
        return Task.CompletedTask;
    }
}
