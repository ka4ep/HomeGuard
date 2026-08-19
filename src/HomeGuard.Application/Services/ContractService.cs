using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using HomeGuard.Domain.ValueObjects;

namespace HomeGuard.Application.Services;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateContractCommand(
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
    IReadOnlyList<string>? Tags = null,
    Guid? PreviousContractId = null);

public sealed record UpdateContractCommand(
    Guid Id,
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
    IReadOnlyList<string>? Tags = null);

public sealed record AddRevisionCommand(
    Guid ContractId,
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
    IReadOnlyList<(string Name, decimal Amount)>? Adjustments = null);

public sealed record SetOpeningCommand(
    Guid ContractId,
    DateOnly AsOfDate,
    int InstallmentsPaid,
    decimal AmountPaid,
    decimal? RemainingBalance = null);

public sealed record AddPaymentCommand(
    Guid ContractId,
    DateOnly DueDate,
    decimal AmountDue,
    PaymentKind Kind = PaymentKind.Scheduled,
    int? InstallmentNo = null,
    string? Note = null);

public sealed record UpdatePaymentCommand(
    Guid PaymentId,
    DateOnly DueDate,
    decimal AmountDue,
    PaymentKind Kind,
    string? Note = null,
    bool Reopen = false);

public sealed record ConfirmPaymentCommand(
    Guid PaymentId,
    DateOnly PaidDate,
    decimal? AmountPaid = null,
    string? Note = null);

// ── Schedule ──────────────────────────────────────────────────────────────────

/// <summary>Where a line on the schedule came from, which is what decides how it renders.</summary>
public enum ScheduleOrigin
{
    /// <summary>Computed from the active revision. Not in the database; may still change.</summary>
    Projected = 0,

    /// <summary>A real row: materialised ahead of time, or entered by hand.</summary>
    Stored = 1,
}

/// <summary>
/// One line of the merged schedule — projections and stored rows in one sequence.
/// <see cref="PrincipalPart"/>/<see cref="InterestPart"/> are the actual split once paid,
/// an estimate from the governing revision's rate before that, and null whenever neither
/// is available — the same fallback-ladder rule as everywhere else in this feature.
/// </summary>
public sealed record ScheduleEntry(
    ScheduleOrigin Origin,
    DateOnly DueDate,
    decimal Amount,
    PaymentStatus? Status,
    int? InstallmentNo,
    PaymentKind Kind,
    Guid? PaymentId,
    bool IsOverdue,
    decimal? PrincipalPart = null,
    decimal? InterestPart = null);

/// <summary>
/// One line of the cross-contract "what is coming" list: enough to name the payment
/// without opening the contract it belongs to.
/// </summary>
public sealed record UpcomingPayment(
    Guid PaymentId,
    Guid ContractId,
    string ContractName,
    ContractKind Kind,
    string Currency,
    DateOnly DueDate,
    decimal AmountDue,
    bool IsOverdue);

/// <summary>What the detail page puts at the top: paid so far, left to pay, where it stands.</summary>
public sealed record ContractSummary(
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
    LoanEstimateGap EstimateGap);

// ── Amortization ─────────────────────────────────────────────────────────────

/// <summary>What to do with a lump sum paid against a loan or lease ahead of schedule.</summary>
public enum EarlyPaymentEffect
{
    /// <summary>Keep the instalment the same; the plan finishes sooner.</summary>
    ReduceTerm = 0,

    /// <summary>Keep the term the same; every remaining instalment gets smaller.</summary>
    ReducePayment = 1,
}

/// <summary>
/// What is missing to compute a loan/lease figure honestly — the fallback ladder from
/// contracts-spec.md §6: never block on missing data, say plainly what is missing instead.
/// </summary>
public enum LoanEstimateGap
{
    /// <summary>Not a loan/lease, or every number the math needs is present.</summary>
    None = 0,

    /// <summary>Balance is known; the rate is not. Term and balance are exact, interest is not shown.</summary>
    MissingRate = 1,

    /// <summary>No starting balance recorded at all — nothing here can be estimated yet.</summary>
    MissingBalance = 2,
}

