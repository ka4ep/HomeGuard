using HomeGuard.Domain.Entities;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IMeterReadingRepository : IRepository<MeterReading>
{
    /// <summary>All standalone readings for an equipment, newest first.</summary>
    Task<IReadOnlyList<MeterReading>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);
}
