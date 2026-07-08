using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeGuard.Client.Services;

public sealed class TimelineInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private string? _elementId;

    public TimelineInterop(IJSRuntime js) => _js = js;

    public async Task CreateAsync(
        string elementId,
        IEnumerable<TimelineItem> items,
        TimelineOptions? options = null,
        IEnumerable<TimelineGroup>? groups = null)
    {
        _elementId = elementId;

        var itemsJson = JsonSerializer.Serialize(items.Select(ToJsItem), Json.Options);
        var optionsJson = JsonSerializer.Serialize(options ?? TimelineOptions.Default, Json.Options);
        var groupsJson = groups is not null
            ? JsonSerializer.Serialize(groups.Select(ToJsGroup), Json.Options)
            : null;

        await _js.InvokeVoidAsync("homeGuardTimeline.create",
            elementId, itemsJson, optionsJson, groupsJson);
    }

    public async Task UpdateItemsAndGroupsAsync(
        IEnumerable<TimelineItem> items,
        IEnumerable<TimelineGroup>? groups = null)
    {
        if (_elementId is null) return;
        var itemsJson = JsonSerializer.Serialize(items.Select(ToJsItem), Json.Options);
        var groupsJson = groups is not null
            ? JsonSerializer.Serialize(groups.Select(ToJsGroup), Json.Options)
            : null;
        await _js.InvokeVoidAsync("homeGuardTimeline.updateItemsAndGroups",
            _elementId, itemsJson, groupsJson);
    }

    public Task FitAsync() => _js.InvokeVoidAsync("homeGuardTimeline.fit", _elementId).AsTask();
    public Task FocusTodayAsync() => _js.InvokeVoidAsync("homeGuardTimeline.focusToday", _elementId).AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_elementId is not null)
            await _js.InvokeVoidAsync("homeGuardTimeline.destroy", _elementId);
    }

    private static object ToJsItem(TimelineItem i) => new
    {
        id = i.Id,
        content = i.Content,
        start = i.Start.ToString("yyyy-MM-dd", null),
        end = i.End?.ToString("yyyy-MM-dd", null),
        group = i.Group,
        subgroup = i.Subgroup,
        className = i.ClassName,
        title = i.Tooltip,
    };

    private static object ToJsGroup(TimelineGroup g) => new
    {
        id = g.Id,
        content = g.Content,
        order = g.Order,
        nestedInGroup = g.NestedInGroup,   // null = toplevel, string id = дочерняя
    };
}

// ── Data records ──────────────────────────────────────────────────────────────

public sealed record TimelineItem(
    string Id,
    string Content,
    DateOnly Start,
    DateOnly? End = null,
    string? Group = null,
    string? Subgroup = null,
    string? ClassName = null,
    string? Tooltip = null
);

public sealed record TimelineGroup(
    string Id,
    string Content,
    int Order = 0,
    string? NestedInGroup = null
);

public sealed record TimelineOptions(
    [property: JsonPropertyName("min")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Min = null,

    [property: JsonPropertyName("max")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Max = null,

    [property: JsonPropertyName("height")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Height = null,

    bool Selectable = false,
    bool Zoomable = true,
    bool Moveable = true,
    bool Stack = true,
    string Orientation = "top"
)
{
    public static TimelineOptions Default => new();
}
