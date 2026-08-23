using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IContractRepository : IRepository<Contract>
{
    /// <summary>
    /// The list view. Every filter is optional; passing none returns everything,
    /// which is the right default for a household with a few dozen contracts.
    /// </summary>
    Task<IReadOnlyList<Contract>> GetAllAsync(
        ContractKind? kind = null,
        ContractStatus? status = null,
        Guid? equipmentId = null,
        CancellationToken ct = default);

    /// <summary>The whole aggregate: revisions, payments, notification rules.</summary>
    Task<Contract?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Contract>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);

    /// <summary>
    /// Contracts whose end date — or, where one is set, whose cancellation deadline —
    /// falls within [fromDate, toDate]. The deadline is the date that actually matters: after it
    /// the contract renews whether or not anyone meant it to.
    /// </summary>
    Task<IReadOnlyList<Contract>> GetExpiringAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// Payments belong to the contract aggregate, but deleting one is a store operation:
    /// the row has to leave the set, not just the in-memory collection.
    /// </summary>
    void RemovePayment(Payment payment);

    /// <summary>Stored payments across all contracts, for the cross-contract views.</summary>
    Task<IReadOnlyList<Payment>> GetPaymentsDueAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>
    /// The same list with each payment's contract alongside it. The Home strip needs the
    /// contract's name and currency to say anything useful, and fetching them one by one
    /// would be a query per row.
    /// </summary>
    Task<IReadOnlyList<(Payment Payment, Contract Contract)>> GetPaymentsDueWithContractAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>
    /// Every active contract with its revisions and payments loaded — one query for the
    /// monthly cash-flow rollup instead of one per contract.
    /// </summary>
    Task<IReadOnlyList<Contract>> GetActiveWithSchedulesAsync(CancellationToken ct = default);
}
