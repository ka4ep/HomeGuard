namespace HomeGuard.Client.Services;

/// <summary>
/// One shared answer to "who is signed in", so that the nav menu, the layout and any
/// page that needs the account all cost a single request to <c>/api/auth/me</c>.
/// </summary>
public sealed class SessionService(AuthApiClient auth)
{
    private Task<MeDto?>? _pending;

    /// <summary>
    /// Concurrent callers share one in-flight request rather than racing each other.
    /// Returns null when nobody is signed in.
    /// </summary>
    public Task<MeDto?> GetAsync(CancellationToken ct = default)
        => _pending ??= auth.GetMeAsync(ct);

    /// <summary>Drops the cached session — call after signing in or out.</summary>
    public void Invalidate() => _pending = null;
}
