using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IBlobEntryRepository : IRepository<BlobEntry>
{
    Task<IReadOnlyList<BlobEntry>> GetByOwnerAsync(Guid ownerEntityId, CancellationToken ct = default);

    /// <summary>Blobs that need to be uploaded to NextCloud.</summary>
    Task<IReadOnlyList<BlobEntry>> GetPendingSyncAsync(CancellationToken ct = default);
}

public interface IScheduledJobRepository : IRepository<ScheduledJob>
{
    /// <summary>
    /// Jobs in Pending status whose RunAfter ≤ <paramref name="now"/>, up to <paramref name="limit"/> rows.
    /// </summary>
    Task<IReadOnlyList<ScheduledJob>> GetReadyJobsAsync(
        DateTimeOffset now, int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Returns true if a Pending or Running job with the given
    /// <paramref name="correlationKey"/> already exists.
    /// Used to avoid creating duplicate notification jobs.
    /// </summary>
    Task<bool> ExistsPendingAsync(string correlationKey, CancellationToken ct = default);
}
