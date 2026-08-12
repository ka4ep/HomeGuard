using HomeGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HomeGuard.Infrastructure;

/// <summary>
/// Used by `dotnet ef` at design time to create a DbContext instance.
/// <para>
/// Run from the solution root. Name the .csproj files, not the directories — the
/// directory form is not resolved reliably and fails with "Unable to retrieve project
/// metadata":
/// </para>
/// <code>
/// dotnet ef migrations add Name \
///   --project src/HomeGuard.Infrastructure/HomeGuard.Infrastructure.csproj \
///   --startup-project src/HomeGuard.Api/HomeGuard.Api.csproj
///
/// dotnet ef database update \
///   --project src/HomeGuard.Infrastructure/HomeGuard.Infrastructure.csproj \
///   --startup-project src/HomeGuard.Api/HomeGuard.Api.csproj
/// </code>
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
