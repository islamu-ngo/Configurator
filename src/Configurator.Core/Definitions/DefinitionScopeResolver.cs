namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;

/// <summary>Resolves admitted deployment metadata against one immutable answer snapshot.</summary>
public static class DefinitionScopeResolver
{
    public static DefinitionConfigurationContext? Resolve(Definition definition, AnswerSnapshot answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        cancellationToken.ThrowIfCancellationRequested();
        if (definition.Deployment is null) return null;
        try { return Resolve(definition, new DefinitionEvaluationInput(answers), new(CompositionLimits.Default), cancellationToken); }
        catch (CompositionException) { throw new DefinitionException("validation-limit"); }
        catch (DefinitionException exception) when (exception.Code == "path-limit")
        { throw new DefinitionException("validation-limit"); }
    }

    internal static DefinitionConfigurationContext? Resolve(Definition definition, DefinitionEvaluationInput answers,
        CompositionBudget budget, CancellationToken cancellationToken)
    {
        return definition.Deployment is { } deployment ? new Evaluation(definition, answers, budget, cancellationToken).Resolve(deployment) : null;
    }

    private sealed class Evaluation(Definition definition, DefinitionEvaluationInput answers, CompositionBudget budget, CancellationToken cancellationToken)
    {
        private readonly Dictionary<DefinitionPath, DefinitionNode> _schemas = [];
        private readonly Dictionary<DefinitionPath, JsonElement> _values = [];
        // Raw values supply row shape only; the validation kernel supplies all condition and presence authority.
        private readonly Dictionary<DefinitionPath, JsonElement> _sourceValues = [];
        private readonly Dictionary<DefinitionPath, List<DefinitionPath>> _children = [];
        private readonly HashSet<DefinitionPath> _included = [];
        private readonly HashSet<DefinitionPath> _expanded = [];
        private readonly HashSet<DefinitionPath> _observed = [];
        private readonly HashSet<DefinitionPath> _uncertain = [];
        private readonly Dictionary<string, JsonElement> _rootValues = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unknownRoots = new(StringComparer.Ordinal);
        private readonly HashSet<DefinitionPath> _unknownLocations = [];
        private bool _unresolvedDependencies;

        internal DefinitionConfigurationContext? Resolve(DefinitionDeployment deployment)
        {
            var root = new JsonObject();
            foreach ((string name, JsonElement supplied) in answers.Values)
                root.Add(name, JsonNode.Parse(supplied.GetRawText()));
            Index(definition.Configuration, DefinitionPath.Parse(""),
                JsonSerializer.SerializeToElement(root, CoreJsonContext.Default.JsonObject));
            Add(DefinitionPath.Parse(deployment.RecipePointer), false);
            Add(DefinitionPath.Parse(deployment.ScopePointer), false);
            bool? applicable = true;
            if (deployment.When is { } when)
            {
                IncludeReferences(when, null);
                Observe();
                applicable = DefinitionValidator.EvaluateCondition(when, _rootValues, _unknownRoots, unresolvedLocations: _unknownLocations);
                if (applicable == false) return null;
            }
            else Observe();
            string? recipeId = Selection(deployment.RecipePointer);
            string? scopeId = Selection(deployment.ScopePointer);
            DefinitionDeploymentRecipe? recipe = deployment.Recipes.SingleOrDefault(item => item.Id == recipeId);
            DefinitionDeploymentScope? scope = recipe?.Scopes.SingleOrDefault(item => item.Id == scopeId);
            if (applicable is null || scope is null)
                return new(recipe?.Id, scope?.Id, false, [], _included);
            foreach (string pointer in scope.PrerequisitePointers) Add(DefinitionPath.Parse(pointer), true);
            foreach (string artifactId in scope.ArtifactIds)
                foreach (DefinitionScopeReference reference in definition.ScopeIndex.ArtifactReferences(artifactId))
                    ExpandReference(reference, 0, []);

            bool changed;
            do
            {
                Observe();
                _unresolvedDependencies = false;
                int before = _included.Count;
                foreach (DefinitionPath path in _included.ToArray())
                {
                    Charge(path);
                    if (!_schemas.TryGetValue(path, out DefinitionNode? schema)) continue;
                    foreach (JsonElement condition in DefinitionValidator.NodeConditions(schema))
                        IncludeReferences(condition, ItemLocation(path));
                    if (schema.Discriminator is { } discriminator)
                        Add(DefinitionPath.FromSegments(path.Segments.Append(discriminator)), true);
                    if (schema.Type == DefinitionNodeType.Mapping) CloseRelationships(path, schema);
                }
                changed = _included.Count != before;
            } while (changed);
            return new(recipeId, scopeId, true, scope.ArtifactIds, _included, scope.PrimaryArtifactId, _unresolvedDependencies);
        }

