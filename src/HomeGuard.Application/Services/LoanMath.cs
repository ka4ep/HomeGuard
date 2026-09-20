using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

/// <summary>What an early payment buys: a shorter loan, or a smaller instalment.</summary>
public enum EarlyPaymentEffect
{
    ReduceTerm = 0,
    ReducePayment = 1,
}

/// <summary>One instalment of an amortisation table.</summary>
public sealed record AmortizationRow(
    int No,
    DateOnly DueDate,
    decimal Payment,
    decimal Interest,
    decimal Principal,
    decimal BalanceAfter);

/// <summary>
/// Where a loan or lease stands right now: what is owed, what the next instalment is, and
/// how the remaining ones split between interest and principal.
/// </summary>
public sealed record LoanPosition(
    decimal Balance,
    bool RateKnown,
    decimal PeriodRate,
    int IntervalMonths,
    decimal Installment,
    decimal Adjustments,
    decimal Residual,
    int? InstallmentsLeft,
    DateOnly NextDue,
    IReadOnlyList<AmortizationRow> Forward,
    IReadOnlyDictionary<Guid, (decimal Principal, decimal Interest)> Splits);

/// <summary>The remaining life of a loan under one set of assumptions.</summary>
public sealed record LoanOutlook(
    int InstallmentsLeft,
    decimal Installment,
    DateOnly? PayoffDate,
    decimal InterestRemaining,
    decimal TotalRemaining);

/// <summary>The "was / will be" of an early payment, before anything is written.</summary>
public sealed record EarlyPaymentPreview(
    bool RateKnown,
    EarlyPaymentEffect Effect,
    decimal Amount,
    DateOnly PaidOn,
    DateOnly EffectiveFrom,
    decimal BalanceBefore,
    decimal BalanceAfter,
    bool PaysOffEverything,
    LoanOutlook Before,
    LoanOutlook After,
    decimal? InterestSaved,
    LoanEstimateGap Gap = LoanEstimateGap.None);

/// <summary>
/// The interest arithmetic, kept pure so it can be checked against a bank's own table.
/// <para>
/// The model is the standard nominal-rate annuity: the periodic rate is
/// <c>annual × interval / 12</c>, interest accrues per instalment on the balance left after
/// the previous one, and the final instalment absorbs whatever rounding is left. Real banks
/// count days; the difference is cents per instalment, and the household compares against
/// the statement anyway, so the statement's balance stays the thing that can be typed in.
/// </para>
/// </summary>
public static class LoanMath
{
    private const int MaxRows = 1200;

    public static decimal Round(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static decimal PeriodRate(decimal? annualRate, int intervalMonths)
        => (annualRate ?? 0m) * intervalMonths / 12m;

    /// <summary>
    /// The level instalment that repays <paramref name="principal"/> down to
    /// <paramref name="residual"/> in <paramref name="count"/> instalments.
    /// </summary>
    public static decimal Annuity(decimal principal, decimal periodRate, int count, decimal residual = 0m)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        if (periodRate == 0m)
            return Round((principal - residual) / count);

        var discount = Math.Pow((double)(1m + periodRate), -count);
        var factor   = (decimal)discount;
        return Round((principal - residual * factor) * periodRate / (1m - factor));
    }

    /// <summary>
    /// The table from <paramref name="balance"/> down to <paramref name="residual"/>.
    /// With a <paramref name="count"/> the last instalment clears whatever is left; without
    /// one the loan simply runs until the balance is gone.
    /// </summary>
    public static IReadOnlyList<AmortizationRow> Amortize(
        decimal balance,
        decimal periodRate,
        decimal installment,
        int? count,
        DateOnly firstDue,
        int intervalMonths,
        decimal residual = 0m)
    {
        var rows = new List<AmortizationRow>();
        var left = balance;

        for (var k = 1; k <= MaxRows; k++)
        {
            if (left <= residual) break;

            var interest = Round(left * periodRate);
            var isLast   = count is { } n && k == n;

            // An open-ended loan whose instalment does not even cover the interest never ends.
            if (!isLast && count is null && installment <= interest) break;

            var clearing = left - residual + interest;
            var payment  = isLast ? clearing : Math.Min(installment, clearing);
            var principal = payment - interest;

            left -= principal;
            rows.Add(new AmortizationRow(
                k, firstDue.AddMonths((k - 1) * intervalMonths),
                payment, interest, principal, left));

            if (isLast) break;
        }

        return rows;
    }

