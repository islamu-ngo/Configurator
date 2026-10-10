namespace Configurator.Browser.Services;

using System.Text.Json;

/// <summary>Only presentation preferences may cross the browser storage boundary.</summary>
public sealed record AppearanceState(string ThemeMode = "system", string Direction = "auto")
{
    public static AppearanceState Parse(string? preference)
    {
        if (string.IsNullOrEmpty(preference) || preference.Length > 256) return new();
        try
        {
            using JsonDocument document = JsonDocument.Parse(preference, new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new();
            string? mode = null;
            string? direction = null;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String) return new();
                if (property.Name == "themeMode" && mode is null) mode = property.Value.GetString();
                else if (property.Name == "direction" && direction is null) direction = property.Value.GetString();
                else return new();
            }
            return IsThemeMode(mode) && IsDirection(direction) ? new(mode!, direction!) : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public static bool IsThemeMode(string? mode) => mode is "system" or "light" or "dark" or "lighthighcontrast" or "darkhighcontrast";

    public static bool IsDirection(string? direction) => direction is "auto" or "ltr" or "rtl";
}
