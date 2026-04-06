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
        => await Set
            .Include(w => w.NotificationRules)
            .Where(w => w.EquipmentId == equipmentId)
            .OrderBy(w => w.Period.End)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Warranty>> GetExpiringAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var fromStr = from.ToString("yyyy-MM-dd", null);
        var toStr   = to.ToString("yyyy-MM-dd", null);
        return await Set
            .Include(w => w.NotificationRules)
            .Where(w => string.Compare(w.Period.End.ToString(provider: null), fromStr, StringComparison.Ordinal) >= 0
                     && string.Compare(w.Period.End.ToString(provider: null), toStr, StringComparison.Ordinal)   <= 0)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Warranty>> GetActiveAsync(
        DateOnly asOf, CancellationToken ct = default)
    {
        var asOfStr = asOf.ToString("yyyy-MM-dd", null);
        return await Set
            .Include(w => w.NotificationRules)
            .Where(w => string.Compare(w.Period.End.ToString(provider: null), asOfStr, StringComparison.Ordinal) >= 0)
            .OrderBy(w => w.Period.End)
            .ToListAsync(ct);
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
        => await Set
            .Include(sr => sr.NotificationRules)
            .Where(sr => sr.EquipmentId == equipmentId)
            .OrderByDescending(sr => sr.ServiceDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ServiceRecord>> GetOverdueAsync(
        DateOnly asOf, CancellationToken ct = default)
    {
        var asOfStr = asOf.ToString("yyyy-MM-dd", null);
        return await Set
            .Where(sr => sr.NextServiceDate != null
                      && string.Compare(sr.NextServiceDate.Value.ToString(provider: null), asOfStr, StringComparison.Ordinal) < 0)
            .OrderBy(sr => sr.NextServiceDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ServiceRecord>> GetDueSoonAsync(
        DateOnly asOf, int withinDays, CancellationToken ct = default)
    {
        var asOfStr  = asOf.ToString("yyyy-MM-dd", null);
        var untilStr = asOf.AddDays(withinDays).ToString("yyyy-MM-dd", null);
        return await Set
            .Include(sr => sr.NotificationRules)
            .Where(sr => sr.NextServiceDate != null
                      && string.Compare(sr.NextServiceDate.Value.ToString(provider: null), asOfStr, StringComparison.Ordinal)  >= 0
                      && string.Compare(sr.NextServiceDate.Value.ToString(provider: null), untilStr, StringComparison.Ordinal) <= 0)
            .OrderBy(sr => sr.NextServiceDate)
            .ToListAsync(ct);
    }
}

// ── BlobEntry ─────────────────────────────────────────────────────────────────

public sealed class BlobEntryRepository : RepositoryBase<BlobEntry>, IBlobEntryRepository
{
    public BlobEntryRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<BlobEntry>> GetByOwnerAsync(
        Guid ownerEntityId, CancellationToken ct = default)
        => await Set
            .Where(b => b.OwnerEntityId == ownerEntityId)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BlobEntry>> GetPendingSyncAsync(CancellationToken ct = default)
        => await Set
            .Where(b => b.SyncStatus == BlobSyncStatus.LocalOnly
                     || b.SyncStatus == BlobSyncStatus.SyncFailed)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);
}

// ── ScheduledJob ──────────────────────────────────────────────────────────────

public sealed class ScheduledJobRepository : RepositoryBase<ScheduledJob>, IScheduledJobRepository
{
    public ScheduledJobRepository(HomeGuardDbContext db) : base(db) { }

    public async Task<IReadOnlyList<ScheduledJob>> GetReadyJobsAsync(
        DateTimeOffset now, int limit = 20, CancellationToken ct = default)
        => await Set
            .Where(j => j.Status == JobStatus.Pending && j.RunAfter <= now)
            .OrderBy(j => j.RunAfter)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<bool> ExistsPendingAsync(string correlationKey, CancellationToken ct = default)
        => await Set.AnyAsync(
            j => j.CorrelationKey == correlationKey
              && (j.Status == JobStatus.Pending || j.Status == JobStatus.Running),
            ct);
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
