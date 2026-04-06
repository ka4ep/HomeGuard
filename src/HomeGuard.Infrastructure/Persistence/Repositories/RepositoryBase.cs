using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeGuard.Infrastructure.Persistence.Repositories;

/// <summary>
/// Base EF Core repository. Concrete repositories inherit and add
/// domain-specific query methods.
/// </summary>
public abstract class RepositoryBase<T> : IRepository<T> where T : Entity
{
    protected HomeGuardDbContext Db { get; }
    protected DbSet<T> Set => Db.Set<T>();

    protected RepositoryBase(HomeGuardDbContext db) => Db = db;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public void Remove(T entity)
        => Set.Remove(entity);
}
