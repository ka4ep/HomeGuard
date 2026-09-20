using System.Globalization;
using HomeGuard.Client.Services;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace HomeGuard.Client.Common;

/// <summary>
/// Status chip colors and labels shared by the warranty and service lists.
/// <para>
/// The label methods take the localizer rather than returning English: these strings
/// are the app's own words, so they belong in resources like every other label.
/// </para>
/// </summary>
public static class Display
{
    public static Color WarrantyColor(int days) => days switch
    {
        < 0   => Color.Default,
        <= 7  => Color.Error,
        <= 30 => Color.Warning,
        _     => Color.Success,
    };

    public static string WarrantyLabel(IStringLocalizer<Strings> l, int days) => days switch
    {
        < 0   => l["Warranty_Expired"],
        0     => l["Warranty_Today"],
        1     => l["Warranty_Tomorrow"],
        <= 30 => l["Warranty_DaysLeft", days],
        _     => l["Warranty_MonthsLeft", days / 30],
    };

    /// <summary>DaysRemaining counts down to End from today, which for a not-yet-started
    /// warranty (e.g. a follow-on chained after one still active) already bundles in the
    /// wait before coverage even begins — a "30 months left" chip next to a same-duration
    /// warranty that shows "24" reads as a bug rather than as "6 of those months haven't
    /// started yet". Showing the start date instead, until it actually starts, says what's
    /// really true without the misleading arithmetic.</summary>
    public static Color WarrantyColor(WarrantyDto w) =>
        w.StartDate > DateOnly.FromDateTime(DateTime.Now) ? Color.Info : WarrantyColor(w.DaysRemaining);

    public static string WarrantyLabel(IStringLocalizer<Strings> l, WarrantyDto w) =>
        w.StartDate > DateOnly.FromDateTime(DateTime.Now)
            ? l["Warranty_StartsOn", w.StartDate.ToString("d", CultureInfo.CurrentCulture)]
            : WarrantyLabel(l, w.DaysRemaining);

    public static string ServiceDaysLabel(IStringLocalizer<Strings> l, int? days) => days switch
    {
        null  => "—",
        0     => l["Service_Today"],
        1     => l["Service_Tomorrow"],
        <= 30 => l["Service_InDays", days],
        _     => l["Service_InMonths", days / 30],
    };
}
