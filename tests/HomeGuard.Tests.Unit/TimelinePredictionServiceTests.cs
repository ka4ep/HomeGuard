using FluentAssertions;
using HomeGuard.Application.Services;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using Xunit;

namespace HomeGuard.Tests.Unit;

public sealed class TimelinePredictionServiceTests
{
    private static readonly DateOnly Purchase = new(2025, 1, 1);

    private static Equipment Car(string? meterUnit = "km")
        => Equipment.Create("Car", EquipmentCategory.Vehicle, Purchase, meterUnit: meterUnit);

    private static ServiceRecord Completed(
        Equipment eq, string title, DateOnly date, decimal? meter = null)
        => ServiceRecord.Create(eq.Id, title, date, ServiceStatus.Completed, meterReading: meter);

    // ── Calendar axis only ────────────────────────────────────────────────────

    [Fact]
    public void FixedIntervalDays_predicts_from_last_service_date()
    {
        var eq = Car(meterUnit: null);
        var rule = RecurringRule.Create(eq.Id, "Inspection", intervalDays: 365, predictionsAhead: 2);
        var records = new[] { Completed(eq, "Inspection", new DateOnly(2026, 1, 10)) };

        var result = TimelinePredictionService.Predict(rule, eq, records, []);

        result.Should().HaveCount(2);
        result[0].Date.Should().Be(new DateOnly(2027, 1, 10));
        result[1].Date.Should().Be(new DateOnly(2028, 1, 10));
    }

    [Fact]
    public void AveragedInterval_uses_purchase_date_anchor_with_single_record()
    {
        var eq = Car(meterUnit: null);
        var rule = RecurringRule.Create(eq.Id, "Filter", predictionsAhead: 1); // anchor default: true
        var records = new[] { Completed(eq, "Filter", Purchase.AddDays(200)) };

        var result = TimelinePredictionService.Predict(rule, eq, records, []);

        result.Should().ContainSingle()
            .Which.Date.Should().Be(Purchase.AddDays(400));
    }

    [Fact]
    public void No_signal_on_either_axis_returns_empty()
    {
        var eq = Car();
        var rule = RecurringRule.Create(eq.Id, "Oil", anchorToPurchaseDate: false);

        var result = TimelinePredictionService.Predict(rule, eq, [], []);

        result.Should().BeEmpty();
    }

    // ── Whichever comes first ─────────────────────────────────────────────────

    [Fact]
    public void Heavy_usage_pulls_meter_based_date_ahead_of_calendar_date()
    {
        // Rule: every 365 days or 10 000 km. Usage: 100 km/day → 10 000 km in 100 days.
        var eq = Car();
        var rule = RecurringRule.Create(eq.Id, "Oil change",
            intervalDays: 365, intervalMeter: 10_000, predictionsAhead: 1);

        var last = new DateOnly(2026, 1, 1);
        var records = new[] { Completed(eq, "Oil change", last, meter: 50_000) };
        var history = new[]
        {
            new MeterPoint(last, 50_000),
            new MeterPoint(last.AddDays(50), 55_000), // 100 km/day
        };

        var result = TimelinePredictionService.Predict(rule, eq, records, history);

        // Threshold 60 000 km reached ~50 days after the latest reading (55 000 km).
        result.Should().ContainSingle();
        result[0].Date.Should().Be(last.AddDays(100));
        result[0].MeterReading.Should().Be(60_000);
    }

    [Fact]
    public void Light_usage_falls_back_to_calendar_date()
    {
        // Same rule, but only 5 km/day → calendar hits first.
        var eq = Car();
        var rule = RecurringRule.Create(eq.Id, "Oil change",
            intervalDays: 365, intervalMeter: 10_000, predictionsAhead: 1);

        var last = new DateOnly(2026, 1, 1);
        var records = new[] { Completed(eq, "Oil change", last, meter: 50_000) };
        var history = new[]
        {
            new MeterPoint(last, 50_000),
            new MeterPoint(last.AddDays(100), 50_500), // 5 km/day
        };

        var result = TimelinePredictionService.Predict(rule, eq, records, history);

        result.Should().ContainSingle();
        result[0].Date.Should().Be(last.AddDays(365));
        // Reading estimated at the calendar date from the usage rate, not the threshold.
        result[0].MeterReading.Should().BeLessThan(60_000);
    }

    [Fact]
    public void Meter_axis_alone_predicts_without_any_day_signal()
    {
        // No IntervalDays, no anchor, single completed record → no calendar signal at all.
        var eq = Car();
        var rule = RecurringRule.Create(eq.Id, "Oil change",
            intervalMeter: 10_000, anchorToPurchaseDate: false, predictionsAhead: 1);

        var last = new DateOnly(2026, 1, 1);
        var records = new[] { Completed(eq, "Oil change", last, meter: 50_000) };
        var history = new[]
        {
            new MeterPoint(last, 50_000),
            new MeterPoint(last.AddDays(10), 51_000), // 100 km/day
        };

        var result = TimelinePredictionService.Predict(rule, eq, records, history);

        result.Should().ContainSingle();
        result[0].Date.Should().Be(last.AddDays(100)); // 10 000 km / 100 km/day
        result[0].MeterReading.Should().Be(60_000);
    }

    [Fact]
    public void Standalone_readings_sharpen_the_rate_between_services()
    {
        // Usage doubled after the last service; recent standalone readings should reflect that.
        var eq = Car();
        var rule = RecurringRule.Create(eq.Id, "Oil change",
            intervalDays: 365, intervalMeter: 10_000, predictionsAhead: 1);

        var last = new DateOnly(2026, 1, 1);
        var records = new[] { Completed(eq, "Oil change", last, meter: 50_000) };
        var history = new[]
        {
            new MeterPoint(last, 50_000),
            new MeterPoint(last.AddDays(10), 52_000),
            new MeterPoint(last.AddDays(20), 54_000), // 200 km/day
        };

        var result = TimelinePredictionService.Predict(rule, eq, records, history);

        // 60 000 km threshold: 6 000 km left after day 20 at 200 km/day → day 50.
        result.Should().ContainSingle();
        result[0].Date.Should().Be(last.AddDays(50));
    }

    // ── EstimateMeterReading ──────────────────────────────────────────────────

    [Fact]
    public void EstimateMeterReading_extrapolates_linearly()
    {
        var history = new[]
        {
            new MeterPoint(new DateOnly(2026, 1, 1), 1000),
            new MeterPoint(new DateOnly(2026, 1, 11), 1100), // 10/day
        };

        var estimate = TimelinePredictionService.EstimateMeterReading(
            history, new DateOnly(2026, 1, 21));

        estimate.Should().Be(1200);
    }

    [Fact]
    public void EstimateMeterReading_returns_null_when_history_is_thin()
    {
        var history = new[] { new MeterPoint(new DateOnly(2026, 1, 1), 1000) };

        TimelinePredictionService.EstimateMeterReading(history, new DateOnly(2026, 2, 1))
            .Should().BeNull();
    }
}
