using HomeGuard.Application.Services;
using HomeGuard.Infrastructure;
using HomeGuard.Api.BackgroundServices;
using HomeGuard.Api.Endpoints;
using HomeGuard.Api;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHomeGuardInfrastructure(builder.Configuration);
builder.Services.AddHomeGuardApplication();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// В продакшне CORS не нужен — клиент и Api на одном origin.
// Оставляем только для dev запуска двух проектов отдельно.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
            policy
                .WithOrigins(
                    builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:5010"])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
}

builder.Services.AddPasskeyAuth(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddHostedService<JobRunnerService>();
builder.Services.AddHostedService<NotificationSchedulerHostedService>();
builder.Services.AddHostedService<BlobSyncHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors();
}

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ── Статика Blazor WASM ───────────────────────────────────────────────────────
// Файлы публикуются в wwwroot при dotnet publish благодаря ProjectReference на Client.
app.UseDefaultFiles();
app.UseStaticFiles();

await app.EnsureDatabaseAsync();

// ── API эндпоинты ─────────────────────────────────────────────────────────────
app.MapEquipmentEndpoints();
app.MapWarrantyEndpoints();
app.MapServiceRecordEndpoints();
app.MapBlobEndpoints();
app.MapSyncEndpoints();
app.MapAuthEndpoints();
app.MapCalendarFeedEndpoint();
app.MapNotificationEndpoints();

// ── Fallback на index.html для Blazor роутинга ────────────────────────────────
// Все не-API запросы (например /equipment/some-guid) отдают index.html,
// дальше Blazor Router разбирается сам.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
