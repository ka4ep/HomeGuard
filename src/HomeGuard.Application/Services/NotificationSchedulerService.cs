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
    private readonly IWarrantyRepository _warranties;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly IScheduledJobRepository _jobs;
    private readonly IUnitOfWork _uow;

    public NotificationSchedulerService(
        IWarrantyRepository warranties,
        IServiceRecordRepository serviceRecords,
        IScheduledJobRepository jobs,
        IUnitOfWork uow)
    {
        _warranties = warranties;
        _serviceRecords = serviceRecords;
        _jobs = jobs;
        _uow = uow;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await ScheduleWarrantyNotificationsAsync(today, ct);
        await ScheduleServiceRecordNotificationsAsync(today, ct);

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
            if (record.NextServiceDate is null) continue;

            foreach (var rule in record.NotificationRules.Where(r => r.IsEnabled))
            {
                var fireDate = rule.FireDate(record.NextServiceDate.Value);

                if (fireDate < today) continue;

                var correlationKey = NotificationCorrelationKey("service", record.Id, rule.Offset);

                if (await _jobs.ExistsPendingAsync(correlationKey, ct)) continue;

                var payload = new NotificationJobPayload(
                    EntityId: record.Id,
                    EntityType: "ServiceRecord",
                    Title: record.Title,
                    TargetDate: record.NextServiceDate.Value,
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
