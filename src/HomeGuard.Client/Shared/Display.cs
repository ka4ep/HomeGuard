using MudBlazor;

namespace HomeGuard.Client.Common;

/// <summary>Status chip colors and labels shared by the warranty and service lists.</summary>
public static class Display
{
    public static Color WarrantyColor(int days) => days switch
    {
        < 0   => Color.Default,
        <= 7  => Color.Error,
        <= 30 => Color.Warning,
        _     => Color.Success,
    };

    public static string WarrantyLabel(int days) => days switch
    {
        < 0   => "Expired",
        0     => "Today",
        1     => "Tomorrow",
        <= 30 => $"{days}d left",
        _     => $"{days / 30}mo left",
    };

    public static string ServiceDaysLabel(int? days) => days switch
    {
        null  => "—",
        0     => "Today",
        1     => "Tomorrow",
        <= 30 => $"In {days}d",
        _     => $"In {days / 30}mo",
    };
}
