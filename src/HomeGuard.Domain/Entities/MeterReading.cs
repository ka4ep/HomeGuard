using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// A standalone meter/odometer reading for a piece of equipment, independent of any
/// service event: "the car showed 42 150 km on 12 July". Value is in the owning
/// Equipment's MeterUnit.
///
/// Readings recorded as part of a service stay on <see cref="ServiceRecord.MeterReading"/>;
/// consumers that need the full usage history (e.g. the prediction engine) merge both sources.
/// </summary>
public sealed class MeterReading : Entity
{
    private MeterReading() { }

    // ── FK ──────────────────────────────────────────────────────────────────
    public Guid EquipmentId { get; private set; }

    // ── Core fields ─────────────────────────────────────────────────────────

    public DateOnly ReadingDate { get; private set; }

    /// <summary>The reading itself, in the owning Equipment's MeterUnit.</summary>
    public decimal Value { get; private set; }

    /// <summary>Manual entry, or pushed by an external ingestor (telemetry, Data Act export…).</summary>
    public MeterReadingSource Source { get; private set; }

    public string? Note { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────

    public static MeterReading Create(
        Guid equipmentId,
        DateOnly readingDate,
        decimal value,
        MeterReadingSource source = MeterReadingSource.Manual,
        string? note = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        var r = new MeterReading();
        r.InitNew();
        r.EquipmentId = equipmentId;
        r.ReadingDate = readingDate;
        r.Value = value;
        r.Source = source;
        r.Note = note?.Trim();
        return r;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Update(DateOnly readingDate, decimal value, string? note)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ReadingDate = readingDate;
        Value = value;
        Note = note?.Trim();
        Touch();
    }
}
