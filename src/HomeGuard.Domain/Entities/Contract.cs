using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;
using HomeGuard.Domain.ValueObjects;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// An agreement the household pays for over time: an insurance policy, a subscription,
/// a loan, a lease.
/// <para>
/// These are one aggregate rather than four features because they differ in vocabulary
/// and in a handful of optional fields, not in shape. Each is a counterparty, a period,
/// and money on a schedule; each needs corrections after the fact; each is usually
/// entered when it has already been running for years. Splitting them would mean
/// writing the versioned plan, the opening position and the payment lifecycle four times.
/// </para>
/// </summary>
public sealed class Contract : Entity
{
    private Contract() { }

    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Null means household-level — a streaming subscription, a bank-card insurance that
    /// belongs to nobody's car. When set, the contract shows up on that item's page and
    /// its cost counts towards owning it.
    /// </summary>
    public Guid? EquipmentId { get; private set; }

    public ContractKind Kind { get; private set; }

    /// <summary>"KASKO Cupra Born", "Netflix Premium". The household's own words.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Insurer, bank, vendor.</summary>
    public string? Provider { get; private set; }

    /// <summary>Policy or agreement number, for the phone call where they ask for it.</summary>
    public string? ContractNumber { get; private set; }

    // ── Period ───────────────────────────────────────────────────────────────

    public DateOnly StartDate { get; private set; }

    /// <summary>Null means open-ended — most subscriptions, some card insurance.</summary>
    public DateOnly? EndDate { get; private set; }

    public RenewalMode Renewal { get; private set; }

    /// <summary>
    /// How many days before <see cref="EndDate"/> the chance to cancel closes. The one
    /// deadline that costs real money to miss, so it gets its own field rather than a note.
    /// </summary>
    public int? CancellationNoticeDays { get; private set; }

    public ContractStatus Status { get; private set; }

    /// <summary>
    /// The previous policy in a renewal chain. Insurance renews into a new contract —
    /// each year has its own document, its own price, its own summary.
    /// </summary>
    public Guid? PreviousContractId { get; private set; }

    // ── Money ────────────────────────────────────────────────────────────────

    /// <summary>ISO-4217. Never converted: an amount is shown in the currency it was agreed in.</summary>
    public string Currency { get; private set; } = null!;

    // ── Text ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The short version of what this contract actually covers, in markdown, rendered in
    /// its own card. Written by hand from the twelve-page PDF that sits in the attachments.
    /// </summary>
    public string? SummaryMarkdown { get; private set; }

    public string? Notes { get; private set; }

    // ── Insurance extras ─────────────────────────────────────────────────────

    public decimal? CoverageAmount { get; private set; }
    public decimal? Deductible { get; private set; }

    // ── Prehistory ───────────────────────────────────────────────────────────

    /// <summary>
    /// Set when the contract was already running before anyone started recording it.
    /// See <see cref="OpeningPosition"/> for the invariant that stops it double counting.
    /// </summary>
    public OpeningPosition? Opening { get; private set; }

    // ── Collections ──────────────────────────────────────────────────────────

    private List<string> _tags = [];
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    private readonly List<PaymentPlanRevision> _revisions = [];

    /// <summary>Append-only, ordered by <see cref="PaymentPlanRevision.Version"/>.</summary>
    public IReadOnlyList<PaymentPlanRevision> Revisions => _revisions.AsReadOnly();

    private readonly List<Payment> _payments = [];
    public IReadOnlyList<Payment> Payments => _payments.AsReadOnly();

    private readonly List<NotificationRule> _notificationRules = [];
    public IReadOnlyList<NotificationRule> NotificationRules => _notificationRules.AsReadOnly();

    private readonly List<BlobEntry> _attachments = [];

    /// <summary>The policy PDF, the signed agreement, the receipts.</summary>
    public IReadOnlyList<BlobEntry> Attachments => _attachments.AsReadOnly();

    // ── Computed ─────────────────────────────────────────────────────────────

