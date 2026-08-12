using Fido2NetLib;
using Fido2NetLib.Objects;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Application.Interfaces;
using HomeGuard.Common.Localization;
using HomeGuard.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace HomeGuard.Api.Endpoints;

public static class AuthEndpoints
{
    private const string AttestationSessionKey  = "fido2.attestation";
    private const string AssertionSessionKey    = "fido2.assertion";
    private const string AddDeviceSessionKey    = "fido2.add-device";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/auth").WithTags("Auth");

        // ── Open (no auth required) ───────────────────────────────────────────
        grp.MapGet ("/setup-required",        SetupRequired);
        grp.MapPost("/register/options",      RegisterOptions);
        grp.MapPost("/register/complete",     RegisterComplete);
        grp.MapPost("/login/options",         LoginOptions);
        grp.MapPost("/login/complete",        LoginComplete);
        grp.MapGet ("/me",                    Me);
        grp.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        // ── Requires authentication ───────────────────────────────────────────
        grp.MapPut   ("/me/language",                     SetLanguage)       .RequireAuthorization();
        grp.MapGet   ("/credentials",                     ListCredentials)   .RequireAuthorization();
        grp.MapDelete("/credentials/{id:guid}",           RevokeCredential)  .RequireAuthorization();
        grp.MapPost  ("/credentials/add-device/options",  AddDeviceOptions)  .RequireAuthorization();
        grp.MapPost  ("/credentials/add-device/complete", AddDeviceComplete) .RequireAuthorization();
    }

    // ── Setup probe ───────────────────────────────────────────────────────────
    // Lets the login page decide whether to show the Register tab.

    private static async Task<IResult> SetupRequired(
        IAppUserRepository userRepo, CancellationToken ct)
    {
        var users = await userRepo.GetAllAsync(ct);
        return Results.Ok(new { Required = users.Count == 0 });
    }

    // ── Registration step 1 ───────────────────────────────────────────────────

    private static async Task<IResult> RegisterOptions(
        [FromBody] RegisterOptionsRequest req,
        IFido2 fido2,
        IAppUserRepository userRepo,
        HttpContext ctx,
        CancellationToken ct)
    {
        var existingUsers = await userRepo.GetAllAsync(ct);

        // Gate: registration is only open when the server has no users yet.
        // To add a new device for an existing user → use /credentials/add-device.
        // To invite a new family member → an admin flow can be built later.
        if (existingUsers.Count > 0)
            return Results.Problem(
                title:      "Registration closed",
                detail:     "HomeGuard already has registered users. " +
                            "Sign in and add a new device from Settings.",
                statusCode: StatusCodes.Status403Forbidden);

        var userId = Guid.CreateVersion7();

        var fido2User = new Fido2User
        {
            Id          = userId.ToByteArray(),
            Name        = req.DisplayName,
            DisplayName = req.DisplayName,
        };

        // On first registration there are no existing credentials to exclude.
        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User                   = fido2User,
            ExcludeCredentials     = [],
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

        // No preference exists yet, so the browser is the only evidence of what
        // language this person reads. They can change it in Settings afterwards.
        var user = AppUser.Create(
            pending.DisplayName,
            AppLanguage.FromAcceptLanguage(ctx.Request.Headers.AcceptLanguage));

        var credential = PasskeyCredential.Create(
            user.Id,
            result.Id,
            result.PublicKey,
            pending.DeviceName,
            result.SignCount);

        // AddCredential puts it into the navigation collection so EF tracks it
        // alongside the user in a single SaveChangesAsync call.
        user.AddCredential(credential);
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

        var options      = AssertionOptions.FromJson(json);
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

    private static async Task<IResult> Me(
        IAppUserRepository userRepo,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        // The language lives on the row, not in the cookie, so that changing it takes
        // effect without re-issuing the sign-in ticket.
        var user = await LoadCurrentUserAsync(userRepo, ctx, ct);
        if (user is null) return Results.Unauthorized();

        return Results.Ok(new MeDto(user.Id, user.DisplayName, user.Language));
    }

    // ── Language preference ───────────────────────────────────────────────────

    private static async Task<IResult> SetLanguage(
        [FromBody] SetLanguageRequest req,
        IAppUserRepository userRepo,
        IUnitOfWork uow,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (!AppLanguage.IsSupported(req.Language))
            return Results.BadRequest(
                $"'{req.Language}' is not one of the supported languages " +
                $"({string.Join(", ", AppLanguage.Supported)}).");

        var user = await LoadCurrentUserAsync(userRepo, ctx, ct);
        if (user is null) return Results.Unauthorized();

        user.SetLanguage(AppLanguage.Normalize(req.Language));
        await uow.SaveChangesAsync(ct);

        return Results.Ok(new MeDto(user.Id, user.DisplayName, user.Language));
    }

    // ── Credential list ───────────────────────────────────────────────────────

    private static async Task<IResult> ListCredentials(
        IAppUserRepository userRepo,
        HttpContext ctx,
        CancellationToken ct)
    {
        var user = await LoadCurrentUserAsync(userRepo, ctx, ct);
        if (user is null) return Results.Unauthorized();

        var dtos = user.Credentials
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CredentialDto(c.Id, c.DeviceName, c.CreatedAt, c.LastUsedAt));

        return Results.Ok(dtos);
    }

    // ── Revoke credential ─────────────────────────────────────────────────────

    private static async Task<IResult> RevokeCredential(
        Guid id,
        IAppUserRepository userRepo,
        IUnitOfWork uow,
        HttpContext ctx,
        CancellationToken ct)
    {
        var user = await LoadCurrentUserAsync(userRepo, ctx, ct);
        if (user is null) return Results.Unauthorized();

        try
        {
            user.RemoveCredential(id);
            await uow.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // Last credential — would lock user out.
            return Results.Problem(
                title: "Cannot revoke last passkey",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    // ── Add device step 1 ─────────────────────────────────────────────────────

    private static async Task<IResult> AddDeviceOptions(
        [FromBody] AddDeviceRequest req,
        IFido2 fido2,
        IAppUserRepository userRepo,
        HttpContext ctx,
        CancellationToken ct)
    {
        var user = await LoadCurrentUserAsync(userRepo, ctx, ct);
        if (user is null) return Results.Unauthorized();

        var fido2User = new Fido2User
        {
            Id          = user.Id.ToByteArray(),
            Name        = user.DisplayName,
            DisplayName = user.DisplayName,
        };

        // Exclude all existing credentials for this user — prevents registering
        // the same authenticator twice under a different name.
        var existingKeys = user.Credentials
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

        ctx.Session.SetString(AddDeviceSessionKey, JsonSerializer.Serialize(
            new PendingAddDevice(options.ToJson(), user.Id, req.DeviceName)));

        return Results.Ok(options);
    }

    // ── Add device step 2 ─────────────────────────────────────────────────────

    private static async Task<IResult> AddDeviceComplete(
        [FromBody] AuthenticatorAttestationRawResponse attestationResponse,
        IFido2 fido2,
        IAppUserRepository userRepo,
        IUnitOfWork uow,
        HttpContext ctx,
        CancellationToken ct)
    {
        var json = ctx.Session.GetString(AddDeviceSessionKey);
        if (json is null) return Results.BadRequest("Session expired. Restart device registration.");
        ctx.Session.Remove(AddDeviceSessionKey);

        var pending = JsonSerializer.Deserialize<PendingAddDevice>(json)!;
        var options = CredentialCreateOptions.FromJson(pending.OptionsJson);

        var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions     = options,
            IsCredentialIdUniqueToUserCallback = async (args, _) =>
                await userRepo.FindByCredentialIdAsync(args.CredentialId, ct) is null,
        }, ct);

        var user = await userRepo.GetWithCredentialsAsync(pending.UserId, ct);
        if (user is null) return Results.Unauthorized();

        var credential = PasskeyCredential.Create(
            user.Id,
            result.Id,
            result.PublicKey,
            pending.DeviceName,
            result.SignCount);

        user.AddCredential(credential);
        await uow.SaveChangesAsync(ct);

        return Results.Ok(new CredentialDto(
            credential.Id, credential.DeviceName, credential.CreatedAt, credential.LastUsedAt));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<AppUser?> LoadCurrentUserAsync(
        IAppUserRepository userRepo, HttpContext ctx, CancellationToken ct)
    {
        var idStr = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (idStr is null || !Guid.TryParse(idStr, out var userId)) return null;
        return await userRepo.GetWithCredentialsAsync(userId, ct);
    }

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

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record RegisterOptionsRequest(string DisplayName, string DeviceName);
public sealed record AddDeviceRequest(string DeviceName);
public sealed record SetLanguageRequest(string Language);
public sealed record MeDto(Guid Id, string DisplayName, string Language);
public sealed record CredentialDto(
    Guid Id,
    string DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

file sealed record PendingRegistration(
    string OptionsJson,
    Guid UserId,
    string DisplayName,
    string DeviceName);

file sealed record PendingAddDevice(
    string OptionsJson,
    Guid UserId,
    string DeviceName);
