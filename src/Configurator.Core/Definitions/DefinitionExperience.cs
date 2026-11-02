namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed record DefinitionExperience
{
    internal DefinitionExperience(JsonElement data, string navigation)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        InitialDisclosure = FieldDisclosure.Guided;
        Navigation = navigation;
        Sections = DefinitionPresentation.Strings(data, "sections");
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public FieldDisclosure InitialDisclosure { get; }
    public string Navigation { get; }
    public IReadOnlyList<string> Sections { get; }
}
