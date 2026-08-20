using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeGuard.Infrastructure.Persistence.Repositories;

// ── Equipment ─────────────────────────────────────────────────────────────────

public sealed class EquipmentRepository : RepositoryBase<Equipment>, IEquipmentRepository
{
    public EquipmentRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<Equipment?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await Set
            .Include(e => e.Warranties).ThenInclude(w => w.NotificationRules)
            .Include(e => e.ServiceRecords).ThenInclude(sr => sr.NotificationRules)
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken ct = default)
        => await Set
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Equipment>> GetByCategoryAsync(
        EquipmentCategory category, CancellationToken ct = default)
        => await Set
            .Where(e => e.Category == category)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Equipment>> SearchByTagAsync(
        string tag, CancellationToken ct = default)
    {
        // SQLite JSON: EF Core translates Contains on a JSON array to a LIKE query.
        var normalised = tag.Trim().ToLowerInvariant();
        return await Set
            .Where(e => EF.Functions.Like(
                Db.Entry(e).Property<string>("Tags").CurrentValue, $"%\"{normalised}\"%"))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
    }
}

// ── Warranty ──────────────────────────────────────────────────────────────────

public sealed class WarrantyRepository : RepositoryBase<Warranty>, IWarrantyRepository
{
    public WarrantyRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<Warranty?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await Set
            .Include(w => w.NotificationRules)
            .Include(w => w.Attachments)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<Warranty>> GetByEquipmentAsync(
        Guid equipmentId, CancellationToken ct = default)
    {
        var all = await Set
            .Include(w => w.NotificationRules)
            .Where(w => w.EquipmentId == equipmentId)
            .ToListAsync(ct);

        return all.OrderBy(w => w.Period.End).ToList();
    }

    public async Task<IReadOnlyList<Warranty>> GetExpiringAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // DateOnly.ToString() is not translatable by EF Core + SQLite.
        // For a home server the total number of warranties is small —
        // loading all with includes and filtering in memory is fine.
        var all = await Set
            .Include(w => w.NotificationRules)
            .ToListAsync(ct);

        return all
            .Where(w => w.Period.End >= from && w.Period.End <= to)
            .ToList();
    }

    public async Task<IReadOnlyList<Warranty>> GetActiveAsync(
        DateOnly asOf, CancellationToken ct = default)
    {
        var all = await Set
            .Include(w => w.NotificationRules)
            .ToListAsync(ct);

        return all
            .Where(w => w.Period.End >= asOf)
            .OrderBy(w => w.Period.End)
            .ToList();
    }
}

// ── ServiceRecord ─────────────────────────────────────────────────────────────

public sealed class ServiceRecordRepository : RepositoryBase<ServiceRecord>, IServiceRecordRepository
{
    public ServiceRecordRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<ServiceRecord?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await Set
            .Include(sr => sr.NotificationRules)
            .Include(sr => sr.Attachments)
            .FirstOrDefaultAsync(sr => sr.Id == id, ct);

    public async Task<IReadOnlyList<ServiceRecord>> GetByEquipmentAsync(
        Guid equipmentId, CancellationToken ct = default)
    {
        var all = await Set
            .Include(sr => sr.NotificationRules)
            .Where(sr => sr.EquipmentId == equipmentId)
            .ToListAsync(ct);

        return all.OrderByDescending(sr => sr.ServiceDate).ToList();
    }

    public async Task<IReadOnlyList<ServiceRecord>> GetOverdueAsync(
        DateOnly asOf, CancellationToken ct = default)
    {
        var all = await Set.ToListAsync(ct);
        return all
            .Where(sr => sr.Status == ServiceStatus.Planned && sr.ServiceDate < asOf)
            .OrderBy(sr => sr.ServiceDate)
            .ToList();
    }

    public async Task<IReadOnlyList<ServiceRecord>> GetDueSoonAsync(
        DateOnly asOf, int withinDays, CancellationToken ct = default)
    {
        var until = asOf.AddDays(withinDays);
        var all   = await Set
            .Include(sr => sr.NotificationRules)
            .ToListAsync(ct);

        return all
            .Where(sr => sr.Status == ServiceStatus.Planned
                      && sr.ServiceDate >= asOf
                      && sr.ServiceDate <= until)
            .OrderBy(sr => sr.ServiceDate)
            .ToList();
    }

