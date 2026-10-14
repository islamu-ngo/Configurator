namespace Configurator.Presentation;

using System.Globalization;
using System.Text.Json;

/// <summary>Converts scalar editor text to typed values; empty text represents an absent answer.</summary>
public static class DefinitionScalarEditing
{
    public static bool TryParse(string type, string text, JsonElement metadata, out JsonElement? value)
    {
        value = null;
        if (text.Length == 0) return true;
        if (text.Length > 65_536) return false;
        switch (type)
        {
            case "string":
                value = JsonSerializer.SerializeToElement(text);
                return true;
            case "boolean":
                if (!bool.TryParse(text, out bool boolean)) return false;
                value = JsonSerializer.SerializeToElement(boolean);
                return true;
            case "integer":
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return false;
                value = JsonSerializer.SerializeToElement(integer);
                return true;
            case "number":
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) return false;
                value = JsonSerializer.SerializeToElement(number);
                return true;
            case "enum":
                foreach (JsonElement choice in metadata.GetProperty("choices").EnumerateArray())
                    if ((choice.ValueKind == JsonValueKind.String ? choice.GetString() : choice.GetRawText()) == text)
                    {
                        value = choice.Clone();
                        return true;
                    }
                return false;
            default:
                return false;
        }
    }
}
