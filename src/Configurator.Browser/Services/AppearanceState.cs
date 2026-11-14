namespace Configurator.Browser.Services;

using System.Text;
using System.Text.Json;
using Configurator.Presentation;

/// <summary>Only presentation preferences may cross the browser storage boundary.</summary>
public sealed record AppearanceState(
    string ThemeMode = "system",
    string Direction = "auto",
    string Density = "comfortable",
    string NamingMode = "humanized")
{
    public static AppearanceState Parse(string? preference)
    {
        if (string.IsNullOrEmpty(preference) || preference.Length > 256 || Encoding.UTF8.GetByteCount(preference) > 256)
            return new();
        try
        {
            using JsonDocument document = JsonDocument.Parse(preference, new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new();
            string? mode = null;
            string? direction = null;
            string? density = null;
            string? namingMode = null;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String) return new();
                if (property.Name == "themeMode" && mode is null) mode = property.Value.GetString();
                else if (property.Name == "direction" && direction is null) direction = property.Value.GetString();
                else if (property.Name == "density" && density is null) density = property.Value.GetString();
                else if (property.Name == "namingMode" && namingMode is null) namingMode = property.Value.GetString();
                else return new();
            }
            if (!IsThemeMode(mode) || !IsDirection(direction)) return new();
            density ??= "comfortable";
            namingMode ??= "humanized";
            return IsDensity(density) && IsNamingMode(namingMode)
                ? new(mode!, direction!, density, namingMode)
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public string ToJson()
    {
        string mode = IsThemeMode(ThemeMode) ? ThemeMode : "system";
        string direction = IsDirection(Direction) ? Direction : "auto";
        string density = IsDensity(Density) ? Density : "comfortable";
        string namingMode = IsNamingMode(NamingMode) ? NamingMode : "humanized";
        return $$"""{"themeMode":"{{mode}}","direction":"{{direction}}","density":"{{density}}","namingMode":"{{namingMode}}"}""";
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

    public static bool IsThemeMode(string? mode) => mode is "system" or "light" or "dark" or "lighthighcontrast" or "darkhighcontrast";

    public static bool IsDirection(string? direction) => direction is "auto" or "ltr" or "rtl";

    public static bool IsDensity(string? density) => density is "spacious" or "comfortable" or "compact" or "dense";

    public static bool IsNamingMode(string? namingMode) => namingMode is "humanized" or "technical";
}
