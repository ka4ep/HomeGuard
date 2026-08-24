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
    public DbSet<RecurringRule>     RecurringRules    => Set<RecurringRule>();
    public DbSet<MeterReading>      MeterReadings     => Set<MeterReading>();
    public DbSet<BlobEntry>         BlobEntries       => Set<BlobEntry>();
    public DbSet<Contract>          Contracts         => Set<Contract>();
    public DbSet<PaymentPlanRevision> PlanRevisions   => Set<PaymentPlanRevision>();
    public DbSet<Payment>           Payments          => Set<Payment>();
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
            e.Property(x => x.MeterUnit).HasMaxLength(20);
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

            // BlobEntry points back with OwnerEntityId + OwnerEntityType, which is a
            // polymorphic link and not a foreign key: the same column also answers to
            // Warranty and ServiceRecord, so a real FK to Equipment here would reject
            // any attachment whose owner isn't Equipment — exactly what broke warranty
            // and service-record photo uploads. Left unmapped here and resolved in the
            // repository instead, matching Contract/Payment below.
            e.Ignore(x => x.Attachments);
        });

        // ── Warranty ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Warranty>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(200);
            e.Property(x => x.ContractNumber).HasMaxLength(100);
            e.Property(x => x.GoogleCalendarEventId).HasMaxLength(500);
            e.Property(x => x.Cost).HasColumnType("TEXT");

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

            e.Ignore(x => x.Attachments);   // polymorphic, see the note on Equipment
        });

        // ── ServiceRecord ─────────────────────────────────────────────────────
        var nullableDateOnlyConverter = new ValueConverter<DateOnly?, string?>(
            d => d.HasValue ? d.Value.ToString("yyyy-MM-dd", null) : null,
            s => s != null ? DateOnly.Parse(s, null) : (DateOnly?)null);

        modelBuilder.Entity<ServiceRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.ServiceProvider).HasMaxLength(200);
            e.Property(x => x.GoogleCalendarEventId).HasMaxLength(500);
            e.Property(x => x.Cost).HasColumnType("TEXT");
            e.Property(x => x.MeterReading).HasColumnType("TEXT");
            e.Property(x => x.ServiceDate).HasConversion(dateOnlyConverter);
            e.Property(x => x.OriginalPredictedDate).HasConversion(nullableDateOnlyConverter);
            e.Property(x => x.Status).HasConversion<int>();

            e.HasOne<RecurringRule>()
                .WithMany()
                .HasForeignKey(x => x.RecurringRuleId)
                .OnDelete(DeleteBehavior.SetNull);

            e.OwnsMany(x => x.NotificationRules, r =>
            {
                r.WithOwner().HasForeignKey("ServiceRecordId");
                r.HasKey("Id");
                r.Property<int>("Id");
                r.Property(x => x.Offset).HasConversion<int>();
            });

            e.Ignore(x => x.Attachments);   // polymorphic, see the note on Equipment
        });

        // ── RecurringRule ─────────────────────────────────────────────────────
        modelBuilder.Entity<RecurringRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.IntervalMeter).HasColumnType("TEXT");
            e.HasIndex(x => x.EquipmentId);

            e.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MeterReading ──────────────────────────────────────────────────────
        modelBuilder.Entity<MeterReading>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ReadingDate).HasConversion(dateOnlyConverter);
            e.Property(x => x.Value).HasColumnType("TEXT");
            e.Property(x => x.Source).HasConversion<int>();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.EquipmentId, x.ReadingDate });

            e.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);
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
        // ── Contracts, payment plans and payments ─────────────────────────────
        // Money is TEXT for the same reason it is everywhere else here: SQLite has no
        // decimal type, and REAL would quietly round amounts the household cares about.

        modelBuilder.Entity<Contract>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(200);
            e.Property(x => x.ContractNumber).HasMaxLength(100);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Kind).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.StatusReason).HasMaxLength(500);
            e.Property(x => x.Renewal).HasConversion<int>();
            e.Property(x => x.StartDate).HasConversion(dateOnlyConverter);
            e.Property(x => x.EndDate).HasConversion(nullableDateOnlyConverter);
            e.Property(x => x.CoverageAmount).HasColumnType("TEXT");
            e.Property(x => x.Deductible).HasColumnType("TEXT");

            e.Property<List<string>>("_tags")
                .HasColumnName("Tags")
                .HasField("_tags")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());

            // Opening position — one nullable owned value, flattened into four columns.
            e.OwnsOne(x => x.Opening, o =>
            {
                o.Property(p => p.AsOfDate)
                    .HasColumnName("OpeningAsOfDate")
                    .HasConversion(dateOnlyConverter);
                o.Property(p => p.InstallmentsPaid).HasColumnName("OpeningInstallmentsPaid");
                o.Property(p => p.AmountPaid).HasColumnName("OpeningAmountPaid").HasColumnType("TEXT");
                o.Property(p => p.RemainingBalance).HasColumnName("OpeningRemainingBalance").HasColumnType("TEXT");
            });

            e.OwnsMany(x => x.NotificationRules, r =>
            {
                r.WithOwner().HasForeignKey("ContractId");
                r.HasKey("Id"); // shadow PK
                r.Property<int>("Id");
                r.Property(x => x.Offset).HasConversion<int>();
            });

            e.HasMany(x => x.Revisions)
                .WithOne()
                .HasForeignKey(r => r.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Payments)
                .WithOne()
                .HasForeignKey(p => p.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            // A household-level contract has no equipment, so the FK is optional and
            // deleting a piece of equipment must not take its policies with it silently.
            e.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.SetNull);

            // BlobEntry points back with OwnerEntityId + OwnerEntityType, which is a
            // polymorphic link and not a foreign key: the same column already answers to
            // Equipment, so a second real FK would demand that one id exist in both
            // tables at once. Left unmapped here and resolved in the repository instead.
            e.Ignore(x => x.Attachments);

            e.HasIndex(x => x.EquipmentId);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<PaymentPlanRevision>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasConversion<int>();
            e.Property(x => x.EffectiveFrom).HasConversion(dateOnlyConverter);
            e.Property(x => x.FirstDueDate).HasConversion(dateOnlyConverter);
            e.Property(x => x.ResidualDueDate).HasConversion(nullableDateOnlyConverter);
            e.Property(x => x.InstallmentAmount).HasColumnType("TEXT");
            e.Property(x => x.RemainingPrincipal).HasColumnType("TEXT");
            e.Property(x => x.AnnualInterestRate).HasColumnType("TEXT");
            e.Property(x => x.ResidualAmount).HasColumnType("TEXT");
            e.Property(x => x.Note).HasMaxLength(1000);

            e.OwnsMany(x => x.Adjustments, a =>
            {
                // Matches the *_NotificationRules naming EF already produced for the
                // other owned collections.
                a.ToTable("PlanRevisions_Adjustments");
                a.WithOwner().HasForeignKey("PlanRevisionId");
                a.HasKey("Id"); // shadow PK
                a.Property<int>("Id");
                a.Property(x => x.Name).HasMaxLength(200).IsRequired();
                a.Property(x => x.Amount).HasColumnType("TEXT");
            });

            // Versions are dense per contract; the unique index is what enforces it in
            // the store as well as in the aggregate.
            e.HasIndex(x => new { x.ContractId, x.Version }).IsUnique();
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.DueDate).HasConversion(dateOnlyConverter);
            e.Property(x => x.PaidDate).HasConversion(nullableDateOnlyConverter);
            e.Property(x => x.AmountDue).HasColumnType("TEXT");
            e.Property(x => x.AmountPaid).HasColumnType("TEXT");
            e.Property(x => x.PrincipalPart).HasColumnType("TEXT");
            e.Property(x => x.InterestPart).HasColumnType("TEXT");
            e.Property(x => x.Note).HasMaxLength(1000);

            e.Ignore(x => x.Attachments);   // polymorphic, see the note on Contract

            // The two questions every screen asks: what is due, and what is due soon.
            e.HasIndex(x => new { x.ContractId, x.DueDate });
            e.HasIndex(x => new { x.Status, x.DueDate });
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Language).HasMaxLength(8).IsRequired().HasDefaultValue("ru");

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
