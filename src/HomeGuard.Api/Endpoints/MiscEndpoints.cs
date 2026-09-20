using HomeGuard.Application.Services;
using HomeGuard.Infrastructure.Calendar;
using HomeGuard.Infrastructure.Notifications;
using HomeGuard.Common.Sync;
using Microsoft.AspNetCore.Mvc;

namespace HomeGuard.Api.Endpoints;

// ── Offline sync ──────────────────────────────────────────────────────────────

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/sync").WithTags("Sync").RequireAuthorization();

        grp.MapPost("/batch", async (
            [FromBody] SyncBatchRequest req,
            SyncProcessorService svc,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            // TODO: extract real userId from ClaimsPrincipal once auth is wired.
            var userId = Guid.Empty;
            var response = await svc.ProcessBatchAsync(userId, req, ct);
            return Results.Ok(response);
        });
    }
}

// ── Blobs ─────────────────────────────────────────────────────────────────────

public static class BlobEndpoints
{
    public static void MapBlobEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/blobs").WithTags("Blobs").RequireAuthorization();

        grp.MapPost("/upload", Upload).DisableAntiforgery();
        grp.MapGet("/", GetByOwner);
        grp.MapGet("/{id:guid}", Download);
        grp.MapDelete("/{id:guid}", Delete);
    }

    // Generic by-owner listing — Contract's own attachments ride along on
    // ContractDetailDto instead (see ContractEndpoints), but Equipment/Warranty/
    // ServiceRecord have no per-entity detail endpoint to carry it, so the
    // attachments card loads its own list straight from here.
    private static async Task<IResult> GetByOwner(
        [FromQuery] Guid ownerEntityId,
        HomeGuard.Application.Interfaces.Repositories.IBlobEntryRepository repo,
        CancellationToken ct)
    {
        var blobs = await repo.GetByOwnerAsync(ownerEntityId, ct);
        return Results.Ok(blobs.Select(BlobDto.From));
    }

    private static async Task<IResult> Upload(
        IFormFile file,
        [FromQuery] Guid ownerEntityId,
        [FromQuery] string ownerEntityType,
        HomeGuard.Application.Interfaces.IBlobStorage storage,
        HomeGuard.Application.Interfaces.Repositories.IBlobEntryRepository repo,
        HomeGuard.Application.Interfaces.IUnitOfWork uow,
        CancellationToken ct,
        [FromQuery] Guid? clientOperationId = null)
    {
        // The offline outbox retries an upload it never got an ack for. Without this,
        // a retried upload after a flaky connection would create a second BlobEntry for
        // the same file every time — the same idempotency guarantee the JSON outbox gets
        // from ClientOperationId, applied here since blobs bypass that batch entirely.
        if (clientOperationId is { } existingId)
        {
            var existing = await repo.GetByIdAsync(existingId, ct);
            if (existing is not null)
                return Results.Ok(new { existing.Id, existing.SyncStatus });
        }

        await using var stream = file.OpenReadStream();
        var localPath = await storage.SaveLocallyAsync(stream, file.FileName, file.ContentType, ct);

        var entry = Domain.Entities.BlobEntry.CreateLocal(
            ownerEntityId, ownerEntityType,
            file.FileName, file.ContentType,
            file.Length, localPath,
            id: clientOperationId);

        await repo.AddAsync(entry, ct);
        await uow.SaveChangesAsync(ct);

        return Results.Created($"/api/blobs/{entry.Id}", new { entry.Id, entry.SyncStatus });
    }

    private static async Task<IResult> Download(
        Guid id,
        HomeGuard.Application.Interfaces.Repositories.IBlobEntryRepository repo,
        HomeGuard.Application.Interfaces.IBlobStorage storage,
        CancellationToken ct)
    {
        var blob = await repo.GetByIdAsync(id, ct);
        if (blob is null) return Results.NotFound();

        var stream = await storage.ReadAsync(blob, ct);
        return Results.Stream(stream, blob.ContentType, blob.FileName);
    }

    private static async Task<IResult> Delete(
        Guid id,
        HomeGuard.Application.Interfaces.Repositories.IBlobEntryRepository repo,
        HomeGuard.Application.Interfaces.IBlobStorage storage,
        HomeGuard.Application.Interfaces.IUnitOfWork uow,
        CancellationToken ct)
    {
        var blob = await repo.GetByIdAsync(id, ct);
        if (blob is null) return Results.NotFound();

        await storage.DeleteAsync(blob, ct);
        repo.Remove(blob);
        await uow.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

public sealed record BlobDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Domain.Enums.BlobSyncStatus SyncStatus,
    DateTimeOffset CreatedAt)
{
    public static BlobDto From(Domain.Entities.BlobEntry b) => new(
        b.Id, b.FileName, b.ContentType, b.SizeBytes, b.SyncStatus, b.CreatedAt);
}

