using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.Infrastructure.Services;

public interface IEmpleadoUsuarioService
{
    Task<Usuario> EnsureUsuarioParaEmpleadoAsync(int empleadoId, CancellationToken ct = default);
}

public class EmpleadoUsuarioService : IEmpleadoUsuarioService
{
    private readonly AppDbContext _db;
    public EmpleadoUsuarioService(AppDbContext db) { _db = db; }

    public async Task<Usuario> EnsureUsuarioParaEmpleadoAsync(int empleadoId, CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.EmpleadoId == empleadoId, ct);
        if (usuario != null) return usuario;
        var empleado = await _db.Empleados.FirstAsync(e => e.Id == empleadoId, ct);
        usuario = new Usuario
        {
            EmpleadoId = empleadoId,
            Nombre = empleado.Nombre,
            ContrasenaHash = "dev"
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);
        return usuario;
    }
}

