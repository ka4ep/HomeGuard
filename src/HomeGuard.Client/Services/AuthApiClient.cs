using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace HomeGuard.Client.Services;

public sealed class AuthApiClient
{
    private readonly HttpClient _http;
    public AuthApiClient(HttpClient http) => _http = http;

    // ── Setup probe ───────────────────────────────────────────────────────────

    /// <summary>Returns true when no users exist yet — Login page shows Register tab.</summary>
    public async Task<bool> IsSetupRequiredAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<SetupRequiredDto>("api/auth/setup-required", ct);
            return resp?.Required ?? false;
        }
        catch { return false; }
    }

    // ── Register ──────────────────────────────────────────────────────────────

    public async Task<string?> GetRegisterOptionsJsonAsync(
        string displayName, string deviceName, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            "api/auth/register/options",
            new { displayName, deviceName }, ct);

        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<AuthResult> CompleteRegisterAsync(
        string attestationJson, CancellationToken ct = default)
    {
        using var content = new StringContent(attestationJson, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("api/auth/register/complete", content, ct);

        if (!resp.IsSuccessStatusCode)
            return AuthResult.Fail(await resp.Content.ReadAsStringAsync(ct));

        var body = await resp.Content.ReadFromJsonAsync<AuthResultDto>(ct);
        return AuthResult.Ok(body?.DisplayName ?? "User");
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public async Task<string?> GetLoginOptionsJsonAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("api/auth/login/options", null, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<AuthResult> CompleteLoginAsync(
        string assertionJson, CancellationToken ct = default)
    {
        using var content = new StringContent(assertionJson, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("api/auth/login/complete", content, ct);

        if (!resp.IsSuccessStatusCode)
            return AuthResult.Fail(await resp.Content.ReadAsStringAsync(ct));

        var body = await resp.Content.ReadFromJsonAsync<AuthResultDto>(ct);
        return AuthResult.Ok(body?.DisplayName ?? "User");
    }

    // ── Session ───────────────────────────────────────────────────────────────

    public async Task<MeDto?> GetMeAsync(CancellationToken ct = default)
    {
        try { return await _http.GetFromJsonAsync<MeDto>("api/auth/me", ct); }
        catch { return null; }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
        => await _http.PostAsync("api/auth/logout", null, ct);

    /// <summary>
    /// Persists the interface language on the account. The browser keeps its own copy
    /// for the next cold start; this one is what push notifications and the calendar
    /// feed read, since they are rendered without a request to take a header from.
    /// </summary>
    public async Task<bool> SetLanguageAsync(string language, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(
                "api/auth/me/language", new { Language = language }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Credential (device) management ───────────────────────────────────────

    public async Task<List<CredentialDto>> GetCredentialsAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<CredentialDto>>(
                "api/auth/credentials", ct) ?? [];
        }
        catch { return []; }
    }

    /// <summary>
    /// Revokes a passkey. Returns false if this was the last credential
    /// (server refuses to leave the user locked out).
    /// </summary>
    public async Task<(bool Success, string? Error)> RevokeCredentialAsync(
        Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/auth/credentials/{id}", ct);
        if (resp.IsSuccessStatusCode) return (true, null);

        var body = await resp.Content.ReadAsStringAsync(ct);
        return (false, body);
    }

    // ── Add device ────────────────────────────────────────────────────────────

    public async Task<string?> GetAddDeviceOptionsJsonAsync(
        string deviceName, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            "api/auth/credentials/add-device/options",
            new { deviceName }, ct);

        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<AuthResult> CompleteAddDeviceAsync(
        string attestationJson, CancellationToken ct = default)
    {
        using var content = new StringContent(attestationJson, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("api/auth/credentials/add-device/complete", content, ct);

        if (!resp.IsSuccessStatusCode)
            return AuthResult.Fail(await resp.Content.ReadAsStringAsync(ct));

        return AuthResult.Ok("Device registered.");
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record AuthResult(bool Success, string? DisplayName, string? Error)
{
    public static AuthResult Ok(string displayName) => new(true, displayName, null);
    public static AuthResult Fail(string error)     => new(false, null, error);
}

public sealed record CredentialDto(
    Guid            Id,
    string          DeviceName,
    DateTimeOffset  CreatedAt,
    DateTimeOffset? LastUsedAt);

file sealed record AuthResultDto(string? DisplayName);
file sealed record SetupRequiredDto(bool Required);
public sealed record MeDto(string Id, string DisplayName, string Language);
