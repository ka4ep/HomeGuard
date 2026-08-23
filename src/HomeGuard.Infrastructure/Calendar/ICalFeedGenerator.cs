using HomeGuard.Application.Interfaces.Repositories;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using ICalCalendar = Ical.Net.Calendar;

namespace HomeGuard.Infrastructure.Calendar;

/// <summary>
/// Generates a standard iCal (.ics) feed from upcoming warranty expirations
/// and next service dates. Family Wall and NextCloud subscribe to this URL
/// and poll it periodically.
///
/// The feed is read-only and stateless — regenerated on every request.
/// No Google Calendar API or CalDAV write credentials required.
/// </summary>
public sealed class ICalFeedGenerator
{
    private readonly IWarrantyRepository _warranties;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly IEquipmentRepository _equipment;
    private readonly IContractRepository _contracts;

    // How far ahead to include events in the feed.
    private static readonly int LookAheadMonths = 13;

    public ICalFeedGenerator(
        IWarrantyRepository warranties,
        IServiceRecordRepository serviceRecords,
        IEquipmentRepository equipment,
        IContractRepository contracts)
    {
        _warranties = warranties;
        _serviceRecords = serviceRecords;
        _equipment = equipment;
        _contracts = contracts;
    }

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var today   = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddMonths(LookAheadMonths);

        var calendar = new ICalCalendar();
        calendar.Properties.Add(new CalendarProperty("X-WR-CALNAME", "HomeGuard"));
        calendar.Properties.Add(new CalendarProperty("X-WR-CALDESC", "Warranties, service and contract reminders"));
        calendar.Properties.Add(new CalendarProperty("REFRESH-INTERVAL;VALUE=DURATION", "PT6H"));

        await AddWarrantyEventsAsync(calendar, today, horizon, ct);
        await AddServiceRecordEventsAsync(calendar, today, horizon, ct);
        await AddContractEventsAsync(calendar, today, horizon, ct);
        await AddPaymentEventsAsync(calendar, today, horizon, ct);