/// <summary>
/// The consequence of paying <c>ExtraAmount</c> against a loan/lease today, before and
/// after. <see cref="InterestSaved"/> is null exactly when <see cref="Gap"/> is not
/// <see cref="LoanEstimateGap.None"/> — the term and payment numbers are still shown in
/// that case, just without a rate-dependent figure attached to them.
/// </summary>
public sealed record EarlyPaymentPreview(
    LoanEstimateGap Gap,
    int InstallmentsBefore,
    int InstallmentsAfter,
    decimal InstallmentAmountBefore,
    decimal InstallmentAmountAfter,
    DateOnly? PayoffDateBefore,
    DateOnly? PayoffDateAfter,
    decimal? InterestSaved);

// ── Finance rollup ───────────────────────────────────────────────────────────

/// <summary>One contract's contribution to one month's total — the stack a budget-load chart draws.</summary>
public sealed record MonthlyLoadContribution(
    Guid ContractId, string ContractName, ContractKind Kind, decimal Amount);

/// <summary>
/// One month's total obligation in one currency. Currencies are never blended (see
/// contracts-spec.md decision 2), so a household paying in two currencies gets two
/// entries for the same month.
/// </summary>
public sealed record MonthlyLoadEntry(
    string Month,           // "2026-09", sorts and parses without a culture
    string Currency,
    decimal Total,
    IReadOnlyList<MonthlyLoadContribution> Contributions);

// ── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Reads and writes contracts, and answers the questions every screen asks: what does the
/// schedule look like, where does this contract stand, and — for loans and leases — what
/// would an early payment actually change. The loan math (<see cref="AmortizationMath"/>)
/// only ever runs when a revision carries the numbers it needs; everywhere else the
/// missing piece is named (<see cref="LoanEstimateGap"/>) instead of guessed at.
/// </summary>
public sealed class ContractService
{
    private readonly IContractRepository _repo;
    private readonly IUnitOfWork _uow;

