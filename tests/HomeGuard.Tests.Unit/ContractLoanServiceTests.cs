using FluentAssertions;
using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Application.Services;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using NSubstitute;
using Xunit;

namespace HomeGuard.Tests.Unit;

/// <summary>
/// What committing an early payment and confirming an instalment actually write down —
/// the parts the pure arithmetic tests cannot see.
/// </summary>
public sealed class ContractLoanServiceTests
{
    private static readonly DateOnly Start = new(2026, 1, 15);

    private readonly IContractRepository _repo = Substitute.For<IContractRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ContractService _svc;

    public ContractLoanServiceTests() => _svc = new ContractService(_repo, _uow);

    private Contract Loan(decimal principal = 10_000m, int count = 12)
    {
        var a = LoanMath.Annuity(principal, 0.01m, count);
        var c = Contract.Create(ContractKind.Loan, "Cupra Born", Start, "EUR");
        c.AddRevision(
            Start, RevisionReason.Initial, Start, 1, a, count,
            remainingPrincipal: principal, annualInterestRate: 0.12m,
            adjustments: [PlanAdjustment.Create("Insurance", 10m)]);

        _repo.GetWithDetailsAsync(c.Id, Arg.Any<CancellationToken>()).Returns(c);
        return c;
    }

    private static Payment PaidInstalment(Contract c, int no)
    {
        var due = Start.AddMonths(no - 1);
        var row = LoanMath.TryGetPosition(c)!.Forward[0];
        var p = Payment.CreatePlanned(c.Id, due, row.Payment + 10m, installmentNo: no);
        p.MarkPaid(due);
        c.AddPayment(p);
        return p;
    }

    [Fact]
    public async Task Committing_an_early_payment_records_the_lump_sum_and_a_new_revision()
    {
        var c = Loan();
        PaidInstalment(c, 1);
        var before = LoanMath.TryGetPosition(c)!;

        await _svc.CommitEarlyPaymentAsync(new EarlyPaymentCommand(
            c.Id, 3_000m, Start.AddDays(20), EarlyPaymentEffect.ReduceTerm), TestContext.Current.CancellationToken);

        var extra = c.Payments.Single(p => p.Kind == PaymentKind.Extra);
        extra.Status.Should().Be(PaymentStatus.Paid);
        extra.AmountPaid.Should().Be(3_000m);
        extra.PrincipalPart.Should().Be(3_000m);
        extra.InterestPart.Should().Be(0m);

        c.Revisions.Should().HaveCount(2);
        var revision = c.ActiveRevision!;
        revision.Reason.Should().Be(RevisionReason.EarlyPayment);
        revision.EffectiveFrom.Should().Be(before.NextDue);
        revision.RemainingPrincipal.Should().Be(before.Balance - 3_000m);
        revision.AnnualInterestRate.Should().Be(0.12m);
        revision.InstallmentAmount.Should().Be(before.Installment);       // the term shrank instead
        revision.InstallmentCount.Should().BeLessThan(before.InstallmentsLeft!.Value);
        revision.Adjustments.Should().ContainSingle(a => a.Name == "Insurance" && a.Amount == 10m);

        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_lump_sum_is_counted_once_after_the_new_revision_takes_over()
    {
        var c = Loan();
        PaidInstalment(c, 1);
        var expected = LoanMath.TryGetPosition(c)!.Balance - 3_000m;

        await _svc.CommitEarlyPaymentAsync(new EarlyPaymentCommand(
            c.Id, 3_000m, Start.AddDays(20), EarlyPaymentEffect.ReducePayment), TestContext.Current.CancellationToken);

        var after = LoanMath.TryGetPosition(c)!;

        after.Balance.Should().Be(expected);
        ContractService.BuildSummary(c).RemainingBalance.Should().Be(expected);
        ContractService.BuildSummary(c).InstallmentsPaid.Should().Be(1);
    }

    [Fact]
    public async Task A_payment_made_after_the_next_due_date_still_falls_before_the_new_revision()
    {
        var c = Loan();
        PaidInstalment(c, 1);
        var nextDue = LoanMath.TryGetPosition(c)!.NextDue;
        var expected = LoanMath.TryGetPosition(c)!.Balance - 3_000m;

        // Paid a week after the instalment date, with that instalment still unconfirmed.
        await _svc.CommitEarlyPaymentAsync(new EarlyPaymentCommand(
            c.Id, 3_000m, nextDue.AddDays(7), EarlyPaymentEffect.ReduceTerm), TestContext.Current.CancellationToken);

        c.Payments.Single(p => p.Kind == PaymentKind.Extra).DueDate.Should().Be(nextDue.AddDays(-1));
        LoanMath.TryGetPosition(c)!.Balance.Should().Be(expected);
    }

    [Fact]
    public async Task Paying_off_everything_ends_the_contract_without_a_new_revision()
    {
        var c = Loan();

        await _svc.CommitEarlyPaymentAsync(new EarlyPaymentCommand(
            c.Id, 10_000m, Start.AddDays(-1), EarlyPaymentEffect.ReduceTerm), TestContext.Current.CancellationToken);

        c.Status.Should().Be(ContractStatus.Ended);
        c.Revisions.Should().HaveCount(1);
        c.Payments.Should().ContainSingle(p => p.Kind == PaymentKind.Extra);
    }

    [Fact]
    public async Task An_early_payment_on_a_contract_with_no_balance_is_refused()
    {
        var c = Contract.Create(ContractKind.Insurance, "KASKO", Start, "EUR");
        c.AddRevision(Start, RevisionReason.Initial, Start, 12, 480m, 1);
        _repo.GetWithDetailsAsync(c.Id, Arg.Any<CancellationToken>()).Returns(c);

        await FluentActions
            .Awaiting(() => _svc.CommitEarlyPaymentAsync(new EarlyPaymentCommand(
                c.Id, 100m, Start, EarlyPaymentEffect.ReduceTerm)))
            .Should().ThrowAsync<InvalidOperationException>();

        c.Payments.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirming_an_instalment_writes_down_the_split_the_bank_applied()
    {
        var c = Loan();
        var row = LoanMath.TryGetPosition(c)!.Forward[0];
        var payment = Payment.CreatePlanned(c.Id, Start, row.Payment + 10m, installmentNo: 1);
        c.AddPayment(payment);
        _repo.GetPaymentAsync(payment.Id, Arg.Any<CancellationToken>()).Returns(payment);

        await _svc.ConfirmPaymentAsync(new ConfirmPaymentCommand(payment.Id, Start), TestContext.Current.CancellationToken);

        payment.InterestPart.Should().Be(row.Interest);           // 100.00 on 10 000 at 1 % a month
        payment.PrincipalPart.Should().Be(row.Principal);
    }

    [Fact]
    public void Reopening_a_confirmed_instalment_clears_its_split()
    {
        var c = Loan();
        var payment = PaidInstalment(c, 1);
        payment.SetLoanSplit(700m, 100m);

        payment.Reopen();

        payment.PrincipalPart.Should().BeNull();
        payment.InterestPart.Should().BeNull();
    }
}
