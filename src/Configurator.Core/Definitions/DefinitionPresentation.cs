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
        OutputSelector = data.TryGetProperty("outputSelector", out var selector)
            ? new DefinitionOutputSelector(selector) : null;
    }

    public string DefaultExperience { get; }
    public IReadOnlyList<DefinitionSection> Sections { get; }
    public IReadOnlyList<DefinitionExperience> Experiences { get; }
    public DefinitionOutputSelector? OutputSelector { get; }

    internal static string? Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) ? value.GetString() : null;

    internal static IReadOnlyList<string> Strings(JsonElement data, string name) => Array.AsReadOnly(
        data.TryGetProperty(name, out var values)
            ? values.EnumerateArray().Select(value => value.GetString()!).ToArray() : []);

    internal static JsonElement? Condition(JsonElement data) =>
        data.TryGetProperty("applicableWhen", out var condition) ? condition.Clone() : null;

    internal static FieldDisclosure DisclosureValue(string? value) => value switch
    {
        "basic" => FieldDisclosure.Essential,
        "advanced" => FieldDisclosure.Advanced,
        _ => FieldDisclosure.Guided
    };
}
