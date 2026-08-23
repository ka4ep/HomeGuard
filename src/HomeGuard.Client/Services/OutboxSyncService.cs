using HomeGuard.Common.Sync;
using Microsoft.Extensions.Logging;
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
    private readonly BlobApiClient _blobs;
    private readonly ILogger<OutboxSyncService> _logger;

    // Raised when the outbox count changes so UI can show a badge.
    public event Action? OutboxChanged;

    public OutboxSyncService(HomeGuardDb db, SyncApiClient api, BlobApiClient blobs, ILogger<OutboxSyncService> logger)
    {
        _db     = db;
        _api    = api;
        _blobs  = blobs;
        _logger = logger;
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

    /// <summary>
    /// Queues a captured file for upload. Unlike <see cref="EnqueueAsync{T}"/>, this never
    /// goes through the JSON batch endpoint — a file can be several MB, which the batch
    /// request is not sized for. <see cref="FlushAsync"/> sends each queued file as its own
    /// request, and the client-generated id makes a retried send idempotent server-side.
    /// </summary>
    public async Task EnqueueBlobUploadAsync(
        byte[] data, string mimeType, string fileName,
        Guid ownerEntityId, string ownerEntityType)
    {
        var entry = new PendingBlobUpload(
            ClientOperationId: Guid.CreateVersion7().ToString(),
            Data:              data,
            MimeType:          mimeType,
            FileName:          fileName,
            OwnerEntityId:     ownerEntityId,
            OwnerEntityType:   ownerEntityType,
            CreatedAt:         DateTimeOffset.UtcNow
        );

        await _db.BlobOutboxAddAsync(entry);
        _logger.LogInformation("Outbox: queued blob upload {FileName} ({Bytes} bytes) for {OwnerType} {OwnerId}",
            fileName, data.Length, ownerEntityType, ownerEntityId);
        OutboxChanged?.Invoke();
    }

    /// <summary>Pending uploads for one owner — lets a detail page show "queued" rows of its own.</summary>
    public async Task<IReadOnlyList<PendingBlobUpload>> GetPendingBlobsForOwnerAsync(Guid ownerEntityId)
        => [.. (await _db.BlobOutboxGetPendingAsync()).Where(b => b.OwnerEntityId == ownerEntityId)];

    /// <summary>Cancels a queued upload before it ever reached the server.</summary>
    public async Task RemovePendingBlobAsync(string clientOperationId)
    {
        await _db.BlobOutboxRemoveAsync(clientOperationId);
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
        var commands = await FlushCommandsAsync(ct);
        var blobs    = await FlushBlobsAsync(ct);

        var result = new FlushResult(
            Sent:      commands.Sent      + blobs.Sent,
            Committed: commands.Committed + blobs.Committed,
            Failed:    commands.Failed    + blobs.Failed
        );

        if (result.Sent > 0)
            _logger.LogInformation("Outbox: flush sent {Sent}, committed {Committed}, failed {Failed}",
                result.Sent, result.Committed, result.Failed);

        return result;
    }

    private async Task<FlushResult> FlushCommandsAsync(CancellationToken ct)
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

    /// <summary>
    /// One request per queued file — there is no batch endpoint for blobs (see
    /// <see cref="EnqueueBlobUploadAsync"/>). A network failure on one file leaves just
    /// that one queued; the rest of the batch still gets a chance to go through.
    /// </summary>
    private async Task<FlushResult> FlushBlobsAsync(CancellationToken ct)
    {
        var pending = await _db.BlobOutboxGetPendingAsync();
        if (pending.Count == 0) return FlushResult.Empty;

        int committed = 0, failed = 0;

        foreach (var item in pending)
        {
            Guid? id;
            try
            {
                id = await _blobs.UploadAsync(
                    item.Data, item.MimeType, item.FileName,
                    item.OwnerEntityId, item.OwnerEntityType,
                    Guid.Parse(item.ClientOperationId), ct);
            }
            catch (Exception ex)
            {
                id = null; // network failure — leave queued, retry on the next flush.
                _logger.LogWarning(ex, "Outbox: blob upload failed for {FileName}, leaving queued", item.FileName);
            }

            if (id is not null)
            {
                await _db.BlobOutboxRemoveAsync(item.ClientOperationId);
                committed++;
            }
            else
            {
                failed++;
            }
        }

        OutboxChanged?.Invoke();
        return new FlushResult(Sent: pending.Count, Committed: committed, Failed: failed);
    }

    public async Task<int> PendingCountAsync()
        => await _db.OutboxCountAsync() + await _db.BlobOutboxCountAsync();
}

public sealed record FlushResult(int Sent, int Committed, int Failed)
{
    public static FlushResult Empty => new(0, 0, 0);
    public bool HasFailures => Failed > 0;
}
