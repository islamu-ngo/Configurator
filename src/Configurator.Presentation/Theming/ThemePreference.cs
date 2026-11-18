namespace Configurator.Presentation.Theming;

using System.Text;
using System.Text.Json;
using Configurator.Presentation;

/// <summary>Bounded non-sensitive presentation preferences shared across Browser and Desktop adapters.</summary>
public sealed record ThemePreference(
    string ThemeMode = "system",
    string ThemeKey = ThemePresetCatalog.DefaultThemeKey,
    string CustomNaturalHex = ThemePreference.DefaultNaturalHex,
    string CustomBrandHex = ThemePreference.DefaultBrandHex,
    string Direction = "auto",
    string Density = "comfortable",
    string NamingMode = "humanized")
{
    public const int MaximumJsonBytes = 512;
    public const string DefaultNaturalHex = "#475569";
    public const string DefaultBrandHex = "#3B82F6";

    public static ThemePreference Parse(string? preference)
    {
        if (string.IsNullOrEmpty(preference)
            || preference.Length > MaximumJsonBytes
            || Encoding.UTF8.GetByteCount(preference) > MaximumJsonBytes)
        {
            return new();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(preference, new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new();

            string? mode = null;
            string? themeKey = null;
            string? customNaturalHex = null;
            string? customBrandHex = null;
            string? direction = null;
            string? density = null;
            string? namingMode = null;

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    return new();

                string value = property.Value.GetString()!;
                switch (property.Name)
                {
                    case "themeMode" when mode is null:
                        mode = value;
                        break;
                    case "themeKey" when themeKey is null:
                        themeKey = value;
                        break;
                    case "customNaturalHex" when customNaturalHex is null:
                        customNaturalHex = value;
                        break;
                    case "customBrandHex" when customBrandHex is null:
                        customBrandHex = value;
                        break;
                    case "direction" when direction is null:
                        direction = value;
                        break;
                    case "density" when density is null:
                        density = value;
                        break;
                    case "namingMode" when namingMode is null:
                        namingMode = value;
                        break;
                    default:
                        return new();
                }
            }

            if (!IsThemeMode(mode) || !IsDirection(direction))
                return new();

            themeKey ??= ThemePresetCatalog.DefaultThemeKey;
            density ??= "comfortable";
            namingMode ??= "humanized";

            if (!IsThemeKey(themeKey) || !IsDensity(density) || !IsNamingMode(namingMode))
                return new();

            string normalizedNatural = DefaultNaturalHex;
            if (customNaturalHex is not null && !HslColor.TryNormalizeHex(customNaturalHex, out normalizedNatural))
                return new();

            string normalizedBrand = DefaultBrandHex;
            if (customBrandHex is not null && !HslColor.TryNormalizeHex(customBrandHex, out normalizedBrand))
                return new();

            if (themeKey == ThemePresetCatalog.CustomThemeKey
                && (customNaturalHex is null || customBrandHex is null))
            {
                return new();
            }

            return new ThemePreference(
                mode!,
                themeKey,
                normalizedNatural,
                normalizedBrand,
                direction!,
                density,
                namingMode);
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public string ToJson()
    {
        string mode = IsThemeMode(ThemeMode) ? ThemeMode : "system";
        string themeKey = IsThemeKey(ThemeKey) ? ThemeKey : ThemePresetCatalog.DefaultThemeKey;
        string naturalHex = HslColor.TryNormalizeHex(CustomNaturalHex, out string normalizedNatural)
            ? normalizedNatural
            : DefaultNaturalHex;
        string brandHex = HslColor.TryNormalizeHex(CustomBrandHex, out string normalizedBrand)
            ? normalizedBrand
            : DefaultBrandHex;
        string direction = IsDirection(Direction) ? Direction : "auto";
        string density = IsDensity(Density) ? Density : "comfortable";
        string namingMode = IsNamingMode(NamingMode) ? NamingMode : "humanized";

        return $$"""{"themeMode":"{{mode}}","themeKey":"{{themeKey}}","customNaturalHex":"{{naturalHex}}","customBrandHex":"{{brandHex}}","direction":"{{direction}}","density":"{{density}}","namingMode":"{{namingMode}}"}""";
    }

    public WorkspaceDensity ToWorkspaceDensity() => Density switch
    {
        "spacious" => WorkspaceDensity.Spacious,
        "compact" => WorkspaceDensity.Compact,
        "dense" => WorkspaceDensity.Dense,
        _ => WorkspaceDensity.Comfortable
    };

    public WorkspaceNamingMode ToWorkspaceNamingMode() => NamingMode == "technical"
        ? WorkspaceNamingMode.Technical
        : WorkspaceNamingMode.Humanized;

    public static string FromWorkspaceDensity(WorkspaceDensity density) => density switch
    {
        WorkspaceDensity.Spacious => "spacious",
        WorkspaceDensity.Compact => "compact",
        WorkspaceDensity.Dense => "dense",
        _ => "comfortable"
    };

    public static string FromWorkspaceNamingMode(WorkspaceNamingMode mode) => mode == WorkspaceNamingMode.Technical
        ? "technical"
        : "humanized";

    public static bool IsThemeMode(string? mode) =>
        mode is "system" or "light" or "dark" or "lighthighcontrast" or "darkhighcontrast";

    public static bool IsThemeKey(string? themeKey) =>
        themeKey == ThemePresetCatalog.CustomThemeKey || ThemePresetCatalog.IsValidPresetKey(themeKey);

    public static bool IsDirection(string? direction) =>
        direction is "auto" or "ltr" or "rtl";

    public static bool IsDensity(string? density) =>
        density is "spacious" or "comfortable" or "compact" or "dense";

    public static bool IsNamingMode(string? namingMode) =>
        namingMode is "humanized" or "technical";
}
