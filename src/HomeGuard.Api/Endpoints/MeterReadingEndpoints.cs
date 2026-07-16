using HomeGuard.Application.Services;
using HomeGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HomeGuard.Api.Endpoints;

public static class MeterReadingEndpoints
{
    public static void MapMeterReadingEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/meter-readings")
            .WithTags("MeterReadings");

        grp.MapGet("/by-equipment/{equipId:guid}", GetByEquipment);
        grp.MapPost("/",                           Create);
        grp.MapPut("/{id:guid}",                   Update);
        grp.MapDelete("/{id:guid}",                Delete);
    }

    private static async Task<IResult> GetByEquipment(
        Guid equipId, MeterReadingService svc, CancellationToken ct)
    {
        var list = await svc.GetByEquipmentAsync(equipId, ct);
        return Results.Ok(list.Select(MeterReadingDto.From));
    }

    private static async Task<IResult> Create(
        [FromBody] CreateMeterReadingRequest req, MeterReadingService svc, CancellationToken ct)
    {
        if (!Enum.TryParse<MeterReadingSource>(req.Source, ignoreCase: true, out var source)
            || source == MeterReadingSource.Service)
            return Results.BadRequest("Source must be Manual or Auto.");

        var cmd = new CreateMeterReadingCommand(
            req.EquipmentId, req.ReadingDate, req.Value, source, req.Note);

        var result = await svc.CreateAsync(cmd, ct);
        return Results.Created($"/api/meter-readings/{result.Id}", MeterReadingDto.From(result));
    }

    private static async Task<IResult> Update(
        Guid id, [FromBody] UpdateMeterReadingRequest req, MeterReadingService svc, CancellationToken ct)
    {
        try
        {
            var result = await svc.UpdateAsync(
                new UpdateMeterReadingCommand(id, req.ReadingDate, req.Value, req.Note), ct);
            return Results.Ok(MeterReadingDto.From(result));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> Delete(
        Guid id, MeterReadingService svc, CancellationToken ct)
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

public sealed record CreateMeterReadingRequest(
    Guid EquipmentId,
    DateOnly ReadingDate,
    decimal Value,
    string Source = "Manual",
    string? Note = null
);

public sealed record UpdateMeterReadingRequest(
    DateOnly ReadingDate,
    decimal Value,
    string? Note = null
);

public sealed record MeterReadingDto(
    Guid Id,
    Guid EquipmentId,
    DateOnly ReadingDate,
    decimal Value,
    string Source,
    string? Note,
    DateTimeOffset UpdatedAt)
{
    public static MeterReadingDto From(Domain.Entities.MeterReading r) => new(
        r.Id, r.EquipmentId, r.ReadingDate, r.Value, r.Source.ToString(), r.Note, r.UpdatedAt);

    public static MeterReadingDto From(MeterReadingView v) => new(
        v.Id, v.EquipmentId, v.ReadingDate, v.Value, v.Source.ToString(), v.Note, v.UpdatedAt);
}
