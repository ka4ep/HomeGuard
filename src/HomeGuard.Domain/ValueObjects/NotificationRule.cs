using HomeGuard.Domain.Enums;

namespace HomeGuard.Domain.ValueObjects;

/// <summary>
/// A single notification trigger attached to a <c>Warranty</c> or <c>ServiceRecord</c>.
/// Stored as an EF Core owned entity collection (separate table rows, no own PK exposed).
///
/// Example: "send a push notification 1 month before the warranty expires".
/// </summary>
public sealed class NotificationRule
{
    // Parameterless ctor for EF Core.
    private NotificationRule() { }

    public NotificationOffset Offset { get; private set; }
    public bool IsEnabled { get; private set; }

    public static NotificationRule Create(NotificationOffset offset, bool isEnabled = true)
        => new() { Offset = offset, IsEnabled = isEnabled };

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    /// <summary>
    /// Returns the concrete date on which this rule should fire,
    /// given the <paramref name="targetDate"/> (warranty end date or next service date).
    /// </summary>
    public DateOnly FireDate(DateOnly targetDate)
        => targetDate.AddDays(-(int)Offset);

    public override string ToString()
        => $"{(IsEnabled ? "✓" : "✗")} {Offset} before target";
}
