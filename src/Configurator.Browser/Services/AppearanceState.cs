namespace Configurator.Browser.Services;

using Configurator.Presentation;
using Configurator.Presentation.Theming;

/// <summary>Only non-sensitive presentation preferences may cross the browser storage boundary.</summary>
public sealed record AppearanceState(
    string ThemeMode = "system",
    string Direction = "auto",
    string Density = "comfortable",
    string NamingMode = "humanized",
    string ThemeKey = ThemePresetCatalog.DefaultThemeKey,
    string CustomNaturalHex = ThemePreference.DefaultNaturalHex,
    string CustomBrandHex = ThemePreference.DefaultBrandHex)
{
    public const int MaximumJsonBytes = ThemePreference.MaximumJsonBytes;

    public static AppearanceState Parse(string? preference) =>
        FromPreference(ThemePreference.Parse(preference));

    public static AppearanceState FromPreference(ThemePreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        return new AppearanceState(
            preference.ThemeMode,
            preference.Direction,
            preference.Density,
            preference.NamingMode,
            preference.ThemeKey,
            preference.CustomNaturalHex,
            preference.CustomBrandHex);
    }

    public ThemePreference ToPreference() =>
        new(ThemeMode, ThemeKey, CustomNaturalHex, CustomBrandHex, Direction, Density, NamingMode);

    public string ToJson() => ToPreference().ToJson();

    public WorkspaceDensity ToWorkspaceDensity() => ToPreference().ToWorkspaceDensity();

    public WorkspaceNamingMode ToWorkspaceNamingMode() => ToPreference().ToWorkspaceNamingMode();

    public static string FromWorkspaceDensity(WorkspaceDensity density) =>
        ThemePreference.FromWorkspaceDensity(density);

    public static string FromWorkspaceNamingMode(WorkspaceNamingMode mode) =>
        ThemePreference.FromWorkspaceNamingMode(mode);

    public static bool IsThemeMode(string? mode) => ThemePreference.IsThemeMode(mode);

    public static bool IsThemeKey(string? themeKey) => ThemePreference.IsThemeKey(themeKey);

    public static bool IsDirection(string? direction) => ThemePreference.IsDirection(direction);

    public static bool IsDensity(string? density) => ThemePreference.IsDensity(density);

    public static bool IsNamingMode(string? namingMode) => ThemePreference.IsNamingMode(namingMode);
}