    /// <summary>The revision in force — the last one appended.</summary>
    public PaymentPlanRevision? ActiveRevision
        => _revisions.Count == 0 ? null : _revisions.OrderByDescending(r => r.Version).First();

    /// <summary>
    /// The day the chance to cancel closes, when the contract has both an end date and a
    /// notice period. This is what the reminder fires on, not the end date itself.
    /// </summary>
    public DateOnly? CancellationDeadline
        => EndDate is { } end && CancellationNoticeDays is { } days
            ? end.AddDays(-days)
            : null;

    public bool IsOpenEnded => EndDate is null;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static Contract Create(
        ContractKind kind,
        string name,
        DateOnly startDate,
        string currency,
        Guid? equipmentId = null,
        string? provider = null,
        string? contractNumber = null,
        DateOnly? endDate = null,
        RenewalMode renewal = RenewalMode.None,
        int? cancellationNoticeDays = null,
        string? summaryMarkdown = null,
        string? notes = null,
        decimal? coverageAmount = null,
        decimal? deductible = null,
        IEnumerable<string>? tags = null,
        Guid? previousContractId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (endDate is { } end && end < startDate)
            throw new ArgumentException("A contract cannot end before it starts.", nameof(endDate));
        if (cancellationNoticeDays is < 0)
            throw new ArgumentOutOfRangeException(
                nameof(cancellationNoticeDays), "A notice period cannot be negative.");

        var c = new Contract();
        c.InitNew();
        c.Kind                   = kind;
        c.Name                   = name.Trim();
        c.StartDate              = startDate;
        c.Currency               = NormaliseCurrency(currency);
        c.EquipmentId            = equipmentId;
        c.Provider               = Clean(provider);
        c.ContractNumber         = Clean(contractNumber);
        c.EndDate                = endDate;
        c.Renewal                = renewal;
        c.CancellationNoticeDays = cancellationNoticeDays;
        c.Status                 = ContractStatus.Active;
        c.SummaryMarkdown        = Clean(summaryMarkdown);
        c.Notes                  = Clean(notes);
        c.CoverageAmount         = coverageAmount;
        c.Deductible             = deductible;
        c.PreviousContractId     = previousContractId;
        c._tags                  = NormaliseTags(tags);
        return c;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Update(
        string name,
        DateOnly startDate,
        DateOnly? endDate = null,
        string? provider = null,
        string? contractNumber = null,
        RenewalMode renewal = RenewalMode.None,
        int? cancellationNoticeDays = null,
        string? summaryMarkdown = null,
        string? notes = null,
        decimal? coverageAmount = null,
        decimal? deductible = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (endDate is { } end && end < startDate)
            throw new ArgumentException("A contract cannot end before it starts.", nameof(endDate));

        Name                   = name.Trim();
        StartDate              = startDate;
        EndDate                = endDate;
        Provider               = Clean(provider);
        ContractNumber         = Clean(contractNumber);
        Renewal                = renewal;
        CancellationNoticeDays = cancellationNoticeDays;
        SummaryMarkdown        = Clean(summaryMarkdown);
        Notes                  = Clean(notes);
        CoverageAmount         = coverageAmount;
        Deductible             = deductible;
        if (tags is not null) _tags = NormaliseTags(tags);
        Touch();
    }

    public void SetStatus(ContractStatus status)
    {
        Status = status;
        Touch();
    }

    public void AttachToEquipment(Guid? equipmentId)
    {
        EquipmentId = equipmentId;
        Touch();
    }

    // ── Plan revisions ───────────────────────────────────────────────────────

    /// <summary>
    /// Appends a revision. The caller supplies everything except the version number and
    /// the contract id, which are the aggregate's business — that is what keeps versions
    /// dense and ordered no matter who is adding one.
    /// </summary>
    public PaymentPlanRevision AddRevision(
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
        var previous = ActiveRevision;
        if (previous is not null && effectiveFrom < previous.EffectiveFrom)
            throw new InvalidOperationException(
                $"Revision {previous.Version} already governs from {previous.EffectiveFrom:O}; " +
                "a new revision cannot take effect earlier. Correct the earlier revision instead.");

        var revision = PaymentPlanRevision.Create(
            contractId:         Id,
            version:            (previous?.Version ?? 0) + 1,
            effectiveFrom:      effectiveFrom,
            reason:             reason,
            firstDueDate:       firstDueDate,
            intervalMonths:     intervalMonths,
            installmentAmount:  installmentAmount,
            installmentCount:   installmentCount,
            remainingPrincipal: remainingPrincipal,
            annualInterestRate: annualInterestRate,
            residualAmount:     residualAmount,
            residualDueDate:    residualDueDate,
            note:               note,
            adjustments:        adjustments);

        _revisions.Add(revision);
        Touch();
        return revision;
    }

    // ── Payments ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a payment row, refusing anything that falls inside the period the opening
    /// position already accounts for — that is the one way this aggregate can start
    /// counting the same money twice.
    /// </summary>
    public void AddPayment(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        GuardAgainstOpeningOverlap(payment.DueDate);
        _payments.Add(payment);
        Touch();
    }

    // ── Opening position ─────────────────────────────────────────────────────

    /// <summary>
    /// Records what happened before tracking began. Rejected if any stored payment falls
    /// before <paramref name="opening"/>'s cut-off, since those payments would then be
    /// counted both individually and inside the summary.
    /// </summary>
    public void SetOpening(OpeningPosition opening)
    {
        ArgumentNullException.ThrowIfNull(opening);

        var earliest = _payments.Count == 0 ? (DateOnly?)null : _payments.Min(p => p.DueDate);
        if (earliest is { } first && first < opening.AsOfDate)
            throw new InvalidOperationException(
                $"A payment is recorded on {first:O}, before the opening position's " +
                $"cut-off of {opening.AsOfDate:O}. Move the cut-off back, or remove the payment.");

        Opening = opening;
        Touch();
    }

    public void ClearOpening()
    {
        Opening = null;
        Touch();
    }

    /// <summary>
    /// Moves the cut-off earlier and shrinks the summarised counters by the same amount,
    /// so that payments dug out of a drawer can be entered individually without the totals
    /// changing. Both sides move together or neither does.
    /// </summary>
    public void RebaseOpening(
        DateOnly newAsOfDate, int installmentsMovedOut, decimal amountMovedOut)
    {
        if (Opening is null)
            throw new InvalidOperationException("This contract has no opening position to rebase.");
        if (newAsOfDate > Opening.AsOfDate)
            throw new ArgumentException(
                "Rebasing only moves the cut-off earlier — that is what makes room for older payments.",
                nameof(newAsOfDate));

        Opening = OpeningPosition.Create(
            newAsOfDate,
            Opening.InstallmentsPaid - installmentsMovedOut,
            Opening.AmountPaid - amountMovedOut,
            Opening.RemainingBalance);
        Touch();
    }

    // ── Notifications and attachments ────────────────────────────────────────

    public void SetNotificationRules(IEnumerable<NotificationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _notificationRules.Clear();
        _notificationRules.AddRange(rules);
        Touch();
    }

    public void AddAttachment(BlobEntry attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
        Touch();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void GuardAgainstOpeningOverlap(DateOnly dueDate)
    {
        if (Opening is { } opening && dueDate < opening.AsOfDate)
            throw new InvalidOperationException(
                $"{dueDate:O} falls before the opening position's cut-off of {opening.AsOfDate:O}, " +
                "where payments are already counted in summary. Rebase the opening position first.");
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormaliseCurrency(string currency)
    {
        var value = currency.Trim().ToUpperInvariant();
        if (value.Length != 3)
            throw new ArgumentException(
                $"'{currency}' is not an ISO-4217 currency code.", nameof(currency));
        return value;
    }

    private static List<string> NormaliseTags(IEnumerable<string>? tags)
        => tags is null
            ? []
            : [.. tags.Where(t => !string.IsNullOrWhiteSpace(t))
                      .Select(t => new Tag(t).Value)
                      .Distinct()];
}
