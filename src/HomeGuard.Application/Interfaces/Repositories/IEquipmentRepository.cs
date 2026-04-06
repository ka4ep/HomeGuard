using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Interfaces.Repositories;

public interface IEquipmentRepository : IRepository<Equipment>
{
    /// <summary>Loads equipment with Warranties, ServiceRecords and Attachments.</summary>
    Task<Equipment?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Equipment>> GetByCategoryAsync(EquipmentCategory category, CancellationToken ct = default);

    Task<IReadOnlyList<Equipment>> SearchByTagAsync(string tag, CancellationToken ct = default);
}
