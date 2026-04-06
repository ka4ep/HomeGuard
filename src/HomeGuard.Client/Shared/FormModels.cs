namespace HomeGuard.Client.Common;

// ── Equipment ─────────────────────────────────────────────────────────────────

public sealed class EquipmentFormModel
{
    public string   Name          { get; set; } = string.Empty;
    public string   Category      { get; set; } = "Other";
    public string?  Brand         { get; set; }
    public string?  Model         { get; set; }
    public string?  SerialNumber  { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string   Tags          { get; set; } = string.Empty;
    public string?  Notes         { get; set; }

    // MudDatePicker binds to DateTime? — we convert on save.
    public DateTime? PurchaseDateNullable { get; set; } = DateTime.Today;

    public DateOnly PurchaseDate
        => PurchaseDateNullable.HasValue
            ? DateOnly.FromDateTime(PurchaseDateNullable.Value)
            : DateOnly.FromDateTime(DateTime.Today);
}

// ── Warranty ──────────────────────────────────────────────────────────────────

public class WarrantyFormModel
{
    public string  Name           { get; set; } = string.Empty;
    public string? Provider       { get; set; }
    public string? ContractNumber { get; set; }
    public string? Notes          { get; set; }

    public DateTime? StartDateNullable { get; set; } = DateTime.Today;
    public DateTime? EndDateNullable   { get; set; } = DateTime.Today.AddYears(2);

    public DateOnly StartDate
        => StartDateNullable.HasValue ? DateOnly.FromDateTime(StartDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today);

    public DateOnly EndDate
        => EndDateNullable.HasValue ? DateOnly.FromDateTime(EndDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today.AddYears(2));
}

// ── ServiceRecord ─────────────────────────────────────────────────────────────

public class ServiceRecordFormModel
{
    public string   Title           { get; set; } = string.Empty;
    public string?  ServiceProvider { get; set; }
    public decimal? Cost            { get; set; }
    public string?  OdometerReading { get; set; }
    public string?  Notes           { get; set; }

    public DateTime? ServiceDateNullable     { get; set; } = DateTime.Today;
    public DateTime? NextServiceDateNullable { get; set; }

    public DateOnly ServiceDate
        => ServiceDateNullable.HasValue ? DateOnly.FromDateTime(ServiceDateNullable.Value) : DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? NextServiceDate
        => NextServiceDateNullable.HasValue ? DateOnly.FromDateTime(NextServiceDateNullable.Value) : null;
}
