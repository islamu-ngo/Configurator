namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>Admitted public layout metadata; it never supplies answers or changes validation.</summary>
public sealed record DefinitionPresentation
{
    internal DefinitionPresentation(JsonElement data)
    {
        DefaultExperience = data.GetProperty("defaultExperience").GetString()!;
        Sections = Array.AsReadOnly(data.GetProperty("sections").EnumerateArray()
            .Select(section => new DefinitionSection(section)).ToArray());
        Experiences = Array.AsReadOnly(data.GetProperty("experiences").EnumerateArray()
            .Select(experience => new DefinitionExperience(experience)).ToArray());
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
        "essential" => SetupFieldDisclosure.Essential,
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
        Disclosure = DefinitionPresentation.DisclosureValue(DefinitionPresentation.Text(data, "disclosure"));
        Fields = DefinitionPresentation.Strings(data, "fields");
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public SetupFieldDisclosure Disclosure { get; }
    public IReadOnlyList<string> Fields { get; }
}

public sealed record DefinitionExperience
{
    internal DefinitionExperience(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        InitialDisclosure = DefinitionPresentation.DisclosureValue(DefinitionPresentation.Text(data, "initialDisclosure"));
        Navigation = DefinitionPresentation.Text(data, "navigation") ?? "sections";
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
