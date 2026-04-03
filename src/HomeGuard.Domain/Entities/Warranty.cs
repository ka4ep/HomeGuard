using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;
using HomeGuard.Domain.ValueObjects;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// A warranty period associated with a piece of equipment.
/// One equipment item can have multiple warranties
/// (manufacturer + extended, or sequential renewals).
/// </summary>
public sealed class Warranty : Entity
{
    private Warranty() { }

    // ── FK ──────────────────────────────────────────────────────────────────
    public Guid EquipmentId { get; private set; }

    // ── Core fields ─────────────────────────────────────────────────────────

    /// <summary>e.g. "Manufacturer warranty" or "Extended warranty – Ergo Insurance".</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Inclusive date range of the warranty coverage.</summary>
    public DateRange Period { get; private set; } = null!;

    /// <summary>Who provides the warranty: manufacturer, insurance company, retailer…</summary>
    public string? Provider { get; private set; }

    /// <summary>Policy / contract number for quick reference.</summary>
    public string? ContractNumber { get; private set; }

    /// <summary>Freeform notes in Markdown: conditions, exclusions, hotline numbers.</summary>
    public string? Notes { get; private set; }

    // ── Calendar sync ────────────────────────────────────────────────────────
    /// <summary>
    /// Event ID on the synced Google Calendar, if the warranty end date
    /// has been written there. Null until first sync.
    /// </summary>
    public string? GoogleCalendarEventId { get; private set; }

    // ── Notification rules ───────────────────────────────────────────────────
    private readonly List<NotificationRule> _notificationRules = [];

    /// <summary>
    /// When to send push notifications before <see cref="Period"/>.End.
    /// Default: 6 months, 1 month, 1 week, same day.
    /// </summary>
    public IReadOnlyList<NotificationRule> NotificationRules => _notificationRules.AsReadOnly();

    // ── Navigation ───────────────────────────────────────────────────────────
    private readonly List<BlobEntry> _attachments = [];
    /// <summary>Scanned warranty cards, receipts, insurance documents.</summary>
    public IReadOnlyList<BlobEntry> Attachments => _attachments.AsReadOnly();

    // ── Computed ─────────────────────────────────────────────────────────────
    public bool IsActive(DateOnly today) => !Period.IsExpired(today);
    public bool IsExpired(DateOnly today) => Period.IsExpired(today);
    public int DaysRemaining(DateOnly today) => Period.DaysUntilEnd(today);

    // ── Factory ──────────────────────────────────────────────────────────────

    public static Warranty Create(
        Guid equipmentId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string? provider = null,
        string? contractNumber = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var w = new Warranty();
        w.InitNew();
        w.EquipmentId = equipmentId;
        w.Name = name.Trim();
        w.Period = new DateRange(startDate, endDate);
        w.Provider = provider?.Trim();
        w.ContractNumber = contractNumber?.Trim();
        w.Notes = notes;
        w.ApplyDefaultNotificationRules();
        return w;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Update(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string? provider = null,
        string? contractNumber = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Period = new DateRange(startDate, endDate);
        Provider = provider?.Trim();
        ContractNumber = contractNumber?.Trim();
        Notes = notes;
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
            NotificationRule.Create(NotificationOffset.SixMonthsBefore),
            NotificationRule.Create(NotificationOffset.OneMonthBefore),
            NotificationRule.Create(NotificationOffset.OneWeekBefore),
            NotificationRule.Create(NotificationOffset.SameDay),
        ]);
    }
}