    // ── Where a contract stands ──────────────────────────────────────────────

    /// <summary>
    /// How many of the active revision's instalments are behind us. An early payment or a
    /// price change starts a new revision that counts from its own first due date, so only
    /// the first revision inherits the opening position's count.
    /// </summary>
    public static int InstallmentsPaidIn(Contract contract, PaymentPlanRevision revision)
    {
        var stored = contract.Payments.Count(p =>
            p.Status == PaymentStatus.Paid
            && p.Kind == PaymentKind.Scheduled
            && p.DueDate >= revision.FirstDueDate);

        return stored + (revision.Version == 1 ? contract.Opening?.InstallmentsPaid ?? 0 : 0);
    }

    /// <summary>
    /// The current position, or null when there is nothing to anchor a balance to — an
    /// insurance policy, a subscription, a loan entered with neither a principal nor a
    /// balance from the bank.
    /// </summary>
    public static LoanPosition? TryGetPosition(Contract contract)
    {
        var revision = contract.ActiveRevision;
        if (revision is null) return null;

        var opening    = contract.Opening;
        var periodRate = PeriodRate(revision.AnnualInterestRate, revision.IntervalMonths);
        var adjustments = revision.EffectiveInstallment - revision.InstallmentAmount;
        var residual   = revision.ResidualAmount ?? 0m;
        var paidIn     = InstallmentsPaidIn(contract, revision);

        var (balance, anchorDate) = Anchor(contract, revision, periodRate, residual, paidIn);
        if (balance is null) return null;

        // ── Replay every confirmed payment since the anchor ──────────────────
        var current = balance.Value;
        var splits  = new Dictionary<Guid, (decimal Principal, decimal Interest)>();

        var confirmed = contract.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.DueDate >= anchorDate)
            .OrderBy(p => p.DueDate)
            .ThenBy(p => p.InstallmentNo);

        foreach (var payment in confirmed)
        {
            var paid = payment.AmountPaid ?? payment.AmountDue;

            switch (payment.Kind)
            {
                case PaymentKind.Scheduled:
                {
                    // Add-ons and discounts ride along in the instalment but never touch the loan.
                    var toLoan   = Math.Max(paid - adjustments, 0m);
                    var interest = Math.Min(Round(current * periodRate), toLoan);
                    var principal = toLoan - interest;
                    current = Math.Max(current - principal, 0m);
                    splits[payment.Id] = (principal, interest);
                    break;
                }
                case PaymentKind.Extra:
                    current = Math.Max(current - paid, 0m);
                    splits[payment.Id] = (paid, 0m);
                    break;
            }
        }

        int? left = revision.InstallmentCount is { } total ? Math.Max(total - paidIn, 0) : null;
        var nextDue = revision.DueDateOf(paidIn + 1);

        IReadOnlyList<AmortizationRow> forward = left == 0
            ? []
            : Amortize(current, periodRate, revision.InstallmentAmount, left, nextDue,
                       revision.IntervalMonths, residual);

