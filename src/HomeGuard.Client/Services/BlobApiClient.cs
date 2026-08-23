using System.Net.Http.Headers;
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
        form.Add(fileContent, "file", file.Name);

        var resp = await _http.PostAsync(UploadUrl(ownerEntityId, ownerEntityType), form, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadFromJsonAsync<BlobUploadResult>(ct);
        return body?.Id;
    }

    /// <summary>
    /// Upload raw bytes (from <c>DocumentCapture</c>) to the server.
    /// <paramref name="clientOperationId"/>, when supplied, makes a retried call after a
    /// dropped connection idempotent — the server returns the original blob instead of
    /// creating a duplicate. Returns the blob ID on success, null on failure.
    /// </summary>
    public async Task<Guid?> UploadAsync(
        byte[] data, string mimeType, string fileName,
        Guid ownerEntityId, string ownerEntityType,
        Guid? clientOperationId = null,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(data);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
        content.Add(fileContent, "file", fileName);

        var url = UploadUrl(ownerEntityId, ownerEntityType, clientOperationId);
        var resp = await _http.PostAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadFromJsonAsync<BlobUploadResult>(ct);
        return body?.Id;
    }

    // ownerEntityId/ownerEntityType/clientOperationId bind server-side via [FromQuery] —
    // they must travel in the URL, not as multipart form fields.
    private static string UploadUrl(Guid ownerEntityId, string ownerEntityType, Guid? clientOperationId = null)
    {
        var url = $"api/blobs/upload?ownerEntityId={ownerEntityId}&ownerEntityType={Uri.EscapeDataString(ownerEntityType)}";
        if (clientOperationId is { } id) url += $"&clientOperationId={id}";
        return url;
    }

    /// <summary>Returns a URL to stream the blob directly from the API.</summary>
    public string GetDownloadUrl(Guid blobId)
        => $"{_http.BaseAddress}api/blobs/{blobId}";

    public Task<List<BlobDto>?> GetByOwnerAsync(Guid ownerEntityId, CancellationToken ct = default)
        // Json.Options, not the bare default — BlobDto.SyncStatus is a real enum and the
        // Api sends it as a string; see Json.Options' own comment for why this needs to
        // be explicit here.
        => _http.GetFromJsonAsync<List<BlobDto>>($"api/blobs?ownerEntityId={ownerEntityId}", Json.Options, ct);

    public async Task<bool> DeleteAsync(Guid blobId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/blobs/{blobId}", ct);
        return resp.IsSuccessStatusCode;
    }
}

public sealed record BlobUploadResult(Guid Id, string SyncStatus);
