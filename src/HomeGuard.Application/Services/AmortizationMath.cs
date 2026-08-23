namespace HomeGuard.Application.Services;

/// <summary>
/// Pure interest math for loans and leases: a fixed nominal annual rate divided by 12 for
/// the monthly rate, applied to a level instalment — the ordinary "amortized loan"
/// convention. Good enough for "what am I paying and when is it over"; not a substitute
/// for the lender's own statement, and this app never claims to be one (see the
/// "not an accounting system" guardrail in contracts-spec.md §1).
/// <para>
/// Every function treats <c>monthlyRate == 0</c> as the interest-free case and falls back
/// to straight-line arithmetic — that is what lets a loan with an unknown rate still show
/// a term and a balance, just without an interest figure attached to them.
/// </para>
/// </summary>
public static class AmortizationMath
{
    public static decimal MonthlyRate(decimal annualRate) => annualRate / 12m;

    /// <summary>
    /// The level instalment that amortizes <paramref name="principal"/> over
    /// <paramref name="months"/> at <paramref name="monthlyRate"/>.
    /// </summary>
    public static decimal AnnuityInstallment(decimal principal, decimal monthlyRate, int months)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(months, 1);
        if (principal <= 0m) return 0m;
        if (monthlyRate == 0m) return principal / months;

        var i = (double)monthlyRate;
        var factor = 1 - Math.Pow(1 + i, -months);
        return (decimal)((double)principal * i / factor);
    }

    /// <summary>
    /// The balance remaining after <paramref name="paymentsMade"/> level instalments of
    /// <paramref name="installment"/> against <paramref name="principal"/>. Closed-form, so
    /// it costs the same whether <paramref name="paymentsMade"/> is 3 or 358.
    /// </summary>
    public static decimal BalanceAfter(
        decimal principal, decimal monthlyRate, decimal installment, int paymentsMade)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(paymentsMade, 0);
        if (paymentsMade == 0) return principal;
        if (monthlyRate == 0m) return Math.Max(principal - installment * paymentsMade, 0m);

        var i = (double)monthlyRate;
        var p = (double)principal;
        var a = (double)installment;
        var growth = Math.Pow(1 + i, paymentsMade);
        var balance = p * growth - a * (growth - 1) / i;
        return (decimal)Math.Max(balance, 0);
    }

    /// <summary>
    /// Splits one instalment into interest (the balance's own upkeep for the period) and
    /// principal (what actually reduces the debt), clamped so principal never exceeds the
    /// balance still owed — the last instalment of a plan is often a few cents short of a
    /// full one.
    /// </summary>
    public static (decimal Principal, decimal Interest) SplitInstallment(
        decimal balanceBefore, decimal monthlyRate, decimal installment)
    {
        if (balanceBefore <= 0m) return (0m, 0m);
        var interest = Math.Round(balanceBefore * monthlyRate, 2);
        var principal = Math.Min(installment - interest, balanceBefore);
        return (Math.Max(principal, 0m), interest);
    }

    /// <summary>
    /// How many instalments of <paramref name="installment"/> it takes to amortize
    /// <paramref name="balance"/>, rounded up — the last one may be a partial remainder.
    /// </summary>
    public static int TermFor(decimal balance, decimal monthlyRate, decimal installment)
    {
        if (balance <= 0m) return 0;
        if (installment <= 0m)
            throw new ArgumentOutOfRangeException(nameof(installment), "An instalment must be positive.");

        if (monthlyRate == 0m) return (int)Math.Ceiling((double)(balance / installment));

        var i = (double)monthlyRate;
        var b = (double)balance;
        var a = (double)installment;
        if (a <= b * i)
            throw new ArgumentException(
                "This instalment does not cover even the interest — the balance would never shrink.",
                nameof(installment));

        var n = -Math.Log(1 - b * i / a) / Math.Log(1 + i);
        return (int)Math.Ceiling(n - 1e-9);
    }

    /// <summary>
    /// Total interest paid over <paramref name="n"/> future instalments of
    /// <paramref name="installment"/> against a starting balance of <paramref name="balance"/>,
    /// using the identity <c>total paid − principal retired = total interest</c> rather than
    /// walking every instalment. Exact when <paramref name="n"/> is the instalment's own
    /// exact term; a few cents optimistic when <paramref name="n"/> is rounded up from
    /// <see cref="TermFor"/>, because the true last instalment is a partial one. That level
    /// of precision is deliberate — see the "not an accounting system" guardrail.
    /// </summary>
    public static decimal TotalInterestOverTerm(decimal balance, decimal installment, int n)
        => n <= 0 ? 0m : Math.Max(installment * n - balance, 0m);
}
