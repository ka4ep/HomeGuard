using FluentAssertions;
using HomeGuard.Application.Services;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using HomeGuard.Domain.ValueObjects;
using Xunit;

namespace HomeGuard.Tests.Unit;

/// <summary>
/// The two things that make a contract's numbers trustworthy: the schedule must not show
/// the same instalment twice, and a contract entered halfway through its life must report
/// the same totals as one tracked from the start.
/// </summary>
public sealed class ContractScheduleTests
{
    private static readonly DateOnly Start = new(2026, 1, 15);

    private static Contract Loan(int installments = 60, decimal amount = 250m)
    {
        var c = Contract.Create(ContractKind.Loan, "Cupra Born", Start, "EUR");
        c.AddRevision(
            effectiveFrom:     Start,
            reason:            RevisionReason.Initial,
            firstDueDate:      Start,
            intervalMonths:    1,
            installmentAmount: amount,
            installmentCount:  installments);
        return c;
    }

    // ── Projection ───────────────────────────────────────────────────────────

    [Fact]
    public void Schedule_projects_one_entry_per_interval()
    {
        var contract = Loan();

        var schedule = ContractService.BuildSchedule(contract, Start, Start.AddMonths(5));

        schedule.Should().HaveCount(6);                       // месяцы 0..5 включительно
        schedule.Should().OnlyContain(e => e.Origin == ScheduleOrigin.Projected);
        schedule.Select(e => e.DueDate).Should().BeInAscendingOrder();
        schedule[3].DueDate.Should().Be(Start.AddMonths(3));
    }

    [Fact]
    public void Schedule_stops_at_the_last_instalment()
    {
        var contract = Loan(installments: 3);

        var schedule = ContractService.BuildSchedule(contract, Start, Start.AddYears(5));

        schedule.Should().HaveCount(3);
    }

    [Fact]
    public void Adjustments_are_added_to_the_projected_amount()
    {
        var c = Contract.Create(ContractKind.Subscription, "Netflix", Start, "EUR");
        c.AddRevision(
            effectiveFrom: Start, reason: RevisionReason.Initial, firstDueDate: Start,
            intervalMonths: 1, installmentAmount: 12.99m, installmentCount: 3,
            adjustments: [PlanAdjustment.Create("4K", 4m), PlanAdjustment.Create("Скидка", -2m)]);

        var schedule = ContractService.BuildSchedule(c, Start, Start.AddMonths(2));

        schedule.Should().OnlyContain(e => e.Amount == 14.99m);
    }

    [Fact]
    public void A_stored_payment_replaces_the_projection_for_its_date()
    {
        var contract = Loan();
        var payment  = Payment.CreatePlanned(contract.Id, Start.AddMonths(1), 250m, installmentNo: 2);
        payment.MarkPaid(Start.AddMonths(1), 260m);           // заплатили чуть больше
        contract.AddPayment(payment);

        var schedule = ContractService.BuildSchedule(contract, Start, Start.AddMonths(2));

        schedule.Should().HaveCount(3);
        var second = schedule.Single(e => e.DueDate == Start.AddMonths(1));
        second.Origin.Should().Be(ScheduleOrigin.Stored);
        second.Amount.Should().Be(260m);
        second.Status.Should().Be(PaymentStatus.Paid);
    }

    // ── Opening position ─────────────────────────────────────────────────────

    [Fact]
    public void Schedule_skips_everything_the_opening_position_already_covers()
    {
        var contract = Loan();
        contract.SetOpening(OpeningPosition.Create(Start.AddMonths(3), 3, 750m, 14_250m));

        var schedule = ContractService.BuildSchedule(contract, Start, Start.AddMonths(5));

        schedule.Should().HaveCount(3);                       // месяцы 3, 4, 5
        schedule.Should().OnlyContain(e => e.DueDate >= Start.AddMonths(3));
    }

    [Fact]
    public void A_payment_before_the_opening_cut_off_is_refused()
    {
        var contract = Loan();
        contract.SetOpening(OpeningPosition.Create(Start.AddMonths(3), 3, 750m));

        var tooEarly = Payment.CreatePlanned(contract.Id, Start.AddMonths(1), 250m);

        contract.Invoking(c => c.AddPayment(tooEarly))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*opening position*");
    }

