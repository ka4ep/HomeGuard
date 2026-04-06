using HomeGuard.Application.Services;
using HomeGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HomeGuard.Api.Endpoints;

public static class EquipmentEndpoints
{
    public static void MapEquipmentEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/equipment")
            .WithTags("Equipment")
            ;

        grp.MapGet("/", GetAll);
        grp.MapGet("/{id:guid}", GetById);
        grp.MapPost("/", Create);
        grp.MapPut("/{id:guid}", Update);
        grp.MapPatch("/{id:guid}/tags", SetTags);
        grp.MapDelete("/{id:guid}", Delete);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetAll(EquipmentService svc, CancellationToken ct)
    {
        var list = await svc.GetAllAsync(ct);
        return Results.Ok(list.Select(EquipmentSummaryDto.From));
    }

    private static async Task<IResult> GetById(Guid id, EquipmentService svc, CancellationToken ct)
    {
        var item = await svc.GetWithDetailsAsync(id, ct);
        return item is null ? Results.NotFound() : Results.Ok(EquipmentDetailDto.From(item));
    }

    private static async Task<IResult> Create(
        [FromBody] CreateEquipmentRequest req, EquipmentService svc, CancellationToken ct)
    {
        var cmd = new CreateEquipmentCommand(
            req.Name, req.Category, req.PurchaseDate,
            req.Brand, req.Model, req.SerialNumber,
            req.PurchasePrice, req.Notes, req.Tags);

        var result = await svc.CreateAsync(cmd, ct);
        return Results.Created($"/api/equipment/{result.Id}", EquipmentSummaryDto.From(result));
    }

    private static async Task<IResult> Update(
        Guid id, [FromBody] UpdateEquipmentRequest req,
        EquipmentService svc, CancellationToken ct)
    {
        try
        {
            var cmd = new UpdateEquipmentCommand(
                id, req.Name, req.Category, req.PurchaseDate,
                req.Brand, req.Model, req.SerialNumber,
                req.PurchasePrice, req.Notes);

            var result = await svc.UpdateAsync(cmd, ct);
            return Results.Ok(EquipmentSummaryDto.From(result));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> SetTags(
        Guid id, [FromBody] SetTagsRequest req,
        EquipmentService svc, CancellationToken ct)
    {
        try
        {
            await svc.SetTagsAsync(id, req.Tags, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }

    private static async Task<IResult> Delete(Guid id, EquipmentService svc, CancellationToken ct)
    {
        try
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }
}

// ── Request models ────────────────────────────────────────────────────────────

public sealed record CreateEquipmentRequest(
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

public sealed record UpdateEquipmentRequest(
    string Name,
    EquipmentCategory Category,
    DateOnly PurchaseDate,
    string? Brand = null,
    string? Model = null,
    string? SerialNumber = null,
    decimal? PurchasePrice = null,
    string? Notes = null
);

public sealed record SetTagsRequest(IReadOnlyList<string> Tags);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record EquipmentSummaryDto(
    Guid Id,
    string Name,
    string Category,
    DateOnly PurchaseDate,
    string? Brand,
    string? Model,
    IReadOnlyList<string> Tags,
    DateTimeOffset UpdatedAt)
{
    public static EquipmentSummaryDto From(Domain.Entities.Equipment e) => new(
        e.Id, e.Name, e.Category.ToString(), e.PurchaseDate,
        e.Brand, e.Model, e.Tags, e.UpdatedAt);
}

public sealed record EquipmentDetailDto(
    Guid Id,
    string Name,
    string Category,
    DateOnly PurchaseDate,
    string? Brand,
    string? Model,
    string? SerialNumber,
    decimal? PurchasePrice,
    string? Notes,
    IReadOnlyList<string> Tags,
    int WarrantyCount,
    int ServiceRecordCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EquipmentDetailDto From(Domain.Entities.Equipment e) => new(
        e.Id, e.Name, e.Category.ToString(), e.PurchaseDate,
        e.Brand, e.Model, e.SerialNumber, e.PurchasePrice, e.Notes,
        e.Tags, e.Warranties.Count, e.ServiceRecords.Count,
        e.CreatedAt, e.UpdatedAt);
}
