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
