namespace HomeGuard.Domain.ValueObjects;

/// <summary>
/// A normalised, validated tag string.
/// Stored as plain strings in the database (JSON column via EF Core).
/// </summary>
public sealed record Tag
{
    public const int MaxLength = 50;

    public string Value { get; }

    public Tag(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToLowerInvariant();
        if (Value.Length > MaxLength)
            throw new ArgumentException($"Tag must be {MaxLength} characters or fewer.", nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(Tag tag) => tag.Value;
    public static explicit operator Tag(string value) => new(value);
}
