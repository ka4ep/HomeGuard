using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record CreateRecurringRuleCommand(
    Guid EquipmentId,
    string Title,
    int? IntervalDays = null,
    decimal? IntervalMeter = null,
    int MaterializeDaysAhead = 30,
    int PredictionsAhead = 2,
    bool AnchorToPurchaseDate = true
);

public sealed record UpdateRecurringRuleCommand(
    Guid Id,
    string Title,
    int? IntervalDays = null,
    decimal? IntervalMeter = null,
    int MaterializeDaysAhead = 30,
    int PredictionsAhead = 2,
    bool AnchorToPurchaseDate = true,
    bool IsActive = true
);

/// <summary>A rule together with its currently-computed predictions.</summary>
public sealed record RecurringRuleWithPredictions(RecurringRule Rule, IReadOnlyList<PredictedEvent> Predictions);

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class RecurringRuleService
{
    private readonly IRecurringRuleRepository _rules;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly IEquipmentRepository _equipment;
    private readonly IMeterReadingRepository _meterReadings;
    private readonly IUnitOfWork _uow;

    public RecurringRuleService(
        IRecurringRuleRepository rules,
        IServiceRecordRepository serviceRecords,
        IEquipmentRepository equipment,
        IMeterReadingRepository meterReadings,
        IUnitOfWork uow)
    {
        _rules = rules;
        _serviceRecords = serviceRecords;
        _equipment = equipment;
        _meterReadings = meterReadings;
        _uow = uow;
    }

    public Task<RecurringRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _rules.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<RecurringRule>> GetByEquipmentAsync(Guid equipmentId, CancellationToken ct = default)
        => _rules.GetByEquipmentAsync(equipmentId, ct);

    /// <summary>Finds the rule for an equipment whose Title matches (case/whitespace-insensitive), if any.</summary>
    public async Task<RecurringRule?> FindByTitleAsync(Guid equipmentId, string title, CancellationToken ct = default)
    {
        var rules = await _rules.GetByEquipmentAsync(equipmentId, ct);
        var norm = title.Trim();
        return rules.FirstOrDefault(r => string.Equals(r.Title, norm, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<RecurringRule> CreateAsync(CreateRecurringRuleCommand cmd, CancellationToken ct = default)
    {
        var rule = RecurringRule.Create(
            cmd.EquipmentId, cmd.Title, cmd.IntervalDays, cmd.IntervalMeter,
            cmd.MaterializeDaysAhead, cmd.PredictionsAhead, cmd.AnchorToPurchaseDate);

        await _rules.AddAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<RecurringRule> UpdateAsync(UpdateRecurringRuleCommand cmd, CancellationToken ct = default)
    {
        var rule = await _rules.GetByIdAsync(cmd.Id, ct)
            ?? throw new KeyNotFoundException($"RecurringRule {cmd.Id} not found.");

        rule.Update(
            cmd.Title, cmd.IntervalDays, cmd.IntervalMeter,
            cmd.MaterializeDaysAhead, cmd.PredictionsAhead, cmd.AnchorToPurchaseDate);
        rule.SetActive(cmd.IsActive);

        await _uow.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _rules.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"RecurringRule {id} not found.");

        _rules.Remove(rule);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Computes predictions for every active rule. Used to render the timeline.</summary>
    public async Task<IReadOnlyList<RecurringRuleWithPredictions>> GetAllWithPredictionsAsync(CancellationToken ct = default)
    {
        var rules = await _rules.GetActiveAsync(ct);
        var results = new List<RecurringRuleWithPredictions>(rules.Count);

        foreach (var rule in rules)
        {
            var predictions = await PredictAsync(rule, ct);
            results.Add(new RecurringRuleWithPredictions(rule, predictions));
        }

        return results;
    }

    public async Task<IReadOnlyList<PredictedEvent>> PredictAsync(RecurringRule rule, CancellationToken ct = default)
    {
        var equipment = await _equipment.GetByIdAsync(rule.EquipmentId, ct);
        if (equipment is null) return [];

        var equipmentRecords = await _serviceRecords.GetByEquipmentAsync(rule.EquipmentId, ct);
        var ruleRecords = equipmentRecords
            .Where(r => string.Equals(r.Title.Trim(), rule.Title, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Usage history from both sources: completed records' readings + standalone readings.
        var standalone = await _meterReadings.GetByEquipmentAsync(rule.EquipmentId, ct);
        var meterHistory = equipmentRecords
            .Where(r => r.Status == ServiceStatus.Completed && r.MeterReading.HasValue)
            .Select(r => new MeterPoint(r.ServiceDate, r.MeterReading!.Value))
            .Concat(standalone.Select(r => new MeterPoint(r.ReadingDate, r.Value)))
            .ToList();

        return TimelinePredictionService.Predict(rule, equipment, ruleRecords, meterHistory);
    }

    /// <summary>
    /// Materializes the nearest prediction right now, regardless of MaterializeDaysAhead.
    /// No-op (returns the existing one) if a Planned record for this rule already exists.
    /// </summary>
    public async Task<ServiceRecord?> MaterializeNowAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = await _rules.GetByIdAsync(ruleId, ct)
            ?? throw new KeyNotFoundException($"RecurringRule {ruleId} not found.");

        var equipmentRecords = await _serviceRecords.GetByEquipmentAsync(rule.EquipmentId, ct);

        var existingPlanned = equipmentRecords.FirstOrDefault(
            r => r.RecurringRuleId == rule.Id && r.Status == ServiceStatus.Planned);
        if (existingPlanned is not null) return existingPlanned;

        var predictions = await PredictAsync(rule, ct);
        if (predictions.Count == 0) return null;
        var next = predictions[0];

        var record = ServiceRecord.Create(
            rule.EquipmentId, rule.Title, next.Date, ServiceStatus.Planned,
            meterReading: next.MeterReading, recurringRuleId: rule.Id, originalPredictedDate: next.Date);

        await _serviceRecords.AddAsync(record, ct);
        await _uow.SaveChangesAsync(ct);
        return record;
    }
}
