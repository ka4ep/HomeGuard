using FluentAssertions;
using HomeGuard.Application.Interfaces;
using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Application.Services;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using Xunit;

namespace HomeGuard.Tests.Unit;

public sealed class MeterReadingServiceTests
{
    private static readonly Guid EquipId = Guid.NewGuid();
    private static readonly DateOnly Day  = new(2026, 7, 20);

    private readonly FakeMeterReadingRepo _readings = new();
    private readonly MeterReadingService _svc;

    public MeterReadingServiceTests()
        => _svc = new MeterReadingService(_readings, new FakeServiceRecordRepo(), new FakeUow());

    private static CreateMeterReadingCommand Cmd(
        decimal value, MeterReadingSource source, DateOnly? date = null, Guid? equipId = null)
        => new(equipId ?? EquipId, date ?? Day, value, source);

    // ── Auto upsert (ingestors repost every cycle) ────────────────────────────

    [Fact]
    public async Task Auto_reading_same_day_updates_existing_row()
    {
        var first  = await _svc.CreateAsync(Cmd(48956m, MeterReadingSource.Auto), TestContext.Current.CancellationToken);
        var second = await _svc.CreateAsync(Cmd(49010m, MeterReadingSource.Auto), TestContext.Current.CancellationToken);

        _readings.Items.Should().ContainSingle();
        second.Id.Should().Be(first.Id);
        _readings.Items[0].Value.Should().Be(49010m);
    }

    [Fact]
    public async Task Auto_readings_on_different_days_create_separate_rows()
    {
        await _svc.CreateAsync(Cmd(48956m, MeterReadingSource.Auto), TestContext.Current.CancellationToken);
        await _svc.CreateAsync(Cmd(49010m, MeterReadingSource.Auto, Day.AddDays(1)), TestContext.Current.CancellationToken);

        _readings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Auto_readings_for_different_equipment_create_separate_rows()
    {
        await _svc.CreateAsync(Cmd(48956m, MeterReadingSource.Auto), TestContext.Current.CancellationToken);
        await _svc.CreateAsync(Cmd(120m, MeterReadingSource.Auto, equipId: Guid.NewGuid()), TestContext.Current.CancellationToken);

        _readings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Manual_readings_same_day_are_not_deduplicated()
    {
        await _svc.CreateAsync(Cmd(48956m, MeterReadingSource.Manual), TestContext.Current.CancellationToken);
        await _svc.CreateAsync(Cmd(48960m, MeterReadingSource.Manual), TestContext.Current.CancellationToken);

        _readings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Auto_reading_does_not_touch_manual_reading_of_same_day()
    {
        await _svc.CreateAsync(Cmd(48956m, MeterReadingSource.Manual), TestContext.Current.CancellationToken);
        await _svc.CreateAsync(Cmd(49010m, MeterReadingSource.Auto), TestContext.Current.CancellationToken);

        _readings.Items.Should().HaveCount(2);
        _readings.Items.Single(r => r.Source == MeterReadingSource.Manual).Value.Should().Be(48956m);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class FakeMeterReadingRepo : IMeterReadingRepository
    {
        public List<MeterReading> Items { get; } = [];

        public Task<MeterReading?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

        public Task AddAsync(MeterReading entity, CancellationToken ct = default)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(MeterReading entity) => Items.Remove(entity);

        public Task<IReadOnlyList<MeterReading>> GetByEquipmentAsync(
            Guid equipmentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MeterReading>>(
                Items.Where(r => r.EquipmentId == equipmentId)
                     .OrderByDescending(r => r.ReadingDate).ToList());

        public Task<MeterReading?> FindAsync(
            Guid equipmentId, DateOnly readingDate, MeterReadingSource source, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(r =>
                r.EquipmentId == equipmentId && r.ReadingDate == readingDate && r.Source == source));
    }

    private sealed class FakeServiceRecordRepo : IServiceRecordRepository
    {
        public Task<ServiceRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<ServiceRecord?>(null);

        public Task AddAsync(ServiceRecord entity, CancellationToken ct = default)
            => Task.CompletedTask;

        public void Remove(ServiceRecord entity) { }

        public Task<IReadOnlyList<ServiceRecord>> GetByEquipmentAsync(
            Guid equipmentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ServiceRecord>>([]);

        public Task<IReadOnlyList<ServiceRecord>> GetOverdueAsync(
            DateOnly asOf, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ServiceRecord>>([]);

        public Task<IReadOnlyList<ServiceRecord>> GetDueSoonAsync(
            DateOnly asOf, int withinDays, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ServiceRecord>>([]);

        public Task<ServiceRecord?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<ServiceRecord?>(null);

        public Task<IReadOnlyList<ServiceRecord>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ServiceRecord>>([]);
    }
}
