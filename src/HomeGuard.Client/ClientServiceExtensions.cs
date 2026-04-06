using HomeGuard.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HomeGuard.Client;

public static class ClientServiceExtensions
{
    public static IServiceCollection AddHomeGuardClientServices(
        this IServiceCollection services, string apiBaseAddress)
    {
        // Typed HTTP clients — all share one HttpClient pointing at the API.
        services.AddHttpClient<EquipmentApiClient>(c => c.BaseAddress = new Uri(apiBaseAddress));
        services.AddHttpClient<WarrantyApiClient>(c => c.BaseAddress = new Uri(apiBaseAddress));
        services.AddHttpClient<ServiceRecordApiClient>(c => c.BaseAddress = new Uri(apiBaseAddress));
        services.AddHttpClient<SyncApiClient>(c => c.BaseAddress = new Uri(apiBaseAddress));
        services.AddHttpClient<NotificationApiClient>(c => c.BaseAddress = new Uri(apiBaseAddress));

        // IndexedDB wrapper — singleton в Blazor WASM (один scope на всё приложение).
        services.AddSingleton<HomeGuardDb>();

        // Outbox sync — singleton, очередь общая для всех страниц.
        services.AddSingleton<OutboxSyncService>();

        // Timeline interop — transient, each timeline page creates its own instance.
        services.AddTransient<TimelineInterop>();

        return services;
    }
}