    public async Task<IReadOnlyList<ServiceRecord>> GetAllAsync(CancellationToken ct = default)
    => await Set
        .Include(sr => sr.NotificationRules)
        .OrderBy(sr => sr.ServiceDate)
        .ToListAsync(ct);
}

// ── RecurringRule ────────────────────────────────────────────────────────────

public sealed class RecurringRuleRepository : RepositoryBase<RecurringRule>, IRecurringRuleRepository
{
    public RecurringRuleRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<RecurringRule>> GetByEquipmentAsync(
        Guid equipmentId, CancellationToken ct = default)
        => await Set
            .Where(r => r.EquipmentId == equipmentId)
            .OrderBy(r => r.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RecurringRule>> GetActiveAsync(CancellationToken ct = default)
        => await Set
            .Where(r => r.IsActive)
            .OrderBy(r => r.Title)
            .ToListAsync(ct);
}

// ── MeterReading ─────────────────────────────────────────────────────────────

public sealed class MeterReadingRepository : RepositoryBase<MeterReading>, IMeterReadingRepository
{
    public MeterReadingRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<MeterReading>> GetByEquipmentAsync(
        Guid equipmentId, CancellationToken ct = default)
    {
        var all = await Set
            .Where(r => r.EquipmentId == equipmentId)
            .ToListAsync(ct);

        return all.OrderByDescending(r => r.ReadingDate).ToList();
    }

    public async Task<MeterReading?> FindAsync(
        Guid equipmentId, DateOnly readingDate, MeterReadingSource source, CancellationToken ct = default)
        => await Set.FirstOrDefaultAsync(
            r => r.EquipmentId == equipmentId && r.ReadingDate == readingDate && r.Source == source, ct);
}

// ── BlobEntry ─────────────────────────────────────────────────────────────────

public sealed class BlobEntryRepository : RepositoryBase<BlobEntry>, IBlobEntryRepository
{
    public BlobEntryRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<BlobEntry>> GetByOwnerAsync(
        Guid ownerEntityId, CancellationToken ct = default)
    {
        // SQLite can't translate ORDER BY over DateTimeOffset (see GetPendingSyncAsync
        // below, which already works around this the same way) — order client-side.
        var owned = await Set.Where(b => b.OwnerEntityId == ownerEntityId).ToListAsync(ct);
        return [.. owned.OrderBy(b => b.CreatedAt)];
    }

    public async Task<IReadOnlyList<BlobEntry>> GetPendingSyncAsync(CancellationToken ct = default)
    {
        var all = await Set.ToListAsync(ct);
        return all
            .Where(b => b.SyncStatus == BlobSyncStatus.LocalOnly
                     || b.SyncStatus == BlobSyncStatus.SyncFailed)
            .OrderBy(b => b.CreatedAt)
            .ToList();
    }
}

// ── ScheduledJob ──────────────────────────────────────────────────────────────

public sealed class ScheduledJobRepository : RepositoryBase<ScheduledJob>, IScheduledJobRepository
{
    public ScheduledJobRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<ScheduledJob>> GetReadyJobsAsync(
        DateTimeOffset now, int limit = 20, CancellationToken ct = default)
    {
        // Enum and DateTimeOffset comparisons are not translatable in SQLite via EF Core.
        // Job count stays small — client-side filtering is fine.
        var all = await Set.ToListAsync(ct);
        return all
            .Where(j => j.Status == JobStatus.Pending && j.RunAfter <= now)
            .OrderBy(j => j.RunAfter)
            .Take(limit)
            .ToList();
    }

    public async Task<bool> ExistsPendingAsync(string correlationKey, CancellationToken ct = default)
    {
        var all = await Set
            .Where(j => j.CorrelationKey == correlationKey)
            .ToListAsync(ct);

        return all.Any(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Running);
    }
}

// ── AppUser ───────────────────────────────────────────────────────────────────

public sealed class AppUserRepository : RepositoryBase<AppUser>, IAppUserRepository
{
    public AppUserRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default)
        => await Set.OrderBy(u => u.DisplayName).ToListAsync(ct);

