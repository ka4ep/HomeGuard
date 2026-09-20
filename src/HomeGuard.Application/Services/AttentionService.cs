namespace HomeGuard.Application.Services;

/// <summary>Whether the item needs acting on now, or is just visible ahead of time.</summary>
public enum AttentionSeverity
{
    Soon,
    Urgent,
}

/// <summary>
/// One line the icon badge and the Home attention strip both point at — enough to name
/// the thing and link to it without opening the entity it came from.
/// </summary>
public sealed record AttentionItem(
    string Kind,   // "warranty" | "service" | "payment" | "contract"
    AttentionSeverity Severity,
    string Title,
    DateOnly Date,
    string Url);

/// <summary><c>Count</c> is what the platform badge shows — see contracts-spec.md §10.2.</summary>
public sealed record AttentionSummary(
    int Count, int Urgent, int Soon, IReadOnlyList<AttentionItem> Items);

/// <summary>
/// Merges the three things a household already tracks — warranty expiry, service due
/// dates, contract payments and cancellation windows — into the one number the app icon
/// and the Home strip agree on. Nothing here is new data; every source is an existing
/// service method (contracts-spec.md §10: "compute one number on the server").
/// </summary>
public sealed class AttentionService
{
    private const int DefaultHorizonDays = 7;

    private readonly WarrantyService _warranties;
    private readonly ServiceRecordService _services;
    private readonly ContractService _contracts;

    public AttentionService(
        WarrantyService warranties, ServiceRecordService services, ContractService contracts)
    {
        _warranties = warranties;
        _services = services;
        _contracts = contracts;
    }

    public async Task<AttentionSummary> GetAsync(
        int horizonDays = DefaultHorizonDays, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(horizonDays);

        // Sequential, not Task.WhenAll: WarrantyService/ServiceRecordService/ContractService
        // share one Scoped HomeGuardDbContext within this request, and EF Core does not
        // allow concurrent operations on one context — running these "in parallel" threw
        // intermittently inside whichever repository call lost the race. The
        // Task.WhenAll-for-parallel-calls convention in CLAUDE.md is for the client's HTTP
        // calls, each of which gets its own DbContext server-side; it does not apply here,
        // inside a single request.
        var warranties = await _warranties.GetExpiringAsync(today, horizon, ct);
        var overdue    = await _services.GetOverdueAsync(ct);
        var dueSoon    = await _services.GetDueSoonAsync(horizonDays, ct);
        var upcoming   = await _contracts.GetUpcomingAsync(horizonDays, ct);
        var expiring   = await _contracts.GetExpiringAsync(horizonDays, ct);

        var items = new List<AttentionItem>();

        foreach (var w in warranties)
            items.Add(new AttentionItem(
                "warranty", AttentionSeverity.Soon, w.Name, w.Period.End, $"/equipment/{w.EquipmentId}"));

        foreach (var s in overdue)
            items.Add(new AttentionItem(
                "service", AttentionSeverity.Urgent, s.Title, s.ServiceDate,
                $"/equipment/{s.EquipmentId}?editService={s.Id}"));

        // GetDueSoonAsync's window is not exclusive of the past — the overdue ones above
        // already cover that half, so only the still-ahead half belongs here too.
        foreach (var s in dueSoon.Where(s => s.ServiceDate >= today))
            items.Add(new AttentionItem(
                "service", AttentionSeverity.Soon, s.Title, s.ServiceDate,
                $"/equipment/{s.EquipmentId}?editService={s.Id}"));

        foreach (var p in upcoming)
            items.Add(new AttentionItem(
                "payment", p.IsOverdue ? AttentionSeverity.Urgent : AttentionSeverity.Soon,
                p.ContractName, p.DueDate, $"/contracts/{p.ContractId}"));

        // The cancellation window, not the end date itself — that is the deadline that
        // actually costs money to miss (contracts-spec.md §6).
        foreach (var c in expiring)
        {
            if (c.CancellationDeadline is not { } deadline || deadline > horizon) continue;
            items.Add(new AttentionItem(
                "contract",
                deadline <= today.AddDays(DefaultHorizonDays) ? AttentionSeverity.Urgent : AttentionSeverity.Soon,
                c.Name, deadline, $"/contracts/{c.Id}"));
        }

        var ordered = items.OrderBy(i => i.Date).ToList();
        var urgent  = ordered.Count(i => i.Severity == AttentionSeverity.Urgent);

        return new AttentionSummary(ordered.Count, urgent, ordered.Count - urgent, ordered);
    }
}
