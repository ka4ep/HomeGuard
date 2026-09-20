using FluentAssertions;
using HomeGuard.Application.Services;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using HomeGuard.Domain.ValueObjects;
using Xunit;

namespace HomeGuard.Tests.Unit;

/// <summary>
/// The interest arithmetic against figures worked out independently, and the way the
/// contract aggregate feeds it: a loan's balance must follow the payments actually made.
/// </summary>
public sealed class LoanMathTests
{
    private static readonly DateOnly Start = new(2026, 1, 15);

    private static Contract Loan(
        decimal principal = 10_000m, decimal rate = 0.12m, int count = 12,
        decimal? installment = null, decimal? residual = null, ContractKind kind = ContractKind.Loan)
    {
        var i = LoanMath.PeriodRate(rate, 1);
        var amount = installment ?? LoanMath.Annuity(principal, i, count, residual ?? 0m);

        var c = Contract.Create(kind, "Cupra Born", Start, "EUR");
        c.AddRevision(
            effectiveFrom: Start, reason: RevisionReason.Initial, firstDueDate: Start,
            intervalMonths: 1, installmentAmount: amount, installmentCount: count,
            remainingPrincipal: principal, annualInterestRate: rate, residualAmount: residual);
        return c;
    }

    private static Payment Paid(Contract c, DateOnly due, decimal amount,
        PaymentKind kind = PaymentKind.Scheduled, int? no = null)
    {
        var p = Payment.CreatePlanned(c.Id, due, amount, kind, installmentNo: no);
        p.MarkPaid(due, amount);
        c.AddPayment(p);
        return p;
    }

    // ── The formula ──────────────────────────────────────────────────────────

    [Fact]
    public void Annuity_matches_the_textbook_figure()
    {
        // 10 000 at 12 % a year over 12 months: 888.4879… a month.
        LoanMath.Annuity(10_000m, 0.01m, 12).Should().Be(888.49m);
    }

    [Fact]
    public void Annuity_without_interest_is_a_plain_division()
    {
        LoanMath.Annuity(1_200m, 0m, 12).Should().Be(100m);
    }

    [Fact]
    public void Annuity_with_a_residual_leaves_exactly_the_residual_owing()
    {
        var a = LoanMath.Annuity(20_000m, 0.005m, 36, residual: 8_000m);
        var rows = LoanMath.Amortize(20_000m, 0.005m, a, 36, Start, 1, residual: 8_000m);

        rows.Should().HaveCount(36);
        rows[^1].BalanceAfter.Should().Be(8_000m);
        Math.Abs(rows[^1].Payment - a).Should().BeLessThan(1m);   // last one only absorbs rounding
    }

    [Fact]
    public void The_table_repays_the_whole_principal_and_ends_on_the_last_date()
    {
        var a    = LoanMath.Annuity(10_000m, 0.01m, 12);
        var rows = LoanMath.Amortize(10_000m, 0.01m, a, 12, Start, 1);

        rows.Should().HaveCount(12);
        rows.Sum(r => r.Principal).Should().Be(10_000m);
        rows[0].Interest.Should().Be(100m);                        // 10 000 × 1 %
        rows[^1].BalanceAfter.Should().Be(0m);
        rows[^1].DueDate.Should().Be(Start.AddMonths(11));
        rows.Select(r => r.Interest).Should().BeInDescendingOrder();
    }

    [Fact]
    public void A_quarterly_loan_uses_a_quarterly_rate()
    {
        LoanMath.PeriodRate(0.12m, 3).Should().Be(0.03m);
    }

    [Fact]
    public void An_open_ended_loan_that_never_covers_its_interest_produces_no_table()
    {
        LoanMath.Amortize(10_000m, 0.01m, 50m, null, Start, 1).Should().BeEmpty();
    }

    // ── Position ─────────────────────────────────────────────────────────────

