namespace Configurator.Core.Definitions;

using System.Text.Json;

internal static partial class DefinitionAdmission
{
    private static void ValidateArtifactUsage(JsonElement artifact, SortedSet<string> capabilities)
    {
        if (artifact.TryGetProperty("title", out var title)) Text(title, 256);
        if (artifact.TryGetProperty("description", out var description)) Text(description, 4096);
        if (artifact.TryGetProperty("consumerIds", out var consumers))
            foreach (string consumer in DeploymentStrings(consumers, 64)) DeploymentIdentifier(consumer);
        bool dotenv = artifact.GetProperty("format").GetString() == "dotenv";
        string? dialect = null;
        if (artifact.TryGetProperty("environmentDialect", out var dialectValue))
        {
            if (!dotenv) throw new DefinitionException("environment-dialect-format");
            Choice(artifact, "environmentDialect", "docker-run", "compose");
            dialect = dialectValue.GetString();
            capabilities.Add("environment." + dialect);
        }
        if (artifact.TryGetProperty("surface", out var surface))
        {
            if (!dotenv) throw new DefinitionException("environment-surface-format");
            Choice(artifact, "surface", "process-environment", "compose-input");
            if ((surface.GetString() == "compose-input") != (dialect == "compose"))
                throw new DefinitionException("environment-dialect-surface");
        }
    }

    private static void ValidateDeployment(JsonElement deployment, DefinitionNode root, JsonElement artifacts,
        SortedSet<string> capabilities)
    {
        Only(deployment, "recipePointer", "scopePointer", "when", "components", "recipes");
        string recipePointer = deployment.GetProperty("recipePointer").GetString()!;
        string scopePointer = deployment.GetProperty("scopePointer").GetString()!;
        if (recipePointer == scopePointer) throw new DefinitionException("deployment-selector");
        DefinitionNode recipeSelector = DeploymentSelector(recipePointer, root);
        DefinitionNode scopeSelector = DeploymentSelector(scopePointer, root);
        if (deployment.TryGetProperty("when", out var condition))
        {
            ValidateCondition(condition, root, capabilities);
            CheckBootstrapCondition(condition);
        }

        JsonElement components = deployment.GetProperty("components");
        if (components.GetArrayLength() is < 1 or > 64) throw new DefinitionException("deployment-limit");
        var componentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement component in components.EnumerateArray())
        {
            Only(component, "id", "title", "description");
            if (!componentIds.Add(DeploymentMetadata(component))) throw new DefinitionException("deployment-duplicate");
        }
        var outputs = artifacts.EnumerateArray().ToDictionary(artifact => artifact.GetProperty("id").GetString()!,
            StringComparer.Ordinal);
        foreach (JsonElement artifact in outputs.Values)
            if (artifact.TryGetProperty("consumerIds", out var consumers))
                foreach (string consumer in DeploymentStrings(consumers, 64))
                    if (!componentIds.Contains(consumer)) throw new DefinitionException("deployment-reference");

