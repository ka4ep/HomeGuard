using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HomeGuard.Api;

/// <summary>
/// Header-based auth for machine clients (ingestors, cron jobs) that cannot
/// complete a FIDO2 ceremony. The key comes from configuration "Auth:ApiKey"
/// (env <c>Auth__ApiKey</c>); when unset the scheme matches nothing, so machine
/// access is opt-in per deployment.
/// </summary>
internal sealed class ApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "ApiKey";
    internal const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = configuration["Auth:ApiKey"];
        if (string.IsNullOrEmpty(configured))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || provided.Count != 1)
            return Task.FromResult(AuthenticateResult.NoResult());

        var expected = Encoding.UTF8.GetBytes(configured);
        var actual   = Encoding.UTF8.GetBytes(provided[0] ?? "");
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "api-key-client"), new Claim("machine", "true")],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
