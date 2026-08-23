using System.Globalization;
using HomeGuard.Common.Localization;
using Microsoft.JSInterop;

namespace HomeGuard.Client.Services;

/// <summary>
/// Owns which language the interface runs in.
/// <para>
/// Two copies exist and they are not redundant. The browser copy is what the app
/// starts in, before any request has been made — the culture has to be set before the
/// host runs, and a network round trip cannot happen that early. The account copy is
/// what the server reads when it renders something without a browser in front of it:
/// a push notification, an iCal feed. This type keeps them in step.
/// </para>
/// </summary>
public sealed class LanguagePreference(IJSRuntime js, AuthApiClient auth)
{
    public const string StorageKey = "homeguard.language";

    /// <summary>
    /// The language to start in, resolved before the Blazor host runs: the device's own
    /// choice if it has one, otherwise what the browser asks for, otherwise the default.
    /// </summary>
    public static async Task<string> ResolveStartupAsync(IJSRuntime js)
    {
        var stored = await TryInvokeAsync<string?>(js, "homeGuardPrefs.get", StorageKey);
        if (AppLanguage.IsSupported(stored)) return AppLanguage.Normalize(stored);

        var browser = await TryInvokeAsync<string?>(js, "homeGuardPrefs.browserLanguage");
        return AppLanguage.Normalize(browser);
    }

    /// <summary>Applies a language to the current process. Call once, before the host runs.</summary>
    public static void Apply(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentCulture   = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>The language currently applied to this process.</summary>
    public static string Current => AppLanguage.Normalize(CultureInfo.CurrentUICulture.Name);

    /// <summary>
    /// Records the choice on the device and on the account, then reloads — the culture
    /// is fixed for the lifetime of the WebAssembly host, so applied strings and date
    /// formats only change on a fresh start.
    /// </summary>
    public async Task ChangeAsync(string language, CancellationToken ct = default)
    {
        var normalized = AppLanguage.Normalize(language);
        if (normalized == Current) return;

        await js.InvokeVoidAsync("homeGuardPrefs.set", ct, StorageKey, normalized);
        await auth.SetLanguageAsync(normalized, ct);
        await js.InvokeVoidAsync("homeGuardPrefs.reload", ct);
    }

    /// <summary>
    /// Called once the session is known. On a device this account has not used before,
    /// the account's language is the right answer and the device has no opinion yet —
    /// adopt it, which costs one reload the first time and nothing afterwards.
    /// </summary>
    public async Task ReconcileWithAccountAsync(string? accountLanguage, CancellationToken ct = default)
    {
        if (!AppLanguage.IsSupported(accountLanguage)) return;

        var fromAccount = AppLanguage.Normalize(accountLanguage);
        var onDevice    = await TryInvokeAsync<string?>(js, "homeGuardPrefs.get", StorageKey);

        // The device has made its own choice — that wins, and it was already pushed to
        // the account when it was made. Nothing to do.
        if (AppLanguage.IsSupported(onDevice)) return;

        await js.InvokeVoidAsync("homeGuardPrefs.set", ct, StorageKey, fromAccount);
        if (fromAccount != Current)
            await js.InvokeVoidAsync("homeGuardPrefs.reload", ct);
    }

    /// <summary>
    /// Interop that must not take the app down: at startup the helper script may not
    /// have run yet, and on a locked-down browser storage throws outright.
    /// </summary>
    private static async Task<T?> TryInvokeAsync<T>(IJSRuntime js, string identifier, params object?[] args)
    {
        try { return await js.InvokeAsync<T>(identifier, args); }
        catch { return default; }
    }
}