        JsonElement recipes = deployment.GetProperty("recipes");
        if (recipes.GetArrayLength() is < 1 or > 16) throw new DefinitionException("deployment-limit");
        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        var allScopeIds = new HashSet<string>(StringComparer.Ordinal);
        string initialRecipe = recipeSelector.Keywords.GetProperty("x-configurator").GetProperty("initialValue").GetString()!;
        string initialScope = scopeSelector.Keywords.GetProperty("x-configurator").GetProperty("initialValue").GetString()!;
        bool compatibleDefault = false;
        int prerequisites = 0;
        foreach (JsonElement recipe in recipes.EnumerateArray())
        {
            Only(recipe, "id", "title", "description", "scopes");
            string recipeId = DeploymentMetadata(recipe);
            if (!recipeIds.Add(recipeId)) throw new DefinitionException("deployment-duplicate");
            JsonElement scopes = recipe.GetProperty("scopes");
            if (scopes.GetArrayLength() is < 1 or > 16) throw new DefinitionException("deployment-limit");
            var scopeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement scope in scopes.EnumerateArray())
            {
                Only(scope, "id", "title", "description", "componentIds", "artifactIds", "primaryArtifactId", "prerequisitePointers");
                string scopeId = DeploymentMetadata(scope);
                if (!scopeIds.Add(scopeId)) throw new DefinitionException("deployment-duplicate");
                allScopeIds.Add(scopeId);
                if (recipeId == initialRecipe && scopeId == initialScope) compatibleDefault = true;
                HashSet<string> selectedComponents = DeploymentStrings(scope.GetProperty("componentIds"), 64)
                    .ToHashSet(StringComparer.Ordinal);
                if (!selectedComponents.IsSubsetOf(componentIds)) throw new DefinitionException("deployment-reference");
                string[] selectedArtifacts = DeploymentStrings(scope.GetProperty("artifactIds"), 64);
                foreach (string artifactId in selectedArtifacts)
                {
                    if (!outputs.TryGetValue(artifactId, out JsonElement artifact)
                        || !artifact.TryGetProperty("consumerIds", out var consumers)
                        || !DeploymentStrings(consumers, 64).All(selectedComponents.Contains))
                        throw new DefinitionException("deployment-consumers");
                    if (artifact.GetProperty("format").GetString() == "dotenv"
                        && (!artifact.TryGetProperty("surface", out _) || !artifact.TryGetProperty("environmentDialect", out _)))
                        throw new DefinitionException("environment-destination-required");
                }
                if (scope.TryGetProperty("primaryArtifactId", out var primary)
                    && !selectedArtifacts.Contains(primary.GetString(), StringComparer.Ordinal))
                    throw new DefinitionException("deployment-reference");
                if (scope.TryGetProperty("prerequisitePointers", out var pointers))
                {
                    prerequisites += pointers.GetArrayLength();
                    if (prerequisites > 1024) throw new DefinitionException("deployment-limit");
                    var seen = new HashSet<DefinitionPath>();
                    foreach (JsonElement value in pointers.EnumerateArray())
                    {
                        string pointer = value.GetString()!;
                        DefinitionPath path = DefinitionPath.Parse(pointer);
                        if (path.Segments.Count == 0 || !seen.Add(path)) throw new DefinitionException("deployment-reference");
                        _ = Reference(JsonSerializer.SerializeToElement(new { scope = "root", path = pointer }), root, null);
                    }
                }
            }
        }
        if (!compatibleDefault || !recipeIds.SetEquals(recipeSelector.Keywords.GetProperty("enum").EnumerateArray()
                .Select(value => value.GetString()!))
            || !allScopeIds.SetEquals(scopeSelector.Keywords.GetProperty("enum").EnumerateArray()
                .Select(value => value.GetString()!)))
            throw new DefinitionException("deployment-default");
        capabilities.Add("deployment.scope");

        void CheckBootstrapCondition(JsonElement predicate)
        {
            if (predicate.TryGetProperty("operands", out var operands))
            {
                foreach (JsonElement operand in operands.EnumerateArray()) CheckBootstrapCondition(operand);
                return;
            }
            JsonElement reference = predicate.GetProperty("reference");
            string pointer = reference.GetProperty("path").GetString()!;
            if (reference.GetProperty("scope").GetString() != "root" || pointer == recipePointer || pointer == scopePointer)
                throw new DefinitionException("deployment-selector-cycle");
            DefinitionNode node = root;
            if (!DeploymentPublic(node)) throw new DefinitionException("deployment-selector");
            foreach (string segment in DefinitionPath.Parse(pointer).Segments)
            {
                if (!node.Properties.TryGetValue(segment, out DefinitionNode? child) || !DeploymentPublic(child)
                    || child.Keywords.GetProperty("x-configurator").TryGetProperty("applicableWhen", out _))
                    throw new DefinitionException("deployment-selector");
                node = child;
            }
            if (FieldDefinition.EffectiveSensitivity(node) != FieldSensitivity.Public)
                throw new DefinitionException("deployment-selector");
        }
    }

    private static DefinitionNode DeploymentSelector(string pointer, DefinitionNode root)
    {
        if (!TryStableSelector(root, DefinitionPath.Parse(pointer), out DefinitionNode? field) || !DeploymentPublic(field)
            || !field.Keywords.TryGetProperty("enum", out var enumeration) || enumeration.GetArrayLength() is < 1 or > 256
            || !field.Keywords.GetProperty("x-configurator").TryGetProperty("initialValue", out var initial)
            || initial.ValueKind != JsonValueKind.String)
            throw new DefinitionException("deployment-selector");
        return field;
    }

    private static bool DeploymentPublic(DefinitionNode node) =>
        FieldDefinition.SensitivityOf(node) == FieldSensitivity.Public
        && node.Keywords.TryGetProperty("x-configurator", out var metadata)
        && metadata.TryGetProperty("sensitivity", out var sensitivity) && sensitivity.GetString() == "public"
        && (!node.Keywords.TryGetProperty("readOnly", out var readOnly) || !readOnly.GetBoolean())
        && (!node.Keywords.TryGetProperty("writeOnly", out var writeOnly) || !writeOnly.GetBoolean());

    private static string DeploymentMetadata(JsonElement item)
    {
        string id = DeploymentIdentifier(Text(item.GetProperty("id"), 128));
        Text(item.GetProperty("title"), 256);
        if (item.TryGetProperty("description", out var description)) Text(description, 4096);
        return id;
    }

    private static string DeploymentIdentifier(string id)
    {
        if (id.Length is < 1 or > 128 || !char.IsAsciiLetter(id[0])
            || !id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
            throw new DefinitionException("deployment-identifier");
        return id;
    }

    private static string[] DeploymentStrings(JsonElement array, int maximum)
    {
        Strings(array, maximum, 128, nonempty: true);
        return array.EnumerateArray().Select(value => value.GetString()!).ToArray();
    }
}
