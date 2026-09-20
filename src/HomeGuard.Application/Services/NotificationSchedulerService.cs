using System.Text.Json;
using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

/// <summary>
/// Scans active warranties and service records, then creates <see cref="ScheduledJob"/> entries
/// for each enabled <c>NotificationRule</c> whose fire date is still in the future.
///
/// Designed to run once a day (via <c>IHostedService</c> timer in the Api project).
/// Idempotent: checks <see cref="IScheduledJobRepository.ExistsPendingAsync"/> before inserting.
/// </summary>
public sealed class NotificationSchedulerService
{
    /// <summary>
    /// Payment reminders are not user-configurable per row like warranties and service
    /// records — nobody wants to set offsets on individual instalments. Fixed defaults
    /// from contracts-spec.md §9.
    /// </summary>
    private static readonly NotificationOffset[] PaymentDefaultOffsets =
        [NotificationOffset.OneWeekBefore, NotificationOffset.OneDayBefore, NotificationOffset.SameDay];

    private readonly IWarrantyRepository _warranties;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly IContractRepository _contracts;
    private readonly IScheduledJobRepository _jobs;
    private readonly IUnitOfWork _uow;

    public NotificationSchedulerService(
        IWarrantyRepository warranties,
        IServiceRecordRepository serviceRecords,
        IContractRepository contracts,
        IScheduledJobRepository jobs,
        IUnitOfWork uow)
    {
        _warranties = warranties;
        _serviceRecords = serviceRecords;
        _contracts = contracts;
        _jobs = jobs;
        _uow = uow;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await ScheduleWarrantyNotificationsAsync(today, ct);
        await ScheduleServiceRecordNotificationsAsync(today, ct);
        await ScheduleContractNotificationsAsync(today, ct);
        await SchedulePaymentNotificationsAsync(today, ct);

        await _uow.SaveChangesAsync(ct);
    }

    // ── Warranties ────────────────────────────────────────────────────────────

    private async Task ScheduleWarrantyNotificationsAsync(DateOnly today, CancellationToken ct)
    {
        // Look ahead 7 months to cover the "6 months before" rule with some buffer.
        var lookAheadEnd = today.AddMonths(7);
        var expiring = await _warranties.GetExpiringAsync(today, lookAheadEnd, ct);

        foreach (var warranty in expiring)
        {
            foreach (var rule in warranty.NotificationRules.Where(r => r.IsEnabled))
            {
                var fireDate = rule.FireDate(warranty.Period.End);

                // Skip if the fire date has already passed.
                if (fireDate < today) continue;

                var correlationKey = NotificationCorrelationKey("warranty", warranty.Id, rule.Offset);

                if (await _jobs.ExistsPendingAsync(correlationKey, ct)) continue;

                var payload = new NotificationJobPayload(
                    EntityId: warranty.Id,
                    EntityType: "Warranty",
                    Title: warranty.Name,
                    TargetDate: warranty.Period.End,
                    Offset: rule.Offset
                );

                var job = ScheduledJob.Create(
                    jobType: JobTypes.SendNotification,
                    payloadJson: JsonSerializer.Serialize(payload),
                    runAfter: fireDate.ToDateTimeOffset(),
                    correlationKey: correlationKey
                );

                await _jobs.AddAsync(job, ct);
            }
        }
    }

    // ── Service records ───────────────────────────────────────────────────────

    private async Task ScheduleServiceRecordNotificationsAsync(DateOnly today, CancellationToken ct)
    {
        var dueSoon = await _serviceRecords.GetDueSoonAsync(today, withinDays: 200, ct);

        foreach (var record in dueSoon)
        {
            foreach (var rule in record.NotificationRules.Where(r => r.IsEnabled))
            {
                var fireDate = rule.FireDate(record.ServiceDate);

                if (fireDate < today) continue;

                var correlationKey = NotificationCorrelationKey("service", record.Id, rule.Offset);

                if (await _jobs.ExistsPendingAsync(correlationKey, ct)) continue;

                var payload = new NotificationJobPayload(
                    EntityId: record.Id,
                    EntityType: "ServiceRecord",
                    Title: record.Title,
                    TargetDate: record.ServiceDate,
                    Offset: rule.Offset
                );

                var job = ScheduledJob.Create(
                    jobType: JobTypes.SendNotification,
                    payloadJson: JsonSerializer.Serialize(payload),
                    runAfter: fireDate.ToDateTimeOffset(),
                    correlationKey: correlationKey
                );

                await _jobs.AddAsync(job, ct);
            }
        }
    }

