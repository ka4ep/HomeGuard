namespace HomeGuard.Common.Localization;

/// <summary>
/// The languages the interface ships in.
/// <para>
/// Codes are ISO 639-1 and double as .NET culture names, so they can be handed
/// straight to <c>CultureInfo</c> and used as resource satellite suffixes.
/// </para>
/// <para>
/// This is a presentation concern, not a domain invariant — the domain stores
/// whatever code it is given, and this type decides what the UI can actually render.
/// </para>
/// </summary>
public static class AppLanguage
{
    public const string Russian = "ru";
    public const string English = "en";

    /// <summary>Used when nothing better is known — an unreadable header, an empty column.</summary>
    public const string Default = Russian;

    public static readonly IReadOnlyList<string> Supported = [Russian, English];

    public static bool IsSupported(string? code) =>
        code is not null && Supported.Contains(Trim(code));

    /// <summary>
    /// Reduces anything browser- or user-supplied ("ru-RU", "RU", "cs") to a code the
    /// UI can render. Never throws and never returns null.
    /// </summary>
    public static string Normalize(string? code)
    {
        var trimmed = Trim(code);
        return Supported.Contains(trimmed) ? trimmed : Default;
    }

    /// <summary>
    /// Picks the best supported language out of an <c>Accept-Language</c> header,
    /// honouring the q-values. Used at registration, when the account has no
    /// preference yet and the browser is the only evidence available.
    /// </summary>
    public static string FromAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return Default;

        var ranked = header
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseEntry)
            .Where(e => e.Quality > 0)
            .OrderByDescending(e => e.Quality);

        foreach (var entry in ranked)
            if (Supported.Contains(entry.Code))
                return entry.Code;

        return Default;
    }

    private static (string Code, double Quality) ParseEntry(string entry)
    {
        // "ru-RU;q=0.9" → ("ru", 0.9); a missing q means 1.0 per RFC 9110.
        var parts = entry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var code = Trim(parts[0]);

        var quality = 1.0;
        foreach (var part in parts.Skip(1))
            if (part.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(part.AsSpan(2), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var q))
                quality = q;

        return (code, quality);
    }

    /// <summary>"ru-RU" → "ru", "  EN " → "en". Region and case are irrelevant to us.</summary>
    private static string Trim(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var value = code.Trim().ToLowerInvariant();
        var dash = value.IndexOf('-');
        return dash > 0 ? value[..dash] : value;
    }
}