    [Fact]
    public void Nothing_paid_yet_means_the_balance_is_the_principal()
    {
        var position = LoanMath.TryGetPosition(Loan())!;

        position.Balance.Should().Be(10_000m);
        position.InstallmentsLeft.Should().Be(12);
        position.NextDue.Should().Be(Start);
        position.RateKnown.Should().BeTrue();
        position.Forward.Should().HaveCount(12);
    }

    [Fact]
    public void An_insurance_policy_has_no_position()
    {
        var c = Contract.Create(ContractKind.Insurance, "KASKO", Start, "EUR");
        c.AddRevision(Start, RevisionReason.Initial, Start, 12, 480m, 1);

        LoanMath.TryGetPosition(c).Should().BeNull();
    }

    [Fact]
    public void Paying_the_scheduled_instalments_follows_the_table()
    {
        var c = Loan();
        var table = LoanMath.TryGetPosition(c)!.Forward;

        Paid(c, Start, table[0].Payment, no: 1);
        Paid(c, Start.AddMonths(1), table[1].Payment, no: 2);

        var position = LoanMath.TryGetPosition(c)!;

        position.Balance.Should().Be(table[1].BalanceAfter);
        position.InstallmentsLeft.Should().Be(10);
        position.NextDue.Should().Be(Start.AddMonths(2));
        position.Forward.Should().HaveCount(10);
        position.Splits.Values.Select(s => s.Interest).Should()
            .Equal(table[0].Interest, table[1].Interest);
    }

    [Fact]
    public void An_extra_payment_comes_straight_off_the_balance()
    {
        var c = Loan();
        var table = LoanMath.TryGetPosition(c)!.Forward;

        Paid(c, Start, table[0].Payment, no: 1);
        Paid(c, Start.AddDays(10), 2_000m, PaymentKind.Extra);

        LoanMath.TryGetPosition(c)!.Balance
            .Should().Be(table[0].BalanceAfter - 2_000m);
    }

    [Fact]
    public void The_banks_balance_at_the_cut_off_beats_the_principal()
    {
        var c = Loan();
        var cutOff = Start.AddMonths(6);
        c.SetOpening(OpeningPosition.Create(cutOff, 6, 5_000m, remainingBalance: 4_800m));

        LoanMath.TryGetPosition(c)!.Balance.Should().Be(4_800m);

        // One more instalment after the cut-off is applied on top of the bank's figure.
        var interest = LoanMath.Round(4_800m * 0.01m);
        Paid(c, cutOff, 888.49m, no: 7);

        LoanMath.TryGetPosition(c)!.Balance.Should().Be(4_800m - (888.49m - interest));
    }

    [Fact]
    public void An_opening_without_a_balance_walks_the_principal_forward()
    {
        var c = Loan();
        var table = LoanMath.TryGetPosition(c)!.Forward;
        c.SetOpening(OpeningPosition.Create(Start.AddMonths(3), 3, 3 * table[0].Payment));

        LoanMath.TryGetPosition(c)!.Balance.Should().Be(table[2].BalanceAfter);
    }

    [Fact]
    public void Without_a_rate_a_payment_simply_reduces_the_balance()
    {
        var c = Contract.Create(ContractKind.Loan, "Sofa", Start, "EUR");
        c.AddRevision(Start, RevisionReason.Initial, Start, 1, 100m, 12, remainingPrincipal: 1_200m);
        Paid(c, Start, 100m, no: 1);

        var position = LoanMath.TryGetPosition(c)!;

        position.RateKnown.Should().BeFalse();
        position.Balance.Should().Be(1_100m);
    }

    [Fact]
    public void Add_ons_in_the_instalment_never_touch_the_principal()
    {
        var c = Contract.Create(ContractKind.Loan, "Car", Start, "EUR");
        c.AddRevision(Start, RevisionReason.Initial, Start, 1, 100m, 12,
            remainingPrincipal: 1_200m,
            adjustments: [PlanAdjustment.Create("Insurance", 10m)]);
        Paid(c, Start, 110m, no: 1);                              // 100 to the loan, 10 to the add-on

        LoanMath.TryGetPosition(c)!.Balance.Should().Be(1_100m);
    }