    // ── Contracts ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Two independent reminders per contract: the ordinary offset-based ones on
    /// <see cref="Domain.Entities.Contract.NotificationRules"/> (the household's own
    /// choice, same mechanism as warranties), plus one fixed reminder a week before the
    /// cancellation window actually closes (contracts-spec.md §6) — that date is what
    /// costs real money to miss, and it is not the same as the end date, so it needs its
    /// own rule rather than relying on the household to have configured one that happens
    /// to land there.
    /// </summary>
    private async Task ScheduleContractNotificationsAsync(DateOnly today, CancellationToken ct)
    {
        var lookAheadEnd = today.AddMonths(7);
        var expiring = await _contracts.GetExpiringAsync(today, lookAheadEnd, ct);

        foreach (var contract in expiring)
        {
            if (contract.EndDate is { } end)
            {
                foreach (var rule in contract.NotificationRules.Where(r => r.IsEnabled))
                {
                    var fireDate = rule.FireDate(end);
                    if (fireDate < today) continue;

                    var correlationKey = NotificationCorrelationKey("contract", contract.Id, rule.Offset);
                    if (await _jobs.ExistsPendingAsync(correlationKey, ct)) continue;

                    var payload = new NotificationJobPayload(
                        EntityId: contract.Id,
                        EntityType: "Contract",
                        Title: contract.Name,
                        TargetDate: end,
                        Offset: rule.Offset
                    );

                    await _jobs.AddAsync(ScheduledJob.Create(
                        JobTypes.SendNotification, JsonSerializer.Serialize(payload),
                        fireDate.ToDateTimeOffset(), correlationKey), ct);
                }
            }

            if (contract.CancellationDeadline is { } deadline)
            {
                var fireDate = deadline.AddDays(-7);
                if (fireDate < today) continue;

                var correlationKey = $"notify:contract-cancel:{contract.Id}";
                if (await _jobs.ExistsPendingAsync(correlationKey, ct)) continue;

                var payload = new NotificationJobPayload(
                    EntityId: contract.Id,
                    EntityType: "Contract",
                    Title: $"{contract.Name} — cancellation window closing",
                    TargetDate: deadline,
                    Offset: NotificationOffset.OneWeekBefore
                );

                await _jobs.AddAsync(ScheduledJob.Create(
                    JobTypes.SendNotification, JsonSerializer.Serialize(payload),
                    fireDate.ToDateTimeOffset(), correlationKey), ct);
            }
        }
    }

    // ── Payments ──────────────────────────────────────────────────────────────

    private async Task SchedulePaymentNotificationsAsync(DateOnly today, CancellationToken ct)
    {
        // Planned rows only exist up to 14 days out (PaymentMaterializationService) —
        // this window just needs to comfortably cover that plus the widest offset.
        var due = await _contracts.GetPaymentsDueWithContractAsync(today, today.AddDays(21), ct);

        foreach (var (payment, contract) in due)
        {
            foreach (var offset in PaymentDefaultOffsets)
            {
                var fireDate = payment.DueDate.AddDays(-(int)offset);
                if (fireDate < today) continue;

                var correlationKey = NotificationCorrelationKey("payment", payment.Id, offset);
                if (await _jobs.ExistsPendingAsync(correlationKey, ct)) continue;

                // EntityType is "Contract" (not "Payment", which has no page of its own)
                // so the link opens the schedule the payment actually lives on.
                var payload = new NotificationJobPayload(
                    EntityId: contract.Id,
                    EntityType: "Contract",
                    Title: $"{contract.Name} — payment due",
                    TargetDate: payment.DueDate,
                    Offset: offset
                );

                await _jobs.AddAsync(ScheduledJob.Create(
                    JobTypes.SendNotification, JsonSerializer.Serialize(payload),
                    fireDate.ToDateTimeOffset(), correlationKey), ct);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NotificationCorrelationKey(
        string type, Guid entityId, NotificationOffset offset)
        => $"notify:{type}:{entityId}:{(int)offset}d";
}

// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>
/// Payload stored in <see cref="ScheduledJob.PayloadJson"/> for SendNotification jobs.
/// </summary>
public sealed record NotificationJobPayload(
    Guid EntityId,
    string EntityType,
    string Title,
    DateOnly TargetDate,
    NotificationOffset Offset
);

/// <summary>Well-known job type strings. The runner maps these to handler classes.</summary>
public static class JobTypes
{
    public const string SendNotification = "SendNotification";
    public const string SyncCalendar     = "SyncCalendar";
    public const string SyncBlob         = "SyncBlob";
}

/// <summary>Extension used by the scheduler to convert a fire date to a UTC DateTimeOffset.</summary>
file static class DateOnlyExtensions
{
    /// <summary>Treats the date as UTC midnight.</summary>
    internal static DateTimeOffset ToDateTimeOffset(this DateOnly date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
}
