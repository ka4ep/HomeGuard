using HomeGuard.Common.Sync;
using System.Text.Json;

namespace HomeGuard.Client.Services;

/// <summary>
/// Manages the client-side Outbox.
///
/// Write path:
///   1. Caller invokes <see cref="EnqueueAsync"/> with a typed command.
///   2. Entry is written to IndexedDB (survives page refresh and offline).
///   3. <see cref="FlushAsync"/> sends pending entries to the server in one batch.
///   4. Server responds per-entry with <see cref="SyncAckStatus"/>.
///   5. Committed entries are deleted from IndexedDB; rejected/failed stay for retry.
///
/// Online detection: browser's navigator.onLine + network error handling.
/// The caller decides when to flush — typically on navigation or a timer.
/// </summary>
public sealed class OutboxSyncService
{
    private readonly HomeGuardDb _db;
    private readonly SyncApiClient _api;

    // Raised when the outbox count changes so UI can show a badge.
    public event Action? OutboxChanged;

    public OutboxSyncService(HomeGuardDb db, SyncApiClient api)
    {
        _db  = db;
        _api = api;
    }

    // ── Enqueue ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Serialise <paramref name="payload"/> and add it to the local Outbox.
    /// Call this instead of calling the API directly when offline support matters.
    /// </summary>
    public async Task EnqueueAsync<T>(string operationType, T payload)
    {
        var entry = new OutboxEntryLocal(
            ClientOperationId: Guid.CreateVersion7().ToString(),
            OperationType:     operationType,
            PayloadJson:       JsonSerializer.Serialize(payload, Json.Options),
            CreatedAt:         DateTimeOffset.UtcNow
        );

        await _db.OutboxAddAsync(entry);
        OutboxChanged?.Invoke();
    }

    // ── Flush ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempt to send all pending outbox entries to the server.
    /// Returns a <see cref="FlushResult"/> summarising what happened.
    /// Safe to call repeatedly — already-delivered entries are not resent.
    /// </summary>
    public async Task<FlushResult> FlushAsync(CancellationToken ct = default)
    {
        var pending = await _db.OutboxGetPendingAsync();
        if (pending.Count == 0) return FlushResult.Empty;

        var request = new SyncBatchRequest(
            pending.Select(e => new OutboxEntry(
                Guid.Parse(e.ClientOperationId),
                e.OperationType,
                e.PayloadJson,
                e.CreatedAt
            )).ToList()
        );

        SyncBatchResponse? response;
        try
        {
            response = await _api.PostBatchAsync(request, ct);
        }
        catch (Exception)
        {
            // Network failure — leave everything in the outbox, will retry later.
            return new FlushResult(Sent: 0, Committed: 0, Failed: pending.Count);
        }

        if (response is null)
            return new FlushResult(Sent: pending.Count, Committed: 0, Failed: pending.Count);

        var committed = new List<string>();
        var failed    = new List<string>();

        foreach (var ack in response.Acks)
        {
            var id = ack.ClientOperationId.ToString();
            if (ack.Status is SyncAckStatus.Committed or SyncAckStatus.Duplicate)
                committed.Add(id);
            else
                failed.Add(id);
        }

        if (committed.Count > 0)
            await _db.OutboxMarkDeliveredAsync(committed);

        foreach (var id in failed)
            await _db.OutboxMarkFailedAsync(id);

        OutboxChanged?.Invoke();

        return new FlushResult(
            Sent:      pending.Count,
            Committed: committed.Count,
            Failed:    failed.Count
        );
    }

    public Task<int> PendingCountAsync() => _db.OutboxCountAsync();
}

public sealed record FlushResult(int Sent, int Committed, int Failed)
{
    public static FlushResult Empty => new(0, 0, 0);
    public bool HasFailures => Failed > 0;
}