    public async Task<AppUser?> GetWithCredentialsAsync(Guid id, CancellationToken ct = default)
        => await Set
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<(AppUser User, PasskeyCredential Credential)?> FindByCredentialIdAsync(
        byte[] credentialId, CancellationToken ct = default)
    {
        var credential = await Db.Credentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId, ct);

        if (credential is null) return null;

        var user = await Set.FindAsync([credential.UserId], ct);
        if (user is null) return null;

        return (user, credential);
    }
}

// ── Contract ──────────────────────────────────────────────────────────────────

public sealed class ContractRepository : RepositoryBase<Contract>, IContractRepository
{
    public ContractRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Contract>> GetAllAsync(
        ContractKind? kind = null,
        ContractStatus? status = null,
        Guid? equipmentId = null,
        CancellationToken ct = default)
    {
        var query = Set.AsQueryable();

        if (kind        is { } k) query = query.Where(c => c.Kind == k);
        if (status      is { } s) query = query.Where(c => c.Status == s);
        if (equipmentId is { } e) query = query.Where(c => c.EquipmentId == e);

        var all = await query.ToListAsync(ct);
        return all.OrderBy(c => c.Name).ToList();
    }

    public async Task<Contract?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await Set
            .Include(c => c.Revisions).ThenInclude(r => r.Adjustments)
            .Include(c => c.Payments)
            .Include(c => c.NotificationRules)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Contract>> GetByEquipmentAsync(
        Guid equipmentId, CancellationToken ct = default)
    {
        var all = await Set
            .Include(c => c.Revisions).ThenInclude(r => r.Adjustments)
            .Where(c => c.EquipmentId == equipmentId)
            .ToListAsync(ct);

        return all.OrderBy(c => c.Name).ToList();
    }

    public async Task<IReadOnlyList<Contract>> GetExpiringAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        // Dates are stored as text and DateOnly comparison is not translatable, so the
        // filtering happens in memory — the same trade-off WarrantyRepository makes, and
        // for the same reason: a household has dozens of these, not millions.
        var all = await Set
            .Where(c => c.Status == ContractStatus.Active && c.EndDate != null)
            .ToListAsync(ct);

        return all
            .Where(c =>
            {
                var watched = c.CancellationDeadline ?? c.EndDate!.Value;
                return watched >= fromDate && watched <= toDate;
            })
            .OrderBy(c => c.CancellationDeadline ?? c.EndDate!.Value)
            .ToList();
    }

    public async Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken ct = default)
        => await Db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public void RemovePayment(Payment payment) => Db.Payments.Remove(payment);

    public async Task<IReadOnlyList<(Payment Payment, Contract Contract)>> GetPaymentsDueWithContractAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await Db.Payments
            .Where(p => p.Status == PaymentStatus.Planned)
            .Join(Set, p => p.ContractId, c => c.Id, (p, c) => new { Payment = p, Contract = c })
            .ToListAsync(ct);

        return rows
            .Where(r => r.Payment.DueDate >= fromDate && r.Payment.DueDate <= toDate)
            .OrderBy(r => r.Payment.DueDate)
            .Select(r => (r.Payment, r.Contract))
            .ToList();
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsDueAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var all = await Db.Payments
            .Where(p => p.Status == PaymentStatus.Planned)
            .ToListAsync(ct);

        return all
            .Where(p => p.DueDate >= fromDate && p.DueDate <= toDate)
            .OrderBy(p => p.DueDate)
            .ToList();
    }

    public async Task<IReadOnlyList<Contract>> GetActiveWithSchedulesAsync(CancellationToken ct = default)
    {
        var all = await Set
            .Include(c => c.Revisions).ThenInclude(r => r.Adjustments)
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active)
            .ToListAsync(ct);

        return all.OrderBy(c => c.Name).ToList();
    }
}
