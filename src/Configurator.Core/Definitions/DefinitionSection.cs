namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed record DefinitionSection
{
    internal DefinitionSection(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        Disclosure = FieldDisclosure.Guided;
        Pointers = DefinitionPresentation.Strings(data, "pointers");
        Fields = Array.AsReadOnly(Pointers.Select(path => DefinitionPath.Parse(path).Segments)
            .Where(segments => segments.Count > 0).Select(segments => DefinitionProfile.FieldId(segments[0])).Distinct(StringComparer.Ordinal).ToArray());
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public FieldDisclosure Disclosure { get; }
    public IReadOnlyList<string> Fields { get; }
    public IReadOnlyList<string> Pointers { get; }
}
