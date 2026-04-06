using System.Text.Json;
using HomeGuard.Application.Interfaces;
using HomeGuard.Common.Sync;
using Microsoft.EntityFrameworkCore;

namespace HomeGuard.Infrastructure.Persistence;

public sealed class ProcessedOperationStore : IProcessedOperationStore
{
    private readonly HomeGuardDbContext _db;
    private readonly WriteSemaphore _semaphore;

    public ProcessedOperationStore(HomeGuardDbContext db, WriteSemaphore semaphore)
    {
        _db = db;
        _semaphore = semaphore;
    }

    public async Task<SyncAck?> GetCachedAckAsync(
        Guid clientOperationId, CancellationToken ct = default)
    {
        var op = await _db.ProcessedOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientOperationId == clientOperationId, ct);

        return op is null
            ? null
            : JsonSerializer.Deserialize<SyncAck>(op.AckJson);
    }

    public async Task RecordAsync(
        Guid clientOperationId,
        Guid userId,
        string operationType,
        SyncAck ack,
        CancellationToken ct = default)
    {
        var op = new ProcessedOperation
        {
            ClientOperationId = clientOperationId,
            UserId = userId,
            OperationType = operationType,
            AckJson = JsonSerializer.Serialize(ack),
            ProcessedAt = DateTimeOffset.UtcNow,
        };

        await _semaphore.WaitAsync(ct);
        try
        {
            _db.ProcessedOperations.Add(op);
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
