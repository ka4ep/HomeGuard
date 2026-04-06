using HomeGuard.Application.Services;
using HomeGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HomeGuard.Api.Endpoints;

public static class WarrantyEndpoints
{
    public static void MapWarrantyEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/warranties")
            .WithTags("Warranties")
            ;

        grp.MapGet("/active",                  GetActive);
        grp.MapGet("/expiring",                GetExpiring);
        grp.MapGet("/by-equipment/{equipId:guid}", GetByEquipment);
        grp.MapPost("/",                       Create);
        grp.MapPut("/{id:guid}",               Update);
        grp.MapPatch("/{id:guid}/notifications", SetNotifications);
        grp.MapDelete("/{id:guid}",            Delete);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetActive(WarrantyService svc, CancellationToken ct)
    {
        var list = await svc.GetActiveAsync(ct);
        return Results.Ok(list.Select(WarrantyDto.From));
    }

    private static async Task<IResult> GetExpiring(
        [FromQuery] int days, WarrantyService svc, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list  = await svc.GetExpiringAsync(today, today.AddDays(days), ct);
        return Results.Ok(list.Select(WarrantyDto.From));
    }

    private static async Task<IResult> GetByEquipment(
        Guid equipId, WarrantyService svc, CancellationToken ct)
    {
        var list = await svc.GetByEquipmentAsync(equipId, ct);
        return Results.Ok(list.Select(WarrantyDto.From));
    }

    private static async Task<IResult> Create(
        [FromBody] CreateWarrantyRequest req, WarrantyService svc, CancellationToken ct)
    {
        var cmd = new CreateWarrantyCommand(
            req.EquipmentId, req.Name, req.StartDate, req.EndDate,
            req.Provider, req.ContractNumber, req.Notes);

        var result = await svc.CreateAsync(cmd, ct);
        return Results.Created($"/api/warranties/{result.Id}", WarrantyDto.From(result));
    }

    private static async Task<IResult> Update(
        Guid id, [FromBody] UpdateWarrantyRequest req,
        WarrantyService svc, CancellationToken ct)
    {
        try
        {
            var cmd = new UpdateWarrantyCommand(
                id, req.Name, req.StartDate, req.EndDate,
                req.Provider, req.ContractNumber, req.Notes);

            var result = await svc.UpdateAsync(cmd, ct);
            return Results.Ok(WarrantyDto.From(result));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> SetNotifications(
        Guid id, [FromBody] SetNotificationRulesRequest req,
        WarrantyService svc, CancellationToken ct)
    {
        try
        {
            var cmd = new SetNotificationRulesCommand(
                id, req.Rules.Select(r => (r.Offset, r.Enabled)).ToList());

            await svc.SetNotificationRulesAsync(cmd, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> Delete(Guid id, WarrantyService svc, CancellationToken ct)
    {
        try
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }
}

// ── Request / response models ──────────────────────────────────────────────────

public sealed record CreateWarrantyRequest(
    Guid EquipmentId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider = null,
    string? ContractNumber = null,
    string? Notes = null
);

public sealed record UpdateWarrantyRequest(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider = null,
    string? ContractNumber = null,
    string? Notes = null
);

public sealed record NotificationRuleRequest(NotificationOffset Offset, bool Enabled);

public sealed record SetNotificationRulesRequest(
    IReadOnlyList<NotificationRuleRequest> Rules
);

public sealed record WarrantyDto(
    Guid Id,
    Guid EquipmentId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Provider,
    string? ContractNumber,
    string? Notes,
    bool IsActive,
    int DaysRemaining,
    IReadOnlyList<NotificationRuleDto> NotificationRules,
    DateTimeOffset UpdatedAt)
{
    public static WarrantyDto From(Domain.Entities.Warranty w)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new(
            w.Id, w.EquipmentId, w.Name,
            w.Period.Start, w.Period.End,
            w.Provider, w.ContractNumber, w.Notes,
            w.IsActive(today), w.DaysRemaining(today),
            w.NotificationRules.Select(NotificationRuleDto.From).ToList(),
            w.UpdatedAt);
    }
}

public sealed record NotificationRuleDto(string Offset, int OffsetDays, bool Enabled)
{
    public static NotificationRuleDto From(Domain.ValueObjects.NotificationRule r) =>
        new(r.Offset.ToString(), (int)r.Offset, r.IsEnabled);
}
