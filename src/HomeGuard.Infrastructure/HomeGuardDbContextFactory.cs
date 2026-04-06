using HomeGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HomeGuard.Infrastructure;

/// <summary>
/// Used by `dotnet ef` at design time to create a DbContext instance.
/// Run from the solution root:
///   dotnet ef migrations add InitialCreate --project src/HomeGuard.Infrastructure --startup-project src/HomeGuard.Api
///   dotnet ef database update             --project src/HomeGuard.Infrastructure --startup-project src/HomeGuard.Api
/// </summary>
public sealed class HomeGuardDbContextFactory : IDesignTimeDbContextFactory<HomeGuardDbContext>
{
    public HomeGuardDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(),
                                      "../HomeGuard.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var opts = new DbContextOptionsBuilder<HomeGuardDbContext>()
            .UseSqlite(
                config.GetConnectionString("DefaultConnection")
                    ?? "Data Source=homeguard-dev.db",
                sqlite => sqlite.MigrationsAssembly(
                    typeof(HomeGuardDbContext).Assembly.FullName))
            .Options;

        return new HomeGuardDbContext(opts);
    }
}
