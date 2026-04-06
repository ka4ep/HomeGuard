using HomeGuard.Common.Sync;
using System.Net.Http.Json;
using System.Text.Json;

namespace HomeGuard.Client.Services;

// ── Shared options ────────────────────────────────────────────────────────────

internal static class Json
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}

// ── Equipment ─────────────────────────────────────────────────────────────────

public sealed class EquipmentApiClient
{
    private readonly HttpClient _http;
    public EquipmentApiClient(HttpClient http) => _http = http;

    public Task<List<EquipmentSummary>?> GetAllAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<EquipmentSummary>>("api/equipment", ct);

    public Task<EquipmentDetail?> GetAsync(Guid id, CancellationToken ct = default)
        => _http.GetFromJsonAsync<EquipmentDetail>($"api/equipment/{id}", ct);

    public async Task<EquipmentSummary?> CreateAsync(CreateEquipmentDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/equipment", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<EquipmentSummary>(ct);
    }

    public async Task<EquipmentSummary?> UpdateAsync(Guid id, UpdateEquipmentDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/equipment/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<EquipmentSummary>(ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => _http.DeleteAsync($"api/equipment/{id}", ct);
}

// ── Warranty ──────────────────────────────────────────────────────────────────

public sealed class WarrantyApiClient
{
    private readonly HttpClient _http;
    public WarrantyApiClient(HttpClient http) => _http = http;

    public Task<List<WarrantyDto>?> GetActiveAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<WarrantyDto>>("api/warranties/active", ct);

    public Task<List<WarrantyDto>?> GetExpiringAsync(int days, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<WarrantyDto>>($"api/warranties/expiring?days={days}", ct);

    public Task<List<WarrantyDto>?> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<WarrantyDto>>($"api/warranties/by-equipment/{equipmentId}", ct);

    public async Task<WarrantyDto?> CreateAsync(CreateWarrantyDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/warranties", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<WarrantyDto>(ct);
    }

    public async Task<WarrantyDto?> UpdateAsync(Guid id, UpdateWarrantyDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/warranties/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<WarrantyDto>(ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => _http.DeleteAsync($"api/warranties/{id}", ct);
}

// ── Service records ───────────────────────────────────────────────────────────

public sealed class ServiceRecordApiClient
{
    private readonly HttpClient _http;
    public ServiceRecordApiClient(HttpClient http) => _http = http;

    public Task<List<ServiceRecordDto>?> GetOverdueAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ServiceRecordDto>>("api/service-records/overdue", ct);

    public Task<List<ServiceRecordDto>?> GetDueSoonAsync(int days, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ServiceRecordDto>>($"api/service-records/due-soon?days={days}", ct);

    public Task<List<ServiceRecordDto>?> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ServiceRecordDto>>($"api/service-records/by-equipment/{equipmentId}", ct);

    public async Task<ServiceRecordDto?> CreateAsync(CreateServiceRecordDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/service-records", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ServiceRecordDto>(ct);
    }

    public async Task<ServiceRecordDto?> UpdateAsync(Guid id, UpdateServiceRecordDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/service-records/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ServiceRecordDto>(ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => _http.DeleteAsync($"api/service-records/{id}", ct);
}

// ── Sync ──────────────────────────────────────────────────────────────────────

public sealed class SyncApiClient
{
    private readonly HttpClient _http;
    public SyncApiClient(HttpClient http) => _http = http;

    public async Task<SyncBatchResponse?> PostBatchAsync(
        SyncBatchRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/sync/batch", request, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<SyncBatchResponse>(ct);
    }
}

// ── Notification ──────────────────────────────────────────────────────────────

public sealed class NotificationApiClient
{
    private readonly HttpClient _http;
    public NotificationApiClient(HttpClient http) => _http = http;

    public async Task<string?> GetVapidPublicKeyAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<VapidKeyResponse>(
            "api/notifications/vapid-public-key", ct);
        return result?.Key;
    }

    public async Task SubscribeAsync(string endpoint, string p256dh, string auth, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/notifications/subscribe",
            new { endpoint, p256dh, auth }, ct);
        resp.EnsureSuccessStatusCode();
    }
}

file sealed record VapidKeyResponse(string Key);
