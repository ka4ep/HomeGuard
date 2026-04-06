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
    DateTimeOffset UpdatedAt
);

public sealed record EquipmentDetail(
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
    IEnumerable<string>? Tags = null
);

public sealed record UpdateEquipmentDto(
    string Name,
    string Category,
    DateOnly PurchaseDate,
    string? Brand = null,
    string? Model = null,
    string? SerialNumber = null,
    decimal? PurchasePrice = null,
    string? Notes = null
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
    DateOnly? NextServiceDate,
    decimal? Cost,
    string? ServiceProvider,
    string? Notes,
    string? OdometerReading,
    bool IsOverdue,
    int? DaysUntilNextService,
    IReadOnlyList<NotificationRuleDto> NotificationRules,
    DateTimeOffset UpdatedAt
);

public sealed record CreateServiceRecordDto(
    Guid EquipmentId,
    string Title,
    DateOnly ServiceDate,
    DateOnly? NextServiceDate = null,
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    string? OdometerReading = null
);

public sealed record UpdateServiceRecordDto(
    string Title,
    DateOnly ServiceDate,
    DateOnly? NextServiceDate = null,
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    string? OdometerReading = null
);

// ── Shared ────────────────────────────────────────────────────────────────────

public sealed record NotificationRuleDto(
    string Offset,
    int OffsetDays,
    bool Enabled
);
