using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace HomeGuard.Diagnostics;

/// <summary>
/// Runs a battery of startup checks against the active configuration.
/// Each rule produces a <see cref="ValidationResult"/> that is printed in
/// the startup card and logged.  Rules are intentionally verbose so that
/// whoever reads the log — even six months later on a fresh server — knows
/// exactly what to do.
/// </summary>
public sealed class StartupValidator
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly List<ValidationResult> _results = [];

    public IReadOnlyList<ValidationResult> Results => _results;

    public StartupValidator(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    // ------------------------------------------------------------------ //
    //  Public entry point
    // ------------------------------------------------------------------ //

    public StartupValidator RunAll()
    {
        CheckDatabaseConnectionString();
        CheckJwtOrAuthSettings();
        CheckCorsOrigins();
        CheckFileStorageDirectory();
        CheckSerilogOutputPath();
        CheckEnvironmentVariable("ASPNETCORE_ENVIRONMENT", required: false,
            hint: "Set to 'Development', 'Staging', or 'Production'. " +
                  "Affects which appsettings.{env}.json is loaded and error detail verbosity.",
            deployNote: "docker run -e ASPNETCORE_ENVIRONMENT=Production  OR  set in docker-compose.yml / k8s ConfigMap");
        CheckEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", required: false,
            hint: "Automatically set by the .NET Docker base image. No action needed if absent on bare-metal.",
            deployNote: "Present inside Docker/Podman containers; used to adjust path defaults.");
        return this;
    }

    // ------------------------------------------------------------------ //
    //  Individual rules
    // ------------------------------------------------------------------ //

    private void CheckDatabaseConnectionString()
    {
        const string area = "Database";
        var cs = _config.GetConnectionString("DefaultConnection")
                 ?? _config.GetConnectionString("HomeGuard")
                 ?? _config.GetConnectionString("Sqlite");

        if (string.IsNullOrWhiteSpace(cs))
        {
            _results.Add(new(
                ValidationSeverity.Error, area,
                "No connection string found.",
                Detail: "Looked for: ConnectionStrings:DefaultConnection, HomeGuard, Sqlite",
                FixHint: "Add to appsettings.json: " +
                         "\"ConnectionStrings\": { \"DefaultConnection\": \"Data Source=homeguard.db\" }",
                AffectedFeature: "Entire application — EF Core cannot open the database.",
                DeployNote: "In Docker/Podman pass as env var (double-underscore = section separator): " +
                            "ConnectionStrings__DefaultConnection='Data Source=/data/homeguard.db'  " +
                            "and mount the directory: -v /host/data:/data"));
            return;
        }

        // Parse without any external library — SQLite CS is simple key=value;key=value
        var dict = cs
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(seg => seg.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);

        var dataSource = dict.GetValueOrDefault("Data Source")
                         ?? dict.GetValueOrDefault("Filename")
                         ?? dict.GetValueOrDefault("DataSource");

        if (string.IsNullOrEmpty(dataSource))
        {
            _results.Add(new(ValidationSeverity.Warning, area,
                "Connection string has no 'Data Source' key.",
                FixHint: "Correct SQLite format: \"Data Source=homeguard.db\" " +
                         "or absolute \"Data Source=/app/data/homeguard.db\"",
                DeployNote: "Relative paths resolve against the process working directory — " +
                            "use absolute paths in containers to avoid surprises."));
            return;
        }

        // In-memory — valid but non-persistent
        if (dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            _results.Add(new(ValidationSeverity.Warning, area,
                "SQLite is configured as :memory: — data will NOT survive a restart.",
                AffectedFeature: "All data is lost when the process exits.",
                FixHint: "Change Data Source to a file path for persistent storage.",
                DeployNote: ":memory: is acceptable only for integration tests."));
            return;
        }

        // File-based — check that the containing directory exists and is writable
        var absPath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, dataSource));

        var dir = Path.GetDirectoryName(absPath);

        if (dir is not null && !Directory.Exists(dir))
        {
            _results.Add(new(ValidationSeverity.Error, area,
                $"SQLite directory does not exist: {dir}",
                Detail: $"Resolved from Data Source: {dataSource}",
                FixHint: $"Create it: mkdir -p \"{dir}\"  — SQLite cannot create parent directories.",
                AffectedFeature: "Application will crash on first DB access.",
                DeployNote: $"Docker: mount the directory  -v /host/data:{dir}  " +
                             "and ensure the container user has write permission."));
            return;
        }

        var fileExists = File.Exists(absPath);
        _results.Add(new(
            fileExists ? ValidationSeverity.Ok : ValidationSeverity.Info,
            area,
            fileExists
                ? $"SQLite DB file found: {absPath}"
                : $"SQLite DB file will be created on first run: {absPath}",
            DeployNote: fileExists ? null
                : "In containers the DB file lives inside the writable layer by default — " +
                  "mount a volume so it survives re-deploys: -v /host/data:/app/data"));
    }

    private void CheckJwtOrAuthSettings()
    {
        const string area = "Auth/JWT";

        var jwtKey = _config["Jwt:Key"] ?? _config["Authentication:JwtBearer:SecurityKey"];
        if (jwtKey is null) { /* Not using JWT — skip silently */ return; }

        if (jwtKey.Length < 32)
            _results.Add(new(ValidationSeverity.Warning, area,
                $"JWT signing key is only {jwtKey.Length} chars; recommended minimum is 32 (256-bit).",
                FixHint: "Generate with: openssl rand -base64 32",
                DeployNote: "Store in Docker secret / k8s Secret, not in appsettings.Production.json."));
        else
            _results.Add(new(ValidationSeverity.Ok, area, "JWT key present and meets minimum length."));

        var issuer = _config["Jwt:Issuer"];
        if (string.IsNullOrEmpty(issuer))
            _results.Add(new(ValidationSeverity.Warning, area,
                "Jwt:Issuer is not set.",
                FixHint: "Set Jwt:Issuer to your app's base URL, e.g. https://homeguard.example.com",
                AffectedFeature: "Token validation will use a null issuer, which may reject valid tokens."));
    }

    private void CheckCorsOrigins()
    {
        const string area = "CORS";

        var origins = _config.GetSection("Cors:Origins").GetChildren()
            .Select(c => c.Value).Where(v => !string.IsNullOrEmpty(v)).ToList();

        if (origins.Count == 0)
        {
            // CORS might be intentionally absent for server-side-only Blazor
            _results.Add(new(ValidationSeverity.Info, area,
                "No CORS origins configured — OK for pure Blazor Server.",
                FixHint: "If you expose a REST or SignalR endpoint to a JS frontend, add: " +
                          "\"Cors\": { \"PolicyName\": \"default\", \"Origins\": [\"https://yourfrontend.com\"] }",
                DeployNote: "In production, never use wildcard '*' for credentialed requests."));
            return;
        }

        var wildcards = origins.Where(o => o!.Contains('*')).ToList();
        if (wildcards.Count > 0)
            _results.Add(new(ValidationSeverity.Warning, area,
                $"Wildcard CORS origin detected: {string.Join(", ", wildcards)}",
                AffectedFeature: "Browsers will block credentialed (cookie/auth) requests to wildcard origins.",
                FixHint: "Replace '*' with the exact origin, e.g. https://app.example.com",
                DeployNote: "Production CORS must list exact origins. Wildcard is acceptable only for public read-only APIs."));
        else
            _results.Add(new(ValidationSeverity.Ok, area,
                $"{origins.Count} origin(s) configured."));
    }

    private void CheckFileStorageDirectory()
    {
        const string area = "FileStorage";

        // Adjust key name to match your actual config
        var paths = new[]
        {
            "FileStorage:RootPath",
            "Storage:Path",
            "DocumentStorage:BasePath",
            "Uploads:Path"
        };

        foreach (var key in paths)
        {
            var path = _config[key];
            if (string.IsNullOrEmpty(path)) continue;

            var expanded = Environment.ExpandEnvironmentVariables(path);
            if (!Directory.Exists(expanded))
                _results.Add(new(ValidationSeverity.Warning, area,
                    $"Directory does not exist: {expanded}",
                    Detail: $"Config key: {key}",
                    FixHint: $"Create it: mkdir -p \"{expanded}\"  or adjust the config key.",
                    AffectedFeature: "File uploads and document capture will fail at runtime.",
                    DeployNote: "In Docker, mount the host directory: -v /data/homeguard/uploads:/app/uploads  " +
                                "and set FileStorage:RootPath=/app/uploads"));
            else
                _results.Add(new(ValidationSeverity.Ok, area, $"Storage directory exists: {expanded}"));
        }
    }

    private void CheckSerilogOutputPath()
    {
        const string area = "Logging";

        var logPath = ExtractSerilogPath(_config);
        if (logPath is null) return;

        // Expand environment variables and rolling-file placeholders like {Date}
        var cleanPath = Environment.ExpandEnvironmentVariables(
            logPath.Replace("{Date}", "TEST").Replace("{Hour}", "TEST")
                   .Replace("{HalfHour}", "TEST").Replace("{yyyy}", "2024")
                   .Replace("{MM}", "01").Replace("{dd}", "01"));

        var dir = Path.GetDirectoryName(cleanPath);
        if (dir is null) return;

        if (!Directory.Exists(dir))
            _results.Add(new(ValidationSeverity.Warning, area,
                $"Log directory does not exist: {dir}",
                Detail: $"Serilog path template: {logPath}",
                FixHint: $"Create it before starting: mkdir -p \"{dir}\"  " +
                          "or update Serilog:WriteTo path in appsettings.",
                AffectedFeature: "Serilog will silently drop file sink if directory is missing. " +
                                 "Console sink still works.",
                DeployNote: "Docker: -v /var/log/homeguard:/app/logs and path=/app/logs/hg-.log"));
        else
            _results.Add(new(ValidationSeverity.Ok, area, $"Log directory exists: {dir}"));
    }

    private void CheckEnvironmentVariable(string name, bool required, string hint, string deployNote)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (required && string.IsNullOrEmpty(value))
            _results.Add(new(ValidationSeverity.Warning, $"Env:{name}",
                $"Required environment variable {name} is not set.",
                FixHint: hint, DeployNote: deployNote));
        // Non-required: silently OK
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    private static string? ExtractSerilogPath(IConfiguration config)
    {
        // Serilog config can have multiple WriteTo sinks; scan them
        for (var i = 0; i < 6; i++)
        {
            var path = config[$"Serilog:WriteTo:{i}:Args:path"];
            if (path is not null) return path;
        }
        return null;
    }
}
