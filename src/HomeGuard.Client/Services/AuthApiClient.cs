using System.Net.Http.Json;
using System.Text.Json;

namespace HomeGuard.Client.Services;

public sealed class AuthApiClient
{
    private readonly HttpClient _http;
    public AuthApiClient(HttpClient http) => _http = http;

    // ── Register ──────────────────────────────────────────────────────────────

    /// <summary>Step 1: get challenge + options from server.</summary>
    public async Task<string?> GetRegisterOptionsJsonAsync(
        string displayName, string deviceName, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            "api/auth/register/options",
            new { displayName, deviceName }, ct);

        if (!resp.IsSuccessStatusCode) return null;

        // Return raw JSON — the browser WebAuthn API needs it as-is.
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Step 2: send attestation response, get session cookie.</summary>
    public async Task<AuthResult> CompleteRegisterAsync(
        string attestationJson, CancellationToken ct = default)
    {
        using var content = new StringContent(
            attestationJson,
            System.Text.Encoding.UTF8,
            "application/json");

        var resp = await _http.PostAsync("api/auth/register/complete", content, ct);

        if (!resp.IsSuccessStatusCode)
            return AuthResult.Fail(await resp.Content.ReadAsStringAsync(ct));

        var body = await resp.Content.ReadFromJsonAsync<AuthResultDto>(ct);
        return AuthResult.Ok(body?.DisplayName ?? "User");
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    /// <summary>Step 1: get assertion challenge from server.</summary>
    public async Task<string?> GetLoginOptionsJsonAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("api/auth/login/options", null, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Step 2: send assertion, get session cookie.</summary>
    public async Task<AuthResult> CompleteLoginAsync(
        string assertionJson, CancellationToken ct = default)
    {
        using var content = new StringContent(
            assertionJson,
            System.Text.Encoding.UTF8,
            "application/json");

        var resp = await _http.PostAsync("api/auth/login/complete", content, ct);

        if (!resp.IsSuccessStatusCode)
            return AuthResult.Fail(await resp.Content.ReadAsStringAsync(ct));

        var body = await resp.Content.ReadFromJsonAsync<AuthResultDto>(ct);
        return AuthResult.Ok(body?.DisplayName ?? "User");
    }

    // ── Me ────────────────────────────────────────────────────────────────────

    public async Task<AuthResultDto?> GetMeAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<AuthResultDto>("api/auth/me", ct);
        }
        catch { return null; }
    }

    public Task LogoutAsync(CancellationToken ct = default)
        => _http.PostAsync("api/auth/logout", null, ct);
}

public sealed record AuthResult(bool Success, string? DisplayName, string? Error)
{
    public static AuthResult Ok(string displayName)   => new(true, displayName, null);
    public static AuthResult Fail(string error)       => new(false, null, error);
}

public sealed record AuthResultDto(string Id, string DisplayName);
