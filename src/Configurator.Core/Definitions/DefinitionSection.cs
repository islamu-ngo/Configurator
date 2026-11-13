namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed record DefinitionSection
{
    internal DefinitionSection(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        Disclosure = DefinitionPresentation.DisclosureValue(DefinitionPresentation.Text(data, "detail"));
        Pointers = DefinitionPresentation.Strings(data, "pointers");
        Fields = Array.AsReadOnly(Pointers.Select(path => DefinitionPath.Parse(path).Segments)
            .Where(segments => segments.Count > 0).Select(segments => DefinitionProfile.FieldId(segments[0])).Distinct(StringComparer.Ordinal).ToArray());
        Groups = Array.AsReadOnly(data.TryGetProperty("groups", out JsonElement groups)
            ? groups.EnumerateArray().Select(group => new DefinitionSectionGroup(group)).ToArray() : []);
        ApplicableWhen = DefinitionPresentation.Condition(data);
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public FieldDisclosure Disclosure { get; }
    /// <summary>Root field identifiers reached by <see cref="Pointers"/>.</summary>
    public IReadOnlyList<string> Fields { get; }
    /// <summary>Exact, possibly nested, JSON Pointers shown by this section.</summary>
    public IReadOnlyList<string> Pointers { get; }
    public IReadOnlyList<DefinitionSectionGroup> Groups { get; }
    /// <summary>Data-only predicate deciding when the section is relevant; it never changes validation.</summary>
    public JsonElement? ApplicableWhen { get; }
}
