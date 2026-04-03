using HomeGuard.Domain.Common;
using HomeGuard.Domain.Enums;
using HomeGuard.Domain.ValueObjects;

namespace HomeGuard.Domain.Entities;

/// <summary>
/// Represents a physical item owned by the household: a car, an appliance, an electronic device, etc.
/// Acts as the aggregate root — Warranties and ServiceRecords hang off it.
/// </summary>
public sealed class Equipment : Entity
{
    // Required by EF Core. Do not use directly — use Create().
    private Equipment() { }

    // ── Core fields ─────────────────────────────────────────────────────────

    /// <summary>Human-readable name, e.g. "Samsung Washing Machine".</summary>
    public string Name { get; private set; } = null!;

    public EquipmentCategory Category { get; private set; }

    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }

    /// <summary>Date of purchase. DateOnly — no time component needed.</summary>
    public DateOnly PurchaseDate { get; private set; }

    /// <summary>Purchase price in the household's local currency.</summary>
    public decimal? PurchasePrice { get; private set; }

    /// <summary>Freeform notes in Markdown.</summary>
    public string? Notes { get; private set; }

    // ── Tags (stored as JSON column via EF Core) ─────────────────────────────

    private List<string> _tags = [];
    /// <summary>Lowercase, normalised tags for search/filtering.</summary>
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    // ── Navigation ──────────────────────────────────────────────────────────

    private readonly List<Warranty> _warranties = [];
    public IReadOnlyList<Warranty> Warranties => _warranties.AsReadOnly();

    private readonly List<ServiceRecord> _serviceRecords = [];
    public IReadOnlyList<ServiceRecord> ServiceRecords => _serviceRecords.AsReadOnly();

    private readonly List<BlobEntry> _attachments = [];
    /// <summary>Receipts, product photos, manuals scanned to image.</summary>
    public IReadOnlyList<BlobEntry> Attachments => _attachments.AsReadOnly();

    // ── Factory ─────────────────────────────────────────────────────────────

    public static Equipment Create(
        string name,
        EquipmentCategory category,
        DateOnly purchaseDate,
        string? brand = null,
        string? model = null,
        string? serialNumber = null,
        decimal? purchasePrice = null,
        string? notes = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var e = new Equipment();
        e.InitNew();
        e.Name = name.Trim();
        e.Category = category;
        e.PurchaseDate = purchaseDate;
        e.Brand = brand?.Trim();
        e.Model = model?.Trim();
        e.SerialNumber = serialNumber?.Trim();
        e.PurchasePrice = purchasePrice;
        e.Notes = notes;
        e._tags = NormaliseTags(tags);
        return e;
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public void Update(
        string name,
        EquipmentCategory category,
        DateOnly purchaseDate,
        string? brand = null,
        string? model = null,
        string? serialNumber = null,
        decimal? purchasePrice = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Category = category;
        PurchaseDate = purchaseDate;
        Brand = brand?.Trim();
        Model = model?.Trim();
        SerialNumber = serialNumber?.Trim();
        PurchasePrice = purchasePrice;
        Notes = notes;
        Touch();
    }

    public void SetTags(IEnumerable<string> tags)
    {
        _tags = NormaliseTags(tags);
        Touch();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<string> NormaliseTags(IEnumerable<string>? tags)
        => tags?
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length is > 0 and <= Tag.MaxLength)
            .Distinct()
            .ToList()
           ?? [];
}
