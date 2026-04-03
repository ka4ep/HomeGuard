namespace HomeGuard.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Id uses UUID v7 (time-sortable) — sortable by creation time without a separate index.
/// </summary>
public abstract class Entity
{
    // protected set: accessible from sealed derived classes and from EF Core via reflection.
    public Guid Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset UpdatedAt { get; protected set; }

    /// <summary>Bumps UpdatedAt. Call from every mutation method.</summary>
    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    /// <summary>Sets Id, CreatedAt, UpdatedAt for a freshly created entity.</summary>
    protected void InitNew()
    {
        Id = Guid.CreateVersion7();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
