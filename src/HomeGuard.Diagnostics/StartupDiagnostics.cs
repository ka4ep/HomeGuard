using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeGuard.Diagnostics;

/// <summary>
/// Collects runtime self-description in phases and renders an ASCII startup card
/// to the console + logger.  Safe to call even when the build/run has failed —
/// the card is always printed, even partially.
///
/// USAGE PATTERN (see Program.cs snippet at the bottom of this file):
///
///   Phase 1  — CollectFromBuilder(builder)        before builder.Build()
///   Phase 2  — CollectFromApp(app)                after  builder.Build()
///   Phase 3  — await CollectDatabaseInfoAsync(app) after  builder.Build()
///   Print    — Print(logger) in finally { }        always
/// </summary>
public sealed class StartupDiagnostics
{
    // ── card geometry ────────────────────────────────────────────────────
    private const int W = 80;           // total line width incl. ║ chars
    private const int Inner = W - 2;    // 78
    private const int KeyW = 18;        // key column width

    // ── collected state ──────────────────────────────────────────────────
    public string AppName { get; private set; } = "Application";
    public string Version { get; private set; } = "?.?.?";
    public string BuildDate { get; private set; } = "unknown";
    public string Environment { get; private set; } = "unknown";
    public string ContentRoot { get; private set; } = ".";
    public string WorkingDir { get; private set; } = ".";

    public List<string> ConfigFilesLoaded { get; } = [];
    public List<string> ListenUrls { get; } = [];
    public string CorsPolicy { get; private set; } = "(not configured)";
    public List<string> CorsOrigins { get; } = [];

    public string DbHost { get; private set; } = "unknown";
    public string DbName { get; private set; } = "unknown";
    public string DbMaskedCs { get; private set; } = "(not set)";
    public string? LastMigration { get; private set; }
    public string? LastMigrationDate { get; private set; }
    public int PendingMigrations { get; private set; }

    public List<string> HostedServices { get; } = [];
    public string? FileLogPath { get; private set; }

    // Full masked config — populated in phase 1
    private List<(string Key, string Value)> _maskedConfig = [];

    // Validation results — set externally
    public List<ValidationResult> ValidationResults { get; } = [];

    // Errors captured during build / run
    public Exception? BuildException { get; set; }
    public Exception? RunException { get; set; }

    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

    // ── Phase 1 ──────────────────────────────────────────────────────────

    /// <summary>Call before builder.Build().</summary>
    public void CollectFromBuilder(WebApplicationBuilder builder)
    {
        try
        {
            var asm = Assembly.GetEntryAssembly();
            AppName = asm?.GetName().Name ?? "Application";
            Version = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                              ?.InformationalVersion
                          ?? asm?.GetName().Version?.ToString(3)
                          ?? "?.?.?";
            BuildDate = GetBuildDate(asm);

            Environment = builder.Environment.EnvironmentName;
            ContentRoot = builder.Environment.ContentRootPath;
            WorkingDir = Directory.GetCurrentDirectory();

            // Which JSON config files were actually loaded?
            if (builder.Configuration is IConfigurationRoot root)
            {
                foreach (var provider in root.Providers)
                {
                    if (provider is JsonConfigurationProvider jp)
                    {
                        // Path property is on JsonConfigurationSource
                        var src = jp.Source;
                        var path = src?.GetType().GetProperty("Path")?.GetValue(src) as string;
                        if (path != null) ConfigFilesLoaded.Add(Path.GetFileName(path));
                    }
                }
            }

            // Listen URLs — read from config / env before Kestrel binds
            CollectListenUrls(builder.Configuration);

            // CORS
            var corsSec = builder.Configuration.GetSection("Cors");
            CorsPolicy = corsSec["PolicyName"] ?? "default";
            CorsOrigins.AddRange(
                corsSec.GetSection("Origins").GetChildren()
                    .Select(c => c.Value ?? "")
                    .Where(v => v.Length > 0));

            // Database
            var cs = builder.Configuration.GetConnectionString("DefaultConnection")
                     ?? builder.Configuration.GetConnectionString("HomeGuard")
                     ?? builder.Configuration.GetConnectionString("Postgres");
            ParseConnectionString(cs);

            // Serilog log file path
            FileLogPath = ExtractSerilogPath(builder.Configuration);

            // Full config dump (masked)
            _maskedConfig = ConfigMasker.MaskAll(builder.Configuration).ToList();
        }
        catch (Exception ex)
        {
            _maskedConfig.Add(("[DiagnosticsCollectionError]", ex.Message));
        }
    }