// ── Client diagnostics ───────────────────────────────────────────────────────

// A phone screen can't be copy-pasted from. The client reports its own unhandled JS/
// WASM errors here (see wwwroot/js/diagnostics.js) so they land in the server log
// instead of a transcription of a red banner. Same auth as everything else under
// /api — this only ever fires for errors hit while actually using the app, i.e.
// already logged in, so requiring the session cookie costs nothing real.
public static class DiagnosticsEndpoints
{
    public static void MapDiagnosticsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/diagnostics/client-error", (
            ClientErrorReport report,
            ILoggerFactory loggerFactory) =>
        {
            var log = loggerFactory.CreateLogger("ClientError");
            log.LogError(
                "[{Source}] {Message} @ {Url} ({UserAgent})\n{Stack}",
                report.Source, report.Message, report.Url, report.UserAgent, report.Stack);

            // The action trail (every ILogger<T> call app-wide, via BrowserBufferLoggerProvider)
            // leading up to this — one block, in order, so "what happened right before" doesn't
            // need fifty grepped-together log lines to reconstruct.
            if (report.Logs is { Count: > 0 } logs)
            {
                var trail = string.Join('\n', logs.Select(e => $"  {e.T} [{e.Level}] {e.Category}: {e.Message}"));
                log.LogInformation("Client log trail ({Count} entries):\n{Trail}", logs.Count, trail);
            }

            return Results.NoContent();
        }).RequireAuthorization();
    }
}

public sealed record ClientLogEntry(string T, string Level, string Category, string Message);

public sealed record ClientErrorReport(
    string Message,
    string? Stack,
    string? Source,
    string? Url,
    string? UserAgent,
    IReadOnlyList<ClientLogEntry>? Logs = null
);

// ── iCal feed ─────────────────────────────────────────────────────────────────

public static class CalendarFeedEndpoints
{
    public static void MapCalendarFeedEndpoint(this WebApplication app)
    {
        // No auth — Family Wall and NextCloud poll this URL anonymously.
        // Keep the URL unguessable via the secret token in appsettings if needed.
        app.MapGet("/api/calendar/feed.ics", async (
            ICalFeedGenerator generator, CancellationToken ct) =>
        {
            var ics = await generator.GenerateAsync(ct);
            return Results.Content(ics, "text/calendar; charset=utf-8");
        })
        .WithTags("Calendar")
        .AllowAnonymous();
    }
}

// ── Web Push subscription management ─────────────────────────────────────────

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        grp.MapPost("/subscribe", Subscribe);
        grp.MapDelete("/subscribe", Unsubscribe);

        // Expose the VAPID public key so the client can build a subscription.
        grp.MapGet("/vapid-public-key", (IConfiguration config) =>
            Results.Ok(new { Key = config["WebPush:VapidPublicKey"] }))
            .AllowAnonymous();
    }

    private static async Task<IResult> Subscribe(
        [FromBody] PushSubscribeRequest req,
        WebPushNotificationSender sender,
        HttpContext ctx,
        CancellationToken ct)
    {
        // TODO: replace Guid.Empty with real userId from claims.
        var userId = Guid.Empty;
        await sender.RegisterSubscriptionAsync(userId, req.Endpoint, req.P256dh, req.Auth, ct);
        return Results.Created();
    }

    private static async Task<IResult> Unsubscribe(
        [FromBody] PushUnsubscribeRequest req,
        WebPushNotificationSender sender,
        CancellationToken ct)
    {
        await sender.RemoveSubscriptionAsync(req.Endpoint, ct);
        return Results.NoContent();
    }
}

public sealed record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);
public sealed record PushUnsubscribeRequest(string Endpoint);

// ── Attention ─────────────────────────────────────────────────────────────────

/// <summary>
/// The one number the app icon badge and the Home strip agree on — see
/// contracts-spec.md §10.2. Cheap enough to poll on every app foreground: three
/// existing service calls merged, nothing recomputed that was not already available.
/// </summary>
public static class AttentionEndpoints
{
    public static void MapAttentionEndpoint(this WebApplication app)
    {
        app.MapGet("/api/attention", async (
                AttentionService svc, CancellationToken ct, [FromQuery] int days = 7) =>
            Results.Ok(await svc.GetAsync(days, ct)))
            .WithTags("Attention")
            .RequireAuthorization();
    }
}
