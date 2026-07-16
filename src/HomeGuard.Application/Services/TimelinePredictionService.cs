using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

/// <summary>One computed-ahead-of-time point: not stored, recomputed on every call.</summary>
public sealed record PredictedEvent(DateOnly Date, decimal? MeterReading);

/// <summary>A dated meter value from any source (standalone reading or a service record's reading).</summary>
public readonly record struct MeterPoint(DateOnly Date, decimal Value);

/// <summary>
/// Computes future maintenance dates for a <see cref="RecurringRule"/> from its completed
/// service history and the equipment's meter history. Nothing here is persisted —
/// <see cref="RecurringRuleMaterializationService"/> is what turns a near-term prediction
/// into a real Planned <see cref="ServiceRecord"/>.
///
/// Semantics follow real-world service schedules — "every 12 months or 15 000 km,
/// whichever comes first": a calendar-based date and a meter-based date are computed
/// independently and the earlier one wins.
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
    /// <param name="meterHistory">
    /// The equipment's full usage history from ALL sources — standalone MeterReadings and
    /// completed service records' readings — any order. Drives the usage-rate estimate:
    /// a broader sample is more stable than any single rule's own history.
    /// </param>
    public static IReadOnlyList<PredictedEvent> Predict(
        RecurringRule rule,
        Equipment equipment,
        IReadOnlyList<ServiceRecord> ruleRecords,
        IReadOnlyList<MeterPoint> meterHistory)
    {
        var completed = ruleRecords
            .Where(r => r.Status == ServiceStatus.Completed)
            .OrderBy(r => r.ServiceDate)
            .ToList();

        var points = meterHistory.OrderBy(p => p.Date).ToList();

        // ── Days step: manual override wins, otherwise average the gaps between data points ──
        var dayPoints = new List<DateOnly>();
        if (rule.AnchorToPurchaseDate)
            dayPoints.Add(equipment.PurchaseDate);
        dayPoints.AddRange(completed.Select(r => r.ServiceDate));

        int? dayStep = null;
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
        if (dayStep <= 0) dayStep = null;

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
        if (meterStep <= 0) meterStep = null;

        // ── Usage rate: how fast the meter runs per day, from the full history ──
        var ratePerDay = EstimateRatePerDay(points);

        var lastDate = dayPoints.Count > 0 ? dayPoints.Max() : equipment.PurchaseDate;

        // Meter value at the last service of THIS rule — the anchor the "+ intervalMeter"
        // thresholds count from. Falls back to an estimate when the record carried no reading.
        var lastServiceMeter = completed.LastOrDefault(r => r.MeterReading.HasValue)?.MeterReading
            ?? EstimateMeterReading(points, lastDate);

        var latest = points.Count > 0 ? points[^1] : (MeterPoint?)null;

        var results = new List<PredictedEvent>(rule.PredictionsAhead);
        for (var i = 1; i <= rule.PredictionsAhead; i++)
        {
            DateOnly? dayDate = dayStep.HasValue ? lastDate.AddDays(dayStep.Value * i) : null;

            // Date the meter is expected to hit the i-th threshold, extrapolated from
            // the latest actual reading. Can land in the past — that reads as overdue.
            DateOnly? meterDate = null;
            decimal? target = null;
            if (meterStep.HasValue && lastServiceMeter.HasValue && ratePerDay > 0 && latest.HasValue)
            {
                target = lastServiceMeter.Value + meterStep.Value * i;
                var days = (double)((target.Value - latest.Value.Value) / ratePerDay.Value);
                meterDate = latest.Value.Date.AddDays((int)Math.Round(days));
            }

            if (dayDate is null && meterDate is null)
                return results; // not enough signal on either axis

            var meterWins = meterDate.HasValue && (dayDate is null || meterDate.Value < dayDate.Value);
            var date = meterWins ? meterDate!.Value : dayDate!.Value;

            var meter = meterWins
                ? target
                : EstimateMeterReading(points, date) ?? (lastServiceMeter + meterStep * i);

            results.Add(new PredictedEvent(date, meter));
        }

        return results;
    }

    /// <summary>
    /// Rough linear extrapolation of the meter value at <paramref name="targetDate"/> from the
    /// most recent points of the merged history. Intentionally approximate; an exact reading
    /// from an external telemetry service should take priority whenever one is available.
    /// </summary>
    public static decimal? EstimateMeterReading(IReadOnlyList<MeterPoint> meterHistory, DateOnly targetDate)
    {
        var points = meterHistory.OrderBy(p => p.Date).ToList();
        var rate = EstimateRatePerDay(points);
        if (rate is null) return null;

        var last = points[^1];
        var daysAhead = targetDate.DayNumber - last.Date.DayNumber;
        var estimate = last.Value + rate.Value * daysAhead;
        return estimate < 0 ? 0 : estimate;
    }

    /// <summary>Average meter units per day over the last few points; null when the history is too thin or inconsistent.</summary>
    private static decimal? EstimateRatePerDay(IReadOnlyList<MeterPoint> orderedPoints)
    {
        var points = orderedPoints.TakeLast(RecentGapsToAverage).ToList();
        if (points.Count < 2) return null;

        var first = points[0];
        var last = points[^1];
        var daySpan = last.Date.DayNumber - first.Date.DayNumber;
        if (daySpan <= 0) return null;

        var ratePerDay = (last.Value - first.Value) / daySpan;
        return ratePerDay < 0 ? null : ratePerDay;
    }
}
