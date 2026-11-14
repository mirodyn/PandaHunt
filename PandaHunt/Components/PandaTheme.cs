using MudBlazor;

namespace PandaHunt.Components;

/// <summary>Strict black &amp; white MudBlazor theme.</summary>
public static class PandaTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Black = "#000000",
            White = "#ffffff",
            Primary = "#000000",
            PrimaryContrastText = "#ffffff",
            Secondary = "#4d4d4d",
            SecondaryContrastText = "#ffffff",
            Tertiary = "#808080",
            TertiaryContrastText = "#ffffff",
            Info = "#262626",
            Success = "#000000",
            Warning = "#4d4d4d",
            Error = "#000000",
            Dark = "#000000",
            Background = "#ffffff",
            BackgroundGray = "#f2f2f2",
            Surface = "#ffffff",
            AppbarBackground = "#000000",
            AppbarText = "#ffffff",
            TextPrimary = "#000000",
            TextSecondary = "#555555",
            TextDisabled = "#9e9e9e",
            ActionDefault = "#000000",
            ActionDisabled = "#9e9e9e",
            ActionDisabledBackground = "#e6e6e6",
            Divider = "#d9d9d9",
            LinesDefault = "#000000",
            LinesInputs = "#000000",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "0px",
        },
    };
}
