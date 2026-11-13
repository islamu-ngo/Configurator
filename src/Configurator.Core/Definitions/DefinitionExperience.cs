namespace Configurator.Core.Definitions;

using System.Text.Json;

public sealed record DefinitionExperience
{
    internal DefinitionExperience(JsonElement data, string navigation)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        InitialDisclosure = DefinitionPresentation.DisclosureValue(DefinitionPresentation.Text(data, "detail"));
        Navigation = navigation;
        Sections = DefinitionPresentation.Strings(data, "sections");
        ApplicableWhen = DefinitionPresentation.Condition(data);
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public FieldDisclosure InitialDisclosure { get; }
    public string Navigation { get; }
    public IReadOnlyList<string> Sections { get; }
    /// <summary>Data-only predicate deciding when the journey is offered; it never changes validation.</summary>
    public JsonElement? ApplicableWhen { get; }
}
