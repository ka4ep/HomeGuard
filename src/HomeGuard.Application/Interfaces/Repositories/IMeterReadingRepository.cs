using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IMeterReadingRepository : IRepository<MeterReading>
{
    /// <summary>All standalone readings for an equipment, newest first.</summary>
    Task<IReadOnlyList<MeterReading>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default);

    /// <summary>Standalone reading for an equipment on a given date from a given source, if any.</summary>
    Task<MeterReading?> FindAsync(
        Guid equipmentId, DateOnly readingDate, MeterReadingSource source, CancellationToken ct = default);
}
