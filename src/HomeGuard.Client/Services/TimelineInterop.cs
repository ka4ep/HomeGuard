using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeGuard.Client.Services;

/// <summary>
/// C# wrapper for the vis-timeline JS component.
/// Each <c>TimelinePage</c> creates one instance tied to a DOM element id.
/// </summary>
public sealed class TimelineInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private string? _elementId;

    public TimelineInterop(IJSRuntime js) => _js = js;

    public async Task CreateAsync(
        string elementId,
        IEnumerable<TimelineItem> items,
        TimelineOptions? options = null)
    {
        _elementId = elementId;

        var itemsJson = JsonSerializer.Serialize(items.Select(ToJs), Json.Options);
        var optionsJson = JsonSerializer.Serialize(options ?? TimelineOptions.Default, Json.Options);

        await _js.InvokeVoidAsync("homeGuardTimeline.create", elementId, itemsJson, optionsJson);
    }

    public async Task UpdateItemsAsync(IEnumerable<TimelineItem> items)
    {
        if (_elementId is null) return;
        var json = JsonSerializer.Serialize(items.Select(ToJs), Json.Options);
        await _js.InvokeVoidAsync("homeGuardTimeline.updateItems", _elementId, json);
    }

    public Task FitAsync()
        => _js.InvokeVoidAsync("homeGuardTimeline.fit", _elementId).AsTask();

    public Task FocusTodayAsync()
        => _js.InvokeVoidAsync("homeGuardTimeline.focusToday", _elementId).AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_elementId is not null)
            await _js.InvokeVoidAsync("homeGuardTimeline.destroy", _elementId);
    }

    // ── vis-timeline JSON shape ────────────────────────────────────────────────

    private static object ToJs(TimelineItem item) => new
    {
        id = item.Id,
        content = item.Content,
        start = item.Start.ToString("yyyy-MM-dd", null),
        end = item.End?.ToString("yyyy-MM-dd", null),
        group = item.Group,
        className = item.ClassName,
        title = item.Tooltip,
    };
}

// ── Data models ───────────────────────────────────────────────────────────────

public sealed record TimelineItem(
    string Id,
    string Content,
    DateOnly Start,
    DateOnly? End = null,
    string? Group = null,
    string? ClassName = null,
    string? Tooltip = null
);

public sealed record TimelineOptions(
    // CamelCase → "min" / "max" — именно так ждёт vis-timeline.
    // Было MinDate/MaxDate → camelCase давал "minDate"/"maxDate" → Unknown option.
    [property: JsonPropertyName("min")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Min         = null,

    [property: JsonPropertyName("max")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Max         = null,

    bool Selectable = false,
    bool Zoomable = true,
    bool Moveable = true,

    // Было string Stack = "true" → vis-timeline получал строку, ждёт булево.
    bool Stack = true,

    string Orientation = "top")
{
    public static TimelineOptions Default => new();
}
