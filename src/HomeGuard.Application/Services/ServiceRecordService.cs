using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateServiceRecordCommand(
    Guid EquipmentId,
    string Title,
    DateOnly ServiceDate,
    DateOnly? NextServiceDate = null,
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    string? OdometerReading = null
);

public sealed record UpdateServiceRecordCommand(
    Guid Id,
    string Title,
    DateOnly ServiceDate,
    DateOnly? NextServiceDate = null,
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    string? OdometerReading = null
);

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class ServiceRecordService
{
    private readonly IServiceRecordRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IEnumerable<ICalendarProvider> _calendarProviders;

    public ServiceRecordService(
        IServiceRecordRepository repo,
        IUnitOfWork uow,
        IEnumerable<ICalendarProvider> calendarProviders)
    {
        _repo = repo;
        _uow = uow;
        _calendarProviders = calendarProviders;
    }

    public Task<IReadOnlyList<ServiceRecord>> GetByEquipmentAsync(
        Guid equipmentId, CancellationToken ct = default)
        => _repo.GetByEquipmentAsync(equipmentId, ct);

    public Task<IReadOnlyList<ServiceRecord>> GetOverdueAsync(CancellationToken ct = default)
        => _repo.GetOverdueAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);

    public Task<IReadOnlyList<ServiceRecord>> GetDueSoonAsync(
        int withinDays, CancellationToken ct = default)
        => _repo.GetDueSoonAsync(DateOnly.FromDateTime(DateTime.UtcNow), withinDays, ct);

    public async Task<ServiceRecord> CreateAsync(
        CreateServiceRecordCommand cmd, CancellationToken ct = default)
    {
        var sr = ServiceRecord.Create(
            cmd.EquipmentId, cmd.Title, cmd.ServiceDate, cmd.NextServiceDate,
            cmd.Cost, cmd.ServiceProvider, cmd.Notes, cmd.OdometerReading);

        await _repo.AddAsync(sr, ct);
        await _uow.SaveChangesAsync(ct);

        if (sr.NextServiceDate.HasValue)
            _ = SyncToCalendarsAsync(sr, ct);

        return sr;
    }

    public async Task<ServiceRecord> UpdateAsync(
        UpdateServiceRecordCommand cmd, CancellationToken ct = default)
    {
        var sr = await _repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new KeyNotFoundException($"ServiceRecord {cmd.Id} not found.");

        sr.Update(cmd.Title, cmd.ServiceDate, cmd.NextServiceDate,
            cmd.Cost, cmd.ServiceProvider, cmd.Notes, cmd.OdometerReading);

        await _uow.SaveChangesAsync(ct);

        if (sr.NextServiceDate.HasValue)
            _ = SyncToCalendarsAsync(sr, ct);

        return sr;
    }

    public async Task SetNotificationRulesAsync(
        Guid id,
        IEnumerable<(NotificationOffset offset, bool enabled)> rules,
        CancellationToken ct = default)
    {
        var sr = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"ServiceRecord {id} not found.");

        sr.SetNotificationRules(rules);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sr = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"ServiceRecord {id} not found.");

        if (sr.GoogleCalendarEventId is not null)
        {
            foreach (var provider in _calendarProviders)
            {
                try { await provider.DeleteEventAsync(sr.GoogleCalendarEventId, ct); }
                catch { /* log and continue */ }
            }
        }

        _repo.Remove(sr);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task SyncToCalendarsAsync(ServiceRecord sr, CancellationToken ct)
    {
        if (sr.NextServiceDate is null) return;

        var evt = new CalendarEventDto(
            ExternalId: sr.GoogleCalendarEventId,
            Title: $"Service due: {sr.Title}",
            Description: sr.ServiceProvider is not null
                ? $"Provider: {sr.ServiceProvider}"
                : null,
            Date: sr.NextServiceDate.Value,
            HomeGuardTag: $"homeguard:service:{sr.Id}"
        );

        foreach (var provider in _calendarProviders)
        {
            try
            {
                var externalId = await provider.UpsertEventAsync(evt, ct);
                sr.SetGoogleCalendarEventId(externalId);
                await _uow.SaveChangesAsync(ct);
                break;
            }
            catch { /* log and try next */ }
        }
    }
}
