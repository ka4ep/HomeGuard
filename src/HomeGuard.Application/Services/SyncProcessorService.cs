using System.Text.Json;
using HomeGuard.Application.Interfaces;
using HomeGuard.Domain.Enums;
using HomeGuard.Common.Sync;

namespace HomeGuard.Application.Services;

/// <summary>
/// Server-side processor for offline sync batches sent by the Blazor client.
///
/// Protocol:
///   1. Client sends a <see cref="SyncBatchRequest"/> with N <see cref="OutboxEntry"/> items.
///   2. This service processes each entry in order.
///   3. Already-processed entries (same ClientOperationId) return the cached <see cref="SyncAck"/>.
///   4. Each successfully processed entry is persisted in <see cref="IProcessedOperationStore"/>.
///   5. A <see cref="SyncBatchResponse"/> with one ack per entry is returned.
///
/// The handler is idempotent: replaying the same batch is safe.
/// </summary>
public sealed class SyncProcessorService
{
    private readonly IProcessedOperationStore _operationStore;
    private readonly EquipmentService _equipmentService;
    private readonly WarrantyService _warrantyService;
    private readonly ContractService _contractService;

    // Add more services as more operation types are introduced.

    public SyncProcessorService(
        IProcessedOperationStore operationStore,
        EquipmentService equipmentService,
        WarrantyService warrantyService,
        ContractService contractService)
    {
        _operationStore = operationStore;
        _equipmentService = equipmentService;
        _warrantyService = warrantyService;
        _contractService = contractService;
    }

    public async Task<SyncBatchResponse> ProcessBatchAsync(
        Guid userId,
        SyncBatchRequest request,
        CancellationToken ct = default)
    {
        var acks = new List<SyncAck>(request.Entries.Count);

        foreach (var entry in request.Entries)
        {
            var cached = await _operationStore.GetCachedAckAsync(entry.ClientOperationId, ct);
            if (cached is not null)
            {
                // Return cached result — client is replaying.
                acks.Add(cached with { Status = SyncAckStatus.Duplicate });
                continue;
            }

            var ack = await DispatchAsync(entry, ct);

            await _operationStore.RecordAsync(
                entry.ClientOperationId, userId, entry.OperationType, ack, ct);

            acks.Add(ack);
        }

        return new SyncBatchResponse(acks);
    }

    // ── Dispatcher ────────────────────────────────────────────────────────────

    private async Task<SyncAck> DispatchAsync(OutboxEntry entry, CancellationToken ct)
    {
        try
        {
            switch (entry.OperationType)
            {
                case SyncOperationTypes.CreateEquipment:
                {
                    var cmd = Deserialize<CreateEquipmentCommand>(entry.PayloadJson);
                    await _equipmentService.CreateAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.UpdateEquipment:
                {
                    var cmd = Deserialize<UpdateEquipmentCommand>(entry.PayloadJson);
                    await _equipmentService.UpdateAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.DeleteEquipment:
                {
                    var payload = Deserialize<DeletePayload>(entry.PayloadJson);
                    await _equipmentService.DeleteAsync(payload.Id, ct);
                    break;
                }
                case SyncOperationTypes.CreateWarranty:
                {
                    var cmd = Deserialize<CreateWarrantyCommand>(entry.PayloadJson);
                    await _warrantyService.CreateAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.UpdateWarranty:
                {
                    var cmd = Deserialize<UpdateWarrantyCommand>(entry.PayloadJson);
                    await _warrantyService.UpdateAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.CreateContract:
                {
                    var cmd = Deserialize<CreateContractCommand>(entry.PayloadJson);
                    await _contractService.CreateAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.UpdateContract:
                {
                    var cmd = Deserialize<UpdateContractCommand>(entry.PayloadJson);
                    await _contractService.UpdateAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.DeleteContract:
                {
                    var payload = Deserialize<DeletePayload>(entry.PayloadJson);
                    await _contractService.DeleteAsync(payload.Id, ct);
                    break;
                }
                case SyncOperationTypes.AddPlanRevision:
                {
                    // The domain itself rejects a revision that starts before the active
                    // one (Contract.AddRevision) — that propagated InvalidOperationException
                    // is exactly what makes this case idempotent-safe under the generic
                    // catch below, so the non-commutative ordering rule needs no extra code.
                    var cmd = Deserialize<AddRevisionCommand>(entry.PayloadJson);
                    await _contractService.AddRevisionAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.CreatePayment:
                {
                    var cmd = Deserialize<AddPaymentCommand>(entry.PayloadJson);
                    await _contractService.AddPaymentAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.ConfirmPayment:
                {
                    var cmd = Deserialize<ConfirmPaymentCommand>(entry.PayloadJson);
                    await _contractService.ConfirmPaymentAsync(cmd, ct);
                    break;
                }
                case SyncOperationTypes.SetOpeningPosition:
                {
                    var cmd = Deserialize<SetOpeningCommand>(entry.PayloadJson);
                    await _contractService.SetOpeningAsync(cmd, ct);
                    break;
                }
                default:
                    return new SyncAck(
                        entry.ClientOperationId,
                        SyncAckStatus.Rejected,
                        $"Unknown operation type: '{entry.OperationType}'");
            }

            return new SyncAck(entry.ClientOperationId, SyncAckStatus.Committed);
        }
        catch (Exception ex)
        {
            // Don't let one bad entry block the rest of the batch.
            return new SyncAck(entry.ClientOperationId, SyncAckStatus.Rejected, ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json)
           ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");
}

/// <summary>Generic delete payload.</summary>
file sealed record DeletePayload(Guid Id);

/// <summary>
/// Centralised string constants for operation types, shared between the client Outbox
/// and the server dispatcher. Add here, handle in <see cref="SyncProcessorService.DispatchAsync"/>.
/// </summary>
public static class SyncOperationTypes
{
    public const string CreateEquipment  = "CreateEquipment";
    public const string UpdateEquipment  = "UpdateEquipment";
    public const string DeleteEquipment  = "DeleteEquipment";
    public const string CreateWarranty   = "CreateWarranty";
    public const string UpdateWarranty   = "UpdateWarranty";
    public const string DeleteWarranty   = "DeleteWarranty";
    public const string CreateServiceRecord = "CreateServiceRecord";
    public const string UpdateServiceRecord = "UpdateServiceRecord";
    public const string DeleteServiceRecord = "DeleteServiceRecord";
    public const string CreateContract      = "CreateContract";
    public const string UpdateContract      = "UpdateContract";
    public const string DeleteContract      = "DeleteContract";
    public const string AddPlanRevision     = "AddPlanRevision";
    public const string CreatePayment       = "CreatePayment";
    public const string ConfirmPayment      = "ConfirmPayment";
    public const string SetOpeningPosition  = "SetOpeningPosition";
}