    // ── Early payment ────────────────────────────────────────────────────────

    [Fact]
    public void Shortening_the_term_keeps_the_instalment_and_saves_interest()
    {
        var position = LoanMath.TryGetPosition(Loan())!;

        var preview = LoanMath.PreviewEarlyPayment(
            position, 4_000m, Start.AddDays(-5), EarlyPaymentEffect.ReduceTerm);

        preview.BalanceAfter.Should().Be(6_000m);
        preview.After.Installment.Should().Be(position.Installment);
        preview.After.InstallmentsLeft.Should().BeLessThan(preview.Before.InstallmentsLeft);
        preview.After.PayoffDate.Should().BeBefore(preview.Before.PayoffDate!.Value);
        preview.InterestSaved.Should().BeGreaterThan(0m);
        preview.PaysOffEverything.Should().BeFalse();
    }

    [Fact]
    public void Reducing_the_payment_keeps_the_term_and_saves_interest()
    {
        var position = LoanMath.TryGetPosition(Loan())!;

        var preview = LoanMath.PreviewEarlyPayment(
            position, 4_000m, Start.AddDays(-5), EarlyPaymentEffect.ReducePayment);

        preview.After.InstallmentsLeft.Should().Be(12);
        preview.After.Installment.Should().BeLessThan(position.Installment);
        preview.After.PayoffDate.Should().Be(preview.Before.PayoffDate);
        preview.InterestSaved.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Shortening_the_term_matches_the_closed_form()
    {
        // n' = −ln(1 − B'·i/A) / ln(1+i), rounded up — the formula from the design spec.
        var position = LoanMath.TryGetPosition(Loan())!;

        var preview = LoanMath.PreviewEarlyPayment(
            position, 4_000m, Start.AddDays(-5), EarlyPaymentEffect.ReduceTerm);

        var expected = (int)Math.Ceiling(
            -Math.Log(1 - (double)(6_000m * 0.01m / position.Installment)) / Math.Log(1.01));

        preview.After.InstallmentsLeft.Should().Be(expected);
    }

    [Fact]
    public void Without_a_rate_the_saving_is_not_claimed()
    {
        var c = Contract.Create(ContractKind.Loan, "Sofa", Start, "EUR");
        c.AddRevision(Start, RevisionReason.Initial, Start, 1, 100m, 12, remainingPrincipal: 1_200m);

        var preview = LoanMath.PreviewEarlyPayment(
            LoanMath.TryGetPosition(c)!, 400m, Start.AddDays(-1), EarlyPaymentEffect.ReduceTerm);

        preview.RateKnown.Should().BeFalse();
        preview.InterestSaved.Should().BeNull();
        preview.After.InstallmentsLeft.Should().Be(8);            // 800 left at 100 a month
    }

    [Fact]
    public void Paying_off_the_whole_balance_is_reported_as_such()
    {
        var position = LoanMath.TryGetPosition(Loan())!;

        var preview = LoanMath.PreviewEarlyPayment(
            position, 10_000m, Start.AddDays(-1), EarlyPaymentEffect.ReduceTerm);

        preview.PaysOffEverything.Should().BeTrue();
        preview.After.InstallmentsLeft.Should().Be(0);
        preview.BalanceAfter.Should().Be(0m);
    }

    [Fact]
    public void Reducing_the_payment_of_an_open_ended_loan_is_refused()
    {
        var c = Contract.Create(ContractKind.Loan, "Line of credit", Start, "EUR");
        c.AddRevision(Start, RevisionReason.Initial, Start, 1, 300m, null,
            remainingPrincipal: 5_000m, annualInterestRate: 0.06m);

        var position = LoanMath.TryGetPosition(c)!;

        FluentActions
            .Invoking(() => LoanMath.PreviewEarlyPayment(
                position, 1_000m, Start.AddDays(-1), EarlyPaymentEffect.ReducePayment))
            .Should().Throw<InvalidOperationException>();
    }

    // ── What the screens read ────────────────────────────────────────────────

    [Fact]
    public void Projected_rows_carry_the_split_and_the_running_balance()
    {
        var c = Loan();

        var first = ContractService.BuildSchedule(c, Start, Start.AddMonths(11))[0];

        first.Interest.Should().Be(100m);
        first.Principal.Should().Be(first.Amount - 100m);
        first.BalanceAfter.Should().Be(10_000m - first.Principal!.Value);
    }

    [Fact]
    public void A_lump_sum_on_an_instalment_date_does_not_hide_that_instalment()
    {
        var c = Loan();
        Paid(c, Start, 2_000m, PaymentKind.Extra);

        var schedule = ContractService.BuildSchedule(c, Start, Start);

        schedule.Should().HaveCount(2);
        schedule.Should().ContainSingle(e => e.Origin == ScheduleOrigin.Projected);
    }

    [Fact]
    public void The_summary_reports_balance_interest_and_payoff()
    {
        var c = Loan();
        var table = LoanMath.TryGetPosition(c)!.Forward;
        Paid(c, Start, table[0].Payment, no: 1);
        Paid(c, Start.AddMonths(1), table[1].Payment, no: 2);

        var summary = ContractService.BuildSummary(c);

        summary.RemainingBalance.Should().Be(table[1].BalanceAfter);
        summary.InterestPaidToDate.Should().Be(table[0].Interest + table[1].Interest);
        summary.InterestRemaining.Should().Be(table.Skip(2).Sum(r => r.Interest));
        summary.PayoffDate.Should().Be(table[^1].DueDate);
        summary.TotalCost.Should().Be(table.Sum(r => r.Payment));
    }

    [Fact]
    public void Interest_paid_covers_the_years_folded_into_the_opening_position()
    {
        var c = Loan();
        var table = LoanMath.TryGetPosition(c)!.Forward;
        var paidBefore = table.Take(6).Sum(r => r.Payment);
        c.SetOpening(OpeningPosition.Create(
            Start.AddMonths(6), 6, paidBefore, table[5].BalanceAfter));

        var summary = ContractService.BuildSummary(c);

        summary.InterestPaidToDate.Should().Be(table.Take(6).Sum(r => r.Interest));
    }

    [Fact]
    public void A_loan_entered_by_balance_alone_does_not_invent_the_interest_paid()
    {
        var c = Contract.Create(ContractKind.Loan, "Old loan", Start, "EUR");
        c.AddRevision(Start, RevisionReason.Initial, Start, 1, 300m, 24, annualInterestRate: 0.06m);
        c.SetOpening(OpeningPosition.Create(Start.AddMonths(6), 6, 1_800m, 4_500m));

        ContractService.BuildSummary(c).InterestPaidToDate.Should().BeNull();
    }

    [Fact]
    public void A_later_revision_counts_its_instalments_from_its_own_first_date()
    {
        var c = Loan();
        var table = LoanMath.TryGetPosition(c)!.Forward;
        Paid(c, Start, table[0].Payment, no: 1);
        Paid(c, Start.AddMonths(1), table[1].Payment, no: 2);

        // 8 left after an early payment: the new revision starts on the third instalment.
        c.AddRevision(Start.AddMonths(2), RevisionReason.EarlyPayment, Start.AddMonths(2), 1,
            600m, 8, remainingPrincipal: 4_000m, annualInterestRate: 0.12m);

        var summary = ContractService.BuildSummary(c);

        summary.InstallmentsPaid.Should().Be(2);
        summary.InstallmentsTotal.Should().Be(10);                // 2 before + 8 promised
        summary.InstallmentsRemaining.Should().Be(8);
        summary.RemainingBalance.Should().Be(4_000m);
    }
}
