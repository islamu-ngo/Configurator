namespace Configurator.Presentation;

using System.Text.Json;
using Configurator.Core.Definitions;

public sealed partial class DefinitionFormModel
{
    public DefinitionConfigurationContext? ConfigurationContext => Validate().ConfigurationContext;
    public IReadOnlyList<DefinitionDeploymentRecipe> ConfigurationRecipes => Definition.Deployment?.Recipes ?? [];
    public IReadOnlyList<DefinitionDeploymentScope> ConfigurationScopes
    {
        get
        {
            string? recipeId = ConfigurationContext?.RecipeId;
            return ConfigurationRecipes.FirstOrDefault(recipe => recipe.Id == recipeId)?.Scopes ?? [];
        }
    }
    public IReadOnlyList<ArtifactDefinition> EligibleArtifacts
    {
        get
        {
            DefinitionValidationResult validation = Validate();
            IReadOnlyList<string>? ids = validation.ConfigurationContext?.EligibleArtifactIds;
            if (ids is null && Definition.Presentation?.OutputSelector is { } selector)
                ids = validation.TryGetEffectiveValue(selector.Location, out JsonElement task)
                    ? selector.Options.FirstOrDefault(option => task.ValueKind == JsonValueKind.String && option.Value == task.GetString())?.ArtifactIds ?? []
                    : [];
            return ids is null ? Definition.Artifacts
                : Array.AsReadOnly(Definition.Artifacts.Where(artifact => ids.Contains(artifact.Id, StringComparer.Ordinal)).ToArray());
        }
    }

    /// <summary>Accepts a compatible pair as one input edit; clients resolve pending document edits first.</summary>
    public bool SelectConfigurationContext(string recipeId, string scopeId)
    {
        ArgumentNullException.ThrowIfNull(recipeId);
        ArgumentNullException.ThrowIfNull(scopeId);
        using var mutation = BeginMutation();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Definition.Deployment is not { } deployment || ConfigurationContext is null ||
            !deployment.Recipes.Any(recipe => recipe.Id == recipeId && recipe.Scopes.Any(scope => scope.Id == scopeId)))
            return false;

        string recipeName = DefinitionPath.Parse(deployment.RecipePointer).Segments[0];
        string scopeName = DefinitionPath.Parse(deployment.ScopePointer).Segments[0];
        AnswerSnapshot original = Snapshot();
        var values = original.Values.ToDictionary();
        values[recipeName] = JsonSerializer.SerializeToElement(recipeId);
        values[scopeName] = JsonSerializer.SerializeToElement(scopeId);
        try
        {
            var candidate = new AnswerSnapshot(InputRevision, values,
                original.Omitted.Where(path => path.Segments[0] != recipeName && path.Segments[0] != scopeName));
            if (DefinitionScopeResolver.Resolve(Definition, candidate) is not { IsReady: true }) return false;
        }
        catch (DefinitionException) { return false; }

        DefinitionAcceptedEditingState replacement = _editing.Capture();
        var changed = new List<int>(2);
        for (int index = 0; index < Fields.Count; index++)
        {
            FieldDefinition field = Fields[index].Definition;
            if (field.Name != recipeName && field.Name != scopeName)
            {
                replacement.Inputs[index].Retire();
                replacement.Inputs[index] = _editing.Input(index);
                continue;
            }
            changed.Add(index);
            DefinitionFieldState input = replacement.Inputs[index];
            input.Retire();
            input.Value = values[field.Name];
            input.Text = values[field.Name].GetString()!;
            input.Origin = DefinitionAnswerOrigin.Explicit;
            input.Origins.Add(([field.Id], DefinitionAnswerOrigin.Explicit));
            replacement.Omitted.RemoveWhere(path => path.Segments[0] == field.Id);
        }
        DefinitionFieldState[] retired = changed.Select(_editing.Input).ToArray();
        _editing.Adopt(replacement);
        foreach (DefinitionFieldState input in retired) input.Retire();
        OnInputChanged();
        foreach (int index in changed) Fields[index].NotifyDocumentChanged();
        return true;
    }

    internal bool IsConfigurationSelector(IReadOnlyList<string> path)
    {
        if (path.Count != 1 || Definition.Deployment is not { } deployment || ConfigurationContext is null)
            return false;
        string name = Fields[_index!.FieldPositions[path[0]]].Definition.Name;
        return name == DefinitionPath.Parse(deployment.RecipePointer).Segments[0] ||
            name == DefinitionPath.Parse(deployment.ScopePointer).Segments[0];
    }
}