        return new LoanPosition(
            Balance:          current,
            RateKnown:        revision.AnnualInterestRate is > 0m,
            PeriodRate:       periodRate,
            IntervalMonths:   revision.IntervalMonths,
            Installment:      revision.InstallmentAmount,
            Adjustments:      adjustments,
            Residual:         residual,
            InstallmentsLeft: left,
            NextDue:          nextDue,
            Forward:          forward,
            Splits:           splits);
    }

    /// <summary>
    /// The most recent thing anyone actually knows about the balance: the bank's figure at
    /// the opening cut-off, or the principal of a revision that started after it. A first
    /// revision entered alongside an opening that carries no balance is walked forward
    /// from its principal by the instalments already paid.
    /// </summary>
    private static (decimal? Balance, DateOnly Date) Anchor(
        Contract contract, PaymentPlanRevision revision,
        decimal periodRate, decimal residual, int paidIn)
    {
        var opening = contract.Opening;

        if (revision.RemainingPrincipal is { } principal
            && (opening?.RemainingBalance is null || revision.EffectiveFrom >= opening.AsOfDate))
        {
            if (revision.Version == 1 && opening is { InstallmentsPaid: > 0 } && opening.RemainingBalance is null)
            {
                var done = Amortize(
                    principal, periodRate, revision.InstallmentAmount, revision.InstallmentCount,
                    revision.FirstDueDate, revision.IntervalMonths, residual);

                var walked = done.Count == 0
                    ? principal
                    : done[Math.Min(opening.InstallmentsPaid, done.Count) - 1].BalanceAfter;

                return (walked, opening.AsOfDate);
            }

            return (principal, revision.EffectiveFrom);
        }

        if (opening?.RemainingBalance is { } bankBalance)
            return (bankBalance, opening.AsOfDate);

        return (null, default);
    }

    // ── Early payment ────────────────────────────────────────────────────────

    public static EarlyPaymentPreview PreviewEarlyPayment(
        LoanPosition position, decimal amount, DateOnly paidOn, EarlyPaymentEffect effect)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "An early payment must be positive.");

        var before = Outlook(position.Forward, position.Residual);

        var balanceAfter = Math.Max(position.Balance - amount, 0m);
        var paysOff      = balanceAfter <= position.Residual;

        if (paysOff)
        {
            return new EarlyPaymentPreview(
                position.RateKnown, effect, amount, paidOn, position.NextDue,
                position.Balance, balanceAfter, true,
                before,
                new LoanOutlook(0, 0m, null, 0m, position.Residual),
                position.RateKnown ? before.InterestRemaining : null,
                GapOf(position));
        }

        IReadOnlyList<AmortizationRow> rows;

        if (effect == EarlyPaymentEffect.ReduceTerm)
        {
            if (Round(balanceAfter * position.PeriodRate) >= position.Installment)
                throw new InvalidOperationException(
                    "The instalment does not cover the interest on what would remain.");

            rows = Amortize(balanceAfter, position.PeriodRate, position.Installment, null,
                            position.NextDue, position.IntervalMonths, position.Residual);
        }
        else
        {
            if (position.InstallmentsLeft is not { } n || n < 1)
                throw new InvalidOperationException(
                    "There is no fixed number of instalments left to spread the balance over.");

            var reduced = Annuity(balanceAfter, position.PeriodRate, n, position.Residual);
            rows = Amortize(balanceAfter, position.PeriodRate, reduced, n,
                            position.NextDue, position.IntervalMonths, position.Residual);
        }

        var after = Outlook(rows, position.Residual);

        return new EarlyPaymentPreview(
            position.RateKnown, effect, amount, paidOn, position.NextDue,
            position.Balance, balanceAfter, false,
            before, after,
            position.RateKnown ? before.InterestRemaining - after.InterestRemaining : null,
            GapOf(position));
    }

    private static LoanEstimateGap GapOf(LoanPosition position)
        => position.RateKnown ? LoanEstimateGap.None : LoanEstimateGap.MissingRate;

    /// <summary>
    /// The rung below "no rate": a plan with no principal and no bank balance. Nothing can
    /// be recomputed, so the preview says so and shows the plan exactly as it stands.
    /// </summary>
    public static EarlyPaymentPreview PreviewWithoutBalance(
        Contract contract, decimal amount, DateOnly paidOn, EarlyPaymentEffect effect)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "An early payment must be positive.");

        var revision = contract.ActiveRevision
            ?? throw new InvalidOperationException("This contract has no active plan to pay against.");

        var paidIn = InstallmentsPaidIn(contract, revision);
        var left   = revision.InstallmentCount is { } total ? Math.Max(total - paidIn, 0) : 0;
        var same   = new LoanOutlook(
            left,
            revision.InstallmentAmount,
            revision.InstallmentCount is { } last ? revision.DueDateOf(last) : null,
            0m,
            left * revision.InstallmentAmount);

        return new EarlyPaymentPreview(
            RateKnown: false, effect, amount, paidOn, revision.DueDateOf(paidIn + 1),
            BalanceBefore: 0m, BalanceAfter: 0m, PaysOffEverything: false,
            same, same, InterestSaved: null, LoanEstimateGap.MissingBalance);
    }

    private static LoanOutlook Outlook(IReadOnlyList<AmortizationRow> rows, decimal residual)
    {
        if (rows.Count == 0)
            return new LoanOutlook(0, 0m, null, 0m, residual);

        return new LoanOutlook(
            InstallmentsLeft:  rows.Count,
            Installment:       rows[0].Payment,
            PayoffDate:        rows[^1].DueDate,
            InterestRemaining: rows.Sum(r => r.Interest),
            TotalRemaining:    rows.Sum(r => r.Payment) + residual);
    }
}
