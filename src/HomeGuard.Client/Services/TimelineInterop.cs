using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeGuard.Client.Services;

/// <summary>
/// JS interop for the self-contained HomeGuard timeline (see wwwroot/js/homeguard-timeline.js).
/// The JS side owns all rendering, pan/zoom/hover interaction and the detail card —
/// this class only ships row data across the interop boundary.
/// </summary>
public sealed class TimelineInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private string? _elementId;

    public TimelineInterop(IJSRuntime js) => _js = js;

    public async Task CreateAsync(string elementId, IEnumerable<TimelineRow> rows)
    {
        _elementId = elementId;
        var rowsJson = JsonSerializer.Serialize(rows, Json.Options);
        await _js.InvokeVoidAsync("homeGuardTimeline.create", elementId, rowsJson);
    }

    public async Task UpdateAsync(IEnumerable<TimelineRow> rows)
    {
        if (_elementId is null) return;
        var rowsJson = JsonSerializer.Serialize(rows, Json.Options);
        await _js.InvokeVoidAsync("homeGuardTimeline.update", _elementId, rowsJson);
    }

    public Task FitAsync() => _js.InvokeVoidAsync("homeGuardTimeline.fit", _elementId).AsTask();
    public Task FocusTodayAsync() => _js.InvokeVoidAsync("homeGuardTimeline.focusToday", _elementId).AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_elementId is not null)
            await _js.InvokeVoidAsync("homeGuardTimeline.destroy", _elementId);
    }
}

// ── Data records ──────────────────────────────────────────────────────────────

/// <summary>One label row on the timeline — a service cluster or a single warranty.</summary>
public sealed record TimelineRow(
    string Id,
    string Label,
    /// <summary>Equipment name, shown only above the first row of that equipment's cluster.</summary>
    string? Group,
    string Color,
    List<TimelineEvent> Events
);

/// <summary>One button on a row: a service record, or a warranty start/expiry point.</summary>
public sealed record TimelineEvent(
    string Id,
    [property: JsonPropertyName("date")] string DateIso,
    string Title,
    string EquipmentName,
    decimal? MeterReading = null,
    string? MeterUnit = null,
    decimal? Cost = null,
    string? ServiceProvider = null,
    string? Notes = null,
    /// <summary>"Completed" | "Planned" for service events; null for warranty events.</summary>
    string? Status = null,
    bool IsStart = false,
    bool IsExpiry = false
);

file static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
