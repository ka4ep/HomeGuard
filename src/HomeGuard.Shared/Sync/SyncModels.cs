namespace HomeGuard.Shared.Sync;

/// <summary>
/// A single client-side operation pending upload to the server.
/// The server is idempotent on <see cref="ClientOperationId"/>:
/// replaying the same entry returns the original result without side-effects.
/// </summary>
public sealed record OutboxEntry(
    Guid ClientOperationId,
    string OperationType,       // e.g. "CreateEquipment", "UpdateWarranty"
    string PayloadJson,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Per-entry acknowledgement returned by the server in a sync batch response.
/// </summary>
public sealed record SyncAck(
    Guid ClientOperationId,
    SyncAckStatus Status,
    string? ErrorMessage = null
);

public enum SyncAckStatus { Committed, Duplicate, Rejected }

/// <summary>
/// Batch request: client sends N pending outbox entries in one HTTP call.
/// </summary>
public sealed record SyncBatchRequest(IReadOnlyList<OutboxEntry> Entries);

/// <summary>
/// Batch response: one ack per entry, in the same order.
/// </summary>
public sealed record SyncBatchResponse(IReadOnlyList<SyncAck> Acks);
