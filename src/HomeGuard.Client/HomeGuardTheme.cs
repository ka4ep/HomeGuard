using MudBlazor;

namespace HomeGuard;

public static class HomeGuardTheme
{
    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            // Акцент — slate blue
            Primary = "#4f6fa0",
            PrimaryDarken = "#3a5278",
            PrimaryLighten = "#a8c4e8",

            // Второстепенный — чуть светлее основного
            Secondary = "#7a9cc8",

            // Страница и поверхности
            Background = "#9c9590",   // --hg-bg
            Surface = "#eeeae4",   // --hg-card
            DrawerBackground = "#7a7672", // --hg-bg-deep
            AppbarBackground = "#7a7672", // --hg-bg-deep

            // Текст
            TextPrimary = "#1e1c1a",   // на карточках
            TextSecondary = "#6b6560",
            DrawerText = "#ffffff",
            AppbarText = "#ffffff",

            // Линии, рамки
            Divider = "rgba(60,50,40,0.12)",
            TableLines = "rgba(60,50,40,0.10)",
            LinesDefault = "rgba(60,50,40,0.12)",

            // Статусы (приглушённые)
            Success = "#4a7c59",
            Warning = "#9a6b20",
            Error = "#8b3a3a",
            Info = "#4f6fa0",

            SuccessContrastText = "#ffffff",
            WarningContrastText = "#ffffff",
            ErrorContrastText = "#ffffff",
            InfoContrastText = "#ffffff",

            // Чипы и оверлеи
            ActionDefault = "#6b6560",
            ActionDisabled = "rgba(60,50,40,0.28)",
            ActionDisabledBackground = "rgba(60,50,40,0.10)",
            OverlayLight = "rgba(30,28,26,0.35)",
        },

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
