using HomeGuard.Application.Interfaces;
using HomeGuard.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebDav;

namespace HomeGuard.Infrastructure.Blob;

public sealed class BlobStorageOptions
{
    public const string Section = "Blob";

    /// <summary>Root directory for local blob storage (Docker volume mount).</summary>
    public string LocalPath { get; set; } = "/app/blobs";

    public string? NextCloudBaseUrl { get; set; }
    public string? NextCloudUser { get; set; }
    public string? NextCloudPassword { get; set; }

    /// <summary>Relative path inside the NextCloud WebDAV root for HomeGuard files.</summary>
    public string NextCloudFolder { get; set; } = "HomeGuard/blobs";

    public bool IsNextCloudConfigured =>
        !string.IsNullOrWhiteSpace(NextCloudBaseUrl) &&
        !string.IsNullOrWhiteSpace(NextCloudUser);
}

public sealed class BlobStorageService : IBlobStorage
{
    private readonly BlobStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        IOptions<BlobStorageOptions> options,
        ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // ── IBlobStorage ──────────────────────────────────────────────────────────

    public async Task<string> SaveLocallyAsync(
        Stream data, string fileName, string contentType, CancellationToken ct = default)
    {
        // Partition files into subdirectories by date to avoid huge flat directories.
        var subDir = Path.Combine(
            _options.LocalPath,
            DateTime.UtcNow.ToString("yyyy-MM", null));

        Directory.CreateDirectory(subDir);

        // Use a GUID prefix to avoid collisions while keeping the original extension readable.
        var safeFileName = $"{Guid.CreateVersion7()}_{SanitizeFileName(fileName)}";
        var localPath    = Path.Combine(subDir, safeFileName);

        await using var fs = new FileStream(localPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await data.CopyToAsync(fs, ct);

        _logger.LogDebug("Blob saved locally: {Path}", localPath);
        return localPath;
    }

    public async Task<bool> SyncToRemoteAsync(BlobEntry blob, CancellationToken ct = default)
    {
        if (!_options.IsNextCloudConfigured)
        {
            _logger.LogDebug("NextCloud not configured — skipping remote sync for blob {Id}.", blob.Id);
            return false;
        }

        if (blob.LocalPath is null)
        {
            _logger.LogWarning("Blob {Id} has no local path to sync from.", blob.Id);
            return false;
        }

        try
        {
            using var client = BuildWebDavClient();

            var remotePath   = $"{_options.NextCloudFolder}/{blob.OwnerEntityId}/{blob.FileName}";
            var fullRemoteUrl = $"{_options.NextCloudBaseUrl?.TrimEnd('/')}/{remotePath}";

            // Ensure the remote directory exists.
            await EnsureRemoteDirectoryAsync(client, blob.OwnerEntityId, ct);

            await using var fs = new FileStream(blob.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await client.PutFile(fullRemoteUrl, fs);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Blob {Id} synced to NextCloud: {RemotePath}", blob.Id, remotePath);
                return true;
            }

            _logger.LogWarning("NextCloud PUT failed for blob {Id}: {Status}", blob.Id, result.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception syncing blob {Id} to NextCloud.", blob.Id);
            return false;
        }
    }

    public async Task<Stream> ReadAsync(BlobEntry blob, CancellationToken ct = default)
    {
        // Prefer NextCloud; fall back to local disk.
        if (blob.NextCloudPath is not null && _options.IsNextCloudConfigured)
        {
            try
            {
                using var client = BuildWebDavClient();
                var fullUrl = $"{_options.NextCloudBaseUrl?.TrimEnd('/')}/{blob.NextCloudPath}";
                var response = await client.GetRawFile(fullUrl);
                if (response.IsSuccessful && response.Stream is not null)
                    return response.Stream;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NextCloud read failed for blob {Id}, falling back to local.", blob.Id);
            }
        }

        if (blob.LocalPath is null)
            throw new FileNotFoundException($"Blob {blob.Id} has no available storage location.");

        return new FileStream(blob.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task DeleteAsync(BlobEntry blob, CancellationToken ct = default)
    {
        // Delete from NextCloud first.
        if (blob.NextCloudPath is not null && _options.IsNextCloudConfigured)
        {
            try
            {
                using var client = BuildWebDavClient();
                var fullUrl = $"{_options.NextCloudBaseUrl?.TrimEnd('/')}/{blob.NextCloudPath}";
                await client.Delete(fullUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete blob {Id} from NextCloud.", blob.Id);
            }
        }

        // Delete local file.
        if (blob.LocalPath is not null && File.Exists(blob.LocalPath))
        {
            File.Delete(blob.LocalPath);
            _logger.LogDebug("Local file deleted: {Path}", blob.LocalPath);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private WebDavClient BuildWebDavClient()
        => new(new WebDavClientParams
        {
            BaseAddress = new Uri(_options.NextCloudBaseUrl!),
            Credentials = new System.Net.NetworkCredential(
                _options.NextCloudUser,
                _options.NextCloudPassword),
        });

    private async Task EnsureRemoteDirectoryAsync(
        WebDavClient client, Guid ownerEntityId, CancellationToken ct)
    {
        var baseUrl  = _options.NextCloudBaseUrl!.TrimEnd('/');
        var rootDir  = $"{baseUrl}/{_options.NextCloudFolder}";
        var entityDir = $"{rootDir}/{ownerEntityId}";

        // MKCOL is idempotent — safe to call even if directory already exists.
        await client.Mkcol(rootDir);
        await client.Mkcol(entityDir);
    }

    private static string SanitizeFileName(string name)
        => string.Concat(
            Path.GetFileNameWithoutExtension(name)
                .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
                .Take(60))
           + Path.GetExtension(name);
}
