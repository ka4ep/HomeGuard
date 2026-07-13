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

    /// <summary>Recurring maintenance pattern this record belongs to, if any.</summary>
    public Guid? RecurringRuleId { get; private set; }

    // ── Core fields ─────────────────────────────────────────────────────────

    /// <summary>Short description, e.g. "Oil change + filter — 60 000 km".</summary>
    public string Title { get; private set; } = null!;

    public DateOnly ServiceDate { get; private set; }

    /// <summary>
    /// Completed = real past event. Planned = materialized future event, not yet confirmed.
    /// A third state, Predicted, is computed at runtime and never stored here.
    /// </summary>
    public ServiceStatus Status { get; private set; }

    /// <summary>
    /// What <see cref="ServiceDate"/> was originally predicted as, before any reschedule.
    /// Set when a Planned record is materialized; kept as an audit trail after reschedules.
    /// </summary>
    public DateOnly? OriginalPredictedDate { get; private set; }

    public decimal? Cost { get; private set; }

    /// <summary>Garage, technician, or service centre name.</summary>
    public string? ServiceProvider { get; private set; }

    /// <summary>Freeform notes in Markdown: what was found, what was replaced, part numbers.</summary>
    public string? Notes { get; private set; }

    /// <summary>Meter reading at time of service, in the owning Equipment's MeterUnit.</summary>
    public decimal? MeterReading { get; private set; }

    // ── Calendar sync ────────────────────────────────────────────────────────
    public string? GoogleCalendarEventId { get; private set; }

    // ── Notification rules ───────────────────────────────────────────────────
    private readonly List<NotificationRule> _notificationRules = [];

    /// <summary>
    /// When to send push notifications before <see cref="ServiceDate"/>, for Planned records.
    /// Empty for Completed records.
    /// Default: 1 month, 1 week, 1 day before.
    /// </summary>
    public IReadOnlyList<NotificationRule> NotificationRules => _notificationRules.AsReadOnly();

    // ── Navigation ───────────────────────────────────────────────────────────
    private readonly List<BlobEntry> _attachments = [];
    /// <summary>Invoices, photos of replaced parts, diagnostic printouts.</summary>
    public IReadOnlyList<BlobEntry> Attachments => _attachments.AsReadOnly();

    // ── Computed ─────────────────────────────────────────────────────────────
    public bool IsOverdue(DateOnly today) =>
        Status == ServiceStatus.Planned && today > ServiceDate;

    public int? DaysUntilNextService(DateOnly today) =>
        Status == ServiceStatus.Planned
            ? ServiceDate.DayNumber - today.DayNumber
            : null;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static ServiceRecord Create(
        Guid equipmentId,
        string title,
        DateOnly serviceDate,
        ServiceStatus status = ServiceStatus.Completed,
        decimal? cost = null,
        string? serviceProvider = null,
        string? notes = null,
        decimal? meterReading = null,
        Guid? recurringRuleId = null,
        DateOnly? originalPredictedDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var sr = new ServiceRecord();
        sr.InitNew();
        sr.EquipmentId = equipmentId;
        sr.Title = title.Trim();
        sr.ServiceDate = serviceDate;
        sr.Status = status;
        sr.Cost = cost;
        sr.ServiceProvider = serviceProvider?.Trim();
        sr.Notes = notes;
        sr.MeterReading = meterReading;
        sr.RecurringRuleId = recurringRuleId;
        sr.OriginalPredictedDate = originalPredictedDate;

        if (status == ServiceStatus.Planned)
            sr.ApplyDefaultNotificationRules();

        return sr;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Update(
        string title,
        DateOnly serviceDate,
        ServiceStatus status = ServiceStatus.Completed,
        decimal? cost = null,
        string? serviceProvider = null,
        string? notes = null,
        decimal? meterReading = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        ServiceDate = serviceDate;
        Status = status;
        Cost = cost;
        ServiceProvider = serviceProvider?.Trim();
        Notes = notes;
        MeterReading = meterReading;
        Touch();
    }

    /// <summary>Moves a Planned record's date. OriginalPredictedDate is preserved as an audit trail.</summary>
    public void Reschedule(DateOnly newServiceDate)
    {
        OriginalPredictedDate ??= ServiceDate;
        ServiceDate = newServiceDate;
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
