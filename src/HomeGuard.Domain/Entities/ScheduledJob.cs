using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// A persisted background job that the <c>JobRunnerService</c> picks up and executes.
/// Survives server restarts. Retry logic with exponential backoff built in.
/// </summary>
public sealed class ScheduledJob : Entity
{
    private ScheduledJob() { }

    /// <summary>
    /// Discriminator for the handler, e.g. "SendWarrantyNotification", "SyncBlob", "SyncCalendar".
    /// The runner maps this to a concrete handler via DI.
    /// </summary>
    public string JobType { get; private set; } = null!;

    /// <summary>JSON-serialised parameters for the handler.</summary>
    public string PayloadJson { get; private set; } = null!;

    /// <summary>The job will not be picked up before this time.</summary>
    public DateTimeOffset RunAfter { get; private set; }

    public JobStatus Status { get; private set; } = JobStatus.Pending;

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>Last exception message, for diagnostics.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Optional deduplication key. Format is caller-defined, e.g. "notify:warranty:{id}:30d".
    /// The scheduler checks this before inserting to avoid duplicate notification jobs.
    /// </summary>
    public string? CorrelationKey { get; private set; }

    // ── Retry policy ─────────────────────────────────────────────────────────
    private const int MaxAttempts = 5;

    /// <summary>Base delay for exponential backoff: 1 min, 2 min, 4 min, 8 min, 16 min.</summary>
    private static TimeSpan BackoffDelay(int attempt) =>
        TimeSpan.FromMinutes(Math.Pow(2, attempt - 1));

    // ── Factory ──────────────────────────────────────────────────────────────

    public static ScheduledJob Create(
        string jobType,
        string payloadJson,
        DateTimeOffset? runAfter = null,
        string? correlationKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        var j = new ScheduledJob();
        j.InitNew();
        j.JobType = jobType;
        j.PayloadJson = payloadJson;
        j.RunAfter = runAfter ?? DateTimeOffset.UtcNow;
        j.CorrelationKey = correlationKey;
        return j;
    }

    // ── State transitions ─────────────────────────────────────────────────────

    public void MarkRunning()
    {
        Status = JobStatus.Running;
        AttemptCount++;
        LastAttemptAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkCompleted()
    {
        Status = JobStatus.Completed;
        LastError = null;
        Touch();
    }

    public void MarkFailed(string error)
    {
        LastError = error;
        LastAttemptAt = DateTimeOffset.UtcNow;

        if (AttemptCount >= MaxAttempts)
        {
            Status = JobStatus.Failed;
        }
        else
        {
            // Re-queue with backoff — back to Pending.
            Status = JobStatus.Pending;
            RunAfter = DateTimeOffset.UtcNow.Add(BackoffDelay(AttemptCount));
        }

        Touch();
    }

    public bool IsReadyToRun(DateTimeOffset now) =>
        Status == JobStatus.Pending && RunAfter <= now;
}
