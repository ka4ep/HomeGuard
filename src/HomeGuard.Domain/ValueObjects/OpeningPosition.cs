namespace HomeGuard.Domain.ValueObjects;

/// <summary>
/// What had already happened on a contract before the household started tracking it.
/// <para>
/// Most contracts are entered years into their life: a mortgage taken out in 2019, a
/// subscription nobody remembers starting. Rather than making someone back-fill forty
/// seven rows to get a correct balance, the whole prehistory collapses into one record —
/// "as of this date, 47 instalments and 15 040 € were paid, 32 400 € is left".
/// </para>
/// <para>
/// The invariant that keeps this from double counting: an opening position covers
/// everything <em>strictly before</em> <see cref="AsOfDate"/>, and every stored payment
/// on the contract must fall on or after it. Digging out an old receipt therefore means
/// moving <see cref="AsOfDate"/> back and decreasing the counters in the same breath —
/// see <c>Contract.RebaseOpening</c>.
/// </para>
/// </summary>
public sealed class OpeningPosition
{
    // Parameterless ctor for EF Core.
    private OpeningPosition() { }

    /// <summary>Tracking starts here. Everything before it is summarised, not itemised.</summary>
    public DateOnly AsOfDate { get; private set; }

    /// <summary>How many instalments had been paid before <see cref="AsOfDate"/>.</summary>
    public int InstallmentsPaid { get; private set; }

    /// <summary>Total money paid before <see cref="AsOfDate"/>, in the contract's currency.</summary>
    public decimal AmountPaid { get; private set; }

    /// <summary>
    /// What the lender says is still owed at <see cref="AsOfDate"/>. Null for contracts
    /// where a balance is meaningless — a subscription does not have one.
    /// </summary>
    public decimal? RemainingBalance { get; private set; }

    public static OpeningPosition Create(
        DateOnly asOfDate,
        int installmentsPaid,
        decimal amountPaid,
        decimal? remainingBalance = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(installmentsPaid);
        ArgumentOutOfRangeException.ThrowIfNegative(amountPaid);
        if (remainingBalance is < 0)
            throw new ArgumentOutOfRangeException(
                nameof(remainingBalance), "A remaining balance cannot be negative.");

        return new OpeningPosition
        {
            AsOfDate         = asOfDate,
            InstallmentsPaid = installmentsPaid,
            AmountPaid       = amountPaid,
            RemainingBalance = remainingBalance,
        };
    }
}
