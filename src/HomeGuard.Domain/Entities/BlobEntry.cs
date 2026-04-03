using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// Tracks a single file attachment (photo, scan, invoice) linked to an entity.
/// The file itself lives either on local disk (fallback) or NextCloud (primary).
/// <see cref="SyncStatus"/> drives the background sync job.
/// </summary>
public sealed class BlobEntry : Entity
{
    private BlobEntry() { }

    // ── Owning entity (polymorphic by convention, not by FK) ─────────────────
    /// <summary>Id of the Equipment, Warranty, or ServiceRecord this blob belongs to.</summary>
    public Guid OwnerEntityId { get; private set; }

    /// <summary>Discriminator: "Equipment" | "Warranty" | "ServiceRecord".</summary>
    public string OwnerEntityType { get; private set; } = null!;

    // ── File metadata ────────────────────────────────────────────────────────
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }

    // ── Storage locations ────────────────────────────────────────────────────
    /// <summary>Absolute path on the server's local disk. Null after confirmed NextCloud-only.</summary>
    public string? LocalPath { get; private set; }

    /// <summary>
    /// Path relative to the NextCloud WebDAV root, e.g. "HomeGuard/blobs/{id}/receipt.jpg".
    /// Null until successfully uploaded.
    /// </summary>
    public string? NextCloudPath { get; private set; }

    public BlobSyncStatus SyncStatus { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────

    public static BlobEntry CreateLocal(
        Guid ownerEntityId,
        string ownerEntityType,
        string fileName,
        string contentType,
        long sizeBytes,
        string localPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerEntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        var b = new BlobEntry();
        b.InitNew();
        b.OwnerEntityId = ownerEntityId;
        b.OwnerEntityType = ownerEntityType;
        b.FileName = fileName;
        b.ContentType = contentType;
        b.SizeBytes = sizeBytes;
        b.LocalPath = localPath;
        b.SyncStatus = BlobSyncStatus.LocalOnly;
        return b;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void MarkSynced(string nextCloudPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nextCloudPath);
        NextCloudPath = nextCloudPath;
        SyncStatus = BlobSyncStatus.Synced;
        Touch();
    }

    public void MarkSyncFailed()
    {
        SyncStatus = BlobSyncStatus.SyncFailed;
        Touch();
    }

    public void MarkPendingSync()
    {
        SyncStatus = BlobSyncStatus.LocalOnly;
        Touch();
    }

    /// <summary>
    /// Returns the best available path for reading the file.
    /// NextCloud is preferred; local path is the fallback.
    /// </summary>
    public string? BestReadPath() => NextCloudPath ?? LocalPath;
}
