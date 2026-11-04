namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>Admitted public deployment choices; selected answers remain workspace-owned.</summary>
public sealed class DefinitionDeployment
{
    internal DefinitionDeployment(JsonElement data)
    {
        RecipePointer = data.GetProperty("recipePointer").GetString()!;
        ScopePointer = data.GetProperty("scopePointer").GetString()!;
        When = data.TryGetProperty("when", out var when) ? when.Clone() : null;
        Components = Array.AsReadOnly(data.GetProperty("components").EnumerateArray().Select(item => new DefinitionDeploymentComponent(item)).ToArray());
        Recipes = Array.AsReadOnly(data.GetProperty("recipes").EnumerateArray().Select(item => new DefinitionDeploymentRecipe(item)).ToArray());
    }
    public string RecipePointer { get; }
    public string ScopePointer { get; }
    public JsonElement? When { get; }
    public IReadOnlyList<DefinitionDeploymentComponent> Components { get; }
    public IReadOnlyList<DefinitionDeploymentRecipe> Recipes { get; }
}

public sealed class DefinitionDeploymentComponent
{
    internal DefinitionDeploymentComponent(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = data.TryGetProperty("description", out var text) ? text.GetString() : null;
    }
    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
}

public sealed class DefinitionDeploymentRecipe
{
    internal DefinitionDeploymentRecipe(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = data.TryGetProperty("description", out var text) ? text.GetString() : null;
        Scopes = Array.AsReadOnly(data.GetProperty("scopes").EnumerateArray().Select(item => new DefinitionDeploymentScope(item)).ToArray());
    }
    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public IReadOnlyList<DefinitionDeploymentScope> Scopes { get; }
}

public sealed class DefinitionDeploymentScope
{
    internal DefinitionDeploymentScope(JsonElement data)
    {
        Id = data.GetProperty("id").GetString()!;
        Title = data.GetProperty("title").GetString()!;
        Description = data.TryGetProperty("description", out var text) ? text.GetString() : null;
        ComponentIds = Strings(data, "componentIds");
        ArtifactIds = Strings(data, "artifactIds");
        PrimaryArtifactId = data.TryGetProperty("primaryArtifactId", out var primary) ? primary.GetString() : null;
        PrerequisitePointers = Strings(data, "prerequisitePointers");
    }
    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public IReadOnlyList<string> ComponentIds { get; }
    public IReadOnlyList<string> ArtifactIds { get; }
    public string? PrimaryArtifactId { get; }
    public IReadOnlyList<string> PrerequisitePointers { get; }
    private static IReadOnlyList<string> Strings(JsonElement data, string name) => data.TryGetProperty(name, out var array)
        ? Array.AsReadOnly(array.EnumerateArray().Select(item => item.GetString()!).ToArray()) : Array.Empty<string>();
}
