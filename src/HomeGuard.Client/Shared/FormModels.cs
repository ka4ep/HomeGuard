using HomeGuard.Client.Services;
using Microsoft.Extensions.Localization;

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

    /// <summary>Tags is a free-typed comma-separated string in the form; both Create and
    /// Update need the same split/trim logic to turn it into the list the API expects.</summary>
    public IReadOnlyList<string> ParsedTags
        => Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

// ── Warranty ──────────────────────────────────────────────────────────────────

public class WarrantyFormModel
{
    public string   Name           { get; set; } = string.Empty;
    public string?  Provider       { get; set; }
    public string?  ContractNumber { get; set; }
    public decimal? Cost           { get; set; }
    public string?  Notes          { get; set; }

    public DateTime? StartDateNullable { get; set; } = DateTime.Today;
    public DateTime? EndDateNullable   { get; set; } = DateTime.Today.AddYears(2);

    /// <summary>
    /// A slider (0-20 half-years, i.e. 0-10 years in 6-month steps), not a fixed list —
    /// most warranties are a round number of years, but some (a part, not the whole
    /// device) run six months, so whole-year steps were too coarse. This drives EndDate
    /// off StartDate instead of making every warranty need its own end-date math. The end
    /// date itself stays directly editable for the rare custom case — editing it moves the
    /// slider back to match (rounded down; see InferDurationHalfYears). 0 has no "no
    /// warranty" meaning here — only the equipment-add flow's slider treats 0 as opting
    /// out, since only there is skipping the whole record a real choice.
    /// </summary>
    public int DurationHalfYears { get; set; } = 4;

    public DateOnly StartDate
        => StartDateNullable.HasValue ? DateOnly.FromDateTime(StartDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today);

    public DateOnly EndDate
        => EndDateNullable.HasValue ? DateOnly.FromDateTime(EndDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today.AddYears(2));

    /// <summary>Whole-steps-minus-a-day is the usual convention: 2 years from a purchase
    /// on the 5th covers up to and including the 4th two years later, not one day into a
    /// third year. AddMonths itself already clamps Feb 29 / month-end overflow correctly
    /// — no special-casing needed here.</summary>
    public static DateTime ComputeEnd(DateTime start, int halfYears) => start.AddMonths(halfYears * 6).AddDays(-1);

    /// <summary>Inverse of ComputeEnd, for opening an existing warranty in the slider UI or
    /// after a manually-typed end date — without this the slider stayed wherever it last
    /// was regardless of the record's actual dates. A manually-set date that falls between
    /// two slider marks rounds down ("a division smaller, to the left") rather than up, so
    /// the slider never implies more coverage than the stored end date actually gives.</summary>
    public static int InferDurationHalfYears(DateTime start, DateTime end)
    {
        for (var h = 20; h >= 0; h--)
            if (ComputeEnd(start, h) <= end) return h;
        return 0;
    }

    /// <summary>Russian has three plural forms and resx has no plural rules (same problem
    /// the warranty countdown labels already sidestep) — so the three forms are picked
    /// here and resx only carries the already-correct string for each. A half-year mark
    /// (odd halfYears) is a fractional year count instead — Russian fractional quantities
    /// always take the noun in genitive singular ("1,5 года"), English "years" is already
    /// fine for any non-1 value, so no plural-form switch is needed for those.</summary>
    public static string YearsLabel(IStringLocalizer<Strings> L, int halfYears)
    {
        if (halfYears <= 0) return L["Warranty_NoWarranty"];

        if (halfYears % 2 != 0)
        {
            var value = (halfYears / 2m).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
            return L["Warranty_YearsFraction", value];
        }

        var years  = halfYears / 2;
        var mod100 = years % 100;
        var mod10  = years % 10;
        var key = mod100 is >= 11 and <= 14
            ? "Warranty_YearsMany"
            : mod10 switch
            {
                1                 => "Warranty_YearsOne",
                >= 2 and <= 4     => "Warranty_YearsFew",
                _                 => "Warranty_YearsMany",
            };
        return L[key, years];
    }
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

    /// <summary>Balance owed at <see cref="EffectiveFrom"/> — loans and leases only.</summary>
    public decimal? RemainingPrincipal { get; set; }

    /// <summary>Nominal annual rate as a whole-number percentage (6.5 for 6.5%), the way a household reads it off a statement.</summary>
    public decimal? AnnualInterestRatePercent { get; set; }

    /// <summary>A lump sum paid today, ahead of schedule. Zero/empty means "no early payment" — a plain plan edit.</summary>
    public decimal? ExtraAmount { get; set; }

    public EarlyPaymentEffect Effect { get; set; } = EarlyPaymentEffect.ReduceTerm;

    public DateTime? EffectiveFromNullable { get; set; } = DateTime.Today;

    public DateOnly EffectiveFrom
        => EffectiveFromNullable is { } d ? DateOnly.FromDateTime(d) : DateOnly.FromDateTime(DateTime.Today);
}
