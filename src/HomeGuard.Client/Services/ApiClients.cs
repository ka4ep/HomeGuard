using System.Net.Http.Json;
using System.Text.Json;
using HomeGuard.Common.Sync;

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

    public Task<EquipmentDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _http.GetFromJsonAsync<EquipmentDetailDto>($"api/equipment/{id}", ct);

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

    public Task<List<ServiceRecordDto>?> GetAllAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ServiceRecordDto>>("api/service-records", ct);

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

// ── Recurring rules ───────────────────────────────────────────────────────────

public sealed class RecurringRuleApiClient
{
    private readonly HttpClient _http;
    public RecurringRuleApiClient(HttpClient http) => _http = http;

    public Task<List<RecurringRuleWithPredictionsDto>?> GetAllWithPredictionsAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<RecurringRuleWithPredictionsDto>>("api/recurring-rules", ct);

    public Task<List<RecurringRuleDto>?> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<RecurringRuleDto>>($"api/recurring-rules/by-equipment/{equipmentId}", ct);

    public async Task<RecurringRuleDto?> FindByTitleAsync(Guid equipmentId, string title, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(
            $"api/recurring-rules/by-equipment/{equipmentId}/by-title?title={Uri.EscapeDataString(title)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RecurringRuleDto>(ct);
    }

    public async Task<RecurringRuleDto?> CreateAsync(CreateRecurringRuleDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/recurring-rules", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RecurringRuleDto>(ct);
    }

    public async Task<RecurringRuleDto?> UpdateAsync(Guid id, UpdateRecurringRuleDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/recurring-rules/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<RecurringRuleDto>(ct);
    }

    public async Task<ServiceRecordDto?> MaterializeNowAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/recurring-rules/{id}/materialize", null, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ServiceRecordDto>(ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => _http.DeleteAsync($"api/recurring-rules/{id}", ct);
}

// ── Meter readings ────────────────────────────────────────────────────────────

public sealed class MeterReadingApiClient
{
    private readonly HttpClient _http;
    public MeterReadingApiClient(HttpClient http) => _http = http;

    /// <summary>Merged history (standalone + service-record readings), newest first.</summary>
    public Task<List<MeterReadingDto>?> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<MeterReadingDto>>($"api/meter-readings/by-equipment/{equipmentId}", ct);

    public async Task<MeterReadingDto?> CreateAsync(CreateMeterReadingDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/meter-readings", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<MeterReadingDto>(ct);
    }

    public async Task<MeterReadingDto?> UpdateAsync(Guid id, UpdateMeterReadingDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/meter-readings/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<MeterReadingDto>(ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => _http.DeleteAsync($"api/meter-readings/{id}", ct);
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

public sealed class AttentionApiClient
{
    private readonly HttpClient _http;
    public AttentionApiClient(HttpClient http) => _http = http;

    public Task<AttentionDto?> GetAsync(int days = 7, CancellationToken ct = default)
        => _http.GetFromJsonAsync<AttentionDto>($"api/attention?days={days}", ct);
}

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

// ── Contracts ─────────────────────────────────────────────────────────────────

public sealed class ContractApiClient
{
    private readonly HttpClient _http;
    public ContractApiClient(HttpClient http) => _http = http;

    public Task<List<ContractDto>?> GetAllAsync(
        ContractKind? kind = null, ContractStatus? status = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (kind   is { } k) query.Add($"kind={(int)k}");
        if (status is { } s) query.Add($"status={(int)s}");
        var suffix = query.Count == 0 ? "" : "?" + string.Join('&', query);

        return _http.GetFromJsonAsync<List<ContractDto>>($"api/contracts{suffix}", ct);
    }

    public Task<List<ContractDto>?> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ContractDto>>($"api/contracts/by-equipment/{equipmentId}", ct);

    public Task<List<ContractDto>?> GetExpiringAsync(int days, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<ContractDto>>($"api/contracts/expiring?days={days}", ct);

    public Task<ContractDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _http.GetFromJsonAsync<ContractDetailDto>($"api/contracts/{id}", ct);

    public Task<ContractSummaryDto?> GetSummaryAsync(Guid id, CancellationToken ct = default)
        => _http.GetFromJsonAsync<ContractSummaryDto>($"api/contracts/{id}/summary", ct);

    public Task<List<ScheduleEntryDto>?> GetScheduleAsync(
        Guid id, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (from is { } f) query.Add($"from={f:yyyy-MM-dd}");
        if (to   is { } t) query.Add($"to={t:yyyy-MM-dd}");
        var suffix = query.Count == 0 ? "" : "?" + string.Join('&', query);

        return _http.GetFromJsonAsync<List<ScheduleEntryDto>>($"api/contracts/{id}/schedule{suffix}", ct);
    }

    public async Task<ContractDto?> CreateAsync(CreateContractDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/contracts", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ContractDto>(ct)
            : null;
    }

    public async Task<ContractDto?> UpdateAsync(Guid id, UpdateContractDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/contracts/{id}", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ContractDto>(ct)
            : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        => await _http.DeleteAsync($"api/contracts/{id}", ct);

    public async Task<ContractDetailDto?> SetOpeningAsync(
        Guid id, SetOpeningDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/contracts/{id}/opening", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ContractDetailDto>(ct)
            : null;
    }

    public async Task<bool> ClearOpeningAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/contracts/{id}/opening", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<PlanRevisionDto?> AddRevisionAsync(
        Guid id, AddRevisionDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/contracts/{id}/revisions", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PlanRevisionDto>(ct)
            : null;
    }

    public async Task<PaymentDto?> AddPaymentAsync(
        Guid id, AddPaymentDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/contracts/{id}/payments", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PaymentDto>(ct)
            : null;
    }

    public Task<List<UpcomingPaymentDto>?> GetUpcomingAsync(int days = 30, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<UpcomingPaymentDto>>($"api/contracts/upcoming?days={days}", ct);

    public async Task<PaymentDto?> UpdatePaymentAsync(
        Guid paymentId, UpdatePaymentDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/payments/{paymentId}", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PaymentDto>(ct)
            : null;
    }

    public async Task<bool> DeletePaymentAsync(Guid paymentId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/payments/{paymentId}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<PaymentDto?> ConfirmPaymentAsync(
        Guid paymentId, ConfirmPaymentDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/payments/{paymentId}/confirm", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PaymentDto>(ct)
            : null;
    }

    public async Task<EarlyPaymentPreviewDto?> PreviewEarlyPaymentAsync(
        Guid id, EarlyPaymentPreviewRequestDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/contracts/{id}/early-payment/preview", dto, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<EarlyPaymentPreviewDto>(ct)
            : null;
    }

    public Task<List<MonthlyLoadEntryDto>?> GetMonthlyLoadAsync(int months = 12, CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<MonthlyLoadEntryDto>>($"api/finance/monthly?months={months}", ct);
}
