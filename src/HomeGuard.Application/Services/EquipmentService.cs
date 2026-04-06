using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateEquipmentCommand(
    string Name,
    EquipmentCategory Category,
    DateOnly PurchaseDate,
    string? Brand = null,
    string? Model = null,
    string? SerialNumber = null,
    decimal? PurchasePrice = null,
    string? Notes = null,
    IEnumerable<string>? Tags = null
);

public sealed record UpdateEquipmentCommand(
    Guid Id,
    string Name,
    EquipmentCategory Category,
    DateOnly PurchaseDate,
    string? Brand = null,
    string? Model = null,
    string? SerialNumber = null,
    decimal? PurchasePrice = null,
    string? Notes = null
);

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class EquipmentService
{
    private readonly IEquipmentRepository _repo;
    private readonly IUnitOfWork _uow;

    public EquipmentService(IEquipmentRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken ct = default)
        => _repo.GetAllAsync(ct);

    public Task<Equipment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _repo.GetByIdAsync(id, ct);

    public Task<Equipment?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => _repo.GetWithDetailsAsync(id, ct);

    public async Task<Equipment> CreateAsync(CreateEquipmentCommand cmd, CancellationToken ct = default)
    {
        var equipment = Equipment.Create(
            cmd.Name, cmd.Category, cmd.PurchaseDate,
            cmd.Brand, cmd.Model, cmd.SerialNumber,
            cmd.PurchasePrice, cmd.Notes, cmd.Tags);

        await _repo.AddAsync(equipment, ct);
        await _uow.SaveChangesAsync(ct);
        return equipment;
    }

    public async Task<Equipment> UpdateAsync(UpdateEquipmentCommand cmd, CancellationToken ct = default)
    {
        var equipment = await _repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Equipment {cmd.Id} not found.");

        equipment.Update(
            cmd.Name, cmd.Category, cmd.PurchaseDate,
            cmd.Brand, cmd.Model, cmd.SerialNumber,
            cmd.PurchasePrice, cmd.Notes);

        await _uow.SaveChangesAsync(ct);
        return equipment;
    }

    public async Task SetTagsAsync(Guid id, IEnumerable<string> tags, CancellationToken ct = default)
    {
        var equipment = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Equipment {id} not found.");

        equipment.SetTags(tags);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var equipment = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Equipment {id} not found.");

        _repo.Remove(equipment);
        await _uow.SaveChangesAsync(ct);
    }
}
