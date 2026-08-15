using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;

namespace HomeGuard.Application.Services;

/// <summary>
/// Scans active contracts and turns near-term projected instalments into real Planned
/// Payments once they fall within <see cref="MaterializeDaysAhead"/> — 14 days, half of
/// the 30 used for service records, because a missed payment is more urgent than a
/// missed oil change (contracts-spec.md §3).
/// <para>
/// Designed to run once a day (via <c>IHostedService</c> timer in the Api project).
/// Idempotent for free: <see cref="ContractService.BuildSchedule"/> already excludes any
/// date a stored row covers, so a date materialized today simply will not reappear as a
/// projection tomorrow — no separate "already pending" check needed, unlike the
/// recurring-rule service this mirrors.
/// </para>
/// </summary>
public sealed class PaymentMaterializationService
{
    private const int MaterializeDaysAhead = 14;

    private readonly IContractRepository _contracts;
    private readonly IUnitOfWork _uow;

    public PaymentMaterializationService(IContractRepository contracts, IUnitOfWork uow)
    {
        _contracts = contracts;
        _uow = uow;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var today   = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(MaterializeDaysAhead);

        var contracts = await _contracts.GetActiveWithSchedulesAsync(ct);

        foreach (var contract in contracts)
        {
            if (contract.ActiveRevision is not { } revision) continue;

            var due = ContractService.BuildSchedule(contract, today, horizon)
                .Where(e => e.Origin == ScheduleOrigin.Projected);

            foreach (var entry in due)
            {
                var payment = Payment.CreatePlanned(
                    contractId:     contract.Id,
                    dueDate:        entry.DueDate,
                    amountDue:      entry.Amount,
                    kind:           entry.Kind,
                    planRevisionId: revision.Id,
                    installmentNo:  entry.InstallmentNo);

                contract.AddPayment(payment);
            }
        }

        await _uow.SaveChangesAsync(ct);
    }
}
