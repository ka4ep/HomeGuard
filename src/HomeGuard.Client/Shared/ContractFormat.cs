using Microsoft.Extensions.Localization;

namespace HomeGuard.Client.Common;

/// <summary>
/// Currency-symbol and interval-label formatting for contracts — one place instead of the
/// copy-pasted switch statement seven files (currency) and four files (interval) each had.
/// </summary>
public static class ContractFormat
{
    public static string CurrencySymbol(string currency) => currency switch
    {
        "EUR" => "€",
        "USD" => "$",
        "CZK" => "Kč",
        "GBP" => "£",
        _     => currency,
    };

    public static string IntervalLabel(IStringLocalizer<Strings> l, int months) => months switch
    {
        1  => l["Contract_PerMonth"],
        3  => l["Contract_PerQuarter"],
        6  => l["Contract_PerHalfYear"],
        12 => l["Contract_PerYear"],
        _  => l["Contract_EveryNMonths", months],
    };
}
