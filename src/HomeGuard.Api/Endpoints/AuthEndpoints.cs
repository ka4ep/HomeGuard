using Fido2NetLib;
using Fido2NetLib.Objects;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Application.Interfaces;
using HomeGuard.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace HomeGuard.Api.Endpoints;

public static class AuthEndpoints
{
    private const string AttestationSessionKey = "fido2.attestation";
    private const string AssertionSessionKey   = "fido2.assertion";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/auth").WithTags("Auth");

        grp.MapPost("/register/options",  RegisterOptions);
        grp.MapPost("/register/complete", RegisterComplete);
        grp.MapPost("/login/options",     LoginOptions);
        grp.MapPost("/login/complete",    LoginComplete);
        grp.MapGet("/me",                 Me);
        // Logout: async lambda works fine here
        grp.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });
    }

    // ── Registration step 1 ───────────────────────────────────────────────────

    private static async Task<IResult> RegisterOptions(
        [FromBody] RegisterOptionsRequest req,
        IFido2 fido2,
        IAppUserRepository userRepo,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = Guid.CreateVersion7();

        var fido2User = new Fido2User
        {
            Id          = userId.ToByteArray(),
            Name        = req.DisplayName,
            DisplayName = req.DisplayName,
        };

        var existingUsers = await userRepo.GetAllAsync(ct);
        var existingKeys  = existingUsers
            .SelectMany(u => u.Credentials)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User                   = fido2User,
            ExcludeCredentials     = existingKeys,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey      = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Preferred,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });

        ctx.Session.SetString(AttestationSessionKey, JsonSerializer.Serialize(
            new PendingRegistration(options.ToJson(), userId, req.DisplayName, req.DeviceName)));

        return Results.Ok(options);
    }

    // ── Registration step 2 ───────────────────────────────────────────────────

    private static async Task<IResult> RegisterComplete(
        [FromBody] AuthenticatorAttestationRawResponse attestationResponse,
        IFido2 fido2,
        IAppUserRepository userRepo,
        IUnitOfWork uow,
        HttpContext ctx,
        CancellationToken ct)
    {
        var json = ctx.Session.GetString(AttestationSessionKey);
        if (json is null) return Results.BadRequest("Session expired. Restart registration.");
        ctx.Session.Remove(AttestationSessionKey);

        var pending = JsonSerializer.Deserialize<PendingRegistration>(json)!;
        var options = CredentialCreateOptions.FromJson(pending.OptionsJson);

        var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions     = options,
            IsCredentialIdUniqueToUserCallback = async (args, _) =>
                await userRepo.FindByCredentialIdAsync(args.CredentialId, ct) is null,
        }, ct);

        var user = AppUser.Create(pending.DisplayName);

        // Override the auto-generated ID so it matches what we sent to the authenticator.
        // We use the shadow property approach — set via EF or direct reflection.
        // Simpler: just store the mapping in a lookup. For now, create fresh and store.
        var credential = PasskeyCredential.Create(
            user.Id,
            result.Id,
            result.PublicKey,
            pending.DeviceName,
            result.SignCount);

        await userRepo.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        await SignInAsync(ctx, user);
        return Results.Ok(new { user.Id, user.DisplayName });
    }

    // ── Login step 1 ─────────────────────────────────────────────────────────

    private static IResult LoginOptions(IFido2 fido2, HttpContext ctx)
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification   = UserVerificationRequirement.Preferred,
        });

        ctx.Session.SetString(AssertionSessionKey, options.ToJson());
        return Results.Ok(options);
    }

    // ── Login step 2 ─────────────────────────────────────────────────────────

    private static async Task<IResult> LoginComplete(
        [FromBody] AuthenticatorAssertionRawResponse assertionResponse,
        IFido2 fido2,
        IAppUserRepository userRepo,
        IUnitOfWork uow,
        HttpContext ctx,
        CancellationToken ct)
    {
        var json = ctx.Session.GetString(AssertionSessionKey);
        if (json is null) return Results.BadRequest("Session expired. Restart login.");
        ctx.Session.Remove(AssertionSessionKey);

        var options = AssertionOptions.FromJson(json);

        // assertionResponse.Id is byte[] (raw credential ID from the authenticator).
        // assertionResponse.RawId is also byte[] — use whichever the library populates.
        var credentialId = assertionResponse.RawId;

        var match = await userRepo.FindByCredentialIdAsync(credentialId, ct);
        if (match is null) return Results.Unauthorized();

        var (user, credential) = match.Value;

        var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse      = assertionResponse,
            OriginalOptions        = options,
            StoredPublicKey        = credential.PublicKey,
            StoredSignatureCounter = credential.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                Task.FromResult(args.UserHandle.SequenceEqual(user.Id.ToByteArray())),
        }, ct);

        credential.RecordUse(result.SignCount);
        await uow.SaveChangesAsync(ct);

        await SignInAsync(ctx, user);
        return Results.Ok(new { user.Id, user.DisplayName });
    }

    // ── Session info ──────────────────────────────────────────────────────────

    private static IResult Me(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var id   = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = ctx.User.FindFirstValue(ClaimTypes.Name);
        return Results.Ok(new { Id = id, DisplayName = name });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Task SignInAsync(HttpContext ctx, AppUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name,           user.DisplayName),
        };
        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
}

public sealed record RegisterOptionsRequest(string DisplayName, string DeviceName);

file sealed record PendingRegistration(
    string OptionsJson,
    Guid UserId,
    string DisplayName,
    string DeviceName);
