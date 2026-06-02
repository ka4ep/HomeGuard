using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HomeGuard.Diagnostics;

/// <summary>
/// Masks sensitive values before writing configuration to logs.
/// Rules are conservative by design — when in doubt, mask.
/// </summary>
public static partial class ConfigMasker
{
    // Keys whose values should always be hidden
    private static readonly string[] SecretKeywords =
    [
        "password", "passwd", "pwd", "secret", "apikey", "api_key",
        "token", "connectionstring", "credential", "auth", "private",
        "cert", "pfx", "hmac", "signing", "encrypt", "salt", "hash"
    ];

    public static string MaskValue(string key, string? value)
    {
        if (value is null) return "(null)";
        if (value.Length == 0) return "(empty)";

        if (SecretKeywords.Any(k => key.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return value.Length > 6 ? $"*****{value[^2..]}" : "*****";

        return value;
    }

    /// <summary>
    /// Masks Password/Pwd segments inside a connection string,
    /// leaving host/database/username visible for diagnostics.
    /// </summary>
    public static string MaskConnectionString(string? cs)
    {
        if (string.IsNullOrEmpty(cs)) return "(not set)";
        return PasswordSegmentRegex().Replace(cs, m => $"{m.Groups[1].Value}=****");
    }

    /// <summary>
    /// Walks the whole IConfiguration tree and returns key/masked-value pairs.
    /// Section nodes (no leaf value) are skipped.
    /// </summary>
    public static IEnumerable<(string Key, string Value)> MaskAll(IConfiguration config) =>
        config
            .AsEnumerable()
            .Where(kv => kv.Value is not null)
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => (kv.Key, MaskValue(kv.Key, kv.Value)));

    [GeneratedRegex(@"(Password|Pwd)=([^;]*)", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordSegmentRegex();
}