    // ── Phase 2 ──────────────────────────────────────────────────────────

    /// <summary>Call after builder.Build() — requires built service container.</summary>
    public void CollectFromApp(WebApplication app)
    {
        try
        {
            // Hosted services — skip framework noise
            foreach (var svc in app.Services.GetServices<IHostedService>())
            {
                var t = svc.GetType();
                if (t.Namespace?.StartsWith("Microsoft", StringComparison.Ordinal) == true) continue;
                if (t.Namespace?.StartsWith("Blazor", StringComparison.Ordinal) == true) continue;
                HostedServices.Add(t.Name);
            }

            // Try to get the actual bound addresses from the server feature.
            // NOTE: these are only populated *after* Run() starts — here we
            // get Kestrel's configured addresses, which is good enough for startup info.
            var server = app.Services.GetService<IServer>();
            var feature = server?.Features.Get<IServerAddressesFeature>();
            if (feature?.Addresses.Count > 0)
            {
                ListenUrls.Clear();
                ListenUrls.AddRange(feature.Addresses);
            }
        }
        catch (Exception ex)
        {
            HostedServices.Add($"[collection error: {ex.Message}]");
        }
    }

    // ── Phase 3 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Queries the EF Core migrations history table.
    /// Runs in its own scope so it doesn't interfere with the app's contexts.
    /// Swallows all errors — DB might not be reachable yet (that's fine;
    /// StartupValidator will report the connectivity problem separately).
    /// </summary>
    public async Task CollectDatabaseInfoAsync<TContext>(IServiceProvider services)
        where TContext : DbContext
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            var all = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            var pend = (await db.Database.GetPendingMigrationsAsync()).ToList();

            PendingMigrations = pend.Count;

