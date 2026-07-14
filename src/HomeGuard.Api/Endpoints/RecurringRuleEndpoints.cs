using HomeGuard.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomeGuard.Api.Endpoints;

public static class RecurringRuleEndpoints
{
    public static void MapRecurringRuleEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/recurring-rules")
            .WithTags("RecurringRules");

        grp.MapGet("/",                             GetAllWithPredictions);
        grp.MapGet("/by-equipment/{equipId:guid}",  GetByEquipment);
        grp.MapGet("/by-equipment/{equipId:guid}/by-title", FindByTitle);
        grp.MapGet("/{id:guid}",                    GetById);
        grp.MapPost("/",                            Create);
        grp.MapPut("/{id:guid}",                    Update);
        grp.MapPost("/{id:guid}/materialize",       MaterializeNow);
        grp.MapDelete("/{id:guid}",                 Delete);
    }

    private static async Task<IResult> GetAllWithPredictions(
        RecurringRuleService svc, CancellationToken ct)
    {
        var list = await svc.GetAllWithPredictionsAsync(ct);
        return Results.Ok(list.Select(RecurringRuleWithPredictionsDto.From));
    }

    private static async Task<IResult> GetByEquipment(
        Guid equipId, RecurringRuleService svc, CancellationToken ct)
    {
        var list = await svc.GetByEquipmentAsync(equipId, ct);
        return Results.Ok(list.Select(RecurringRuleDto.From));
    }

    private static async Task<IResult> FindByTitle(
        Guid equipId, [FromQuery] string title, RecurringRuleService svc, CancellationToken ct)
    {
        var rule = await svc.FindByTitleAsync(equipId, title, ct);
        return rule is null ? Results.NotFound() : Results.Ok(RecurringRuleDto.From(rule));
    }

    private static async Task<IResult> GetById(
        Guid id, RecurringRuleService svc, CancellationToken ct)
    {
        var rule = await svc.GetByIdAsync(id, ct);
        return rule is null ? Results.NotFound() : Results.Ok(RecurringRuleDto.From(rule));
    }

    private static async Task<IResult> Create(
        [FromBody] CreateRecurringRuleRequest req, RecurringRuleService svc, CancellationToken ct)
    {
        var cmd = new CreateRecurringRuleCommand(
            req.EquipmentId, req.Title, req.IntervalDays, req.IntervalMeter,
            req.MaterializeDaysAhead, req.PredictionsAhead, req.AnchorToPurchaseDate);

        var result = await svc.CreateAsync(cmd, ct);
        return Results.Created($"/api/recurring-rules/{result.Id}", RecurringRuleDto.From(result));
    }

    private static async Task<IResult> Update(
        Guid id, [FromBody] UpdateRecurringRuleRequest req, RecurringRuleService svc, CancellationToken ct)
    {
        try
        {
            var cmd = new UpdateRecurringRuleCommand(
                id, req.Title, req.IntervalDays, req.IntervalMeter,
                req.MaterializeDaysAhead, req.PredictionsAhead, req.AnchorToPurchaseDate, req.IsActive);

            var result = await svc.UpdateAsync(cmd, ct);
            return Results.Ok(RecurringRuleDto.From(result));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> MaterializeNow(
        Guid id, RecurringRuleService svc, CancellationToken ct)
    {
        try
        {
            var record = await svc.MaterializeNowAsync(id, ct);
            return record is null
                ? Results.UnprocessableEntity("Not enough history to predict a date yet.")
                : Results.Ok(ServiceRecordDto.From(record));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> Delete(
        Guid id, RecurringRuleService svc, CancellationToken ct)
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

public sealed record CreateRecurringRuleRequest(
    Guid EquipmentId,
    string Title,
    int? IntervalDays = null,
    decimal? IntervalMeter = null,
    int MaterializeDaysAhead = 30,
    int PredictionsAhead = 2,
    bool AnchorToPurchaseDate = true
);

public sealed record UpdateRecurringRuleRequest(
    string Title,
    int? IntervalDays = null,
    decimal? IntervalMeter = null,
    int MaterializeDaysAhead = 30,
    int PredictionsAhead = 2,
    bool AnchorToPurchaseDate = true,
    bool IsActive = true
);

public sealed record RecurringRuleDto(
    Guid Id,
    Guid EquipmentId,
    string Title,
    int? IntervalDays,
    decimal? IntervalMeter,
    int MaterializeDaysAhead,
    int PredictionsAhead,
    bool AnchorToPurchaseDate,
    bool IsActive,
    DateTimeOffset UpdatedAt)
{
    public static RecurringRuleDto From(Domain.Entities.RecurringRule r) => new(
        r.Id, r.EquipmentId, r.Title, r.IntervalDays, r.IntervalMeter,
        r.MaterializeDaysAhead, r.PredictionsAhead, r.AnchorToPurchaseDate, r.IsActive, r.UpdatedAt);
}

public sealed record PredictedEventDto(DateOnly Date, decimal? MeterReading)
{
    public static PredictedEventDto From(Application.Services.PredictedEvent e) => new(e.Date, e.MeterReading);
}

public sealed record RecurringRuleWithPredictionsDto(
    RecurringRuleDto Rule,
    IReadOnlyList<PredictedEventDto> Predictions)
{
    public static RecurringRuleWithPredictionsDto From(RecurringRuleWithPredictions x) => new(
        RecurringRuleDto.From(x.Rule),
        x.Predictions.Select(PredictedEventDto.From).ToList());
}
