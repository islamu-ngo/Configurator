namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>
/// A collapsible group inside one section. Its pointers stay within the owning section's pointers, so a group
/// organizes existing settings and never widens what the section exposes.
/// </summary>
public sealed record DefinitionSectionGroup
{
    internal DefinitionSectionGroup(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = DefinitionPresentation.Text(data, "description");
        Disclosure = DefinitionPresentation.DisclosureValue(DefinitionPresentation.Text(data, "detail"));
        InitiallyCollapsed = data.TryGetProperty("collapsed", out JsonElement collapsed) && collapsed.ValueKind == JsonValueKind.True;
        Pointers = DefinitionPresentation.Strings(data, "pointers");
        ApplicableWhen = DefinitionPresentation.Condition(data);
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public FieldDisclosure Disclosure { get; }
    public bool InitiallyCollapsed { get; }
    public IReadOnlyList<string> Pointers { get; }
    /// <summary>Data-only predicate deciding when the group is relevant; it never changes validation.</summary>
    public JsonElement? ApplicableWhen { get; }
}
