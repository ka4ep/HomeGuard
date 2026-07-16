using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateMeterReadingCommand(
    Guid EquipmentId,
    DateOnly ReadingDate,
    decimal Value,
    MeterReadingSource Source = MeterReadingSource.Manual,
    string? Note = null
);

public sealed record UpdateMeterReadingCommand(
    Guid Id,
    DateOnly ReadingDate,
    decimal Value,
    string? Note = null
);

/// <summary>
/// One point of an equipment's merged usage history: either a standalone
/// <see cref="MeterReading"/> (editable, has its own Id) or a reading carried on a
/// completed <see cref="ServiceRecord"/> (Source = Service, Id points at the record,
/// edited through the record itself).
/// </summary>
public sealed record MeterReadingView(
    Guid Id,
    Guid EquipmentId,
    DateOnly ReadingDate,
    decimal Value,
    MeterReadingSource Source,
    string? Note,
    DateTimeOffset UpdatedAt
);

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class MeterReadingService
{
    private readonly IMeterReadingRepository _readings;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly IUnitOfWork _uow;

    public MeterReadingService(
        IMeterReadingRepository readings,
        IServiceRecordRepository serviceRecords,
        IUnitOfWork uow)
    {
        _readings = readings;
        _serviceRecords = serviceRecords;
        _uow = uow;
    }

    /// <summary>Merged history — standalone readings plus completed service records' readings, newest first.</summary>
    public async Task<IReadOnlyList<MeterReadingView>> GetByEquipmentAsync(
        Guid equipmentId, CancellationToken ct = default)
    {
        var standalone = await _readings.GetByEquipmentAsync(equipmentId, ct);
        var records    = await _serviceRecords.GetByEquipmentAsync(equipmentId, ct);

        var merged = standalone
            .Select(r => new MeterReadingView(
                r.Id, r.EquipmentId, r.ReadingDate, r.Value, r.Source, r.Note, r.UpdatedAt))
            .Concat(records
                .Where(sr => sr.Status == ServiceStatus.Completed && sr.MeterReading.HasValue)
                .Select(sr => new MeterReadingView(
                    sr.Id, sr.EquipmentId, sr.ServiceDate, sr.MeterReading!.Value,
                    MeterReadingSource.Service, sr.Title, sr.UpdatedAt)))
            .OrderByDescending(v => v.ReadingDate)
            .ThenByDescending(v => v.UpdatedAt)
            .ToList();

        return merged;
    }

    public async Task<MeterReading> CreateAsync(CreateMeterReadingCommand cmd, CancellationToken ct = default)
    {
        var reading = MeterReading.Create(
            cmd.EquipmentId, cmd.ReadingDate, cmd.Value, cmd.Source, cmd.Note);

        await _readings.AddAsync(reading, ct);
        await _uow.SaveChangesAsync(ct);
        return reading;
    }

    public async Task<MeterReading> UpdateAsync(UpdateMeterReadingCommand cmd, CancellationToken ct = default)
    {
        var reading = await _readings.GetByIdAsync(cmd.Id, ct)
            ?? throw new KeyNotFoundException($"MeterReading {cmd.Id} not found.");

        reading.Update(cmd.ReadingDate, cmd.Value, cmd.Note);
        await _uow.SaveChangesAsync(ct);
        return reading;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var reading = await _readings.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"MeterReading {id} not found.");

        _readings.Remove(reading);
        await _uow.SaveChangesAsync(ct);
    }
}
