using Microsoft.Extensions.Logging;
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
    private readonly ILogger<HomeGuardDb> _logger;

    public HomeGuardDb(IJSRuntime js, ILogger<HomeGuardDb> logger)
    {
        _js     = js;
        _logger = logger;
    }

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
            // Confirmed live, the actual root cause: a byte[]-typed value nested in a
            // generic interop argument gets Blazor's own byte-array-transfer treatment —
            // the JSON payload carries only a transient reference id ({"__byte[]": 0}),
            // valid for the duration of this one call, not the real bytes. IndexedDB
            // storage outlives that call by definition, so anything read back later was
            // always going to be an unrecoverable dangling reference. Converting to a
            // plain string here means the property is never byte[]-typed at the interop
            // boundary at all, so that whole path never triggers.
            data              = Convert.ToBase64String(entry.Data),
            mimeType          = entry.MimeType,
            fileName          = entry.FileName,
            ownerEntityId     = entry.OwnerEntityId.ToString(),
            ownerEntityType   = entry.OwnerEntityType,
            createdAt         = entry.CreatedAt.ToUnixTimeMilliseconds(),
        };
        await _js.InvokeVoidAsync("homeGuardDb.blobOutboxAdd", obj);
    }

    /// <summary>
    /// homeguard-db.js's own blobOutboxGetPending already normalizes `data` to a base64
    /// string, but this doesn't trust that that fix has actually reached the browser
    /// being read from (a PWA's cached JS can lag a fresh publish) — it accepts
    /// whichever shape .NET's own byte[]-argument interop marshalling happened to write
    /// under the old, unnormalized path: a plain base64 string, a JSON array of byte
    /// values, or Blazor's own {"__byte[]": ...} interop wire format for a byte[] that
    /// wasn't statically typed as byte[] at the call site — confirmed live to carry
    /// either the base64 payload directly, *or*, when the value is a transient
    /// byte-array-transfer reference id (a plain number) rather than the payload, no
    /// recoverable data at all: that reference is only valid for the one interop call
    /// that created it, which IndexedDB storage necessarily outlives. Root-caused and
    /// fixed at the write side now (BlobOutboxAddAsync converts to a plain string before
    /// the interop call, so new rows never take this path) — this stays to explain, and
    /// cleanly drop, whatever is already stuck in an existing IndexedDB from before that.
    /// </summary>
    private static byte[] ReadBlobData(JsonElement entry)
    {
        var data = entry.GetProperty("data");
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("__byte[]", out var wrapped))
        {
            if (wrapped.ValueKind == JsonValueKind.String)
                return wrapped.GetBytesFromBase64();

            throw new InvalidOperationException(
                $"'data' is a transient byte-array interop reference ({wrapped.GetRawText()}), " +
                "not the actual bytes — this row's real data no longer exists anywhere to recover.");
        }

        return data.ValueKind switch
        {
            JsonValueKind.String => data.GetBytesFromBase64(),
            JsonValueKind.Array  => [.. data.EnumerateArray().Select(x => (byte)x.GetInt32())],
            JsonValueKind.Object => [.. data.EnumerateObject()
                                          .OrderBy(p => int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture))
                                          .Select(p => (byte)p.Value.GetInt32())],
            var kind => throw new InvalidOperationException($"Unexpected blob data shape: {kind}"),
        };
    }

    public async Task<IReadOnlyList<PendingBlobUpload>> BlobOutboxGetPendingAsync()
    {
        var raw    = await _js.InvokeAsync<JsonElement[]>("homeGuardDb.blobOutboxGetPending");
        var result = new List<PendingBlobUpload>(raw.Length);

        foreach (var e in raw)
        {
            string? clientOperationId = null;
            try
            {
                clientOperationId = e.GetProperty("clientOperationId").GetString();
                result.Add(new PendingBlobUpload(
                    ClientOperationId: clientOperationId!,
                    Data:              ReadBlobData(e),
                    MimeType:          e.GetProperty("mimeType").GetString()!,
                    FileName:          e.GetProperty("fileName").GetString()!,
                    OwnerEntityId:     Guid.Parse(e.GetProperty("ownerEntityId").GetString()!),
                    OwnerEntityType:   e.GetProperty("ownerEntityType").GetString()!,
                    CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(
                                   e.GetProperty("createdAt").GetInt64())
                ));
            }
            catch (Exception ex)
            {
                // Two guesses at the actual wire shape have both been wrong — dump the
                // real thing instead of a third guess. Capped well short of the full
                // payload (a photo's base64 alone can run six figures of characters);
                // enough to show the property names/shape, not the file itself.
                string dataShape;
                try
                {
                    var text = e.TryGetProperty("data", out var data) ? data.GetRawText() : "<no data property>";
                    dataShape = text.Length > 200 ? text[..200] + "…" : text;
                }
                catch (Exception shapeEx)
                {
                    dataShape = $"<failed to read shape: {shapeEx.Message}>";
                }

                // One row this defensive a parse still can't make sense of is genuinely
                // corrupt — drop it (and remove it, if its id was even readable) instead
                // of taking every other queued upload down with it.
                _logger.LogWarning(ex, "Dropping unreadable blobOutbox entry {ClientOperationId} — data shape: {DataShape}",
                    clientOperationId, dataShape);
                if (clientOperationId is not null)
                    await BlobOutboxRemoveAsync(clientOperationId);
            }
        }

        return result;
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
