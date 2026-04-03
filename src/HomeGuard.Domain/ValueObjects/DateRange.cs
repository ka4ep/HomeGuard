namespace HomeGuard.Domain.ValueObjects;

/// <summary>
/// An inclusive date interval. Used for warranty periods.
/// Time-of-day intentionally absent — warranties are date-only concepts.
/// </summary>
public sealed record DateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new ArgumentException($"End ({end}) must be on or after Start ({start}).");
        Start = start;
        End = end;
    }

    public bool IsExpired(DateOnly today) => today > End;

    /// <summary>
    /// Days remaining until End, relative to <paramref name="today"/>.
    /// Negative if already expired.
    /// </summary>
    public int DaysUntilEnd(DateOnly today) => End.DayNumber - today.DayNumber;

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public override string ToString() => $"{Start:yyyy-MM-dd} — {End:yyyy-MM-dd}";
}
