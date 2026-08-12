namespace HomeGuard.Domain.Enums;

public enum EquipmentCategory
{
    Vehicle       = 1,
    HomeAppliance = 2,   // washing machine, dishwasher, fridge…
    Electronics   = 3,   // TV, laptop, phone…
    Heating       = 4,   // boiler, heat pump…
    Garden        = 5,
    Furniture     = 6,
    Other         = 99,
}

/// <summary>
/// How far in advance to send a notification relative to the target date.
/// Values are stored as integers in the database.
/// </summary>
public enum NotificationOffset
{
    SameDay         = 0,
    OneDayBefore    = 1,
    OneWeekBefore   = 7,
    OneMonthBefore  = 30,
    ThreeMonthsBefore = 90,
    SixMonthsBefore = 180,
}

public enum BlobSyncStatus
{
    /// <summary>File saved locally only; not yet uploaded to NextCloud.</summary>
    LocalOnly  = 0,
    /// <summary>File exists on both local disk and NextCloud.</summary>
    Synced     = 1,
    /// <summary>Upload attempted but failed. Will retry.</summary>
    SyncFailed = 2,
}

public enum JobStatus
{
    Pending   = 0,
    Running   = 1,
    Completed = 2,
    Failed    = 3,
}

/// <summary>
/// Lifecycle state of a stored <see cref="Entities.ServiceRecord"/>.
/// "Predicted" is not part of this enum — predicted events are computed at
/// runtime from <see cref="Entities.RecurringRule"/> and never persisted.
/// </summary>
public enum ServiceStatus
{
    Completed = 0,
    Planned   = 1,
}

/// <summary>
/// Where a <see cref="Entities.MeterReading"/> came from.
/// Service is reserved for readings derived from <see cref="Entities.ServiceRecord.MeterReading"/>
/// when the two sources are merged into one history — never stored on a MeterReading row.
/// </summary>
public enum MeterReadingSource
{
    Manual  = 0,
    Service = 1,
    Auto    = 2,
}

// ── Contracts, payment plans and payments ────────────────────────────────────

/// <summary>
/// What kind of agreement a <see cref="Entities.Contract"/> is. One aggregate covers
/// all four because they differ in vocabulary and in a few optional fields, not in shape:
/// each is a party, a period, and money on a schedule.
/// </summary>
public enum ContractKind
{
    Insurance    = 1,
    Subscription = 2,
    Loan         = 3,
    Lease        = 4,
    Other        = 99,
}

public enum ContractStatus
{
    Active    = 1,
    Ended     = 2,
    Cancelled = 3,
    Suspended = 4,
}

/// <summary>What happens when the contract reaches its end date.</summary>
public enum RenewalMode
{
    None   = 0,
    Auto   = 1,
    Manual = 2,
}

public enum PaymentKind
{
    Scheduled   = 0,
    Extra       = 1,
    DownPayment = 2,
    Residual    = 3,
    Fee         = 4,
    Refund      = 5,
}

/// <summary>
/// Mirrors the service-record lifecycle: Planned rows are real and feed the calendar,
/// Paid rows are history. "Projected" is not here — projections are computed from the
/// active plan revision and never stored.
/// </summary>
public enum PaymentStatus
{
    Planned = 0,
    Paid    = 1,
    Skipped = 2,
    Failed  = 3,
}

/// <summary>
/// Why a new <see cref="Entities.PaymentPlanRevision"/> was appended. The plan is never
/// edited in place, so this is the audit trail of every correction the household made.
/// </summary>
public enum RevisionReason
{
    Initial      = 0,
    PriceChange  = 1,
    EarlyPayment = 2,
    TermChange   = 3,
    RateChange   = 4,
    Pause        = 5,
    AddOn        = 6,
    Correction   = 99,
}
