using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateWarrantyCommand(
    Guid EquipmentId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider = null,
    string? ContractNumber = null,
    string? Notes = null
);

public sealed record UpdateWarrantyCommand(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider = null,
    string? ContractNumber = null,
    string? Notes = null
);

public sealed record SetNotificationRulesCommand(
    Guid WarrantyId,
    IReadOnlyList<(NotificationOffset Offset, bool Enabled)> Rules
);

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class WarrantyService
{
    private readonly IWarrantyRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IEnumerable<ICalendarProvider> _calendarProviders;

    public WarrantyService(
        IWarrantyRepository repo,
        IUnitOfWork uow,
        IEnumerable<ICalendarProvider> calendarProviders)
    {
        _repo = repo;
        _uow = uow;
        _calendarProviders = calendarProviders;
    }

    public Task<IReadOnlyList<Warranty>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _repo.GetByEquipmentAsync(equipmentId, ct);

    public Task<IReadOnlyList<Warranty>> GetExpiringAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => _repo.GetExpiringAsync(from, to, ct);

    public Task<IReadOnlyList<Warranty>> GetActiveAsync(CancellationToken ct = default)
        => _repo.GetActiveAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);

    public async Task<Warranty> CreateAsync(CreateWarrantyCommand cmd, CancellationToken ct = default)
    {
        var warranty = Warranty.Create(
            cmd.EquipmentId, cmd.Name,
            cmd.StartDate, cmd.EndDate,
            cmd.Provider, cmd.ContractNumber, cmd.Notes);

        await _repo.AddAsync(warranty, ct);
        await _uow.SaveChangesAsync(ct);

        // Fire-and-forget calendar sync — don't fail the create if calendar is unavailable.
        _ = SyncToCalendarsAsync(warranty, ct);

        return warranty;
    }

    public async Task<Warranty> UpdateAsync(UpdateWarrantyCommand cmd, CancellationToken ct = default)
    {
        var warranty = await _repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Warranty {cmd.Id} not found.");

        warranty.Update(cmd.Name, cmd.StartDate, cmd.EndDate,
            cmd.Provider, cmd.ContractNumber, cmd.Notes);

        await _uow.SaveChangesAsync(ct);
        _ = SyncToCalendarsAsync(warranty, ct);
        return warranty;
    }

    public async Task SetNotificationRulesAsync(SetNotificationRulesCommand cmd, CancellationToken ct = default)
    {
        var warranty = await _repo.GetByIdAsync(cmd.WarrantyId, ct)
            ?? throw new KeyNotFoundException($"Warranty {cmd.WarrantyId} not found.");

        warranty.SetNotificationRules(cmd.Rules);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var warranty = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Warranty {id} not found.");

        // Remove calendar events from all providers.
        if (warranty.GoogleCalendarEventId is not null)
        {
            foreach (var provider in _calendarProviders)
            {
                try { await provider.DeleteEventAsync(warranty.GoogleCalendarEventId, ct); }
                catch { /* log and continue */ }
            }
        }

        _repo.Remove(warranty);
        await _uow.SaveChangesAsync(ct);
    }

    // ── Calendar sync ────────────────────────────────────────────────────────

    private async Task SyncToCalendarsAsync(Warranty warranty, CancellationToken ct)
    {
        var evt = new CalendarEventDto(
            ExternalId: warranty.GoogleCalendarEventId,
            Title: $"Warranty expires: {warranty.Name}",
            Description: string.IsNullOrWhiteSpace(warranty.Provider)
                ? null
                : $"Provider: {warranty.Provider}",
            Date: warranty.Period.End,
            HomeGuardTag: $"homeguard:warranty:{warranty.Id}"
        );

        // Update the stored event ID from the first available provider.
        foreach (var provider in _calendarProviders)
        {
            try
            {
                var externalId = await provider.UpsertEventAsync(evt, ct);
                warranty.SetGoogleCalendarEventId(externalId);
                await _uow.SaveChangesAsync(ct);
                break;  // one provider is enough for event storage
            }
            catch { /* log and try next */ }
        }
    }
}
