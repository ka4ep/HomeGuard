using HomeGuard.Application.Services;
using HomeGuard.Infrastructure;
using HomeGuard.Api.BackgroundServices;
using HomeGuard.Api.Endpoints;
using HomeGuard.Api;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure + Application ──────────────────────────────────────────────
builder.Services.AddHomeGuardInfrastructure(builder.Configuration);
builder.Services.AddHomeGuardApplication();

// ── CORS — allow Blazor WASM dev server ───────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5010"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

// ── Auth — Passkeys via Fido2 ─────────────────────────────────────────────────
builder.Services.AddPasskeyAuth(builder.Configuration);

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ── Background services ───────────────────────────────────────────────────────
builder.Services.AddHostedService<JobRunnerService>();
builder.Services.AddHostedService<NotificationSchedulerHostedService>();
builder.Services.AddHostedService<BlobSyncHostedService>();

var app = builder.Build();

// ── Middleware pipeline (порядок важен) ───────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// CORS должен идти первым — до Auth и эндпоинтов.
app.UseCors();

// Session нужна для Fido2 challenge storage.
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// ── Ensure DB is created / migrated on startup ────────────────────────────────
await app.EnsureDatabaseAsync();

// ── Endpoint groups ───────────────────────────────────────────────────────────
app.MapEquipmentEndpoints();
app.MapWarrantyEndpoints();
app.MapServiceRecordEndpoints();
app.MapBlobEndpoints();
app.MapSyncEndpoints();
app.MapAuthEndpoints();
app.MapCalendarFeedEndpoint();
app.MapNotificationEndpoints();

app.Run();

// Visible to integration test project via WebApplicationFactory<Program>.
public partial class Program { }