        private string? Selection(string pointer) => _values.TryGetValue(DefinitionPath.Parse(pointer), out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private void Index(DefinitionNode schema, DefinitionPath path, JsonElement? supplied)
        {
            Charge(path);
            if (answers.Omitted.Contains(path)) supplied = null;
            else if (supplied is null && schema.Keywords.TryGetProperty("x-configurator", out var metadata)
                && metadata.TryGetProperty("initialValue", out var initial)) supplied = initial;
            _schemas[path] = schema;
            if (path.Segments.Count > 0)
            {
                DefinitionPath parent = DefinitionPath.FromSegments(path.Segments.SkipLast(1));
                if (!_children.TryGetValue(parent, out var children)) _children.Add(parent, children = []);
                if (!children.Contains(path)) children.Add(path);
            }
            if (supplied is { } value) _sourceValues[path] = value;
            if (schema.Variants.Count > 0)
            {
                DefinitionNode? branch = supplied is { } variantValue ? DefinitionValidator.SelectVariant(schema, variantValue) : null;
                if (branch is not null) Index(branch, path, supplied);
                return;
            }
            foreach ((string name, DefinitionNode child) in schema.Properties)
            {
                JsonElement? childValue = supplied is { ValueKind: JsonValueKind.Object } mapping
                    && mapping.TryGetProperty(name, out var member) ? member : null;
                Index(child, DefinitionPath.FromSegments(path.Segments.Append(name)), childValue);
            }
            if (schema.Items is { } item && supplied is { ValueKind: JsonValueKind.Array } sequence)
                for (int index = 0; index < sequence.GetArrayLength(); index++)
                    Index(item, DefinitionPath.FromSegments(path.Segments.Append(index.ToString(CultureInfo.InvariantCulture))), sequence[index]);
            if (schema.AdditionalProperties is { } map && supplied is { ValueKind: JsonValueKind.Object } entries)
                foreach (JsonProperty entry in entries.EnumerateObject())
                    if (!schema.Properties.ContainsKey(entry.Name)) Index(map,
                        DefinitionPath.FromSegments(path.Segments.Append(entry.Name)), entry.Value);
        }

        private void ExpandReference(DefinitionScopeReference reference, int index, string[] prefix)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index == reference.Segments.Count) { Add(DefinitionPath.FromSegments(prefix), reference.Descendants); return; }
            DefinitionScopeSegment segment = reference.Segments[index];
            if (!segment.Repeated) ExpandReference(reference, index + 1, [.. prefix, segment.Name]);
            else
            {
                DefinitionPath parent = DefinitionPath.FromSegments(prefix);
                Add(parent, false);
                if (_sourceValues.TryGetValue(parent, out var sequence) && sequence.ValueKind == JsonValueKind.Array)
                    for (int row = 0; row < sequence.GetArrayLength(); row++)
                        ExpandReference(reference, index + 1, [.. prefix, row.ToString(CultureInfo.InvariantCulture)]);
            }
        }

        private void Add(DefinitionPath path, bool descendants)
        {
            Charge(path);
            for (int length = 0; length <= path.Segments.Count; length++)
                _included.Add(DefinitionPath.FromSegments(path.Segments.Take(length)));
            if (!descendants || !_expanded.Add(path)) return;
            var pending = new Stack<DefinitionPath>();
            pending.Push(path);
            while (pending.TryPop(out DefinitionPath? candidate))
            {
                Charge(candidate);
                _included.Add(candidate);
                if (!_children.TryGetValue(candidate, out var children)) continue;
                foreach (DefinitionPath child in children)
                    if (_expanded.Add(child)) pending.Push(child);
            }
        }

        private void IncludeReferences(JsonElement condition, DefinitionPath? itemLocation)
        {
            if (condition.TryGetProperty("operands", out var operands))
            {
                foreach (JsonElement operand in operands.EnumerateArray()) IncludeReferences(operand, itemLocation);
                return;
            }
            JsonElement reference = condition.GetProperty("reference");
            DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
            if (reference.GetProperty("scope").GetString() == "item")
            {
                if (itemLocation is null) throw new DefinitionException("reference-context");
                path = DefinitionPath.FromSegments(itemLocation.Segments.Concat(path.Segments));
            }
            Add(path, true);
        }

        private DefinitionPath? ItemLocation(DefinitionPath path)
        {
            for (int count = path.Segments.Count - 1; count > 0; count--)
            {
                DefinitionPath parent = DefinitionPath.FromSegments(path.Segments.Take(count - 1));
                if (_schemas.TryGetValue(parent, out var node) && node.Type == DefinitionNodeType.Sequence)
                    return DefinitionPath.FromSegments(path.Segments.Take(count));
            }
            return null;
        }

