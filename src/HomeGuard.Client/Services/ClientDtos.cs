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

// ── Meter readings ────────────────────────────────────────────────────────────

/// <summary>Source "Service" marks a reading derived from a completed service record (read-only here).</summary>
public sealed record MeterReadingDto(
    Guid Id,
    Guid EquipmentId,
    DateOnly ReadingDate,
    decimal Value,
    string Source,
    string? Note,
    DateTimeOffset UpdatedAt
);

public sealed record CreateMeterReadingDto(
    Guid EquipmentId,
    DateOnly ReadingDate,
    decimal Value,
    string Source = "Manual",
    string? Note = null
);

public sealed record UpdateMeterReadingDto(
    DateOnly ReadingDate,
    decimal Value,
    string? Note = null
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

// ── Contracts ─────────────────────────────────────────────────────────────────
// Enums travel as their integer values; the client mirrors them so a status never
// has to be compared as a string.

public enum ContractKind   { Insurance = 1, Subscription = 2, Loan = 3, Lease = 4, Other = 99 }
public enum ContractStatus { Active = 1, Ended = 2, Cancelled = 3, Suspended = 4 }
public enum RenewalMode    { None = 0, Auto = 1, Manual = 2 }
public enum PaymentKind    { Scheduled = 0, Extra = 1, DownPayment = 2, Residual = 3, Fee = 4, Refund = 5 }
public enum PaymentStatus  { Planned = 0, Paid = 1, Skipped = 2, Failed = 3 }
public enum RevisionReason { Initial = 0, PriceChange = 1, EarlyPayment = 2, TermChange = 3,
                             RateChange = 4, Pause = 5, AddOn = 6, Correction = 99 }
public enum ScheduleOrigin { Projected = 0, Stored = 1 }
public enum EarlyPaymentEffect { ReduceTerm = 0, ReducePayment = 1 }
public enum LoanEstimateGap    { None = 0, MissingRate = 1, MissingBalance = 2 }

public sealed record ContractDto(
    Guid Id,
    Guid? EquipmentId,
    ContractKind Kind,
    string Name,
    string? Provider,
    string? ContractNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? CancellationDeadline,
    RenewalMode Renewal,
    ContractStatus Status,
    string Currency,
    decimal? CurrentInstallment,
    int? IntervalMonths,
    IReadOnlyList<string> Tags
);

public sealed record OpeningPositionDto(
    DateOnly AsOfDate,
    int InstallmentsPaid,
    decimal AmountPaid,
    decimal? RemainingBalance
);

public sealed record PlanAdjustmentDto(string Name, decimal Amount);

public sealed record PlanRevisionDto(
    Guid Id,
    int Version,
    DateOnly EffectiveFrom,
    RevisionReason Reason,
    DateOnly FirstDueDate,
    int IntervalMonths,
    int? InstallmentCount,
    decimal InstallmentAmount,
    decimal EffectiveInstallment,
    decimal? RemainingPrincipal,
    decimal? AnnualInterestRate,
    decimal? ResidualAmount,
    DateOnly? ResidualDueDate,
    string? Note,
    IReadOnlyList<PlanAdjustmentDto> Adjustments
);

public sealed record PaymentDto(
    Guid Id,
    Guid ContractId,
    int? InstallmentNo,
    PaymentKind Kind,
    PaymentStatus Status,
    DateOnly DueDate,
    decimal AmountDue,
    DateOnly? PaidDate,
    decimal? AmountPaid,
    decimal? PrincipalPart,
    decimal? InterestPart,
    string? Note
);

public sealed record ContractDetailDto(
    ContractDto Contract,
    string? SummaryMarkdown,
    string? Notes,
    decimal? CoverageAmount,
    decimal? Deductible,
    OpeningPositionDto? Opening,
    IReadOnlyList<PlanRevisionDto> Revisions,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<NotificationRuleDto> NotificationRules
);

/// <summary>
/// One line of the merged schedule: a stored payment or a computed projection.
/// <c>PrincipalPart</c>/<c>InterestPart</c> are the actual split once paid, an estimate
/// from the governing revision's rate before that, and null when neither is available.
/// </summary>
public sealed record ScheduleEntryDto(
    ScheduleOrigin Origin,
    DateOnly DueDate,
    decimal Amount,
    PaymentStatus? Status,
    int? InstallmentNo,
    PaymentKind Kind,
    Guid? PaymentId,
    bool IsOverdue,
    decimal? PrincipalPart = null,
    decimal? InterestPart = null
);

public sealed record ContractSummaryDto(
    Guid ContractId,
    string Currency,
    decimal PaidToDate,
    int InstallmentsPaid,
    int? InstallmentsTotal,
    int? InstallmentsRemaining,
    decimal? RemainingBalance,
    decimal? CurrentInstallment,
    DateOnly? NextDueDate,
    decimal? NextDueAmount,
    int OverdueCount,
    decimal InterestPaidToDate,
    DateOnly? PayoffDate,
    LoanEstimateGap EstimateGap
);

public sealed record CreateContractDto(
    ContractKind Kind,
    string Name,
    DateOnly StartDate,
    string Currency,
    Guid? EquipmentId = null,
    string? Provider = null,
    string? ContractNumber = null,
    DateOnly? EndDate = null,
    RenewalMode Renewal = RenewalMode.None,
    int? CancellationNoticeDays = null,
    string? SummaryMarkdown = null,
    string? Notes = null,
    decimal? CoverageAmount = null,
    decimal? Deductible = null,
    IReadOnlyList<string>? Tags = null
);

public sealed record UpdateContractDto(
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate = null,
    string? Provider = null,
    string? ContractNumber = null,
    RenewalMode Renewal = RenewalMode.None,
    int? CancellationNoticeDays = null,
    string? SummaryMarkdown = null,
    string? Notes = null,
    decimal? CoverageAmount = null,
    decimal? Deductible = null,
    IReadOnlyList<string>? Tags = null
);

public sealed record SetOpeningDto(
    DateOnly AsOfDate,
    int InstallmentsPaid,
    decimal AmountPaid,
    decimal? RemainingBalance = null
);

public sealed record AddRevisionDto(
    DateOnly EffectiveFrom,
    RevisionReason Reason,
    DateOnly FirstDueDate,
    int IntervalMonths,
    decimal InstallmentAmount,
    int? InstallmentCount = null,
    decimal? RemainingPrincipal = null,
    decimal? AnnualInterestRate = null,
    decimal? ResidualAmount = null,
    DateOnly? ResidualDueDate = null,
    string? Note = null,
    IReadOnlyList<PlanAdjustmentDto>? Adjustments = null
);

public sealed record AddPaymentDto(
    DateOnly DueDate,
    decimal AmountDue,
    PaymentKind Kind = PaymentKind.Scheduled,
    int? InstallmentNo = null,
    string? Note = null
);

public sealed record UpcomingPaymentDto(
    Guid PaymentId,
    Guid ContractId,
    string ContractName,
    ContractKind Kind,
    string Currency,
    DateOnly DueDate,
    decimal AmountDue,
    bool IsOverdue
);

public sealed record UpdatePaymentDto(
    DateOnly DueDate,
    decimal AmountDue,
    PaymentKind Kind = PaymentKind.Scheduled,
    string? Note = null,
    bool Reopen = false
);

public sealed record ConfirmPaymentDto(
    DateOnly PaidDate,
    decimal? AmountPaid = null,
    string? Note = null
);

public sealed record EarlyPaymentPreviewRequestDto(
    decimal ExtraAmount,
    EarlyPaymentEffect Effect = EarlyPaymentEffect.ReduceTerm
);

/// <summary>
/// Before/after of paying <c>ExtraAmount</c> today. <c>InterestSaved</c> is null exactly
/// when <c>Gap</c> is not <see cref="LoanEstimateGap.None"/> — the term and payment numbers
/// are still real in that case, just without a rate-dependent figure next to them.
/// </summary>
public sealed record EarlyPaymentPreviewDto(
    LoanEstimateGap Gap,
    int InstallmentsBefore,
    int InstallmentsAfter,
    decimal InstallmentAmountBefore,
    decimal InstallmentAmountAfter,
    DateOnly? PayoffDateBefore,
    DateOnly? PayoffDateAfter,
    decimal? InterestSaved
);

// ── Finance rollup ───────────────────────────────────────────────────────────

public sealed record MonthlyLoadContributionDto(
    Guid ContractId, string ContractName, ContractKind Kind, decimal Amount);

public sealed record MonthlyLoadEntryDto(
    string Month,
    string Currency,
    decimal Total,
    IReadOnlyList<MonthlyLoadContributionDto> Contributions
);

// ── Attention ─────────────────────────────────────────────────────────────────

public enum AttentionSeverity { Soon = 0, Urgent = 1 }

public sealed record AttentionItemDto(
    string Kind, AttentionSeverity Severity, string Title, DateOnly Date, string Url);

public sealed record AttentionDto(
    int Count, int Urgent, int Soon, IReadOnlyList<AttentionItemDto> Items);
