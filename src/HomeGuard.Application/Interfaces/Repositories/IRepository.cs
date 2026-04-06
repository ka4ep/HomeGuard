using HomeGuard.Domain.Common;

namespace HomeGuard.Application.Interfaces.Repositories;

/// <summary>
/// Minimal generic repository. EF Core change tracking handles updates automatically —
/// modify entity properties, then call IUnitOfWork.SaveChangesAsync().
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Remove(T entity);
}
