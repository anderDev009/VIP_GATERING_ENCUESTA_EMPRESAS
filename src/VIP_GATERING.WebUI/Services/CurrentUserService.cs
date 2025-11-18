using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Infrastructure.Data;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.WebUI.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? EmpleadoId { get; }
    Guid? EmpresaId { get; }
    Guid? SucursalId { get; }
    Task SetUsuarioAsync(Guid usuarioId); // legacy dev helper
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private Guid? _cachedUserId;
    private bool _userIdResolved;
    private UserSnapshot? _userSnapshot;
    private bool _snapshotResolved;

    private sealed record UserSnapshot(Guid? EmpleadoId, Guid? EmpresaId);

    public CurrentUserService(IHttpContextAccessor http, AppDbContext db, UserManager<ApplicationUser> users)
    {
        _http = http;
        _db = db;
        _users = users;
    }

    public Guid? UserId => EnsureUserId();

    public Guid? EmpleadoId => EnsureSnapshot()?.EmpleadoId;

    public Guid? EmpresaId => EnsureSnapshot()?.EmpresaId;

    public Guid? SucursalId
    {
        get
        {
            var empleadoId = EmpleadoId;
            if (empleadoId == null) return null;

            return _db.Empleados
                .AsNoTracking()
                .Where(e => e.Id == empleadoId)
                .Select(e => (Guid?)e.SucursalId)
                .FirstOrDefault();
        }
    }

    public Task SetUsuarioAsync(Guid usuarioId)
    {
        // Legacy no-op in Identity world; kept to avoid breaking callers.
        return Task.CompletedTask;
    }

    private Guid? EnsureUserId()
    {
        if (_userIdResolved) return _cachedUserId;

        var principal = _http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            var userIdString = _users.GetUserId(principal);
            if (Guid.TryParse(userIdString, out var parsed))
            {
                _cachedUserId = parsed;
            }
        }

        _userIdResolved = true;
        return _cachedUserId;
    }

    private UserSnapshot? EnsureSnapshot()
    {
        if (_snapshotResolved) return _userSnapshot;

        var uid = EnsureUserId();
        if (uid != null)
        {
            _userSnapshot = _db.Set<ApplicationUser>()
                .AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => new UserSnapshot(u.EmpleadoId, u.EmpresaId))
                .FirstOrDefault();
        }

        _snapshotResolved = true;
        return _userSnapshot;
    }
}
