using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Infrastructure.Blob;
using HomeGuard.Infrastructure.Calendar;
using HomeGuard.Infrastructure.Notifications;
using HomeGuard.Infrastructure.Persistence;
using HomeGuard.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeGuard.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddHomeGuardInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── SQLite ────────────────────────────────────────────────────────────
        services.AddDbContext<HomeGuardDbContext>(opts =>
            opts.UseSqlite(
                configuration.GetConnectionString("DefaultConnection")
                    ?? "Data Source=/app/data/homeguard.db",
                sqlite => sqlite.MigrationsAssembly(
                    typeof(HomeGuardDbContext).Assembly.FullName)));

        // Singleton write semaphore — process-wide, not per-request.
        services.AddSingleton<WriteSemaphore>();

        // ── Unit of work + repositories (Scoped) ──────────────────────────────
        services.AddScoped<IUnitOfWork, HomeGuardUnitOfWork>();
        services.AddScoped<IEquipmentRepository,   EquipmentRepository>();
        services.AddScoped<IWarrantyRepository,     WarrantyRepository>();
        services.AddScoped<IServiceRecordRepository, ServiceRecordRepository>();
        services.AddScoped<IBlobEntryRepository,    BlobEntryRepository>();
        services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
        services.AddScoped<IAppUserRepository,      AppUserRepository>();
        services.AddScoped<IProcessedOperationStore, ProcessedOperationStore>();

        // ── Blob storage ──────────────────────────────────────────────────────
        services.Configure<BlobStorageOptions>(
            configuration.GetSection(BlobStorageOptions.Section));
        services.AddScoped<IBlobStorage, BlobStorageService>();

        // ── Notifications ─────────────────────────────────────────────────────
        services.Configure<WebPushOptions>(
            configuration.GetSection(WebPushOptions.Section));
        services.AddScoped<INotificationSender, WebPushNotificationSender>();
        services.AddScoped<WebPushNotificationSender>(); // also directly for subscription management

        // ── iCal feed ─────────────────────────────────────────────────────────
        services.AddScoped<ICalFeedGenerator>();

        // ── Google Calendar (optional) ────────────────────────────────────────
        var gcalEnabled = configuration
            .GetSection(GoogleCalendarOptions.Section)
            .GetValue<bool>("Enabled");

        if (gcalEnabled)
        {
            services.Configure<GoogleCalendarOptions>(
                configuration.GetSection(GoogleCalendarOptions.Section));
            services.AddScoped<ICalendarProvider, GoogleCalendarProvider>();
        }

        return services;
    }
}