    public ContractService(IContractRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ── Reads ────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<Contract>> GetAllAsync(
        ContractKind? kind = null,
        ContractStatus? status = null,
        Guid? equipmentId = null,
        CancellationToken ct = default)
        => _repo.GetAllAsync(kind, status, equipmentId, ct);

    public Task<Contract?> GetAsync(Guid id, CancellationToken ct = default)
        => _repo.GetWithDetailsAsync(id, ct);

    public Task<IReadOnlyList<Contract>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _repo.GetByEquipmentAsync(equipmentId, ct);

    public Task<IReadOnlyList<Contract>> GetExpiringAsync(int days, CancellationToken ct = default)
        => _repo.GetExpiringAsync(Today, Today.AddDays(days), ct);

    // ── Writes ───────────────────────────────────────────────────────────────

    public async Task<Contract> CreateAsync(CreateContractCommand cmd, CancellationToken ct = default)
    {
        var contract = Contract.Create(
            kind:                   cmd.Kind,
            name:                   cmd.Name,
            startDate:              cmd.StartDate,
            currency:               cmd.Currency,
            equipmentId:            cmd.EquipmentId,
            provider:               cmd.Provider,
            contractNumber:         cmd.ContractNumber,
            endDate:                cmd.EndDate,
            renewal:                cmd.Renewal,
            cancellationNoticeDays: cmd.CancellationNoticeDays,
            summaryMarkdown:        cmd.SummaryMarkdown,
            notes:                  cmd.Notes,
            coverageAmount:         cmd.CoverageAmount,
            deductible:             cmd.Deductible,
            tags:                   cmd.Tags,
            previousContractId:     cmd.PreviousContractId);

        await _repo.AddAsync(contract, ct);
        await _uow.SaveChangesAsync(ct);
        return contract;
    }

    public async Task<Contract?> UpdateAsync(UpdateContractCommand cmd, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(cmd.Id, ct);
        if (contract is null) return null;

        contract.Update(
            name:                   cmd.Name,
            startDate:              cmd.StartDate,
            endDate:                cmd.EndDate,
            provider:               cmd.Provider,
            contractNumber:         cmd.ContractNumber,
            renewal:                cmd.Renewal,
            cancellationNoticeDays: cmd.CancellationNoticeDays,
            summaryMarkdown:        cmd.SummaryMarkdown,
            notes:                  cmd.Notes,
            coverageAmount:         cmd.CoverageAmount,
            deductible:             cmd.Deductible,
            tags:                   cmd.Tags);

        await _uow.SaveChangesAsync(ct);
        return contract;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var contract = await _repo.GetByIdAsync(id, ct);
        if (contract is null) return false;

        _repo.Remove(contract);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Contract?> SetStatusAsync(
        Guid id, ContractStatus status, string? reason = null, CancellationToken ct = default)
    {
        var contract = await _repo.GetByIdAsync(id, ct);
        if (contract is null) return null;

        contract.SetStatus(status, reason);
        await _uow.SaveChangesAsync(ct);
        return contract;
    }

    public async Task<Contract?> SetSummaryMarkdownAsync(
        Guid id, string? markdown, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(id, ct);
        if (contract is null) return null;

        contract.Update(
            name:                   contract.Name,
            startDate:              contract.StartDate,
            endDate:                contract.EndDate,
            provider:               contract.Provider,
            contractNumber:         contract.ContractNumber,
            renewal:                contract.Renewal,
            cancellationNoticeDays: contract.CancellationNoticeDays,
            summaryMarkdown:        markdown,
            notes:                  contract.Notes,
            coverageAmount:         contract.CoverageAmount,
            deductible:             contract.Deductible);

        await _uow.SaveChangesAsync(ct);
        return contract;
    }

    public async Task<Contract?> SetNotificationRulesAsync(
        Guid id,
        IReadOnlyList<(NotificationOffset Offset, bool Enabled)> rules,
        CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(id, ct);
        if (contract is null) return null;

        contract.SetNotificationRules(
            rules.Select(r => NotificationRule.Create(r.Offset, r.Enabled)));

        await _uow.SaveChangesAsync(ct);
        return contract;
    }

    // ── Plan revisions ───────────────────────────────────────────────────────

    public async Task<PaymentPlanRevision?> AddRevisionAsync(
        AddRevisionCommand cmd, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(cmd.ContractId, ct);
        if (contract is null) return null;

        var revision = contract.AddRevision(
            effectiveFrom:      cmd.EffectiveFrom,
            reason:             cmd.Reason,
            firstDueDate:       cmd.FirstDueDate,
            intervalMonths:     cmd.IntervalMonths,
            installmentAmount:  cmd.InstallmentAmount,
            installmentCount:   cmd.InstallmentCount,
            remainingPrincipal: cmd.RemainingPrincipal,
            annualInterestRate: cmd.AnnualInterestRate,
            residualAmount:     cmd.ResidualAmount,
            residualDueDate:    cmd.ResidualDueDate,
            note:               cmd.Note,
            adjustments:        cmd.Adjustments?.Select(a => PlanAdjustment.Create(a.Name, a.Amount)));

        await _uow.SaveChangesAsync(ct);
        return revision;
    }

    // ── Opening position ─────────────────────────────────────────────────────

    public async Task<Contract?> SetOpeningAsync(SetOpeningCommand cmd, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(cmd.ContractId, ct);
        if (contract is null) return null;

        contract.SetOpening(OpeningPosition.Create(
            cmd.AsOfDate, cmd.InstallmentsPaid, cmd.AmountPaid, cmd.RemainingBalance));

        await _uow.SaveChangesAsync(ct);
        return contract;
    }

    /// <summary>
    /// Removes the opening position entirely. Not the same as one with zero counters:
    /// an empty opening still carries a cut-off date, and a cut-off refuses every payment
    /// before it. "No prehistory recorded" has to mean no cut-off at all.
    /// </summary>
    public async Task<Contract?> ClearOpeningAsync(Guid contractId, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(contractId, ct);
        if (contract is null) return null;

        contract.ClearOpening();
        await _uow.SaveChangesAsync(ct);
        return contract;
    }

    // ── Payments ─────────────────────────────────────────────────────────────

    public async Task<Payment?> AddPaymentAsync(AddPaymentCommand cmd, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(cmd.ContractId, ct);
        if (contract is null) return null;

        var payment = Payment.CreatePlanned(
            contractId:     contract.Id,
            dueDate:        cmd.DueDate,
            amountDue:      cmd.AmountDue,
            kind:           cmd.Kind,
            planRevisionId: contract.ActiveRevision?.Id,
            installmentNo:  cmd.InstallmentNo,
            note:           cmd.Note);

        contract.AddPayment(payment);      // enforces the opening-position invariant
        await _uow.SaveChangesAsync(ct);
        return payment;
    }

    public async Task<Payment?> ConfirmPaymentAsync(
        ConfirmPaymentCommand cmd, CancellationToken ct = default)
    {
        var payment = await _repo.GetPaymentAsync(cmd.PaymentId, ct);
        if (payment is null) return null;

        payment.MarkPaid(cmd.PaidDate, cmd.AmountPaid, cmd.Note);

        // The principal/interest split only means something for a loan/lease instalment
        // whose revision carries a starting balance — everything else (insurance,
        // subscriptions, a hand-entered payment with no InstallmentNo) simply has none.
        if (payment.PlanRevisionId is { } revisionId && payment.InstallmentNo is { } no)
        {
            var contract = await _repo.GetWithDetailsAsync(payment.ContractId, ct);
            var revision = contract?.Revisions.FirstOrDefault(r => r.Id == revisionId);
            if (contract is { Kind: ContractKind.Loan or ContractKind.Lease } && revision is not null)
            {
                var split = SplitInstallmentAt(revision, no);
                if (split is { } s) payment.SetLoanSplit(s.Principal, s.Interest);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return payment;
    }

    /// <summary>
    /// Corrects a payment that was entered wrong. <c>Reopen</c> puts a confirmed payment
    /// back into the schedule — the way out of "marked paid by mistake", which otherwise
    /// leaves the totals quietly wrong.
    /// </summary>
    public async Task<Payment?> UpdatePaymentAsync(
        UpdatePaymentCommand cmd, CancellationToken ct = default)
    {
        var payment = await _repo.GetPaymentAsync(cmd.PaymentId, ct);
        if (payment is null) return null;

        if (cmd.Reopen) payment.Reopen();
        payment.Reschedule(cmd.DueDate, cmd.AmountDue);
        await _uow.SaveChangesAsync(ct);
        return payment;
    }

    /// <summary>
    /// Everything still unpaid up to <paramref name="days"/> ahead, overdue rows included —
    /// they are the whole reason anyone looks at this list.
    /// </summary>
    public async Task<IReadOnlyList<UpcomingPayment>> GetUpcomingAsync(
        int days, CancellationToken ct = default)
    {
        var today = Today;

        // The "from" bound reaches back deliberately: an overdue payment does not stop
        // being due because it is old.
        var rows = await _repo.GetPaymentsDueWithContractAsync(
            today.AddYears(-10), today.AddDays(days), ct);

        return [.. rows.Select(r => new UpcomingPayment(
            r.Payment.Id,
            r.Contract.Id,
            r.Contract.Name,
            r.Contract.Kind,
            r.Contract.Currency,
            r.Payment.DueDate,
            r.Payment.AmountDue,
            r.Payment.IsOverdue(today)))];
    }

    public async Task<bool> DeletePaymentAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _repo.GetPaymentAsync(paymentId, ct);
        if (payment is null) return false;

        _repo.RemovePayment(payment);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    // ── The schedule ─────────────────────────────────────────────────────────

    /// <summary>
    /// The merged schedule between two dates: every stored payment, plus projections
    /// computed from the active revision for the dates no stored payment covers.
    /// <para>
    /// Projections are never written down. That is what lets a plan change without
    /// leaving a trail of stale rows to clean up — only the near future gets materialised,
    /// and only because the calendar and the notifications need something real to point at.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ScheduleEntry>> GetScheduleAsync(
        Guid contractId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(contractId, ct);
        if (contract is null) return [];

        return BuildSchedule(contract, from, to);
    }

    /// <summary>Pure: the same contract always yields the same schedule. Public so it can be tested directly.</summary>
    public static IReadOnlyList<ScheduleEntry> BuildSchedule(
        Contract contract, DateOnly from, DateOnly to)
    {
        var today = Today;
        var revisionsById = contract.Revisions.ToDictionary(r => r.Id);

        var stored = contract.Payments
            .Where(p => p.DueDate >= from && p.DueDate <= to)
            .Select(p =>
            {
                // Actual split once paid; before that, an estimate from the revision that
                // produced the row — null for anything that is not a loan/lease instalment,
                // or whose revision never recorded a starting balance.
                var estimate = p.PrincipalPart is null && p.PlanRevisionId is { } rid
                             && revisionsById.TryGetValue(rid, out var r) && p.InstallmentNo is { } no
                    ? SplitInstallmentAt(r, no)
                    : null;

                return new ScheduleEntry(
                    ScheduleOrigin.Stored,
                    p.DueDate,
                    p.Status == PaymentStatus.Paid ? p.AmountPaid ?? p.AmountDue : p.AmountDue,
                    p.Status,
                    p.InstallmentNo,
                    p.Kind,
                    p.Id,
                    p.IsOverdue(today),
                    PrincipalPart: p.PrincipalPart ?? estimate?.Principal,
                    InterestPart:  p.InterestPart  ?? estimate?.Interest);
            })
            .ToList();

        var revision = contract.ActiveRevision;
        if (revision is null)
            return [.. stored.OrderBy(e => e.DueDate)];

        // A date already covered by a real row must not also appear as a projection —
        // that is the same double-counting the opening position guards against, one
        // level down.
        var covered = stored.Select(e => e.DueDate).ToHashSet();

        var projected = new List<ScheduleEntry>();
        var amount    = revision.EffectiveInstallment;

        for (var n = 1; ; n++)
        {
            if (revision.IsPastEnd(n)) break;

            var due = revision.DueDateOf(n);
            if (due > to) break;
            if (due < from) continue;

            // Anything before the opening cut-off is already counted in summary.
            if (contract.Opening is { } opening && due < opening.AsOfDate) continue;
            if (covered.Contains(due)) continue;

            var split = SplitInstallmentAt(revision, n);

            projected.Add(new ScheduleEntry(
                ScheduleOrigin.Projected, due, amount, null, n, PaymentKind.Scheduled, null,
                IsOverdue: due < today,
                PrincipalPart: split?.Principal, InterestPart: split?.Interest));

            // An open-ended plan would otherwise run forever; the window is the limit.
            if (projected.Count > 1000) break;
        }

        return [.. stored.Concat(projected).OrderBy(e => e.DueDate).ThenBy(e => e.InstallmentNo)];
    }

    /// <summary>
    /// Paid so far, left to pay, and what falls due next — with the opening position
    /// folded in, so a contract entered halfway through its life reports the same totals
    /// as one tracked from the start.
    /// </summary>
    public async Task<ContractSummary?> GetSummaryAsync(Guid contractId, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(contractId, ct);
        return contract is null ? null : BuildSummary(contract);
    }

    /// <summary>Pure, and public for the same reason as <see cref="BuildSchedule"/>.</summary>
    public static ContractSummary BuildSummary(Contract contract)
    {
        var today    = Today;
        var revision = contract.ActiveRevision;

        var paidRows = contract.Payments.Where(p => p.Status == PaymentStatus.Paid).ToList();

        var paidToDate = (contract.Opening?.AmountPaid ?? 0m)
                       + paidRows.Sum(p => p.AmountPaid ?? p.AmountDue);

        var installmentsPaid = (contract.Opening?.InstallmentsPaid ?? 0)
                             + paidRows.Count(p => p.Kind == PaymentKind.Scheduled);

        var total = revision?.InstallmentCount;

        var next = contract.Payments
            .Where(p => p.Status == PaymentStatus.Planned && p.DueDate >= today)
            .OrderBy(p => p.DueDate)
            .FirstOrDefault();

        // Nothing materialised yet — fall back to the first projection from today on.
        ScheduleEntry? nextProjected = null;
        if (next is null && revision is not null)
        {
            nextProjected = BuildSchedule(contract, today, today.AddYears(2))
                .FirstOrDefault(e => e.Origin == ScheduleOrigin.Projected);
        }

        var payoffDate = revision?.InstallmentCount is { } count
            ? revision.DueDateOf(count)
            : (DateOnly?)null;

        var gap = contract.Kind is ContractKind.Loan or ContractKind.Lease
            ? revision?.RemainingPrincipal is null
                ? LoanEstimateGap.MissingBalance
                : revision.AnnualInterestRate is null
                    ? LoanEstimateGap.MissingRate
                    : LoanEstimateGap.None
            : LoanEstimateGap.None;

        return new ContractSummary(
            ContractId:            contract.Id,
            Currency:              contract.Currency,
            PaidToDate:            paidToDate,
            InstallmentsPaid:      installmentsPaid,
            InstallmentsTotal:     total,
            InstallmentsRemaining: total is { } t ? Math.Max(t - installmentsPaid, 0) : null,
            RemainingBalance:      contract.Opening?.RemainingBalance ?? revision?.RemainingPrincipal,
            CurrentInstallment:    revision?.EffectiveInstallment,
            NextDueDate:           next?.DueDate ?? nextProjected?.DueDate,
            NextDueAmount:         next?.AmountDue ?? nextProjected?.Amount,
            OverdueCount:          contract.Payments.Count(p => p.IsOverdue(today)),
            InterestPaidToDate:    paidRows.Sum(p => p.InterestPart ?? 0m),
            PayoffDate:            payoffDate,
            EstimateGap:           gap);
    }

    // ── Loan math ────────────────────────────────────────────────────────────

    /// <summary>
    /// The balance immediately before instalment <paramref name="installmentNo"/> of
    /// <paramref name="revision"/>, walked forward from <see cref="PaymentPlanRevision.RemainingPrincipal"/>
    /// — the balance the revision itself recorded at <see cref="PaymentPlanRevision.EffectiveFrom"/>.
    /// Null when the revision never recorded one; there is nothing to walk forward from, and
    /// guessing would be exactly the invented number contracts-spec.md §6 rules out.
    /// An absent rate is treated as 0% — the interest-free ladder used for
    /// <see cref="LoanEstimateGap.MissingRate"/>.
    /// </summary>
    public static decimal? BalanceBeforeInstallment(PaymentPlanRevision revision, int installmentNo)
    {
        if (revision.RemainingPrincipal is not { } principal) return null;
        var monthlyRate = AmortizationMath.MonthlyRate(revision.AnnualInterestRate ?? 0m);
        return AmortizationMath.BalanceAfter(
            principal, monthlyRate, revision.EffectiveInstallment, installmentNo - 1);
    }

    /// <summary>The principal/interest split of one instalment, or null under the same conditions as <see cref="BalanceBeforeInstallment"/>.</summary>
    public static (decimal Principal, decimal Interest)? SplitInstallmentAt(
        PaymentPlanRevision revision, int installmentNo)
    {
        if (BalanceBeforeInstallment(revision, installmentNo) is not { } balance) return null;
        var monthlyRate = AmortizationMath.MonthlyRate(revision.AnnualInterestRate ?? 0m);
        return AmortizationMath.SplitInstallment(balance, monthlyRate, revision.EffectiveInstallment);
    }

    /// <summary>
    /// What paying <paramref name="extraAmount"/> against the active revision today would
    /// change. Pure and side-effect-free — nothing is written until the caller turns the
    /// result into a real revision via <see cref="AddRevisionAsync"/>, which is what lets
    /// the dialog show consequences before the household commits to them.
    /// </summary>
    public static EarlyPaymentPreview PreviewEarlyPayment(
        Contract contract, decimal extraAmount, EarlyPaymentEffect effect)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extraAmount);
        var revision = contract.ActiveRevision
            ?? throw new InvalidOperationException("This contract has no active plan to pay against.");

        var paidUnderRevision = contract.Payments.Count(p =>
            p.PlanRevisionId == revision.Id
            && p.Status == PaymentStatus.Paid
            && p.Kind == PaymentKind.Scheduled);

        var installment = revision.EffectiveInstallment;
        var termLeftBefore = revision.InstallmentCount is { } total
            ? Math.Max(total - paidUnderRevision, 0)
            : 0;
        var payoffBefore = revision.InstallmentCount is { } tot
            ? revision.DueDateOf(tot)
            : (DateOnly?)null;

        if (revision.RemainingPrincipal is not { } principal)
        {
            // No starting balance recorded at all: term and payment stay whatever they
            // already were, and there is nothing left to say about the pay-off itself.
            return new EarlyPaymentPreview(
                LoanEstimateGap.MissingBalance,
                termLeftBefore, termLeftBefore,
                installment, installment,
                payoffBefore, payoffBefore,
                InterestSaved: null);
        }

        var rateKnown = revision.AnnualInterestRate is not null;
        var monthlyRate = AmortizationMath.MonthlyRate(revision.AnnualInterestRate ?? 0m);
        var balanceNow = AmortizationMath.BalanceAfter(principal, monthlyRate, installment, paidUnderRevision);
        var balanceAfter = Math.Max(balanceNow - extraAmount, 0m);
        var gap = rateKnown ? LoanEstimateGap.None : LoanEstimateGap.MissingRate;

        if (balanceAfter <= 0m || termLeftBefore == 0)
        {
            var interestSaved = rateKnown
                ? AmortizationMath.TotalInterestOverTerm(balanceNow, installment, termLeftBefore)
                : (decimal?)null;

            return new EarlyPaymentPreview(
                gap, termLeftBefore, 0, installment, 0m,
                payoffBefore, PayoffDateAfter: null,
                InterestSaved: interestSaved);
        }

        int termLeftAfter;
        decimal installmentAfter;
        if (effect == EarlyPaymentEffect.ReducePayment)
        {
            termLeftAfter = termLeftBefore;
            installmentAfter = monthlyRate == 0m
                ? balanceAfter / termLeftBefore
                : AmortizationMath.AnnuityInstallment(balanceAfter, monthlyRate, termLeftBefore);
        }
        else
        {
            installmentAfter = installment;
            termLeftAfter = AmortizationMath.TermFor(balanceAfter, monthlyRate, installment);
        }

        var payoffAfter = revision.DueDateOf(paidUnderRevision + termLeftAfter);

        decimal? saved = null;
        if (rateKnown)
        {
            var interestBefore = AmortizationMath.TotalInterestOverTerm(balanceNow, installment, termLeftBefore);
            var interestAfter  = AmortizationMath.TotalInterestOverTerm(balanceAfter, installmentAfter, termLeftAfter);
            saved = interestBefore - interestAfter;
        }

        return new EarlyPaymentPreview(
            gap, termLeftBefore, termLeftAfter,
            installment, installmentAfter,
            payoffBefore, payoffAfter,
            InterestSaved: saved);
    }

    // ── Finance rollup ───────────────────────────────────────────────────────

    /// <summary>
    /// Every active contract's schedule for the next <paramref name="months"/> months,
    /// merged into one month-by-month, currency-by-currency total — the numbers a
    /// budget-load chart needs to show which month several obligations land on at once.
    /// Currencies are never blended (contracts-spec.md decision 2): a household paying in
    /// two currencies gets two entries for the same month.
    /// </summary>
    public async Task<IReadOnlyList<MonthlyLoadEntry>> GetMonthlyLoadAsync(
        int months, CancellationToken ct = default)
    {
        var today = Today;
        var from  = new DateOnly(today.Year, today.Month, 1);
        var to    = from.AddMonths(months).AddDays(-1);

        var contracts = await _repo.GetActiveWithSchedulesAsync(ct);
        return BuildMonthlyLoad(contracts, from, to);
    }

    /// <summary>Pure, and public for the same reason as <see cref="BuildSchedule"/>.</summary>
    public static IReadOnlyList<MonthlyLoadEntry> BuildMonthlyLoad(
        IEnumerable<Contract> contracts, DateOnly from, DateOnly to)
    {
        var buckets = new Dictionary<(string Month, string Currency), List<MonthlyLoadContribution>>();

        foreach (var contract in contracts)
        {
            foreach (var entry in BuildSchedule(contract, from, to))
            {
                if (entry.Status == PaymentStatus.Skipped) continue;

                var key = (entry.DueDate.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                           contract.Currency);
                if (!buckets.TryGetValue(key, out var list))
                    buckets[key] = list = [];

                list.Add(new MonthlyLoadContribution(contract.Id, contract.Name, contract.Kind, entry.Amount));
            }
        }

        return [.. buckets
            .Select(kv => new MonthlyLoadEntry(
                kv.Key.Month, kv.Key.Currency, kv.Value.Sum(c => c.Amount), kv.Value))
            .OrderBy(e => e.Month).ThenBy(e => e.Currency)];
    }
}
