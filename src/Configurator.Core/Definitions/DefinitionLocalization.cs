namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>Resolves admitted public text only; it never changes schema, values or output policy.</summary>
public sealed class DefinitionLocalization
{
    private readonly System.Collections.Frozen.FrozenDictionary<string, LocaleTable>? _tables;

    public DefinitionLocalization(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Locales = definition.Locales;
        using JsonDocument document = JsonDocument.Parse(definition.CanonicalBytes);
        if (document.RootElement.TryGetProperty("localization", out JsonElement localization))
        {
            DefaultLocale = localization.GetProperty("defaultLocale").GetString()!;
            JsonElement translations = localization.GetProperty("translations");
            if (translations.ValueKind == JsonValueKind.Object)
                _tables = BuildTables(translations);
        }
        else DefaultLocale = definition.Locales[0];
    }

    public string DefaultLocale { get; }
    public IReadOnlyList<string> Locales { get; }

    public string Direction(string locale) =>
        _tables is not null && _tables.TryGetValue(locale, out LocaleTable table)
            ? table.Direction
            : "ltr";

    public string? Text(string locale, string schemaPath, string? baseText)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);
        return _tables is not null &&
            _tables.TryGetValue(locale, out LocaleTable table) &&
            table.Overrides is not null &&
            table.Overrides.TryGetValue(schemaPath, out string? text)
                ? text
                : baseText;
    }

    private static System.Collections.Frozen.FrozenDictionary<string, LocaleTable> BuildTables(JsonElement translations)
    {
        var tables = new Dictionary<string, LocaleTable>(StringComparer.Ordinal);
        foreach (JsonProperty entry in translations.EnumerateObject())
        {
            JsonElement translation = entry.Value;
            string direction = translation.ValueKind == JsonValueKind.Object &&
                translation.TryGetProperty("direction", out JsonElement dirElement)
                    ? dirElement.GetString()!
                    : "ltr";

            System.Collections.Frozen.FrozenDictionary<string, string?>? overrides = null;
            if (translation.ValueKind == JsonValueKind.Object &&
                translation.TryGetProperty("overrides", out JsonElement overridesElement) &&
                overridesElement.ValueKind == JsonValueKind.Object)
            {
                var map = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (JsonProperty item in overridesElement.EnumerateObject())
                    map[item.Name] = item.Value.GetString();
                overrides = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(map, StringComparer.Ordinal);
            }

            tables[entry.Name] = new LocaleTable(direction, overrides);
        }

        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(tables, StringComparer.Ordinal);
    }

    private readonly record struct LocaleTable(
        string Direction,
        System.Collections.Frozen.FrozenDictionary<string, string?>? Overrides);
}
