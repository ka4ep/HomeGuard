using HomeGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeGuard.Api;

internal static class DatabaseStartupExtensions
{
    internal static async Task EnsureDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeGuardDbContext>();

        await db.Database.MigrateAsync();

        // GetDataSource() is SQLite-specific and may not exist in all EF versions.
        // Log the connection string instead — safe and always works.
        var connStr = db.Database.GetConnectionString() ?? "unknown";
        app.Logger.LogInformation("Database ready. Connection: {ConnStr}", connStr);
    }
}
