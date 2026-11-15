namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
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
    public bool SelectConfigurationContext(string recipeId, string scopeId, long? expectedInputRevision = null)
    {
        ArgumentNullException.ThrowIfNull(recipeId);
        ArgumentNullException.ThrowIfNull(scopeId);
        using var mutation = BeginMutation(PresentationStrings.Get("UndoActionSelectContext", Locale));
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (expectedInputRevision is { } expected && expected != InputRevision)
            return false;
        if (Definition.Deployment is not { } deployment || ConfigurationContext is null ||
            !deployment.Recipes.Any(recipe => recipe.Id == recipeId && recipe.Scopes.Any(scope => scope.Id == scopeId)))
            return false;

        DefinitionPath recipePath = DefinitionPath.Parse(deployment.RecipePointer);
        DefinitionPath scopePath = DefinitionPath.Parse(deployment.ScopePointer);
        if (recipePath.Segments.Count == 0 || scopePath.Segments.Count == 0) return false;

        AnswerSnapshot original = Snapshot();
        var values = original.Values.ToDictionary();
        if (!TryWriteContextScalar(values, recipePath, recipeId) ||
            !TryWriteContextScalar(values, scopePath, scopeId))
            return false;

        try
        {
            var candidate = new AnswerSnapshot(InputRevision, values,
                original.Omitted.Where(path => !IsContextPrefix(path, recipePath) && !IsContextPrefix(path, scopePath)));
            if (DefinitionScopeResolver.Resolve(Definition, candidate) is not { IsReady: true }) return false;
        }
        catch (DefinitionException) { return false; }

        DefinitionAcceptedEditingState replacement = _editing.Capture();
        var changed = new List<int>(2);
        string[] targetRoots = new[] { recipePath.Segments[0], scopePath.Segments[0] }
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (string rootName in targetRoots)
        {
            int index = -1;
            for (int candidateIndex = 0; candidateIndex < Fields.Count; candidateIndex++)
            {
                if (Fields[candidateIndex].Definition.Name == rootName)
                {
                    index = candidateIndex;
                    break;
                }
            }
            if (index < 0) continue;

            changed.Add(index);
            DefinitionFieldInput field = Fields[index];
            DefinitionFieldState input = replacement.Inputs[index];
            input.Retire();
            JsonElement updatedRoot = values[rootName].Clone();
            input.Value = updatedRoot;
            input.Text = updatedRoot.ValueKind == JsonValueKind.String ? updatedRoot.GetString()! : updatedRoot.GetRawText();

            if (recipePath.Segments[0] == rootName)
                RecordContextSelectionOrigin(input, field.Definition.Id, recipePath);
            if (scopePath.Segments[0] == rootName)
                RecordContextSelectionOrigin(input, field.Definition.Id, scopePath);
            replacement.Omitted.RemoveWhere(path => IsContextPrefix(path, recipePath) || IsContextPrefix(path, scopePath));
        }
        DefinitionFieldState[] retired = changed.Select(_editing.Input).ToArray();
        _editing.Adopt(replacement);
        foreach (DefinitionFieldState input in retired) input.Retire();
        OnInputChanged();
        foreach (int index in changed) Fields[index].NotifyDocumentChanged();
        return true;
    }

    private static bool TryWriteContextScalar(Dictionary<string, JsonElement> values, DefinitionPath path, string scalarValue)
    {
        if (path.Segments.Count == 1)
        {
            values[path.Segments[0]] = JsonSerializer.SerializeToElement(scalarValue);
            return true;
        }

        string rootName = path.Segments[0];
        JsonObject rootObject = values.TryGetValue(rootName, out JsonElement existing) && existing.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(existing.GetRawText()) as JsonObject ?? new JsonObject()
            : new JsonObject();

        JsonObject cursor = rootObject;
        for (int index = 1; index < path.Segments.Count - 1; index++)
        {
            string segment = path.Segments[index];
            if (cursor[segment] is not JsonObject next)
            {
                next = new JsonObject();
                cursor[segment] = next;
            }
            cursor = next;
        }
        cursor[path.Segments[^1]] = scalarValue;
        values[rootName] = JsonSerializer.SerializeToElement(rootObject);
        return true;
    }

    private static bool IsContextPrefix(DefinitionPath omitted, DefinitionPath target) =>
        omitted.Segments.Count <= target.Segments.Count &&
        omitted.Segments.SequenceEqual(target.Segments.Take(omitted.Segments.Count));

    private static void RecordContextSelectionOrigin(DefinitionFieldState input, string fieldId, DefinitionPath target)
    {
        if (input.Origin == DefinitionAnswerOrigin.Omitted || target.Segments.Count == 1)
            input.Origin = DefinitionAnswerOrigin.Explicit;
        string[] originPath = target.Segments.Count == 1
            ? [fieldId]
            : new[] { fieldId }.Concat(target.Segments.Skip(1)).ToArray();
        input.Origins.RemoveAll(existing => existing.Path.SequenceEqual(originPath));
        input.Origins.Add((originPath, DefinitionAnswerOrigin.Explicit));
        if (target.Segments.Count > 1)
        {
            input.Origins.RemoveAll(existing => existing.Path.SequenceEqual([fieldId]));
            input.Origins.Add(([fieldId], DefinitionAnswerOrigin.Explicit));
        }
    }

    internal bool IsConfigurationSelector(IReadOnlyList<string> path, DefinitionValidationResult? validation = null)
    {
        if (path.Count != 1) return false;
        return IsConfigurationSelectorRoot(path[0], validation);
    }

    internal bool IsConfigurationSelectorRoot(string fieldId, DefinitionValidationResult? validation = null)
    {
        if (Definition.Deployment is not { } deployment ||
            (validation ?? Validate()).ConfigurationContext is null ||
            !_index!.FieldPositions.TryGetValue(fieldId, out int position))
            return false;
        string name = Fields[position].Definition.Name;
        return name == DefinitionPath.Parse(deployment.RecipePointer).Segments[0] ||
            name == DefinitionPath.Parse(deployment.ScopePointer).Segments[0];
    }
}
