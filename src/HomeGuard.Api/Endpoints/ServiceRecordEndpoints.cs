using HomeGuard.Application.Services;
using HomeGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HomeGuard.Api.Endpoints;

public static class ServiceRecordEndpoints
{
    public static void MapServiceRecordEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/service-records")
            .WithTags("ServiceRecords")
            ;

        grp.MapGet("/overdue",                     GetOverdue);
        grp.MapGet("/due-soon",                    GetDueSoon);
        grp.MapGet("/by-equipment/{equipId:guid}", GetByEquipment);
        grp.MapPost("/",                           Create);
        grp.MapPut("/{id:guid}",                   Update);
        grp.MapPatch("/{id:guid}/notifications",   SetNotifications);
        grp.MapDelete("/{id:guid}",                Delete);
    }

    private static async Task<IResult> GetOverdue(
        ServiceRecordService svc, CancellationToken ct)
    {
        var list = await svc.GetOverdueAsync(ct);
        return Results.Ok(list.Select(ServiceRecordDto.From));
    }

    private static async Task<IResult> GetDueSoon(
        [FromQuery] int days, ServiceRecordService svc, CancellationToken ct)
    {
        var list = await svc.GetDueSoonAsync(days, ct);
        return Results.Ok(list.Select(ServiceRecordDto.From));
    }

    private static async Task<IResult> GetByEquipment(
        Guid equipId, ServiceRecordService svc, CancellationToken ct)
    {
        var list = await svc.GetByEquipmentAsync(equipId, ct);
        return Results.Ok(list.Select(ServiceRecordDto.From));
    }

    private static async Task<IResult> Create(
        [FromBody] CreateServiceRecordRequest req,
        ServiceRecordService svc, CancellationToken ct)
    {
        var cmd = new CreateServiceRecordCommand(
            req.EquipmentId, req.Title, req.ServiceDate, req.NextServiceDate,
            req.Cost, req.ServiceProvider, req.Notes, req.OdometerReading);

        var result = await svc.CreateAsync(cmd, ct);
        return Results.Created($"/api/service-records/{result.Id}", ServiceRecordDto.From(result));
    }

    private static async Task<IResult> Update(
        Guid id, [FromBody] UpdateServiceRecordRequest req,
        ServiceRecordService svc, CancellationToken ct)
    {
        try
        {
            var cmd = new UpdateServiceRecordCommand(
                id, req.Title, req.ServiceDate, req.NextServiceDate,
                req.Cost, req.ServiceProvider, req.Notes, req.OdometerReading);

            var result = await svc.UpdateAsync(cmd, ct);
            return Results.Ok(ServiceRecordDto.From(result));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> SetNotifications(
        Guid id, [FromBody] SetNotificationRulesRequest req,
        ServiceRecordService svc, CancellationToken ct)
    {
        try
        {
            await svc.SetNotificationRulesAsync(
                id, req.Rules.Select(r => (r.Offset, r.Enabled)).ToList(), ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> Delete(
        Guid id, ServiceRecordService svc, CancellationToken ct)
    {
        try
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }
}

// ── Request / response models ─────────────────────────────────────────────────

public sealed record CreateServiceRecordRequest(
    Guid EquipmentId,
    string Title,
    DateOnly ServiceDate,
    DateOnly? NextServiceDate = null,
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    string? OdometerReading = null
);

public sealed record UpdateServiceRecordRequest(
    string Title,
    DateOnly ServiceDate,
    DateOnly? NextServiceDate = null,
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    string? OdometerReading = null
);

public sealed record ServiceRecordDto(
    Guid Id,
    Guid EquipmentId,
    string Title,
    DateOnly ServiceDate,
    DateOnly? NextServiceDate,
    decimal? Cost,
    string? ServiceProvider,
    string? Notes,
    string? OdometerReading,
    bool IsOverdue,
    int? DaysUntilNextService,
    IReadOnlyList<NotificationRuleDto> NotificationRules,
    DateTimeOffset UpdatedAt)
{
    public static ServiceRecordDto From(Domain.Entities.ServiceRecord sr)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new(
            sr.Id, sr.EquipmentId, sr.Title,
            sr.ServiceDate, sr.NextServiceDate,
            sr.Cost, sr.ServiceProvider, sr.Notes, sr.OdometerReading,
            sr.IsOverdue(today), sr.DaysUntilNextService(today),
            sr.NotificationRules.Select(NotificationRuleDto.From).ToList(),
            sr.UpdatedAt);
    }
}
