using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WebPush;

namespace HomeGuard.Infrastructure.Notifications;

public sealed class WebPushOptions
{
    public const string Section = "WebPush";

    public string VapidPublicKey  { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>mailto: or https: URI identifying the server operator.</summary>
    public string VapidSubject { get; set; } = "mailto:admin@homeguard.local";
}

/// <summary>
/// Stores push subscriptions per user and sends Web Push notifications via VAPID.
/// Subscriptions are persisted in their own EF Core table (see <see cref="PushSubscriptionEntity"/>).
/// </summary>
public sealed class WebPushNotificationSender : INotificationSender
{
    private readonly WebPushOptions _options;
    private readonly Persistence.HomeGuardDbContext _db;
    private readonly ILogger<WebPushNotificationSender> _logger;

    public WebPushNotificationSender(
        IOptions<WebPushOptions> options,
        Persistence.HomeGuardDbContext db,
        ILogger<WebPushNotificationSender> logger)
    {
        _options = options.Value;
        _db      = db;
        _logger  = logger;
    }

    public async Task SendAsync(
        PushNotification notification, Guid userId, CancellationToken ct = default)
    {
        var subscriptions = await _db.Set<PushSubscriptionEntity>()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        await SendToSubscriptionsAsync(notification, subscriptions, ct);
    }

    public async Task SendToAllAsync(PushNotification notification, CancellationToken ct = default)
    {
        var subscriptions = await _db.Set<PushSubscriptionEntity>().ToListAsync(ct);
        await SendToSubscriptionsAsync(notification, subscriptions, ct);
    }

    // ── Subscription management ───────────────────────────────────────────────

    public async Task RegisterSubscriptionAsync(
        Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct = default)
    {
        var existing = await _db.Set<PushSubscriptionEntity>()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

        if (existing is not null)
        {
            existing.P256dh   = p256dh;
            existing.Auth     = auth;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.Set<PushSubscriptionEntity>().Add(new PushSubscriptionEntity
            {
                Id        = Guid.CreateVersion7(),
                UserId    = userId,
                Endpoint  = endpoint,
                P256dh    = p256dh,
                Auth      = auth,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveSubscriptionAsync(string endpoint, CancellationToken ct = default)
    {
        var entity = await _db.Set<PushSubscriptionEntity>()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

        if (entity is not null)
        {
            _db.Set<PushSubscriptionEntity>().Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private async Task SendToSubscriptionsAsync(
        PushNotification notification,
        IEnumerable<PushSubscriptionEntity> subscriptions,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = notification.Title,
            body  = notification.Body,
            url   = notification.Url ?? "/",
            tag   = notification.Tag ?? "homeguard",
        });

        var vapidDetails = new VapidDetails(
            _options.VapidSubject,
            _options.VapidPublicKey,
            _options.VapidPrivateKey);

        var client = new WebPushClient();

        foreach (var sub in subscriptions)
        {
            try
            {
                var subscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(subscription, payload, vapidDetails, ct);
                _logger.LogDebug("Push sent to endpoint {Endpoint}", sub.Endpoint[..Math.Min(30, sub.Endpoint.Length)]);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // Subscription expired — remove it.
                _logger.LogInformation("Removing expired push subscription: {Endpoint}", sub.Endpoint[..Math.Min(30, sub.Endpoint.Length)]);
                _db.Set<PushSubscriptionEntity>().Remove(sub);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification to {Endpoint}", sub.Endpoint[..Math.Min(30, sub.Endpoint.Length)]);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>EF Core entity for a stored Web Push subscription.</summary>
public sealed class PushSubscriptionEntity
{
    public Guid   Id        { get; set; }
    public Guid   UserId    { get; set; }
    public string Endpoint  { get; set; } = null!;
    public string P256dh    { get; set; } = null!;
    public string Auth      { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
