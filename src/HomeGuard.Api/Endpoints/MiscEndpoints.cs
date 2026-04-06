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
        var grp = app.MapGroup("/api/sync").WithTags("Sync");

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
        var grp = app.MapGroup("/api/blobs").WithTags("Blobs");

        grp.MapPost("/upload", Upload).DisableAntiforgery();
        grp.MapGet("/{id:guid}", Download);
        grp.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> Upload(
        IFormFile file,
        [FromQuery] Guid ownerEntityId,
        [FromQuery] string ownerEntityType,
        HomeGuard.Application.Interfaces.IBlobStorage storage,
        HomeGuard.Application.Interfaces.Repositories.IBlobEntryRepository repo,
        HomeGuard.Application.Interfaces.IUnitOfWork uow,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var localPath = await storage.SaveLocallyAsync(stream, file.FileName, file.ContentType, ct);

        var entry = Domain.Entities.BlobEntry.CreateLocal(
            ownerEntityId, ownerEntityType,
            file.FileName, file.ContentType,
            file.Length, localPath);

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
            ;

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
