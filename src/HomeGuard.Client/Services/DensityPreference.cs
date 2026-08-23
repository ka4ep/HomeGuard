using Microsoft.JSInterop;

namespace HomeGuard.Client.Services;

/// <summary>How a list surface lays its records out. See DESIGN.md, "Layout".</summary>
public enum DensityMode
{
    /// <summary>One card per record — room for a second object and a status line.</summary>
    Cards = 0,

    /// <summary>One row per record — more records in view, values compared down columns.</summary>
    List = 1,
}

/// <summary>Names of the surfaces that carry a density switch. Constants, not strings at call sites.</summary>
public static class DensitySurface
{
    public const string Equipment      = "equipment";
    public const string Warranties     = "warranties";
    public const string ServiceRecords = "service";
    public const string Contracts      = "contracts";
    public const string Payments       = "payments";
}

/// <summary>
/// Remembers the Cards / List choice per surface and per device.
/// <para>
/// Per device, because the answer genuinely differs: standing at the car with one hand,
/// a list wins; at the desk on a wide screen, cards do. So the default follows the
/// viewport rather than the account, and an explicit choice is remembered only for the
/// device that made it.
/// </para>
/// </summary>
public sealed class DensityPreference(IJSRuntime js)
{
    private const string KeyPrefix = "homeguard.density.";

    private bool? _wideViewport;

    /// <summary>The stored choice for this surface, or the viewport's default if none was made.</summary>
    public async Task<DensityMode> GetAsync(string surface, CancellationToken ct = default)
    {
        var stored = await TryInvokeAsync<string?>("homeGuardPrefs.get", ct, KeyPrefix + surface);

        return stored switch
        {
            nameof(DensityMode.Cards) => DensityMode.Cards,
            nameof(DensityMode.List)  => DensityMode.List,
            _                         => await DefaultAsync(ct),
        };
    }

    public async Task SetAsync(string surface, DensityMode mode, CancellationToken ct = default)
        => await TryInvokeVoidAsync("homeGuardPrefs.set", ct, KeyPrefix + surface, mode.ToString());

    /// <summary>Cards on a desktop, List on a phone — the breakpoint is DESIGN.md's 900px.</summary>
    private async Task<DensityMode> DefaultAsync(CancellationToken ct)
    {
        _wideViewport ??= await TryInvokeAsync<bool>("homeGuardPrefs.isWideViewport", ct);
        return _wideViewport is true ? DensityMode.Cards : DensityMode.List;
    }

    private async Task<T?> TryInvokeAsync<T>(string identifier, CancellationToken ct, params object?[] args)
    {
        try { return await js.InvokeAsync<T>(identifier, ct, args); }
        catch { return default; }
    }

    private async Task TryInvokeVoidAsync(string identifier, CancellationToken ct, params object?[] args)
    {
        try { await js.InvokeVoidAsync(identifier, ct, args); }
        catch { /* storage unavailable — the choice simply does not survive the session */ }
    }
}
