using MudBlazor;

namespace HomeGuard;

public static class HomeGuardTheme
{
    public static readonly MudTheme Instance = new()
    {
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Plus Jakarta Sans", "system-ui", "sans-serif"],
                FontSize = "0.9375rem",
                LineHeight = "1.6",
            },
            H5 = new H5Typography
            {
                FontFamily = ["Plus Jakarta Sans", "system-ui", "sans-serif"],
                FontWeight = "600",
            },
            H6 = new H6Typography
            {
                FontFamily = ["Plus Jakarta Sans", "system-ui", "sans-serif"],
                FontWeight = "600",
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Plus Jakarta Sans", "system-ui", "sans-serif"],
                FontWeight = "500",
                FontSize = "0.8125rem",
                TextTransform = "none",   // убираем ALL CAPS из MudBlazor по умолчанию
            },
            Caption = new CaptionTypography
            {
                FontFamily = ["Plus Jakarta Sans", "system-ui", "sans-serif"],
                FontSize = "0.75rem",
            },
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",  // --hg-radius-md
        },

        Shadows = new Shadow(),

        ZIndex = new ZIndex(),
    };
}