    [Fact]
    public void An_opening_position_later_than_a_recorded_payment_is_refused()
    {
        var contract = Loan();
        contract.AddPayment(Payment.CreatePlanned(contract.Id, Start.AddMonths(1), 250m));

        contract.Invoking(c => c.SetOpening(OpeningPosition.Create(Start.AddMonths(3), 3, 750m)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rebasing_moves_the_cut_off_back_and_shrinks_the_counters_together()
    {
        var contract = Loan();
        contract.SetOpening(OpeningPosition.Create(Start.AddMonths(4), 4, 1000m, 14_000m));

        contract.RebaseOpening(Start.AddMonths(2), installmentsMovedOut: 2, amountMovedOut: 500m);

        contract.Opening!.AsOfDate.Should().Be(Start.AddMonths(2));
        contract.Opening.InstallmentsPaid.Should().Be(2);
        contract.Opening.AmountPaid.Should().Be(500m);
        contract.Opening.RemainingBalance.Should().Be(14_000m);   // остаток не трогаем

        // Освободившееся место можно заполнить настоящими платежами.
        contract.Invoking(c => c.AddPayment(
                Payment.CreatePlanned(c.Id, Start.AddMonths(2), 250m)))
            .Should().NotThrow();
    }

    // ── Summary ──────────────────────────────────────────────────────────────

    [Fact]
    public void Summary_folds_the_opening_position_into_the_totals()
    {
        var contract = Loan(installments: 60);
        contract.SetOpening(OpeningPosition.Create(Start.AddMonths(3), 3, 750m, 14_250m));

        var paid = Payment.CreatePlanned(contract.Id, Start.AddMonths(3), 250m, installmentNo: 4);
        paid.MarkPaid(Start.AddMonths(3));
        contract.AddPayment(paid);

        var summary = ContractService.BuildSummary(contract);

        summary.PaidToDate.Should().Be(1000m);            // 750 свёрнутых + 250 записанный
        summary.InstallmentsPaid.Should().Be(4);
        summary.InstallmentsTotal.Should().Be(60);
        summary.InstallmentsRemaining.Should().Be(56);
        summary.RemainingBalance.Should().Be(14_250m);
        summary.CurrentInstallment.Should().Be(250m);
        summary.Currency.Should().Be("EUR");
    }

    // ── Correcting a payment ─────────────────────────────────────────────────

    [Fact]
    public void Reopening_a_payment_takes_it_back_out_of_the_totals()
    {
        var contract = Loan();
        var payment  = Payment.CreatePlanned(contract.Id, Start, 250m, installmentNo: 1);
        contract.AddPayment(payment);

        payment.MarkPaid(Start, 260m);
        ContractService.BuildSummary(contract).PaidToDate.Should().Be(260m);

        payment.Reopen();

        var summary = ContractService.BuildSummary(contract);
        summary.PaidToDate.Should().Be(0m);
        summary.InstallmentsPaid.Should().Be(0);
        payment.Status.Should().Be(PaymentStatus.Planned);
        payment.PaidDate.Should().BeNull();
        payment.AmountPaid.Should().BeNull();
    }

    [Fact]
    public void Rescheduling_moves_the_row_on_the_schedule()
    {
        var contract = Loan();
        var payment  = Payment.CreatePlanned(contract.Id, Start, 250m, installmentNo: 1);
        contract.AddPayment(payment);

        payment.Reschedule(Start.AddDays(10), 275m);

        var entry = ContractService.BuildSchedule(contract, Start, Start.AddMonths(1))
            .Single(e => e.Origin == ScheduleOrigin.Stored);

        entry.DueDate.Should().Be(Start.AddDays(10));
        entry.Amount.Should().Be(275m);
    }

    [Fact]
    public void An_overdue_planned_payment_is_counted_as_overdue()
    {
        var contract = Loan();
        var longAgo  = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-2);
        contract.AddPayment(Payment.CreatePlanned(contract.Id, longAgo, 250m));

        ContractService.BuildSummary(contract).OverdueCount.Should().Be(1);
    }

    // ── Revisions ────────────────────────────────────────────────────────────

    [Fact]
    public void The_active_revision_is_the_last_one_appended()
    {
        var contract = Loan(amount: 250m);
        contract.AddRevision(
            effectiveFrom: Start.AddMonths(6), reason: RevisionReason.PriceChange,
            firstDueDate: Start.AddMonths(6), intervalMonths: 1,
            installmentAmount: 275m, installmentCount: 54);

        contract.Revisions.Should().HaveCount(2);
        contract.ActiveRevision!.Version.Should().Be(2);
        contract.ActiveRevision.EffectiveInstallment.Should().Be(275m);
    }

    [Fact]
    public void A_revision_cannot_take_effect_before_the_active_one()
    {
        var contract = Loan();
        contract.AddRevision(
            effectiveFrom: Start.AddMonths(6), reason: RevisionReason.PriceChange,
            firstDueDate: Start.AddMonths(6), intervalMonths: 1,
            installmentAmount: 275m, installmentCount: 54);

        contract.Invoking(c => c.AddRevision(
                effectiveFrom: Start.AddMonths(2), reason: RevisionReason.Correction,
                firstDueDate: Start.AddMonths(2), intervalMonths: 1,
                installmentAmount: 260m))
            .Should().Throw<InvalidOperationException>();
    }
}
