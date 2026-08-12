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
    bool IsOverdue);

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
    int OverdueCount);

// ── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Reads and writes contracts, and answers the two questions every screen asks:
/// what does the schedule look like, and where does this contract stand.
/// <para>
/// Deliberately not here yet: amortisation and the early-payoff preview. Those need the
/// interest math and belong with the loan work, not with the plumbing.
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

        var stored = contract.Payments
            .Where(p => p.DueDate >= from && p.DueDate <= to)
            .Select(p => new ScheduleEntry(
                ScheduleOrigin.Stored,
                p.DueDate,
                p.Status == PaymentStatus.Paid ? p.AmountPaid ?? p.AmountDue : p.AmountDue,
                p.Status,
                p.InstallmentNo,
                p.Kind,
                p.Id,
                p.IsOverdue(today)))
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

            projected.Add(new ScheduleEntry(
                ScheduleOrigin.Projected, due, amount, null, n, PaymentKind.Scheduled, null,
                IsOverdue: due < today));

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
            OverdueCount:          contract.Payments.Count(p => p.IsOverdue(today)));
    }
}