        return calendar.ToString() ?? string.Empty;
    }

    // ── Warranty events ───────────────────────────────────────────────────────

    private async Task AddWarrantyEventsAsync(
        ICalCalendar calendar, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var expiring = await _warranties.GetExpiringAsync(from, to, ct);

        foreach (var warranty in expiring)
        {
            var equipment = await _equipment.GetByIdAsync(warranty.EquipmentId, ct);
            var summary   = equipment is not null
                ? $"Warranty expires: {warranty.Name} ({equipment.Name})"
                : $"Warranty expires: {warranty.Name}";

            // All-day event: DtStart and DtEnd without time, IsAllDay is set by
            // constructing CalDateTime with date-only overload (no time component).
            var startDt = new CalDateTime(warranty.Period.End.Year,
                                          warranty.Period.End.Month,
                                          warranty.Period.End.Day);
            var endDt   = new CalDateTime(warranty.Period.End.Year,
                                          warranty.Period.End.Month,
                                          warranty.Period.End.Day);
            endDt.AddDays(1);

            var evt = new CalendarEvent
            {
                Uid          = $"warranty-{warranty.Id}@homeguard",
                Summary      = summary,
                Description  = BuildWarrantyDescription(warranty),
                DtStart      = startDt,
                DtEnd        = endDt,
                LastModified = new CalDateTime(warranty.UpdatedAt.UtcDateTime),
            };

            evt.Properties.Add(new CalendarProperty("X-HOMEGUARD-TAG", $"warranty:{warranty.Id}"));
            calendar.Events.Add(evt);
        }
    }

    // ── Service record events ─────────────────────────────────────────────────

    private async Task AddServiceRecordEventsAsync(
        ICalCalendar calendar, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var due = await _serviceRecords.GetDueSoonAsync(from, (to.DayNumber - from.DayNumber), ct);

        foreach (var record in due)
        {
            var equipment = await _equipment.GetByIdAsync(record.EquipmentId, ct);
            var summary   = equipment is not null
                ? $"Service due: {record.Title} ({equipment.Name})"
                : $"Service due: {record.Title}";

            var nsd     = record.ServiceDate;
            var startDt = new CalDateTime(nsd.Year, nsd.Month, nsd.Day);
            var endDt   = new CalDateTime(nsd.Year, nsd.Month, nsd.Day);
            endDt.AddDays(1);

            var evt = new CalendarEvent
            {
                Uid          = $"service-next-{record.Id}@homeguard",
                Summary      = summary,
                Description  = BuildServiceDescription(record),
                DtStart      = startDt,
                DtEnd        = endDt,
                LastModified = new CalDateTime(record.UpdatedAt.UtcDateTime),
            };

            evt.Properties.Add(new CalendarProperty("X-HOMEGUARD-TAG", $"service:{record.Id}"));
            calendar.Events.Add(evt);
        }
    }

    // ── Contract events ────────────────────────────────────────────────────────

    /// <summary>
    /// One event per contract in the window: on <see cref="Domain.Entities.Contract.CancellationDeadline"/>
    /// when there is one — the date that actually costs money to miss — otherwise on
    /// <see cref="Domain.Entities.Contract.EndDate"/> itself.
    /// </summary>
    private async Task AddContractEventsAsync(
        ICalCalendar calendar, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var expiring = await _contracts.GetExpiringAsync(from, to, ct);

        foreach (var contract in expiring)
        {
            var watched = contract.CancellationDeadline ?? contract.EndDate;
            if (watched is not { } date) continue;

            var summary = contract.CancellationDeadline is not null
                ? $"Cancel by: {contract.Name}"
                : $"Contract ends: {contract.Name}";

            var startDt = new CalDateTime(date.Year, date.Month, date.Day);
            var endDt   = new CalDateTime(date.Year, date.Month, date.Day);
            endDt.AddDays(1);

            var evt = new CalendarEvent
            {
                Uid          = $"contract-{contract.Id}@homeguard",
                Summary      = summary,
                Description  = BuildContractDescription(contract),
                DtStart      = startDt,
                DtEnd        = endDt,
                LastModified = new CalDateTime(contract.UpdatedAt.UtcDateTime),
            };

            evt.Properties.Add(new CalendarProperty("X-HOMEGUARD-TAG", $"contract:{contract.Id}"));
            calendar.Events.Add(evt);
        }
    }

    // ── Payment events ─────────────────────────────────────────────────────────

    /// <summary>
    /// One event per Planned payment. Only rows already materialized appear here — the
    /// same characteristic <see cref="AddServiceRecordEventsAsync"/> already has for
    /// predicted-but-not-yet-Planned service records, not a new limitation.
    /// </summary>
    private async Task AddPaymentEventsAsync(
        ICalCalendar calendar, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var due = await _contracts.GetPaymentsDueWithContractAsync(from, to, ct);

        foreach (var (payment, contract) in due)
        {
            var summary = $"Payment due: {contract.Name} ({payment.AmountDue:F2} {contract.Currency})";

            var startDt = new CalDateTime(payment.DueDate.Year, payment.DueDate.Month, payment.DueDate.Day);
            var endDt   = new CalDateTime(payment.DueDate.Year, payment.DueDate.Month, payment.DueDate.Day);
            endDt.AddDays(1);

            var evt = new CalendarEvent
            {
                Uid          = $"payment-{payment.Id}@homeguard",
                Summary      = summary,
                Description  = BuildPaymentDescription(payment, contract),
                DtStart      = startDt,
                DtEnd        = endDt,
                LastModified = new CalDateTime(payment.UpdatedAt.UtcDateTime),
            };

            evt.Properties.Add(new CalendarProperty("X-HOMEGUARD-TAG", $"payment:{payment.Id}"));
            calendar.Events.Add(evt);
        }
    }

    // ── Description builders ──────────────────────────────────────────────────

    private static string BuildWarrantyDescription(Domain.Entities.Warranty w)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(w.Provider))
            parts.Add($"Provider: {w.Provider}");
        if (!string.IsNullOrWhiteSpace(w.ContractNumber))
            parts.Add($"Contract: {w.ContractNumber}");
        if (!string.IsNullOrWhiteSpace(w.Notes))
            parts.Add(w.Notes);
        return string.Join("\n", parts);
    }

    private static string BuildServiceDescription(Domain.Entities.ServiceRecord sr)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sr.ServiceProvider))
            parts.Add($"Provider: {sr.ServiceProvider}");
        if (sr.Cost.HasValue)
            parts.Add($"Cost: {sr.Cost:F2}");
        if (sr.MeterReading.HasValue)
            parts.Add($"Meter reading: {sr.MeterReading}");
        if (!string.IsNullOrWhiteSpace(sr.Notes))
            parts.Add(sr.Notes);
        return string.Join("\n", parts);
    }

    private static string BuildContractDescription(Domain.Entities.Contract c)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.Provider))
            parts.Add($"Provider: {c.Provider}");
        if (!string.IsNullOrWhiteSpace(c.ContractNumber))
            parts.Add($"Contract: {c.ContractNumber}");
        if (c.CancellationNoticeDays is { } notice)
            parts.Add($"Notice period: {notice} days");
        return string.Join("\n", parts);
    }

    private static string BuildPaymentDescription(Domain.Entities.Payment p, Domain.Entities.Contract c)
    {
        var parts = new List<string> { $"Kind: {p.Kind}" };
        if (!string.IsNullOrWhiteSpace(c.ContractNumber))
            parts.Add($"Contract: {c.ContractNumber}");
        if (!string.IsNullOrWhiteSpace(p.Note))
            parts.Add(p.Note);
        return string.Join("\n", parts);
    }
}
