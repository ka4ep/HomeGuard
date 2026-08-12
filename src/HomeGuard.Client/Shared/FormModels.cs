using HomeGuard.Client.Services;

namespace HomeGuard.Client.Common;

// ── Equipment ─────────────────────────────────────────────────────────────────

public sealed class EquipmentFormModel
{
    public string   Name          { get; set; } = string.Empty;
    public string   Category      { get; set; } = "Other";
    public string?  Brand         { get; set; }
    public string?  Model         { get; set; }
    public string?  SerialNumber  { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string   Tags          { get; set; } = string.Empty;
    public string?  Notes         { get; set; }
    public string?  MeterUnit     { get; set; }

    // MudDatePicker binds to DateTime? — we convert on save.
    public DateTime? PurchaseDateNullable { get; set; } = DateTime.Today;

    public DateOnly PurchaseDate
        => PurchaseDateNullable.HasValue
            ? DateOnly.FromDateTime(PurchaseDateNullable.Value)
            : DateOnly.FromDateTime(DateTime.Today);
}

// ── Warranty ──────────────────────────────────────────────────────────────────

public class WarrantyFormModel
{
    public string  Name           { get; set; } = string.Empty;
    public string? Provider       { get; set; }
    public string? ContractNumber { get; set; }
    public string? Notes          { get; set; }

    public DateTime? StartDateNullable { get; set; } = DateTime.Today;
    public DateTime? EndDateNullable   { get; set; } = DateTime.Today.AddYears(2);

    public DateOnly StartDate
        => StartDateNullable.HasValue ? DateOnly.FromDateTime(StartDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today);

    public DateOnly EndDate
        => EndDateNullable.HasValue ? DateOnly.FromDateTime(EndDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today.AddYears(2));
}

// ── ServiceRecord ─────────────────────────────────────────────────────────────

public class ServiceRecordFormModel
{
    public string   Title           { get; set; } = string.Empty;
    public string?  ServiceProvider { get; set; }
    public decimal? Cost            { get; set; }
    public decimal? MeterReading    { get; set; }
    public string?  Notes           { get; set; }
    public string   Status          { get; set; } = "Completed";

    public DateTime? ServiceDateNullable { get; set; } = DateTime.Today;

    public DateOnly ServiceDate
        => ServiceDateNullable.HasValue ? DateOnly.FromDateTime(ServiceDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today);
}

// ── RecurringRule ────────────────────────────────────────────────────────────

public class RecurringRuleFormModel
{
    public string   Title                { get; set; } = string.Empty;
    public int?     IntervalDays         { get; set; }
    public decimal? IntervalMeter        { get; set; }
    public int      MaterializeDaysAhead { get; set; } = 30;
    public int      PredictionsAhead     { get; set; } = 2;
    public bool     AnchorToPurchaseDate { get; set; } = true;
    public bool     IsActive             { get; set; } = true;
}

// ── Contract ──────────────────────────────────────────────────────────────────

public sealed class ContractFormModel
{
    public ContractKind Kind           { get; set; } = ContractKind.Subscription;
    public string       Name           { get; set; } = string.Empty;
    public string?      Provider       { get; set; }
    public string?      ContractNumber { get; set; }
    public string       Currency       { get; set; } = "EUR";
    public Guid?        EquipmentId    { get; set; }
    public RenewalMode  Renewal        { get; set; } = RenewalMode.None;
    public int?         CancellationNoticeDays { get; set; }
    public string?      SummaryMarkdown { get; set; }
    public string?      Notes          { get; set; }
    public decimal?     CoverageAmount { get; set; }
    public decimal?     Deductible     { get; set; }
    public string       Tags           { get; set; } = string.Empty;

    public DateTime?    StartDateNullable { get; set; } = DateTime.Today;
    public DateTime?    EndDateNullable   { get; set; }

    /// <summary>
    /// The first plan revision, entered on the same screen as the contract. Splitting it
    /// into a second dialog would mean a contract can exist with no idea what it costs.
    /// </summary>
    public bool     HasPlan           { get; set; } = true;
    public decimal? InstallmentAmount { get; set; }
    public int      IntervalMonths    { get; set; } = 1;
    public int?     InstallmentCount  { get; set; }
    public DateTime? FirstDueDateNullable { get; set; } = DateTime.Today;

    public DateOnly StartDate
        => StartDateNullable is { } d ? DateOnly.FromDateTime(d) : DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? EndDate
        => EndDateNullable is { } d ? DateOnly.FromDateTime(d) : null;

    public DateOnly FirstDueDate
        => FirstDueDateNullable is { } d ? DateOnly.FromDateTime(d) : StartDate;

    public IReadOnlyList<string> TagList
        => Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed class OpeningFormModel
{
    public DateTime? AsOfDateNullable { get; set; } = DateTime.Today;
    public int       InstallmentsPaid { get; set; }
    public decimal   AmountPaid       { get; set; }
    public decimal?  RemainingBalance { get; set; }

    public DateOnly AsOfDate
        => AsOfDateNullable is { } d ? DateOnly.FromDateTime(d) : DateOnly.FromDateTime(DateTime.Today);
}

public sealed class RevisionFormModel
{
    public RevisionReason Reason         { get; set; } = RevisionReason.PriceChange;
    public decimal?       InstallmentAmount { get; set; }
    public int            IntervalMonths { get; set; } = 1;
    public int?           InstallmentCount { get; set; }
    public string?        Note           { get; set; }

    public DateTime? EffectiveFromNullable { get; set; } = DateTime.Today;

    public DateOnly EffectiveFrom
        => EffectiveFromNullable is { } d ? DateOnly.FromDateTime(d) : DateOnly.FromDateTime(DateTime.Today);
}