        private void CloseRelationships(DefinitionPath path, DefinitionNode schema)
        {
            JsonElement keywords = schema.Keywords;
            if (keywords.TryGetProperty("dependentRequired", out var dependencies))
                foreach (JsonProperty dependency in dependencies.EnumerateObject())
                {
                    DefinitionPath trigger = DefinitionPath.FromSegments(path.Segments.Append(dependency.Name));
                    if (!_included.Contains(trigger)) continue;
                    if (Uncertain(trigger)) { _unresolvedDependencies = true; continue; }
                    if (!Effective(trigger)) continue;
                    foreach (JsonElement target in dependency.Value.EnumerateArray())
                        Add(DefinitionPath.FromSegments(path.Segments.Append(target.GetString()!)), true);
                }
            if (!keywords.TryGetProperty("if", out var guard)) return;
            var controllers = new HashSet<string>(StringComparer.Ordinal);
            if (guard.TryGetProperty("required", out var required))
                controllers.UnionWith(required.EnumerateArray().Select(item => item.GetString()!));
            if (guard.TryGetProperty("properties", out var properties)) controllers.UnionWith(properties.EnumerateObject().Select(item => item.Name));
            string[] targets = keywords.GetProperty("then").GetProperty("required").EnumerateArray().Select(item => item.GetString()!)
                .Concat(keywords.TryGetProperty("else", out var alternative)
                    ? alternative.GetProperty("required").EnumerateArray().Select(item => item.GetString()!) : []).ToArray();
            if (!controllers.Concat(targets).Any(name => _included.Contains(DefinitionPath.FromSegments(path.Segments.Append(name))))) return;
            foreach (string name in controllers) Add(DefinitionPath.FromSegments(path.Segments.Append(name)), true);
            if (controllers.Any(name => !_observed.Contains(DefinitionPath.FromSegments(path.Segments.Append(name))))) return;
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var unknown = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in controllers)
            {
                DefinitionPath child = DefinitionPath.FromSegments(path.Segments.Append(name));
                if (Uncertain(child)) unknown.Add(name);
                if (Effective(child) && _values.TryGetValue(child, out var value))
                {
                    values.Add(name, value);
                }
            }
            bool? holds = DefinitionImportWork.CompleteSynchronously(DefinitionValidator.EvaluateGuardAsync(guard, values, unknown));
            if (holds is null) { _unresolvedDependencies = true; return; }
            JsonElement? branch = holds == true ? keywords.GetProperty("then")
                : keywords.TryGetProperty("else", out var otherwise) ? otherwise : null;
            if (branch is { } selected)
                foreach (JsonElement name in selected.GetProperty("required").EnumerateArray())
                    Add(DefinitionPath.FromSegments(path.Segments.Append(name.GetString()!)), true);
        }

        private bool Effective(DefinitionPath path)
        {
            return _values.ContainsKey(path);
        }

        private bool Uncertain(DefinitionPath path)
        {
            foreach (DefinitionPath uncertain in _uncertain)
            {
                Charge(uncertain);
                if (DefinitionImportProjection.IsPrefix(uncertain, path)
                    || DefinitionImportProjection.IsPrefix(path, uncertain)) return true;
            }
            return false;
        }

        private void Observe()
        {
            DefinitionValidationResult result = DefinitionValidator.ObserveScope(definition, answers, _included, budget, cancellationToken);
            _observed.Clear();
            _observed.UnionWith(_included);
            _values.Clear();
            _rootValues.Clear();
            _unknownRoots.Clear();
            _uncertain.Clear();
            _uncertain.UnionWith(result.UncertainLocations);
            foreach ((string name, JsonElement value) in result.Values)
            {
                _rootValues.Add(name, value);
                IndexValues(DefinitionPath.FromSegments([name]), value);
            }
            _unknownRoots.UnionWith(result.UndecidedRoots);
            _unknownLocations.Clear();
            _unknownLocations.UnionWith(result.UndecidedLocations);
            _unknownRoots.UnionWith(result.Fields.Where(field => field.HasError).Select(field => field.Definition.Name));
        }

        private void IndexValues(DefinitionPath path, JsonElement value)
        {
            Charge(path);
            _values[path] = value;
            if (value.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in value.EnumerateObject())
                    IndexValues(DefinitionPath.FromSegments(path.Segments.Append(property.Name)), property.Value);
            else if (value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in value.EnumerateArray())
                    IndexValues(DefinitionPath.FromSegments(path.Segments.Append((index++).ToString(CultureInfo.InvariantCulture))), item);
            }
        }

        private void Charge(DefinitionPath path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            budget.Node(path.Segments.Count + 1);
        }

    }
}
