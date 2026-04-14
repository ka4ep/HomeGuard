using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HomeGuard.Api;

internal static class PasskeyAuthExtensions
{
    internal static IServiceCollection AddPasskeyAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(opts =>
            {
                opts.Cookie.Name       = "hg_session";
                opts.Cookie.HttpOnly   = true;
                opts.Cookie.SameSite   = SameSiteMode.Strict;
                opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opts.ExpireTimeSpan    = TimeSpan.FromDays(30);
                opts.SlidingExpiration = true;
                opts.LoginPath         = "/login";

                opts.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    else
                        ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        // MemoryCache is required by Fido2 internals.
        services.AddMemoryCache();

        // Session for storing Fido2 challenges between the two registration/login steps.
        services.AddDistributedMemoryCache();
        services.AddSession(opts =>
        {
            opts.Cookie.Name     = "hg_fido_session";
            opts.Cookie.HttpOnly = true;
            opts.Cookie.SameSite = SameSiteMode.Strict;
            opts.IdleTimeout     = TimeSpan.FromMinutes(5);
        });

        // Fido2 v4 — reads config from "Fido2" section.
        services.AddFido2(opts =>
        {
            opts.ServerDomain = configuration["Fido2:ServerDomain"] ?? "localhost";
            opts.ServerName   = configuration["Fido2:ServerName"]   ?? "HomeGuard";
            opts.Origins      = configuration
                .GetSection("Fido2:Origins")
                .Get<HashSet<string>>()
                ?? ["http://localhost:5010", "https://localhost:5011"];
            opts.TimestampDriftTolerance = 300_000;
        })
        .AddCachedMetadataService(config =>
        {
            // FIDO Metadata Service — validates authenticator provenance.
            // For a home server, this is optional but good practice.
            config.AddFidoMetadataRepository(http => { });
        });

        return services;
    }
}
