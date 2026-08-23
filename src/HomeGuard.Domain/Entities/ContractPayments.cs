using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// One version of a contract's payment schedule.
/// <para>
/// The plan is immutable and versioned: nothing is ever edited in place. Paying off a
/// chunk early, a price rise, a pause, a correction of a typo — each appends a revision,
/// and the schedule in force is simply the last one. That is what makes it possible to
/// answer "what were we paying in 2023?" a year later, and what keeps a correction from
/// silently rewriting history the household may have already acted on.
/// </para>
/// </summary>
public sealed class PaymentPlanRevision : Entity
{
    private PaymentPlanRevision() { }

    public Guid ContractId { get; private set; }

    /// <summary>1, 2, 3… — dense and ordered within the contract.</summary>
    public int Version { get; private set; }

    /// <summary>The first due date this revision governs. Earlier dates belong to earlier revisions.</summary>
    public DateOnly EffectiveFrom { get; private set; }

    public RevisionReason Reason { get; private set; }

    // ── The schedule itself ──────────────────────────────────────────────────

    /// <summary>Anchor for generating due dates; every date is FirstDueDate + n × IntervalMonths.</summary>
    public DateOnly FirstDueDate { get; private set; }

    /// <summary>1 = monthly, 3 = quarterly, 12 = yearly. Any positive number is allowed.</summary>
    public int IntervalMonths { get; private set; }

    /// <summary>Null means open-ended — a subscription that runs until someone cancels it.</summary>
    public int? InstallmentCount { get; private set; }

    /// <summary>The base instalment, before <see cref="Adjustments"/>.</summary>
    public decimal InstallmentAmount { get; private set; }

    // ── Loans and leases only ────────────────────────────────────────────────

    /// <summary>Balance owed at <see cref="EffectiveFrom"/>.</summary>
    public decimal? RemainingPrincipal { get; private set; }

    /// <summary>Nominal annual rate as a fraction, e.g. 0.079 for 7.9%.</summary>
    public decimal? AnnualInterestRate { get; private set; }

    /// <summary>Balloon or buy-out payment at the end of a lease.</summary>
    public decimal? ResidualAmount { get; private set; }

    public DateOnly? ResidualDueDate { get; private set; }

    public string? Note { get; private set; }

    // ── Adjustments ──────────────────────────────────────────────────────────

    private readonly List<PlanAdjustment> _adjustments = [];

    /// <summary>
    /// The add-ons and discounts folded into the instalment, kept as separate lines so the
    /// detail page can show <em>why</em> the monthly figure is what it is.
    /// </summary>
    public IReadOnlyList<PlanAdjustment> Adjustments => _adjustments.AsReadOnly();

    /// <summary>What actually leaves the account each period.</summary>
    public decimal EffectiveInstallment
        => InstallmentAmount + _adjustments.Sum(a => a.Amount);

    // ── Factory ──────────────────────────────────────────────────────────────

    public static PaymentPlanRevision Create(
        Guid contractId,
        int version,
        DateOnly effectiveFrom,
        RevisionReason reason,
        DateOnly firstDueDate,
        int intervalMonths,
        decimal installmentAmount,
        int? installmentCount = null,
        decimal? remainingPrincipal = null,
        decimal? annualInterestRate = null,
        decimal? residualAmount = null,
        DateOnly? residualDueDate = null,
        string? note = null,
        IEnumerable<PlanAdjustment>? adjustments = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalMonths, 1);
        if (installmentCount is < 1)
            throw new ArgumentOutOfRangeException(
                nameof(installmentCount), "An instalment count must be positive, or null for open-ended.");
        if (annualInterestRate is < 0)
            throw new ArgumentOutOfRangeException(
                nameof(annualInterestRate), "An interest rate cannot be negative.");

        var r = new PaymentPlanRevision();
        r.InitNew();
        r.ContractId         = contractId;
        r.Version            = version;
        r.EffectiveFrom      = effectiveFrom;
        r.Reason             = reason;
        r.FirstDueDate       = firstDueDate;
        r.IntervalMonths     = intervalMonths;
        r.InstallmentCount   = installmentCount;
        r.InstallmentAmount  = installmentAmount;
        r.RemainingPrincipal = remainingPrincipal;
        r.AnnualInterestRate = annualInterestRate;
        r.ResidualAmount     = residualAmount;
        r.ResidualDueDate    = residualDueDate;
        r.Note               = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (adjustments is not null) r._adjustments.AddRange(adjustments);
        return r;
    }

    /// <summary>
    /// The due date of instalment <paramref name="number"/>, counting from 1.
    /// </summary>
    public DateOnly DueDateOf(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return FirstDueDate.AddMonths((number - 1) * IntervalMonths);
    }

    /// <summary>
    /// Whether this revision has run out of instalments by <paramref name="number"/>.
    /// Always false for an open-ended plan.
    /// </summary>
    public bool IsPastEnd(int number) => InstallmentCount is { } count && number > count;
}

