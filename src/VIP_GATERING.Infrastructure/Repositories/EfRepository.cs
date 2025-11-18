using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Application.Abstractions;
using VIP_GATERING.Infrastructure.Data;

namespace VIP_GATERING.Infrastructure.Repositories;

public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _db;
    private readonly DbSet<T> _set;
    public EfRepository(AppDbContext db)
    {
        _db = db;
        _set = _db.Set<T>();
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _set.AddAsync(entity, ct);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await _set.AddRangeAsync(entities, ct);
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _set.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        IQueryable<T> query = _set.AsQueryable();
        if (predicate != null)
            query = query.Where(predicate);
        return await query.ToListAsync(ct);
    }

    public void Remove(T entity)
    {
        _set.Remove(entity);
    }

    public void Update(T entity)
    {
        _set.Update(entity);
    }
}

