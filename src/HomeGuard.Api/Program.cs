// Program.cs — HomeGuard API
// ASP.NET Core Minimal API + Blazor WASM (hosted) server.

using HomeGuard.Api;
using HomeGuard.Api.BackgroundServices;
using HomeGuard.Api.Endpoints;
using HomeGuard.Application.Services;
using HomeGuard.Diagnostics;
using HomeGuard.Infrastructure;
using HomeGuard.Infrastructure.Persistence;
using Serilog;
using Serilog.Exceptions;
using Serilog.Extensions.Logging;

// ── Pre-build bootstrap logger ────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(formatProvider: null)
    .CreateBootstrapLogger();

var diag = new StartupDiagnostics();
WebApplication? app = null;

// ════════════════════════════════════════════════════════════════════════════
//  PHASE 1 — Configure and build
// ════════════════════════════════════════════════════════════════════════════
try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog full config (replaces bootstrap logger after Build) ───────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithExceptionDetails());

    // ── Collect diagnostics / run startup validation ──────────────────────
    diag.CollectFromBuilder(builder);
    var validator = new StartupValidator(builder.Configuration, builder.Environment);
    validator.RunAll();
    diag.ValidationResults.AddRange(validator.Results);

    // ── JSON: enums travel as strings ("Vehicle", "Completed") in both directions ──
    builder.Services.ConfigureHttpJsonOptions(o =>
        o.SerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

    // ── Application-layer services ────────────────────────────────────────
    builder.Services.AddHomeGuardApplication();

    // ── Infrastructure: SQLite, repositories, blob, push, iCal ──────────
    builder.Services.AddHomeGuardInfrastructure(builder.Configuration);

    // ── Auth: Passkey (FIDO2) + Cookie session ────────────────────────────
    builder.Services.AddPasskeyAuth(builder.Configuration);

    // ── Background services ───────────────────────────────────────────────
    builder.Services.AddHostedService<JobRunnerService>();
    builder.Services.AddHostedService<RecurringRuleMaterializationHostedService>();
    builder.Services.AddHostedService<PaymentMaterializationHostedService>();
    builder.Services.AddHostedService<NotificationSchedulerHostedService>();
    builder.Services.AddHostedService<BlobSyncHostedService>();

    // ── OpenAPI (Scalar / Swagger available at /openapi/v1.json) ─────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    // ── CORS ──────────────────────────────────────────────────────────────
    // In production the WASM client is served from the same origin (wwwroot),
    // so CORS is only required in development when the WASM dev server runs
    // on a separate port.  Configure "Cors:Origins" in appsettings to override.
    var corsOrigins = builder.Configuration
        .GetSection("Cors:Origins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(opts =>
        opts.AddPolicy("default", policy =>
        {
            var origins = corsOrigins.Length > 0
                ? corsOrigins
                : ["http://localhost:5010", "https://localhost:5011"];

            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }));

    // ── Build ─────────────────────────────────────────────────────────────
    app = builder.Build();

    // ── Collect post-build diagnostics ────────────────────────────────────
    diag.CollectFromApp(app);
    // Uncomment when EF migrations are in place:
    // await diag.CollectDatabaseInfoAsync<HomeGuardDbContext>(app.Services);

    // ── Run EF migrations on startup ──────────────────────────────────────
    await app.EnsureDatabaseAsync();

    // ════════════════════════════════════════════════════════════════════════
    //  MIDDLEWARE PIPELINE
    // ════════════════════════════════════════════════════════════════════════

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();                   // /openapi/v1.json
    }
    else
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }

    app.UseRouting();

    // CORS must come after UseRouting and before auth.
    app.UseCors("default");
    app.UseHttpsRedirection();

    // Static files: _framework/, _content/, icons, service-worker.js, etc.
    // Must extend MIME mappings — UseStaticFiles() returns 404 for unknown
    // extensions, which makes the browser hash an empty body (SHA-256 of ""
    // = 47DEQpj8…), which fails SRI and crashes Blazor WASM boot.
    var mimeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    mimeProvider.Mappings[".pdb"] = "application/octet-stream"; // debug symbols
    mimeProvider.Mappings[".dat"] = "application/octet-stream"; // ICU data (icudt_EFIGS.dat)
    mimeProvider.Mappings[".blat"] = "application/octet-stream"; // Blazor compressed payload
    mimeProvider.Mappings[".br"] = "application/octet-stream"; // Brotli-compressed assets
    app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = mimeProvider });

    // Session before Authentication — FIDO2 challenge is stored in server session
    // between initiation and completion requests.
    app.UseSession();

    app.UseAuthentication();
    app.UseAuthorization();

    // ── API endpoints ─────────────────────────────────────────────────────
    app.MapAuthEndpoints();
    app.MapEquipmentEndpoints();
    app.MapWarrantyEndpoints();
    app.MapServiceRecordEndpoints();
    app.MapRecurringRuleEndpoints();
    app.MapMeterReadingEndpoints();
    app.MapContractEndpoints();
    app.MapSyncEndpoints();
    app.MapBlobEndpoints();
    app.MapCalendarFeedEndpoint();
    app.MapNotificationEndpoints();
    app.MapAttentionEndpoint();

    // ── SPA fallback — any unmatched GET → Blazor WASM index.html ────────
    // Handles client-side routing (e.g. /equipment/123 on hard refresh).
    app.MapFallbackToFile("index.html");
}
catch (Exception ex)
{
    diag.BuildException = ex;
    Log.Fatal(ex, "Application failed during build/configuration phase");
}
finally
{
    using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
    diag.Print(loggerFactory.CreateLogger<StartupDiagnostics>());
}

// ── Bail out if build failed ───────────────────────────────────────────────
if (app is null)
{
    Log.Information("Exiting due to build failure.");
    await Log.CloseAndFlushAsync();
    return 1;
}

// ════════════════════════════════════════════════════════════════════════════
//  PHASE 2 — Run
// ════════════════════════════════════════════════════════════════════════════
try
{
    app.Run();
    return 0;
}
catch (Exception ex)
{
    diag.RunException = ex;
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.Information("Application shutting down. Flushing logs...");
    await Log.CloseAndFlushAsync();
}
