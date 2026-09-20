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

/// <summary>One line of the merged schedule — projections and stored rows in one sequence.</summary>
public sealed record ScheduleEntry(
    ScheduleOrigin Origin,
    DateOnly DueDate,
    decimal Amount,
    PaymentStatus? Status,
    int? InstallmentNo,
    PaymentKind Kind,
    Guid? PaymentId,
    bool IsOverdue,
    decimal? Principal = null,
    decimal? Interest = null,
    decimal? BalanceAfter = null);

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
    decimal? InterestPaidToDate = null,
    decimal? InterestRemaining = null,
    decimal? TotalCost = null,
    DateOnly? PayoffDate = null);

public sealed record EarlyPaymentCommand(
    Guid ContractId,
    decimal Amount,
    DateOnly PaidOn,
    EarlyPaymentEffect Effect,
    string? Note = null);

// ── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Reads and writes contracts, and answers the two questions every screen asks:
/// what does the schedule look like, and where does this contract stand.
/// <para>
/// Loan arithmetic itself lives in <see cref="LoanMath"/>; this class only feeds it and
/// writes down the outcome.
/// </para>
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

    public async Task<Contract?> SetStatusAsync(Guid id, ContractStatus status, CancellationToken ct = default)
    {
        var contract = await _repo.GetByIdAsync(id, ct);
        if (contract is null) return null;

        contract.SetStatus(status);
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

        // For a loan the split is written down at confirmation, so the history keeps what
        // the bank actually applied even if the plan is revised later.
        var contract = await _repo.GetWithDetailsAsync(payment.ContractId, ct);
        if (contract is not null
            && LoanMath.TryGetPosition(contract) is { } position
            && position.Splits.TryGetValue(payment.Id, out var split))
        {
            payment.SetLoanSplit(split.Principal, split.Interest);
        }

        await _uow.SaveChangesAsync(ct);
        return payment;
    }

    // ── Early payment ────────────────────────────────────────────────────────

    /// <summary>
    /// What paying a lump sum would do, without writing anything. Null when the contract
    /// does not exist; throws when there is no balance to reduce.
    /// </summary>
    public async Task<EarlyPaymentPreview?> PreviewEarlyPaymentAsync(
        EarlyPaymentCommand cmd, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(cmd.ContractId, ct);
        if (contract is null) return null;

        var position = LoanMath.TryGetPosition(contract)
            ?? throw new InvalidOperationException(
                "This contract has no known balance. Enter the loan amount or the balance still owed first.");

        return LoanMath.PreviewEarlyPayment(position, cmd.Amount, cmd.PaidOn, cmd.Effect);
    }

    /// <summary>
    /// Records the lump sum as a paid <see cref="PaymentKind.Extra"/> and appends a revision
    /// that starts at the next instalment. Nothing already paid is touched.
    /// </summary>
    public async Task<Contract?> CommitEarlyPaymentAsync(
        EarlyPaymentCommand cmd, CancellationToken ct = default)
    {
        var contract = await _repo.GetWithDetailsAsync(cmd.ContractId, ct);
        if (contract is null) return null;

        var position = LoanMath.TryGetPosition(contract)
            ?? throw new InvalidOperationException(
                "This contract has no known balance. Enter the loan amount or the balance still owed first.");

        var preview = LoanMath.PreviewEarlyPayment(position, cmd.Amount, cmd.PaidOn, cmd.Effect);
        var revision = contract.ActiveRevision!;

        // The row's due date is what places it on the timeline and decides which revision's
        // replay picks it up. It must fall before the next instalment, or the new revision —
        // whose principal is already net of this payment — would count it a second time.
        var dueDate = cmd.PaidOn < position.NextDue ? cmd.PaidOn : position.NextDue.AddDays(-1);

        var extra = Payment.CreatePlanned(
            contractId:     contract.Id,
            dueDate:        dueDate,
            amountDue:      cmd.Amount,
            kind:           PaymentKind.Extra,
            planRevisionId: revision.Id,
            note:           cmd.Note);
        extra.MarkPaid(cmd.PaidOn, cmd.Amount);
        extra.SetLoanSplit(cmd.Amount, 0m);
        contract.AddPayment(extra);        // enforces the opening-position invariant

        if (preview.PaysOffEverything)
        {
            contract.SetStatus(ContractStatus.Ended);
        }
        else
        {
            contract.AddRevision(
                effectiveFrom:      position.NextDue,
                reason:             RevisionReason.EarlyPayment,
                firstDueDate:       position.NextDue,
                intervalMonths:     revision.IntervalMonths,
                installmentAmount:  preview.After.Installment,
                installmentCount:   preview.After.InstallmentsLeft,
                remainingPrincipal: preview.BalanceAfter,
                annualInterestRate: revision.AnnualInterestRate,
                residualAmount:     revision.ResidualAmount,
                residualDueDate:    revision.ResidualDueDate,
                note:               cmd.Note,
                adjustments:        revision.Adjustments.Select(a => PlanAdjustment.Create(a.Name, a.Amount)));
        }

        await _uow.SaveChangesAsync(ct);
        return contract;
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
        var today    = Today;
        var position = LoanMath.TryGetPosition(contract);

        var stored = contract.Payments
            .Where(p => p.DueDate >= from && p.DueDate <= to)
            .Select(p =>
            {
                // The split written at confirmation wins; the replay fills in for rows
                // confirmed before the rate was known.
                decimal? principal = p.PrincipalPart, interest = p.InterestPart;
                if (principal is null && position is not null
                    && position.Splits.TryGetValue(p.Id, out var split))
                {
                    (principal, interest) = (split.Principal, split.Interest);
                }

                return new ScheduleEntry(
                    ScheduleOrigin.Stored,
                    p.DueDate,
                    p.Status == PaymentStatus.Paid ? p.AmountPaid ?? p.AmountDue : p.AmountDue,
                    p.Status,
                    p.InstallmentNo,
                    p.Kind,
                    p.Id,
                    p.IsOverdue(today),
                    principal,
                    interest);
            })
            .ToList();

        var revision = contract.ActiveRevision;
        if (revision is null)
            return [.. stored.OrderBy(e => e.DueDate)];

        // A date already covered by a real instalment must not also appear as a projection —
        // that is the same double-counting the opening position guards against, one level
        // down. Only scheduled rows count: a lump sum on the same day covers nothing.
        var covered = stored
            .Where(e => e.Kind == PaymentKind.Scheduled)
            .Select(e => e.DueDate)
            .ToHashSet();

        var forward = position?.Forward.ToDictionary(r => r.DueDate)
                      ?? new Dictionary<DateOnly, AmortizationRow>();
        var payoff  = position is { Forward.Count: > 0 } ? position.Forward[^1].DueDate : (DateOnly?)null;

        var projected = new List<ScheduleEntry>();
        var amount    = revision.EffectiveInstallment;

        for (var n = 1; ; n++)
        {
            if (revision.IsPastEnd(n)) break;

            var due = revision.DueDateOf(n);
            if (due > to) break;
            if (payoff is { } last && due > last) break;      // the balance is gone before the count is
            if (due < from) continue;

            // Anything before the opening cut-off is already counted in summary.
            if (contract.Opening is { } opening && due < opening.AsOfDate) continue;
            if (covered.Contains(due)) continue;

            if (forward.TryGetValue(due, out var row))
            {
                projected.Add(new ScheduleEntry(
                    ScheduleOrigin.Projected, due, row.Payment + (position?.Adjustments ?? 0m),
                    null, n, PaymentKind.Scheduled, null,
                    IsOverdue: due < today,
                    Principal: row.Principal, Interest: row.Interest, BalanceAfter: row.BalanceAfter));
            }
            else
            {
                projected.Add(new ScheduleEntry(
                    ScheduleOrigin.Projected, due, amount, null, n, PaymentKind.Scheduled, null,
                    IsOverdue: due < today));
            }

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

        // A revision counts its instalments from its own first due date, so the total for
        // the contract is what was paid before it plus what it promises. For the first
        // revision the two coincide with the count on the plan.
        var paidInRevision = revision is null ? 0 : LoanMath.InstallmentsPaidIn(contract, revision);
        var total          = revision?.InstallmentCount is { } count
            ? installmentsPaid - paidInRevision + count
            : (int?)null;

        var position = LoanMath.TryGetPosition(contract);

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

        return new ContractSummary(
            ContractId:            contract.Id,
            Currency:              contract.Currency,
            PaidToDate:            paidToDate,
            InstallmentsPaid:      installmentsPaid,
            InstallmentsTotal:     total,
            InstallmentsRemaining: total is { } t ? Math.Max(t - installmentsPaid, 0) : null,
            RemainingBalance:      position?.Balance,
            CurrentInstallment:    revision?.EffectiveInstallment,
            NextDueDate:           next?.DueDate ?? nextProjected?.DueDate,
            NextDueAmount:         next?.AmountDue ?? nextProjected?.Amount,
            OverdueCount:          contract.Payments.Count(p => p.IsOverdue(today)),
            InterestPaidToDate:    InterestPaidToDate(contract, position, paidToDate, paidRows),
            InterestRemaining:     position is { RateKnown: true } ? position.Forward.Sum(r => r.Interest) : null,
            TotalCost:             position is null
                                       ? null
                                       : paidToDate + position.Forward.Sum(r => r.Payment) + position.Residual,
            PayoffDate:            position is { Forward.Count: > 0 } ? position.Forward[^1].DueDate : null);
    }

    /// <summary>
    /// Everything paid, minus the part that reduced the principal, minus fees. It needs no
    /// per-payment history, which is what lets it cover the years folded into an opening
    /// position — but it does need the original principal, so a loan entered by balance
    /// alone honestly reports nothing.
    /// </summary>
    private static decimal? InterestPaidToDate(
        Contract contract, LoanPosition? position, decimal paidToDate, List<Payment> paidRows)
    {
        if (position is not { RateKnown: true }) return null;

        var original = contract.Revisions.FirstOrDefault(r => r.Version == 1)?.RemainingPrincipal;
        if (original is not { } principal0) return null;

        var fees      = paidRows.Where(p => p.Kind == PaymentKind.Fee).Sum(p => p.AmountPaid ?? p.AmountDue);
        var principalRepaid = principal0 - position.Balance;

        return Math.Clamp(paidToDate - fees - principalRepaid, 0m, paidToDate);
    }
}
