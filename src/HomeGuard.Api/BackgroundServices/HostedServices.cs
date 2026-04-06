using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Application.Services;
using HomeGuard.Domain.Enums;
using System.Text.Json;

namespace HomeGuard.Api.BackgroundServices;

// ── Job runner ────────────────────────────────────────────────────────────────

/// <summary>
/// Polls the ScheduledJobs table every 60 seconds, picks up ready jobs
/// and dispatches them to registered handlers.
///
/// Each tick runs in a short-lived DI scope so services (DbContext, etc.)
/// are properly lifetime-managed.
/// </summary>
public sealed class JobRunnerService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobRunnerService> _logger;

    public JobRunnerService(IServiceScopeFactory scopeFactory, ILogger<JobRunnerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Job runner started.");

        // Stagger startup to let the app finish initialising.
        await Task.Delay(TimeSpan.FromSeconds(10), ct);

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await RunTickAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in job runner tick.");
            }
        }
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var jobs   = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();
        var uow    = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        var ready = await jobs.GetReadyJobsAsync(DateTimeOffset.UtcNow, limit: 20, ct);
        if (ready.Count == 0) return;

        _logger.LogDebug("Job runner: {Count} job(s) to process.", ready.Count);

        foreach (var job in ready)
        {
            job.MarkRunning();
            await uow.SaveChangesAsync(ct);

            try
            {
                await DispatchAsync(job.JobType, job.PayloadJson, sender, ct);
                job.MarkCompleted();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Job {JobId} ({Type}) failed.", job.Id, job.JobType);
                job.MarkFailed(ex.Message);
            }

            await uow.SaveChangesAsync(ct);
        }
    }

    private static async Task DispatchAsync(
        string jobType, string payloadJson,
        INotificationSender sender, CancellationToken ct)
    {
        switch (jobType)
        {
            case JobTypes.SendNotification:
            {
                var payload = JsonSerializer.Deserialize<NotificationJobPayload>(payloadJson)!;
                var daysRemaining = payload.TargetDate.DayNumber
                                    - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;

                var body = daysRemaining switch
                {
                    0        => $"{payload.EntityType} event is today: {payload.Title}",
                    1        => $"Tomorrow: {payload.Title}",
                    <= 7     => $"In {daysRemaining} days: {payload.Title}",
                    <= 31    => $"In ~{daysRemaining / 7} week(s): {payload.Title}",
                    _        => $"In ~{daysRemaining / 30} month(s): {payload.Title}",
                };

                var notification = new PushNotification(
                    Title: "HomeGuard reminder",
                    Body: body,
                    Url: $"/{payload.EntityType.ToLowerInvariant()}s/{payload.EntityId}",
                    Tag: $"hg-{payload.EntityType.ToLower()}-{payload.EntityId}"
                );

                await sender.SendToAllAsync(notification, ct);
                break;
            }

            default:
                throw new InvalidOperationException($"Unknown job type: '{jobType}'");
        }
    }
}

// ── Notification scheduler ────────────────────────────────────────────────────

/// <summary>
/// Runs the <see cref="NotificationSchedulerService"/> once per day at midnight UTC
/// to create upcoming notification jobs.
/// </summary>
public sealed class NotificationSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationSchedulerHostedService> _logger;

    public NotificationSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Run once immediately on startup, then every 24 hours.
        while (!ct.IsCancellationRequested)
        {
            await RunOnceAsync(ct);

            var nextMidnightUtc = DateTime.UtcNow.Date.AddDays(1);
            var delay = nextMidnightUtc - DateTime.UtcNow;
            await Task.Delay(delay, ct);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<NotificationSchedulerService>();
            await scheduler.RunAsync(ct);
            _logger.LogInformation("Notification scheduler completed.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification scheduler failed.");
        }
    }
}

// ── Blob sync ─────────────────────────────────────────────────────────────────

/// <summary>
/// Periodically syncs LocalOnly blobs to NextCloud.
/// Runs every 15 minutes — lightweight, handles the fallback scenario
/// when NextCloud was temporarily unavailable during upload.
/// </summary>
public sealed class BlobSyncHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BlobSyncHostedService> _logger;

    public BlobSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BlobSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct); // let the app settle

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await SyncPendingBlobsAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Blob sync tick failed.");
            }
        }
    }

    private async Task SyncPendingBlobsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var repo    = scope.ServiceProvider.GetRequiredService<IBlobEntryRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IBlobStorage>();
        var uow     = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pending = await repo.GetPendingSyncAsync(ct);
        if (pending.Count == 0) return;

        _logger.LogDebug("Blob sync: {Count} blob(s) pending.", pending.Count);

        foreach (var blob in pending)
        {
            var success = await storage.SyncToRemoteAsync(blob, ct);

            if (success)
                blob.MarkSynced(
                    $"{blob.OwnerEntityId}/{blob.FileName}");
            else
                blob.MarkSyncFailed();
        }

        await uow.SaveChangesAsync(ct);
    }
}
