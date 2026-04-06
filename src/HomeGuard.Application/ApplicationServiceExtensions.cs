using Microsoft.Extensions.DependencyInjection;

namespace HomeGuard.Application.Services;

/// <summary>Registers Application-layer services into the DI container.</summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddHomeGuardApplication(this IServiceCollection services)
    {
        services.AddScoped<EquipmentService>();
        services.AddScoped<WarrantyService>();
        services.AddScoped<ServiceRecordService>();
        services.AddScoped<NotificationSchedulerService>();
        services.AddScoped<SyncProcessorService>();
        return services;
    }
}
