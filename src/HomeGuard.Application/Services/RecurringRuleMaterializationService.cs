using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Enums;

namespace HomeGuard.Application.Services;

/// <summary>
/// Scans active RecurringRules and turns near-term predictions into real Planned
/// ServiceRecords once they fall within the rule's MaterializeDaysAhead window.
/// Designed to run once a day (via <c>IHostedService</c> timer in the Api project).
/// Idempotent: skips a rule that already has an un-completed Planned record.
/// </summary>
public sealed class RecurringRuleMaterializationService
{
    private readonly IRecurringRuleRepository _rules;
    private readonly IServiceRecordRepository _serviceRecords;
    private readonly RecurringRuleService _ruleService;
    private readonly IUnitOfWork _uow;

    public RecurringRuleMaterializationService(
        IRecurringRuleRepository rules,
        IServiceRecordRepository serviceRecords,
        RecurringRuleService ruleService,
        IUnitOfWork uow)
    {
        _rules = rules;
        _serviceRecords = serviceRecords;
        _ruleService = ruleService;
        _uow = uow;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rules = await _rules.GetActiveAsync(ct);

        foreach (var rule in rules)
        {
            var equipmentRecords = await _serviceRecords.GetByEquipmentAsync(rule.EquipmentId, ct);

            var alreadyPending = equipmentRecords.Any(
                r => r.RecurringRuleId == rule.Id && r.Status == ServiceStatus.Planned);
            if (alreadyPending) continue;

            var predictions = await _ruleService.PredictAsync(rule, ct);
            if (predictions.Count == 0) continue;
            var next = predictions[0];

            if (next.Date > today.AddDays(rule.MaterializeDaysAhead)) continue;

            var record = Domain.Entities.ServiceRecord.Create(
                rule.EquipmentId, rule.Title, next.Date, ServiceStatus.Planned,
                meterReading: next.MeterReading, recurringRuleId: rule.Id, originalPredictedDate: next.Date);

            await _serviceRecords.AddAsync(record, ct);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
