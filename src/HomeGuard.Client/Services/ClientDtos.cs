namespace HomeGuard.Client.Services;

// ── Equipment ─────────────────────────────────────────────────────────────────

public sealed record EquipmentSummary(
    Guid Id,
    string Name,
    string Category,
    DateOnly PurchaseDate,
    string? Brand,
    string? Model,
    IReadOnlyList<string> Tags,
    string? MeterUnit,
    DateTimeOffset UpdatedAt
);

public sealed record EquipmentDetailDto(
    Guid Id,
    string Name,
    string Category,
    DateOnly PurchaseDate,
    string? Brand,
    string? Model,
    string? SerialNumber,
    decimal? PurchasePrice,
    string? Notes,
    IReadOnlyList<string> Tags,
    string? MeterUnit,
    int WarrantyCount,
    int ServiceRecordCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateEquipmentDto(
    string Name,
    string Category,
    DateOnly PurchaseDate,
    string? Brand = null,
    string? Model = null,
    string? SerialNumber = null,
    decimal? PurchasePrice = null,
    string? Notes = null,
    IEnumerable<string>? Tags = null,
    string? MeterUnit = null
);

public sealed record UpdateEquipmentDto(
    string Name,
    string Category,
    DateOnly PurchaseDate,
    string? Brand = null,
    string? Model = null,
    string? SerialNumber = null,
    decimal? PurchasePrice = null,
    string? Notes = null,
    string? MeterUnit = null
);

// ── Warranty ──────────────────────────────────────────────────────────────────

public sealed record WarrantyDto(
    Guid Id,
    Guid EquipmentId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider,
    string? ContractNumber,
    string? Notes,
    bool IsActive,
    int DaysRemaining,
    IReadOnlyList<NotificationRuleDto> NotificationRules,
    DateTimeOffset UpdatedAt
);

public sealed record CreateWarrantyDto(
    Guid EquipmentId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider = null,
    string? ContractNumber = null,
    string? Notes = null
);

public sealed record UpdateWarrantyDto(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider = null,
    string? ContractNumber = null,
    string? Notes = null
);

// ── ServiceRecord ─────────────────────────────────────────────────────────────

public sealed record ServiceRecordDto(
    Guid Id,
    Guid EquipmentId,
    string Title,
    DateOnly ServiceDate,
    string Status,
    decimal? Cost,
    string? ServiceProvider,
    string? Notes,
    decimal? MeterReading,
    Guid? RecurringRuleId,
    bool IsOverdue,
    int? DaysUntilNextService,
    IReadOnlyList<NotificationRuleDto> NotificationRules,
    DateTimeOffset UpdatedAt
);

public sealed record CreateServiceRecordDto(
    Guid EquipmentId,
    string Title,
    DateOnly ServiceDate,
    string Status = "Completed",
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    decimal? MeterReading = null
);

public sealed record UpdateServiceRecordDto(
    string Title,
    DateOnly ServiceDate,
    string Status = "Completed",
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    decimal? MeterReading = null
);

// ── Recurring rules ───────────────────────────────────────────────────────────

public sealed record RecurringRuleDto(
    Guid Id,
    Guid EquipmentId,
    string Title,
    int? IntervalDays,
    decimal? IntervalMeter,
    int MaterializeDaysAhead,
    int PredictionsAhead,
    bool AnchorToPurchaseDate,
    bool IsActive,
    DateTimeOffset UpdatedAt
);

public sealed record CreateRecurringRuleDto(
    Guid EquipmentId,
    string Title,
    int? IntervalDays = null,
    decimal? IntervalMeter = null,
    int MaterializeDaysAhead = 30,
    int PredictionsAhead = 2,
    bool AnchorToPurchaseDate = true
);

public sealed record UpdateRecurringRuleDto(
    string Title,
    int? IntervalDays = null,
    decimal? IntervalMeter = null,
    int MaterializeDaysAhead = 30,
    int PredictionsAhead = 2,
    bool AnchorToPurchaseDate = true,
    bool IsActive = true
);

public sealed record PredictedEventDto(DateOnly Date, decimal? MeterReading);

public sealed record RecurringRuleWithPredictionsDto(
    RecurringRuleDto Rule,
    IReadOnlyList<PredictedEventDto> Predictions
);

// ── Shared ────────────────────────────────────────────────────────────────────

public sealed record NotificationRuleDto(
    string Offset,
    int OffsetDays,
    bool Enabled
);
