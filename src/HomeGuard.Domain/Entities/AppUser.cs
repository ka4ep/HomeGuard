using HomeGuard.Domain.Common;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// A family member who can log in to HomeGuard.
/// Authentication is entirely via Passkeys (WebAuthn) — no passwords.
/// </summary>
public sealed class AppUser : Entity
{
    private AppUser() { }

    /// <summary>
    /// Friendly name shown in the UI, e.g. "Mom", "Alex", "Dad".
    /// Not used for authentication.
    /// </summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary>
    /// Interface language as an ISO 639-1 code, e.g. "ru" or "en".
    /// <para>
    /// Kept on the account, not only in the browser, because push notifications and
    /// the calendar feed are rendered on the server — where there is no request and
    /// therefore no <c>Accept-Language</c> to read.
    /// </para>
    /// </summary>
    public string Language { get; private set; } = null!;

    private readonly List<PasskeyCredential> _credentials = [];
    public IReadOnlyList<PasskeyCredential> Credentials => _credentials.AsReadOnly();

    // ── Factory ──────────────────────────────────────────────────────────────

    public static AppUser Create(string displayName, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var u = new AppUser();
        u.InitNew();
        u.DisplayName = displayName.Trim();
        u.Language    = NormalizeLanguage(language);
        return u;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
        Touch();
    }

    /// <summary>
    /// Which languages the UI actually ships in is a presentation decision
    /// (see <c>HomeGuard.Common.Localization.AppLanguage</c>), so the domain only
    /// insists on the shape of the code and leaves the choice to the caller.
    /// </summary>
    public void SetLanguage(string language)
    {
        var normalized = NormalizeLanguage(language);
        if (normalized == Language) return;
        Language = normalized;
        Touch();
    }

    private static string NormalizeLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var value = language.Trim().ToLowerInvariant();
        if (value.Length is < 2 or > 8)
            throw new ArgumentException(
                $"'{language}' is not a language code.", nameof(language));
        return value;
    }

    /// <summary>
    /// Attaches a newly created <see cref="PasskeyCredential"/> to this user.
    /// EF Core change tracking picks up the new entity via the navigation collection.
    /// Call before <c>SaveChangesAsync</c>.
    /// </summary>
    public void AddCredential(PasskeyCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credentials.Add(credential);
        Touch();
    }

    /// <summary>
    /// Removes a credential by its entity id.
    /// EF Core cascades the DELETE automatically (OnDelete: Cascade is configured).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Credential not found on this user.</exception>
    /// <exception cref="InvalidOperationException">
    /// Attempt to remove the last credential — would lock the user out.
    /// </exception>
    public void RemoveCredential(Guid credentialId)
    {
        if (_credentials.Count <= 1)
            throw new InvalidOperationException(
                "Cannot remove the last passkey — the user would be locked out.");

        var cred = _credentials.FirstOrDefault(c => c.Id == credentialId)
            ?? throw new KeyNotFoundException(
                $"Credential {credentialId} does not belong to user {Id}.");

        _credentials.Remove(cred);
        Touch();
    }
}

/// <summary>
/// A WebAuthn / Passkey credential registered for a user on a specific device.
/// One user can have credentials from multiple devices (phone, laptop, tablet).
/// </summary>
public sealed class PasskeyCredential : Entity
{
    private PasskeyCredential() { }

    public Guid UserId { get; private set; }

    /// <summary>Raw credential ID bytes as returned by WebAuthn registration.</summary>
    public byte[] CredentialId { get; private set; } = [];

    /// <summary>COSE-encoded public key for signature verification.</summary>
    public byte[] PublicKey { get; private set; } = [];

    /// <summary>
    /// Human-readable device label, e.g. "iPhone 16 Pro" or "Work laptop".
    /// Set by the user during registration.
    /// </summary>
    public string DeviceName { get; private set; } = null!;

    /// <summary>
    /// Monotonically increasing counter sent by the authenticator.
    /// Used to detect cloned credentials.
    /// </summary>
    public uint SignCount { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────

    public static PasskeyCredential Create(
        Guid userId,
        byte[] credentialId,
        byte[] publicKey,
        string deviceName,
        uint initialSignCount = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        if (credentialId.Length == 0) throw new ArgumentException("CredentialId must not be empty.");
        if (publicKey.Length == 0)   throw new ArgumentException("PublicKey must not be empty.");

        var c = new PasskeyCredential();
        c.InitNew();
        c.UserId       = userId;
        c.CredentialId = credentialId;
        c.PublicKey    = publicKey;
        c.DeviceName   = deviceName.Trim();
        c.SignCount     = initialSignCount;
        return c;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    /// <summary>Called on every successful authentication to update the counter and timestamp.</summary>
    public void RecordUse(uint newSignCount)
    {
        if (newSignCount < SignCount)
            throw new InvalidOperationException(
                $"Sign count decreased ({SignCount} → {newSignCount}). Possible cloned credential.");
        SignCount  = newSignCount;
        LastUsedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Rename(string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        DeviceName = deviceName.Trim();
        Touch();
    }
}
