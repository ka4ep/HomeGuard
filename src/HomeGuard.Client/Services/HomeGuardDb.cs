using Microsoft.JSInterop;
using System.Text.Json;

namespace HomeGuard.Client.Services;

/// <summary>
/// C# wrapper over the homeguard-db.js IndexedDB API.
/// All methods are async and call into JS via IJSRuntime.
/// </summary>
public sealed class HomeGuardDb
{
    private readonly IJSRuntime _js;

    public HomeGuardDb(IJSRuntime js) => _js = js;

    // ── Outbox ────────────────────────────────────────────────────────────────

    public async Task OutboxAddAsync(OutboxEntryLocal entry)
    {
        var obj = new
        {
            clientOperationId = entry.ClientOperationId,
            operationType     = entry.OperationType,
            payloadJson       = entry.PayloadJson,
            createdAt         = entry.CreatedAt.ToUnixTimeMilliseconds(),
        };
        await _js.InvokeVoidAsync("homeGuardDb.outboxAdd", obj);
    }

    public async Task<IReadOnlyList<OutboxEntryLocal>> OutboxGetPendingAsync()
    {
        var raw = await _js.InvokeAsync<JsonElement[]>("homeGuardDb.outboxGetPending");
        return raw.Select(e => new OutboxEntryLocal(
            ClientOperationId: e.GetProperty("clientOperationId").GetString()!,
            OperationType:     e.GetProperty("operationType").GetString()!,
            PayloadJson:       e.GetProperty("payloadJson").GetString()!,
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(
                           e.GetProperty("createdAt").GetInt64())
        )).ToList();
    }

    public async Task OutboxMarkDeliveredAsync(IEnumerable<string> clientOperationIds)
        => await _js.InvokeVoidAsync("homeGuardDb.outboxMarkDelivered",
               clientOperationIds.ToArray());

    public async Task OutboxMarkFailedAsync(string clientOperationId)
        => await _js.InvokeVoidAsync("homeGuardDb.outboxMarkFailed", clientOperationId);

    public async Task<int> OutboxCountAsync()
        => await _js.InvokeAsync<int>("homeGuardDb.outboxCount");

    // ── Blob outbox ──────────────────────────────────────────────────────────────
    // Separate from the JSON outbox above: a file can be several MB, and the batch
    // endpoint (api/sync/batch) is sized for small command payloads, not attachments.
    // Each pending upload gets its own POST to api/blobs/upload when flushed.

    public async Task BlobOutboxAddAsync(PendingBlobUpload entry)
    {
        var obj = new
        {
            clientOperationId = entry.ClientOperationId,
            data              = entry.Data,
            mimeType          = entry.MimeType,
            fileName          = entry.FileName,
            ownerEntityId     = entry.OwnerEntityId.ToString(),
            ownerEntityType   = entry.OwnerEntityType,
            createdAt         = entry.CreatedAt.ToUnixTimeMilliseconds(),
        };
        await _js.InvokeVoidAsync("homeGuardDb.blobOutboxAdd", obj);
    }

    public async Task<IReadOnlyList<PendingBlobUpload>> BlobOutboxGetPendingAsync()
    {
        var raw = await _js.InvokeAsync<JsonElement[]>("homeGuardDb.blobOutboxGetPending");
        return raw.Select(e => new PendingBlobUpload(
            ClientOperationId: e.GetProperty("clientOperationId").GetString()!,
            Data:              e.GetProperty("data").GetBytesFromBase64(),
            MimeType:          e.GetProperty("mimeType").GetString()!,
            FileName:          e.GetProperty("fileName").GetString()!,
            OwnerEntityId:     Guid.Parse(e.GetProperty("ownerEntityId").GetString()!),
            OwnerEntityType:   e.GetProperty("ownerEntityType").GetString()!,
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(
                           e.GetProperty("createdAt").GetInt64())
        )).ToList();
    }

    public async Task BlobOutboxRemoveAsync(string clientOperationId)
        => await _js.InvokeVoidAsync("homeGuardDb.blobOutboxRemove", clientOperationId);

    public async Task<int> BlobOutboxCountAsync()
        => await _js.InvokeAsync<int>("homeGuardDb.blobOutboxCount");

    // ── Cache ─────────────────────────────────────────────────────────────────

    public async Task CacheSetAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, Json.Options);
        await _js.InvokeVoidAsync("homeGuardDb.cacheSet", key, json);
    }

    public async Task<T?> CacheGetAsync<T>(string key)
    {
        var json = await _js.InvokeAsync<string?>("homeGuardDb.cacheGet", key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, Json.Options);
    }

    public Task CacheDeleteAsync(string key)
        => _js.InvokeVoidAsync("homeGuardDb.cacheDelete", key).AsTask();
}

/// <summary>Client-side outbox entry stored in IndexedDB.</summary>
public sealed record OutboxEntryLocal(
    string ClientOperationId,
    string OperationType,
    string PayloadJson,
    DateTimeOffset CreatedAt
);

/// <summary>A file capture waiting for a server round-trip, stored in IndexedDB.</summary>
public sealed record PendingBlobUpload(
    string ClientOperationId,
    byte[] Data,
    string MimeType,
    string FileName,
    Guid OwnerEntityId,
    string OwnerEntityType,
    DateTimeOffset CreatedAt
);
