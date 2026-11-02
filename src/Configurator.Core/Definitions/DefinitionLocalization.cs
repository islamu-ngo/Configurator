namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>Resolves admitted public text only; it never changes schema, values or output policy.</summary>
public sealed class DefinitionLocalization
{
    private readonly JsonElement _translations;

    public DefinitionLocalization(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Locales = definition.Locales;
        using JsonDocument document = JsonDocument.Parse(definition.CanonicalBytes);
        if (document.RootElement.TryGetProperty("localization", out JsonElement localization))
        {
            DefaultLocale = localization.GetProperty("defaultLocale").GetString()!;
            _translations = localization.GetProperty("translations").Clone();
        }
        else DefaultLocale = definition.Locales[0];
    }

    public string DefaultLocale { get; }
    public IReadOnlyList<string> Locales { get; }

    public string Direction(string locale) =>
        _translations.ValueKind == JsonValueKind.Object &&
        _translations.TryGetProperty(locale, out JsonElement translation) &&
        translation.TryGetProperty("direction", out JsonElement direction)
            ? direction.GetString()! : "ltr";

    public string? Text(string locale, string schemaPath, string? baseText)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);
        return _translations.ValueKind == JsonValueKind.Object &&
            _translations.TryGetProperty(locale, out JsonElement translation) &&
            translation.TryGetProperty("overrides", out JsonElement overrides) &&
            overrides.TryGetProperty(schemaPath, out JsonElement text)
                ? text.GetString() : baseText;
    }
}
