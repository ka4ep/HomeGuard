using HomeGuard.Domain.Entities;
using HomeGuard.Common.Sync;

namespace HomeGuard.Application.Interfaces;

// ── Unit of work ─────────────────────────────────────────────────────────────

/// <summary>
/// Wraps the database transaction. Call after making changes to entities
/// to persist everything in a single atomic write.
/// SQLite + SemaphoreSlim in the implementation ensures no concurrent writes.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// ── Calendar ──────────────────────────────────────────────────────────────────

/// <summary>
/// Abstraction over Google Calendar API, CalDAV (NextCloud), or any other backend.
/// The iCal feed endpoint is a separate, read-only concern and does not go through this.
/// </summary>
public interface ICalendarProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Create or update a calendar event. Returns the provider-specific event ID.
    /// Pass <paramref name="externalId"/> to update an existing event; null to create.
    /// </summary>
    Task<string> UpsertEventAsync(CalendarEventDto evt, CancellationToken ct = default);

    Task DeleteEventAsync(string externalId, CancellationToken ct = default);
}

/// <summary>DTO used between Application and Infrastructure for calendar operations.</summary>
public sealed record CalendarEventDto(
    string? ExternalId,
    string Title,
    string? Description,
    DateOnly Date,
    /// <summary>
    /// Tag written into the event's extended properties so we can identify our own events.
    /// Format: "homeguard:{type}:{entityId}"
    /// </summary>
    string HomeGuardTag
);

// ── Blob storage ──────────────────────────────────────────────────────────────

public interface IBlobStorage
{
    /// <summary>Save a stream to local disk. Returns the absolute local path.</summary>
    Task<string> SaveLocallyAsync(
        Stream data, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Upload a blob from local disk to NextCloud via WebDAV.
    /// Returns true on success, false on transient failure.
    /// </summary>
    Task<bool> SyncToRemoteAsync(BlobEntry blob, CancellationToken ct = default);

    /// <summary>Open a stream for reading, preferring NextCloud, falling back to local path.</summary>
    Task<Stream> ReadAsync(BlobEntry blob, CancellationToken ct = default);

    Task DeleteAsync(BlobEntry blob, CancellationToken ct = default);
}

// ── Push notifications ────────────────────────────────────────────────────────

public sealed record PushNotification(
    string Title,
    string Body,
    string? Url = null,
    string? Tag = null
);

public interface INotificationSender
{
    /// <summary>Send a Web Push notification to a specific user (all their devices).</summary>
    Task SendAsync(PushNotification notification, Guid userId, CancellationToken ct = default);

    /// <summary>Broadcast to all registered users.</summary>
    Task SendToAllAsync(PushNotification notification, CancellationToken ct = default);
}

// ── Sync idempotency store ────────────────────────────────────────────────────

/// <summary>
/// Persists completed sync operations so the server can return cached results
/// when the client replays the same ClientOperationId.
/// </summary>
public interface IProcessedOperationStore
{
    Task<SyncAck?> GetCachedAckAsync(Guid clientOperationId, CancellationToken ct = default);

    Task RecordAsync(
        Guid clientOperationId,
        Guid userId,
        string operationType,
        SyncAck ack,
        CancellationToken ct = default);
}
