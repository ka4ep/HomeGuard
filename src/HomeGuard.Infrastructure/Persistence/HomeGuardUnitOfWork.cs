using HomeGuard.Application.Interfaces;

namespace HomeGuard.Infrastructure.Persistence;

/// <summary>
/// Wraps EF Core SaveChangesAsync with a process-wide SemaphoreSlim(1,1).
///
/// SQLite in WAL mode allows concurrent reads but only one writer.
/// Rather than fighting the driver with retry logic, we simply serialise
/// all writes through this semaphore. For a home server with 2-4 users
/// the throughput is more than sufficient.
///
/// Registered as Scoped — one instance per HTTP request.
/// The semaphore itself is a singleton injected separately.
/// </summary>
public sealed class HomeGuardUnitOfWork : IUnitOfWork
{
    private readonly HomeGuardDbContext _db;
    private readonly WriteSemaphore _semaphore;

    public HomeGuardUnitOfWork(HomeGuardDbContext db, WriteSemaphore semaphore)
    {
        _db = db;
        _semaphore = semaphore;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Singleton wrapper around a SemaphoreSlim so it can be injected cleanly.
/// </summary>
public sealed class WriteSemaphore : IDisposable
{
    private readonly SemaphoreSlim _inner = new(1, 1);

    public Task WaitAsync(CancellationToken ct) => _inner.WaitAsync(ct);
    public void Release() => _inner.Release();

    public void Dispose() => _inner.Dispose();
}
