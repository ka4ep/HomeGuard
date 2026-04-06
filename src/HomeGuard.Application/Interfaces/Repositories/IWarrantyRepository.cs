using HomeGuard.Domain.Entities;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IWarrantyRepository : IRepository<Warranty>
{
    Task<IReadOnlyList<Warranty>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);

    /// <summary>Active warranties whose end date falls within [from, to].</summary>
    Task<IReadOnlyList<Warranty>> GetExpiringAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>All warranties not yet expired as of <paramref name="asOf"/>.</summary>
    Task<IReadOnlyList<Warranty>> GetActiveAsync(DateOnly asOf, CancellationToken ct = default);

    Task<Warranty?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}