/// <summary>
/// One line folded into the instalment — an add-on, a rider, a discount.
/// Owned by its revision, so changing one means appending a new revision.
/// </summary>
public sealed class PlanAdjustment
{
    // Parameterless ctor for EF Core.
    private PlanAdjustment() { }

    /// <summary>"Roadside assistance", "4K plan", "loyalty discount".</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Signed: +4.99 for an add-on, −2.00 for a discount.</summary>
    public decimal Amount { get; private set; }

    public static PlanAdjustment Create(string name, decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new PlanAdjustment { Name = name.Trim(), Amount = amount };
    }
}

/// <summary>
/// A single money event on a contract — one row per instalment, extra payment, fee or refund.
/// <para>
/// Planned rows are materialised from the active revision a while before they fall due, so
/// they can reach the calendar and the notifications. Everything further out stays a
/// projection computed on demand, never stored — the same three-state lifecycle the
/// service records already use.
/// </para>
/// </summary>
public sealed class Payment : Entity
{
    private Payment() { }

    public Guid ContractId { get; private set; }

    /// <summary>Which revision produced this row. Null for a payment entered by hand.</summary>
    public Guid? PlanRevisionId { get; private set; }

    /// <summary>Position in the schedule, counting from 1. Null for anything unscheduled.</summary>
    public int? InstallmentNo { get; private set; }

    public PaymentKind Kind { get; private set; }
    public PaymentStatus Status { get; private set; }

    public DateOnly DueDate { get; private set; }
    public decimal AmountDue { get; private set; }

    public DateOnly? PaidDate { get; private set; }

    /// <summary>What was actually paid — not always what was due.</summary>
    public decimal? AmountPaid { get; private set; }

    // ── Loan split ───────────────────────────────────────────────────────────
    public decimal? PrincipalPart { get; private set; }
    public decimal? InterestPart { get; private set; }

    public string? Note { get; private set; }

    private readonly List<BlobEntry> _attachments = [];

    /// <summary>Receipt photos, bank confirmations.</summary>
    public IReadOnlyList<BlobEntry> Attachments => _attachments.AsReadOnly();

    public bool IsOverdue(DateOnly today) => Status == PaymentStatus.Planned && DueDate < today;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static Payment CreatePlanned(
        Guid contractId,
        DateOnly dueDate,
        decimal amountDue,
        PaymentKind kind = PaymentKind.Scheduled,
        Guid? planRevisionId = null,
        int? installmentNo = null,
        string? note = null)
    {
        if (installmentNo is < 1)
            throw new ArgumentOutOfRangeException(
                nameof(installmentNo), "Instalment numbers count from 1.");

        var p = new Payment();
        p.InitNew();
        p.ContractId     = contractId;
        p.PlanRevisionId = planRevisionId;
        p.InstallmentNo  = installmentNo;
        p.Kind           = kind;
        p.Status         = PaymentStatus.Planned;
        p.DueDate        = dueDate;
        p.AmountDue      = amountDue;
        p.Note           = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return p;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    /// <summary>
    /// Records that the money actually moved. The amount is separate from the amount due
    /// because they disagree often enough to matter — a rounding difference, a part payment.
    /// </summary>
    public void MarkPaid(DateOnly paidDate, decimal? amountPaid = null, string? note = null)
    {
        Status     = PaymentStatus.Paid;
        PaidDate   = paidDate;
        AmountPaid = amountPaid ?? AmountDue;
        if (note is not null) Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Touch();
    }

    public void MarkSkipped(string? note = null)
    {
        Status   = PaymentStatus.Skipped;
        PaidDate = null;
        if (note is not null) Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Touch();
    }

    public void MarkFailed(string? note = null)
    {
        Status = PaymentStatus.Failed;
        if (note is not null) Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Touch();
    }

    /// <summary>Puts a payment back in the schedule after it was marked paid or skipped by mistake.</summary>
    public void Reopen()
    {
        Status     = PaymentStatus.Planned;
        PaidDate   = null;
        AmountPaid = null;
        Touch();
    }

    public void Reschedule(DateOnly dueDate, decimal? amountDue = null)
    {
        DueDate = dueDate;
        if (amountDue.HasValue) AmountDue = amountDue.Value;
        Touch();
    }

    /// <summary>Splits the payment into principal and interest — loans and leases only.</summary>
    public void SetLoanSplit(decimal principalPart, decimal interestPart)
    {
        PrincipalPart = principalPart;
        InterestPart  = interestPart;
        Touch();
    }

    public void AddAttachment(BlobEntry attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
        Touch();
    }
}