            if (all.Count > 0)
            {
                var last = all[^1];
                LastMigration = last;
                // EF migration names: 20240115123456_AddDocuments
                // First 14 chars are the timestamp
                if (last.Length >= 14 &&
                    DateTime.TryParseExact(last[..14], "yyyyMMddHHmmss",
                        null, System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var dt))
                    LastMigrationDate = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
        }
        catch
        {
            // Silently: DB issue is surfaced by StartupValidator
        }
    }

    // ── Print ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Always safe to call — even when Build or Run threw.
    /// Writes to Console.Out and, if logger is not null, to the log.
    /// Full config dump goes to logger at Debug level.
    /// </summary>
    public void Print(ILogger? logger = null)
    {
        var sb = new StringBuilder(capacity: 2048);
        var ok = BuildException is null &&
                  !ValidationResults.Any(r => r.Severity == ValidationSeverity.Error);
        var statusLabel = BuildException is not null ? "BUILD FAILED"
                        : ok ? "OK ✓"
                                                     : "STARTED WITH WARNINGS";

        // ── header ────────────────────────────────────────────────────────
        TopLine(sb);
        Center(sb, $"  {AppName}  v{Version}  ");
        Mid(sb);
        KV(sb, "Environment", Environment);
        KV(sb, "Build date", BuildDate);
        KV(sb, "Config files", ConfigFilesLoaded);
        KV(sb, "Content root", ContentRoot);
        KV(sb, "Working dir", WorkingDir);

        // ── network ───────────────────────────────────────────────────────
        Mid(sb);
        Section(sb, "NETWORK");
        KV(sb, "Listen",
            ListenUrls.Count > 0 ? ListenUrls : ["(check Kestrel config / ASPNETCORE_URLS)"],
            "  ");
        KV(sb, "CORS policy", CorsPolicy);
        if (CorsOrigins.Count > 0)
            KV(sb, "CORS origins", CorsOrigins);

        // ── database ─────────────────────────────────────────────────────
        Mid(sb);
        Section(sb, "DATABASE");
        KV(sb, "Host", DbHost);
        KV(sb, "Database", DbName);
        KV(sb, "Connection", DbMaskedCs);
        if (LastMigration is not null)
        {
            var migName = LastMigration.Length > 40
                ? "…" + LastMigration[^38..]
                : LastMigration;
            KV(sb, "Last migration", migName);
            if (LastMigrationDate is not null)
                KV(sb, "Applied", LastMigrationDate);
        }
        if (PendingMigrations > 0)
            KV(sb, "⚠ Pending", $"{PendingMigrations} unapplied migration(s) — run dotnet ef database update");

        // ── hosted services ───────────────────────────────────────────────
        if (HostedServices.Count > 0)
        {
            Mid(sb);
            Section(sb, "HOSTED SERVICES");
            KV(sb, "[+]", HostedServices);
        }

        // ── logging ───────────────────────────────────────────────────────
        if (FileLogPath is not null)
        {
            Mid(sb);
            Section(sb, "LOGGING");
            KV(sb, "File sink", FileLogPath);
        }

        // ── validation ────────────────────────────────────────────────────
        var warnings = ValidationResults.Where(r => r.Severity == ValidationSeverity.Warning).ToList();
        var errors = ValidationResults.Where(r => r.Severity == ValidationSeverity.Error).ToList();
        var infos = ValidationResults.Where(r => r.Severity == ValidationSeverity.Info).ToList();

        if (errors.Count + warnings.Count + infos.Count > 0)
        {
            Mid(sb);
            Section(sb, "STARTUP CHECKS");
            foreach (var r in errors) KV(sb, "✗ " + r.Area, Clip(r.Message, Inner - KeyW - 4));
            foreach (var r in warnings) KV(sb, "⚠ " + r.Area, Clip(r.Message, Inner - KeyW - 4));
            foreach (var r in infos) KV(sb, "ℹ " + r.Area, Clip(r.Message, Inner - KeyW - 4));
        }

        // ── footer ────────────────────────────────────────────────────────
        Mid(sb);
        Center(sb, $"  {statusLabel}  ({_sw.Elapsed.TotalMilliseconds:F0} ms)  ");
        BotLine(sb);

        // ── emit card ─────────────────────────────────────────────────────
        var card = sb.ToString();
        Console.WriteLine(card);
        logger?.LogInformation("Startup diagnostics:\n{StartupCard}", card);

        // ── validation detail block ───────────────────────────────────────
        if (errors.Count + warnings.Count > 0)
        {
            var detail = new StringBuilder();
            detail.AppendLine("──────────────── STARTUP CHECK DETAIL ────────────────");
            foreach (var r in errors.Concat(warnings))
            {
                var icon = r.Severity == ValidationSeverity.Error ? "✗" : "⚠";
                detail.AppendLine($"{icon} [{r.Area}] {r.Message}");
                if (r.Detail is not null) detail.AppendLine($"   Detail   : {r.Detail}");
                if (r.FixHint is not null) detail.AppendLine($"   Fix      : {r.FixHint}");
                if (r.AffectedFeature is not null) detail.AppendLine($"   Affects  : {r.AffectedFeature}");
                if (r.DeployNote is not null) detail.AppendLine($"   Deploy   : {r.DeployNote}");
                detail.AppendLine();
            }
            detail.AppendLine("───────────────────────────────────────────────────────");

            Console.WriteLine(detail);
            if (errors.Count > 0)
                logger?.LogError("Startup check failures:\n{Detail}", detail.ToString());
            else
                logger?.LogWarning("Startup check warnings:\n{Detail}", detail.ToString());
        }

        // ── build exception ───────────────────────────────────────────────
        if (BuildException is not null)
        {
            Console.Error.WriteLine($"[FATAL] Application failed to build: {BuildException}");
            logger?.LogCritical(BuildException, "Application failed to build");
        }

        // ── full config dump (Debug level only) ───────────────────────────
        if (_maskedConfig.Count > 0 && logger is not null && logger.IsEnabled(LogLevel.Debug))
        {
            var configDump = new StringBuilder("Full active configuration (secrets masked):\n");
            foreach (var (k, v) in _maskedConfig)
                configDump.AppendLine($"  {k} = {v}");
            logger.LogDebug("{ConfigDump}", configDump.ToString());
        }
    }

    // ── Card drawing helpers ──────────────────────────────────────────────

    private static void TopLine(StringBuilder sb) =>
        sb.AppendLine($"╔{new string('═', Inner)}╗");

    private static void Mid(StringBuilder sb) =>
        sb.AppendLine($"╠{new string('═', Inner)}╣");

    private static void BotLine(StringBuilder sb) =>
        sb.AppendLine($"╚{new string('═', Inner)}╝");

    private static void Center(StringBuilder sb, string text)
    {
        var padded = text.PadLeft((Inner + text.Length) / 2).PadRight(Inner);
        sb.AppendLine($"║{Clip(padded, Inner)}║");
    }

    private static void Section(StringBuilder sb, string title) =>
        sb.AppendLine($"║  {title.PadRight(Inner - 2)}║");

    // Single string — backward-compatible shorthand
    private static void KV(StringBuilder sb, string key, string value)
        => KV(sb, key, (IEnumerable<string>)[value]);

    /// <summary>
    /// Renders a key/value row (or rows) inside the card box.
    /// </summary>
    /// <param name="separator">
    /// <c>null</c> or <c>Environment.NewLine</c> (default) —
    ///   each item starts on its own continuation line, indented to the value column.
    /// Any other string (<c>", "</c>, <c>"; "</c>, …) —
    ///   all items are joined inline and the result is hard-wrapped at the column boundary.
    /// </param>
    private static void KV(StringBuilder sb, string key,
                            IEnumerable<string> values,
                            string? separator = null)
    {
        var prefix = $"  {key.PadRight(KeyW)} : ";
        var indent = new string(' ', prefix.Length);
        var remain = Math.Max(1, Inner - prefix.Length);

        bool lineBreak = separator is null
                         || separator == "\n"
                         || separator == "\r\n"
                         || separator == "\r"
                         || separator == System.Environment.NewLine;

        IEnumerable<string> segments = lineBreak
            ? values.DefaultIfEmpty("(none)").SelectMany(v => HardWrap(v, remain))
            : HardWrap(string.Join(separator, values.DefaultIfEmpty("(none)")), remain);

        bool first = true;
        foreach (var seg in segments)
        {
            var lineContent = first ? prefix + seg : indent + seg;
            sb.AppendLine($"║{lineContent.PadRight(Inner)}║");
            first = false;
        }
    }

    // Hard-wraps a string into chunks of exactly maxWidth chars.
    // No word-boundary heuristics — paths and structured values must wrap
    // at the precise column, not at an arbitrary space character.
    private static IEnumerable<string> HardWrap(string text, int maxWidth)
    {
        if (text.Length == 0) { yield return string.Empty; yield break; }
        for (var i = 0; i < text.Length; i += maxWidth)
            yield return text.Substring(i, Math.Min(maxWidth, text.Length - i));
    }

    private static string Clip(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    // ── Private helpers ───────────────────────────────────────────────────

    private void CollectListenUrls(ConfigurationManager config)
    {
        // Priority: explicit Kestrel endpoint config > "urls" key > env var > fallback
        var kestrelSection = config.GetSection("Kestrel:Endpoints");
        foreach (var ep in kestrelSection.GetChildren())
        {
            var u = ep["Url"];
            if (u is not null && !ListenUrls.Contains(u)) ListenUrls.Add(u);
        }

        if (ListenUrls.Count > 0) return;

        var urlsValue = config["urls"]
                        ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
                        ?? System.Environment.GetEnvironmentVariable("DOTNET_URLS");
        if (!string.IsNullOrEmpty(urlsValue))
            ListenUrls.AddRange(urlsValue.Split(';', StringSplitOptions.RemoveEmptyEntries));
    }

    private void ParseConnectionString(string? cs)
    {
        DbMaskedCs = ConfigMasker.MaskConnectionString(cs);
        if (string.IsNullOrEmpty(cs)) { DbHost = "(not set)"; DbName = "(not set)"; return; }

        // SQLite connection strings: "Data Source=path/to/file.db;Cache=Shared"
        // No external parser needed — split on ';' then on first '='.
        var dict = cs
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(seg => seg.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);

        var dataSource = dict.GetValueOrDefault("Data Source")
                         ?? dict.GetValueOrDefault("Filename")
                         ?? dict.GetValueOrDefault("DataSource");

        DbHost = "SQLite (embedded)";
        DbName = dataSource switch
        {
            null or "" => "(unknown)",
            ":memory:" => ":memory: (in-process, non-persistent)",
            var path => Path.GetFullPath(path)   // resolve relative → absolute
        };
    }

    private static string? ExtractSerilogPath(ConfigurationManager config)
    {
        for (var i = 0; i < 8; i++)
        {
            var v = config[$"Serilog:WriteTo:{i}:Args:path"];
            if (v is not null) return v;
        }
        return null;
    }

    private static string GetBuildDate(Assembly? asm)
    {
        if (asm?.Location is { Length: > 0 } loc)
        {
            try { return File.GetLastWriteTime(loc).ToString("yyyy-MM-dd HH:mm"); }
            catch { /* ignore */ }
        }
        return "unknown";
    }
}
