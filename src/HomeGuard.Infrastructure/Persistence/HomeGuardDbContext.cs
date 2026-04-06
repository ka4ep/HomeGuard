using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using HomeGuard.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HomeGuard.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the entire application.
///
/// SQLite concurrency strategy: a SemaphoreSlim(1,1) wraps every
/// SaveChangesAsync call via <see cref="HomeGuardUnitOfWork"/>.
/// All reads go through the normal EF async path — only writes are serialised.
/// This is intentional: SQLite's WAL mode allows concurrent reads, but only
/// one writer at a time.
/// </summary>
public sealed class HomeGuardDbContext : DbContext
{
    public HomeGuardDbContext(DbContextOptions<HomeGuardDbContext> options) : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────

    public DbSet<Equipment>         Equipment         => Set<Equipment>();
    public DbSet<Warranty>          Warranties        => Set<Warranty>();
    public DbSet<ServiceRecord>     ServiceRecords    => Set<ServiceRecord>();
    public DbSet<BlobEntry>         BlobEntries       => Set<BlobEntry>();
    public DbSet<AppUser>           Users             => Set<AppUser>();
    public DbSet<PasskeyCredential> Credentials       => Set<PasskeyCredential>();
    public DbSet<ScheduledJob>      ScheduledJobs     => Set<ScheduledJob>();
    public DbSet<ProcessedOperation> ProcessedOperations => Set<ProcessedOperation>();

    public DbSet<PushSubscriptionEntity> PushSubscriptions => Set<PushSubscriptionEntity>();

    // ── Model configuration ───────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Converters shared across entities ─────────────────────────────────
        var dateOnlyConverter = new ValueConverter<DateOnly, string>(
            d => d.ToString("yyyy-MM-dd", null),
            s => DateOnly.Parse(s, null));

        // ── Equipment ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Equipment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Brand).HasMaxLength(100);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.Property(x => x.Category).HasConversion<int>();
            e.Property(x => x.PurchaseDate).HasConversion(dateOnlyConverter);
            e.Property(x => x.PurchasePrice).HasColumnType("TEXT"); // SQLite stores as text
            e.Property<List<string>>("_tags")
                .HasColumnName("Tags")
                .HasField("_tags")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());

            e.HasMany(x => x.Warranties)
                .WithOne()
                .HasForeignKey(w => w.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.ServiceRecords)
                .WithOne()
                .HasForeignKey(sr => sr.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Attachments)
                .WithOne()
                .HasForeignKey(b => b.OwnerEntityId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Warranty ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Warranty>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(200);
            e.Property(x => x.ContractNumber).HasMaxLength(100);
            e.Property(x => x.GoogleCalendarEventId).HasMaxLength(500);

            // DateRange as owned type — flattened to two columns.
            e.OwnsOne(x => x.Period, p =>
            {
                p.Property(d => d.Start)
                    .HasColumnName("PeriodStart")
                    .HasConversion(dateOnlyConverter);
                p.Property(d => d.End)
                    .HasColumnName("PeriodEnd")
                    .HasConversion(dateOnlyConverter);
            });

            // NotificationRules as owned entity collection.
            e.OwnsMany(x => x.NotificationRules, r =>
            {
                r.WithOwner().HasForeignKey("WarrantyId");
                r.HasKey("Id"); // shadow PK
                r.Property<int>("Id");
                r.Property(x => x.Offset).HasConversion<int>();
            });
        });

        // ── ServiceRecord ─────────────────────────────────────────────────────
        modelBuilder.Entity<ServiceRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.ServiceProvider).HasMaxLength(200);
            e.Property(x => x.OdometerReading).HasMaxLength(50);
            e.Property(x => x.GoogleCalendarEventId).HasMaxLength(500);
            e.Property(x => x.Cost).HasColumnType("TEXT");
            e.Property(x => x.ServiceDate).HasConversion(dateOnlyConverter);
            e.Property(x => x.NextServiceDate)
                .HasConversion(
                    d => d.HasValue ? d.Value.ToString("yyyy-MM-dd", null) : null,
                    s => s != null ? DateOnly.Parse(s, null) : (DateOnly?)null);

            e.OwnsMany(x => x.NotificationRules, r =>
            {
                r.WithOwner().HasForeignKey("ServiceRecordId");
                r.HasKey("Id");
                r.Property<int>("Id");
                r.Property(x => x.Offset).HasConversion<int>();
            });
        });

        // ── BlobEntry ─────────────────────────────────────────────────────────
        modelBuilder.Entity<BlobEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.OwnerEntityType).HasMaxLength(50).IsRequired();
            e.Property(x => x.LocalPath).HasMaxLength(1000);
            e.Property(x => x.NextCloudPath).HasMaxLength(1000);
            e.Property(x => x.SyncStatus).HasConversion<int>();
            e.HasIndex(x => new { x.OwnerEntityId, x.OwnerEntityType });
            e.HasIndex(x => x.SyncStatus);
        });

        // ── AppUser + PasskeyCredential ───────────────────────────────────────
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();

            e.HasMany(x => x.Credentials)
                .WithOne()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasskeyCredential>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.CredentialId).IsUnique();
        });

        // ── ScheduledJob ──────────────────────────────────────────────────────
        modelBuilder.Entity<ScheduledJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.JobType).HasMaxLength(100).IsRequired();
            e.Property(x => x.CorrelationKey).HasMaxLength(300);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.Status, x.RunAfter }); // picked up by GetReadyJobsAsync
            e.HasIndex(x => x.CorrelationKey);
        });

        // ── ProcessedOperation (idempotency store) ────────────────────────────
        modelBuilder.Entity<ProcessedOperation>(e =>
        {
            e.HasKey(x => x.ClientOperationId);
            e.Property(x => x.OperationType).HasMaxLength(100).IsRequired();
            e.Property(x => x.AckJson).IsRequired();
            e.HasIndex(x => x.ProcessedAt);
        });

        modelBuilder.Entity<PushSubscriptionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Endpoint).HasMaxLength(1000).IsRequired();
            e.Property(x => x.P256dh).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Auth).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => x.UserId);
        });
    }
}

/// <summary>
/// Persisted record of a completed sync operation.
/// The ClientOperationId is the PK — looking up by it is a point query.
/// </summary>
public sealed class ProcessedOperation
{
    public Guid ClientOperationId { get; set; }
    public Guid UserId { get; set; }
    public string OperationType { get; set; } = null!;
    public string AckJson { get; set; } = null!;
    public DateTimeOffset ProcessedAt { get; set; }
}
