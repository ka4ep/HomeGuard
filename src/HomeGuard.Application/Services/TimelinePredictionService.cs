using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

/// <summary>One computed-ahead-of-time point: not stored, recomputed on every call.</summary>
public sealed record PredictedEvent(DateOnly Date, decimal? MeterReading);

/// <summary>
/// Computes future maintenance dates for a <see cref="RecurringRule"/> from its completed
/// service history. Nothing here is persisted — <see cref="RecurringRuleMaterializationService"/>
/// is what turns a near-term prediction into a real Planned <see cref="ServiceRecord"/>.
/// </summary>
public static class TimelinePredictionService
{
    private const int RecentGapsToAverage = 5;

    /// <summary>
    /// Predicts up to <see cref="RecurringRule.PredictionsAhead"/> future dates for
    /// <paramref name="rule"/>.
    /// </summary>
    /// <param name="rule">The recurring rule.</param>
    /// <param name="equipment">The rule's equipment (for PurchaseDate / MeterUnit).</param>
    /// <param name="ruleRecords">Completed ServiceRecords for this rule, any order.</param>
    /// <param name="equipmentMeterHistory">
    /// All of the equipment's completed records that have a MeterReading, any order — used as a
    /// fallback to estimate a predicted date's meter reading when this rule's own records don't
    /// give enough signal (e.g. usage rate is steadier across all maintenance types than within
    /// one narrow part's history). A future exact reading from an external telemetry service, if
    /// one is ever wired up, should simply be preferred by the caller over this estimate.
    /// </param>
    public static IReadOnlyList<PredictedEvent> Predict(
        RecurringRule rule,
        Equipment equipment,
        IReadOnlyList<ServiceRecord> ruleRecords,
        IReadOnlyList<ServiceRecord> equipmentMeterHistory)
    {
        var completed = ruleRecords
            .Where(r => r.Status == ServiceStatus.Completed)
            .OrderBy(r => r.ServiceDate)
            .ToList();

        // ── Days step: manual override wins, otherwise average the gaps between data points ──
        var dayPoints = new List<DateOnly>();
        if (rule.AnchorToPurchaseDate)
            dayPoints.Add(equipment.PurchaseDate);
        dayPoints.AddRange(completed.Select(r => r.ServiceDate));

        int dayStep;
        if (rule.IntervalDays.HasValue)
        {
            dayStep = rule.IntervalDays.Value;
        }
        else if (dayPoints.Count >= 2)
        {
            var gaps = dayPoints
                .Zip(dayPoints.Skip(1), (a, b) => b.DayNumber - a.DayNumber)
                .TakeLast(RecentGapsToAverage)
                .ToList();
            dayStep = (int)Math.Round(gaps.Average());
        }
        else
        {
            return []; // not enough signal yet to guess a cadence
        }

        if (dayStep <= 0) return [];

        // ── Meter step: manual override wins, otherwise average this rule's own deltas ──
        decimal? meterStep = rule.IntervalMeter;
        if (meterStep is null)
        {
            var meterPoints = completed
                .Where(r => r.MeterReading.HasValue)
                .Select(r => r.MeterReading!.Value)
                .ToList();
            if (meterPoints.Count >= 2)
            {
                var deltas = meterPoints.Zip(meterPoints.Skip(1), (a, b) => b - a).TakeLast(RecentGapsToAverage).ToList();
                if (deltas.All(d => d > 0))
                    meterStep = deltas.Average();
            }
        }

        var lastDate = dayPoints.Max();
        var lastMeter = completed.LastOrDefault(r => r.MeterReading.HasValue)?.MeterReading;

        var results = new List<PredictedEvent>(rule.PredictionsAhead);
        for (var i = 1; i <= rule.PredictionsAhead; i++)
        {
            var date = lastDate.AddDays(dayStep * i);

            decimal? meter = lastMeter.HasValue && meterStep.HasValue
                ? lastMeter + meterStep * i
                : EstimateMeterReading(equipmentMeterHistory, date);

            results.Add(new PredictedEvent(date, meter));
        }

        return results;
    }

    /// <summary>
    /// Rough linear extrapolation of an equipment's meter reading at <paramref name="targetDate"/>
    /// from its most recent readings across ALL its service records (not just one rule's) — a
    /// broader usage-rate sample is more stable than any single part's own history. This is
    /// intentionally approximate; an exact reading from an external service should take priority
    /// whenever one becomes available.
    /// </summary>
    public static decimal? EstimateMeterReading(IReadOnlyList<ServiceRecord> equipmentMeterHistory, DateOnly targetDate)
    {
        var points = equipmentMeterHistory
            .Where(r => r.Status == ServiceStatus.Completed && r.MeterReading.HasValue)
            .OrderBy(r => r.ServiceDate)
            .TakeLast(RecentGapsToAverage)
            .ToList();

        if (points.Count < 2) return null;

        var first = points[0];
        var last = points[^1];
        var daySpan = last.ServiceDate.DayNumber - first.ServiceDate.DayNumber;
        if (daySpan <= 0) return null;

        var ratePerDay = (last.MeterReading!.Value - first.MeterReading!.Value) / daySpan;
        if (ratePerDay < 0) return null;

        var daysAhead = targetDate.DayNumber - last.ServiceDate.DayNumber;
        var estimate = last.MeterReading.Value + ratePerDay * daysAhead;
        return estimate < 0 ? 0 : estimate;
    }
}
