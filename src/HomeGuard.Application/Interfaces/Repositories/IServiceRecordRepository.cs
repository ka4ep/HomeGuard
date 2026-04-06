using HomeGuard.Domain.Entities;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IServiceRecordRepository : IRepository<ServiceRecord>
{
    Task<IReadOnlyList<ServiceRecord>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);

    /// <summary>Records whose NextServiceDate has already passed as of <paramref name="asOf"/>.</summary>
    Task<IReadOnlyList<ServiceRecord>> GetOverdueAsync(DateOnly asOf, CancellationToken ct = default);

    /// <summary>Records with NextServiceDate in [asOf, asOf + withinDays].</summary>
    Task<IReadOnlyList<ServiceRecord>> GetDueSoonAsync(DateOnly asOf, int withinDays, CancellationToken ct = default);

    Task<ServiceRecord?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}
