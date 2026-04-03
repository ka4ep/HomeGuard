using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;
using HomeGuard.Domain.ValueObjects;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// A single maintenance or service event for a piece of equipment.
/// Examples: oil change, annual boiler inspection, tyre rotation, filter replacement.
/// </summary>
public sealed class ServiceRecord : Entity
{
    private ServiceRecord() { }

    // ── FK ──────────────────────────────────────────────────────────────────
    public Guid EquipmentId { get; private set; }

    // ── Core fields ─────────────────────────────────────────────────────────

    /// <summary>Short description, e.g. "Oil change + filter — 60 000 km".</summary>
    public string Title { get; private set; } = null!;

    public DateOnly ServiceDate { get; private set; }

    /// <summary>
    /// When the next service of this type is due.
    /// Null if it was a one-off event or the interval is unknown.
    /// </summary>
    public DateOnly? NextServiceDate { get; private set; }

    public decimal? Cost { get; private set; }

    /// <summary>Garage, technician, or service centre name.</summary>
    public string? ServiceProvider { get; private set; }

    /// <summary>Freeform notes in Markdown: what was found, what was replaced, part numbers.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Odometer reading at time of service.
    /// Stored as string to support both km and miles without conversion.
    /// </summary>
    public string? OdometerReading { get; private set; }

    // ── Calendar sync ────────────────────────────────────────────────────────
    public string? GoogleCalendarEventId { get; private set; }

    // ── Notification rules ───────────────────────────────────────────────────
    private readonly List<NotificationRule> _notificationRules = [];

    /// <summary>
    /// When to send push notifications before <see cref="NextServiceDate"/>.
    /// Empty if <see cref="NextServiceDate"/> is null.
    /// Default: 1 month, 1 week, 1 day before next service.
    /// </summary>
    public IReadOnlyList<NotificationRule> NotificationRules => _notificationRules.AsReadOnly();

    // ── Navigation ───────────────────────────────────────────────────────────
    private readonly List<BlobEntry> _attachments = [];
    /// <summary>Invoices, photos of replaced parts, diagnostic printouts.</summary>
    public IReadOnlyList<BlobEntry> Attachments => _attachments.AsReadOnly();

    // ── Computed ─────────────────────────────────────────────────────────────
    public bool IsOverdue(DateOnly today) =>
        NextServiceDate.HasValue && today > NextServiceDate.Value;

    public int? DaysUntilNextService(DateOnly today) =>
        NextServiceDate.HasValue
            ? NextServiceDate.Value.DayNumber - today.DayNumber
            : null;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static ServiceRecord Create(
        Guid equipmentId,
        string title,
        DateOnly serviceDate,
        DateOnly? nextServiceDate = null,
        decimal? cost = null,
        string? serviceProvider = null,
        string? notes = null,
        string? odometerReading = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var sr = new ServiceRecord();
        sr.InitNew();
        sr.EquipmentId = equipmentId;
        sr.Title = title.Trim();
        sr.ServiceDate = serviceDate;
        sr.NextServiceDate = nextServiceDate;
        sr.Cost = cost;
        sr.ServiceProvider = serviceProvider?.Trim();
        sr.Notes = notes;
        sr.OdometerReading = odometerReading?.Trim();

        if (nextServiceDate.HasValue)
            sr.ApplyDefaultNotificationRules();

        return sr;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Update(
        string title,
        DateOnly serviceDate,
        DateOnly? nextServiceDate = null,
        decimal? cost = null,
        string? serviceProvider = null,
        string? notes = null,
        string? odometerReading = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        ServiceDate = serviceDate;
        NextServiceDate = nextServiceDate;
        Cost = cost;
        ServiceProvider = serviceProvider?.Trim();
        Notes = notes;
        OdometerReading = odometerReading?.Trim();
        Touch();
    }

    public void SetNotificationRules(IEnumerable<(NotificationOffset offset, bool enabled)> rules)
    {
        _notificationRules.Clear();
        _notificationRules.AddRange(rules.Select(r => NotificationRule.Create(r.offset, r.enabled)));
        Touch();
    }

    public void SetGoogleCalendarEventId(string? eventId)
    {
        GoogleCalendarEventId = eventId;
        Touch();
    }

    // ── Defaults ─────────────────────────────────────────────────────────────

    private void ApplyDefaultNotificationRules()
    {
        _notificationRules.AddRange([
            NotificationRule.Create(NotificationOffset.OneMonthBefore),
            NotificationRule.Create(NotificationOffset.OneWeekBefore),
            NotificationRule.Create(NotificationOffset.OneDayBefore),
        ]);
    }
}
