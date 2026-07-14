using HomeGuard.Domain.Common;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// Defines a recurring maintenance pattern for a piece of equipment
/// (e.g. "Oil change every 9 months / 7,000 km" for a car).
///
/// Drives the prediction engine: <see cref="Title"/> is the canonical timeline
/// row key, and completed <see cref="ServiceRecord"/>s linked to this rule are
/// used to compute the average interval for future predictions.
/// </summary>
public sealed class RecurringRule : Entity
{
    private RecurringRule() { }

    // ── FK ──────────────────────────────────────────────────────────────────
    public Guid EquipmentId { get; private set; }

    // ── Core fields ─────────────────────────────────────────────────────────

    /// <summary>Canonical title — also used to group timeline rows, e.g. "Engine oil".</summary>
    public string Title { get; private set; } = null!;

    public int? IntervalDays { get; private set; }

    public decimal? IntervalMeter { get; private set; }

    /// <summary>How many days ahead of a predicted date to materialize a real Planned record.</summary>
    public int MaterializeDaysAhead { get; private set; } = 30;

    /// <summary>How many future predictions to show on the timeline.</summary>
    public int PredictionsAhead { get; private set; } = 2;

    /// <summary>
    /// When true (default), the item is assumed to have existed since <c>Equipment.PurchaseDate</c> —
    /// that date is used as a virtual first data point when averaging the interval, so a single
    /// completed record is already enough to predict the next one. Turn off for parts/items that
    /// were only added to the equipment later (their history should start at first mention, not purchase).
    /// </summary>
    public bool AnchorToPurchaseDate { get; private set; } = true;

    public bool IsActive { get; private set; } = true;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static RecurringRule Create(
        Guid equipmentId,
        string title,
        int? intervalDays = null,
        decimal? intervalMeter = null,
        int materializeDaysAhead = 30,
        int predictionsAhead = 2,
        bool anchorToPurchaseDate = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var rule = new RecurringRule();
        rule.InitNew();
        rule.EquipmentId = equipmentId;
        rule.Title = title.Trim();
        rule.IntervalDays = intervalDays;
        rule.IntervalMeter = intervalMeter;
        rule.MaterializeDaysAhead = materializeDaysAhead;
        rule.PredictionsAhead = predictionsAhead;
        rule.AnchorToPurchaseDate = anchorToPurchaseDate;
        rule.IsActive = true;
        return rule;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Update(
        string title,
        int? intervalDays,
        decimal? intervalMeter,
        int materializeDaysAhead,
        int predictionsAhead,
        bool anchorToPurchaseDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        IntervalDays = intervalDays;
        IntervalMeter = intervalMeter;
        MaterializeDaysAhead = materializeDaysAhead;
        PredictionsAhead = predictionsAhead;
        AnchorToPurchaseDate = anchorToPurchaseDate;
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }
}
