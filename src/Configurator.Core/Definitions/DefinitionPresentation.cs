namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>Admitted public layout metadata; it never supplies answers or changes validation.</summary>
public sealed record DefinitionPresentation
{
    internal DefinitionPresentation(JsonElement data)
    {
        DefaultExperience = data.GetProperty("defaultJourney").GetString()!;
        Sections = Array.AsReadOnly(data.GetProperty("sections").EnumerateArray()
            .Select(section => new DefinitionSection(section)).ToArray());
        string navigation = Text(data, "navigation") ?? "sections";
        Experiences = Array.AsReadOnly(data.GetProperty("journeys").EnumerateArray()
            .Select(experience => new DefinitionExperience(experience, navigation)).ToArray());
    }

    public string DefaultExperience { get; }
    public IReadOnlyList<DefinitionSection> Sections { get; }
    public IReadOnlyList<DefinitionExperience> Experiences { get; }

    internal static string? Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) ? value.GetString() : null;

    internal static IReadOnlyList<string> Strings(JsonElement data, string name) => Array.AsReadOnly(
        data.TryGetProperty(name, out var values)
            ? values.EnumerateArray().Select(value => value.GetString()!).ToArray() : []);

    internal static SetupFieldDisclosure DisclosureValue(string? value) => value switch
    {
        "basic" => SetupFieldDisclosure.Essential,
        "advanced" => SetupFieldDisclosure.Advanced,
        _ => SetupFieldDisclosure.Guided
    };
}

public sealed record DefinitionSection
{
    internal DefinitionSection(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        Disclosure = SetupFieldDisclosure.Guided;
        Pointers = DefinitionPresentation.Strings(data, "pointers");
        Fields = Array.AsReadOnly(Pointers.Select(path => DefinitionPath.Parse(path).Segments)
            .Where(segments => segments.Count > 0).Select(segments => DefinitionProfile.FieldId(segments[0])).Distinct(StringComparer.Ordinal).ToArray());
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public SetupFieldDisclosure Disclosure { get; }
    public IReadOnlyList<string> Fields { get; }
    public IReadOnlyList<string> Pointers { get; }
}

public sealed record DefinitionExperience
{
    internal DefinitionExperience(JsonElement data, string navigation)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        InitialDisclosure = SetupFieldDisclosure.Guided;
        Navigation = navigation;
        Sections = DefinitionPresentation.Strings(data, "sections");
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public SetupFieldDisclosure InitialDisclosure { get; }
    public string Navigation { get; }
    public IReadOnlyList<string> Sections { get; }
}

public sealed record DefinitionFieldGuidance
{
    internal DefinitionFieldGuidance(JsonElement data)
    {
        Instructions = DefinitionPresentation.Text(data, "instructions");
        Links = Array.AsReadOnly(data.TryGetProperty("links", out var links)
            ? links.EnumerateArray().Select(link => new DefinitionGuidanceLink(
                link.GetProperty("title").GetString()!, link.GetProperty("url").GetString()!)).ToArray() : []);
        SearchTerms = DefinitionPresentation.Strings(data, "searchTerms");
    }

    public string? Instructions { get; }
    public IReadOnlyList<DefinitionGuidanceLink> Links { get; }
    public IReadOnlyList<string> SearchTerms { get; }
}

public sealed record DefinitionGuidanceLink(string Title, string Url);
/// <summary>Describes a secret editor's purpose and acquisition task without holding a credential.</summary>
public sealed record DefinitionInputPurpose(string Kind, string Acquisition);

/// <summary>Resolves admitted public text only; it never changes schema, values or output policy.</summary>
public sealed class DefinitionLocalization
{
    private readonly JsonElement _translations;

    public DefinitionLocalization(SetupDefinition definition)
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
