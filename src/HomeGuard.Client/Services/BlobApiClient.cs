using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;

namespace HomeGuard.Client.Services;

public sealed class BlobApiClient
{
    private readonly HttpClient _http;

    // 20 MB max per file — enough for photos and scanned documents.
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    public BlobApiClient(HttpClient http) => _http = http;

    /// <summary>
    /// Upload a browser file (from InputFile) to the server.
    /// Returns the new blob ID on success, null on failure.
    /// </summary>
    public async Task<Guid?> UploadAsync(
        IBrowserFile file,
        Guid ownerEntityId,
        string ownerEntityType,
        CancellationToken ct = default)
    {
        await using var stream  = file.OpenReadStream(MaxFileSizeBytes, ct);
        using var fileContent   = new StreamContent(stream);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

        using var form = new MultipartFormDataContent();
        form.Add(fileContent,                           "file",            file.Name);
        form.Add(new StringContent(ownerEntityId.ToString()), "ownerEntityId");
        form.Add(new StringContent(ownerEntityType),          "ownerEntityType");

        var resp = await _http.PostAsync("api/blobs/upload", form, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadFromJsonAsync<BlobUploadResult>(ct);
        return body?.Id;
    }

    /// <summary>Returns a URL to stream the blob directly from the API.</summary>
    public string GetDownloadUrl(Guid blobId)
        => $"{_http.BaseAddress}api/blobs/{blobId}";

    public async Task<bool> DeleteAsync(Guid blobId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/blobs/{blobId}", ct);
        return resp.IsSuccessStatusCode;
    }
}

public sealed record BlobUploadResult(Guid Id, string SyncStatus);
