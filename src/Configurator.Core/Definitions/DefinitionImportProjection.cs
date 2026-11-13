namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Compiles presence-preserving assignments with bounded scalar-alias agreement.</summary>
internal sealed class DefinitionImportProjection
{
    private sealed record Instruction(JsonElement Projection, DefinitionPath? Path, DefinitionNode? Schema,
        IReadOnlyList<(string Name, Instruction Child)> Children, bool AlwaysEmitted, bool IsAlias);

    private sealed class AssignmentScope
    {
        internal bool Assigned { get; set; }
        internal Dictionary<string, AssignmentScope> Children { get; } = new(StringComparer.Ordinal);
        internal bool HasAssignments => Assigned || Children.Count > 0;
    }

    private readonly Instruction _root;
    private readonly IReadOnlyDictionary<DefinitionPath, JsonElement> _context;
    internal ArtifactDefinition Artifact { get; }

    private DefinitionImportProjection(ArtifactDefinition artifact, Instruction root,
        IReadOnlyDictionary<DefinitionPath, JsonElement> context)
    {
        Artifact = artifact;
        _root = root;
        _context = new ReadOnlyDictionary<DefinitionPath, JsonElement>(
            context.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()));
    }

    internal static async ValueTask<(DefinitionImportProjection? Projection, string Code)> CompileAsync(
        Definition definition, string artifactId, DefinitionImportWork work, AnswerSnapshot? context = null)
    {
        string code = "import-unsupported";
        ArtifactDefinition? artifact = definition.Artifacts.FirstOrDefault(item => item.Id == artifactId);
        if (artifact is null || artifact.Format is not (DefinitionArtifactFormat.Json or DefinitionArtifactFormat.Yaml or DefinitionArtifactFormat.Dotenv)) return (null, code);
        code = "inverse-unsupported";
        bool ambiguous = false;
        var contextValues = new Dictionary<DefinitionPath, JsonElement>();
        var assignments = new AssignmentScope();
        var scalarAssignments = new Dictionary<DefinitionPath, (DefinitionPath Parent, JsonElement? Condition)>();
        bool scopedArtifact = definition.Deployment?.Recipes.SelectMany(recipe => recipe.Scopes)
            .Any(scope => scope.ArtifactIds.Contains(artifactId, StringComparer.Ordinal)) == true;
        DefinitionValidationResult? contextValidation = null;
        HashSet<string>? unknown = null;
        try
        {
            if (definition.Deployment is { } deployment)
            {
                if (scopedArtifact && context is null) return (null, "context-required");
                if (scopedArtifact && !await ResolveContext()) return (null, "context-invalid");
                if (contextValidation?.ConfigurationContext is { } selected)
                {
                    if (!selected.IsReady) return (null, "context-unresolved");
                    if (!selected.EligibleArtifactIds.Contains(artifactId, StringComparer.Ordinal))
                        return (null, "context-artifact-ineligible");
                    if (deployment.When is { } deploymentCondition && await Active(deploymentCondition) != true)
                        return (null, "context-unresolved");
                    if (!await CaptureContext(DefinitionPath.Parse(deployment.RecipePointer)) ||
                        !await CaptureContext(DefinitionPath.Parse(deployment.ScopePointer)))
                        return (null, "context-unresolved");
                }
                else if (scopedArtifact && context is not null) return (null, "context-artifact-ineligible");
            }
            JsonElement projection = artifact.Projection;
            if (artifact.Format == DefinitionArtifactFormat.Dotenv && projection.GetProperty("op").GetString() != "object") return (null, code);
            if (projection.TryGetProperty("when", out JsonElement condition))
            {
                bool inferSelector = !scopedArtifact && condition.GetProperty("op").GetString() == "equals";
                if (!inferSelector)
                {
                    if (await Active(condition) != true) return (null, "context-unresolved");
                }
                else
                {
                    if (condition.GetProperty("op").GetString() != "equals" ||
                        condition.GetProperty("reference").GetProperty("scope").GetString() != "root") return (null, code);
                    DefinitionPath selector = DefinitionPath.Parse(condition.GetProperty("reference").GetProperty("path").GetString()!);
                    if (selector.Segments.Count != 1) return (null, code);
                    DefinitionNode? selected = SourceNode(definition.Configuration, selector, out _, out bool publicPath);
                    JsonElement selectorValue = condition.GetProperty("value");
                    if (selected is null || !publicPath || selected.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence ||
                        selected.Keywords.TryGetProperty("x-configurator", out var metadata) && metadata.TryGetProperty("applicableWhen", out _) ||
                        !DefinitionValidator.ValueValid(selected, selectorValue)) return (null, code);
                    if (!Reserve(selector)) return (null, "inverse-ambiguous");
                    contextValues.Add(selector, selectorValue);
                }
            }
            Instruction? root = await Compile(projection, definition.Configuration, false, assignments, DefinitionPath.Parse(""), true);
            if (ambiguous) code = "inverse-ambiguous";
            if (root is null) return (null, code);
            // The existing YAML codec admits object documents, not scalar/sequence documents.
            if (artifact.Format == DefinitionArtifactFormat.Yaml && projection.GetProperty("op").GetString() is not ("object" or "copy")) return (null, code);
            return (new(artifact, root, contextValues), "inverse-supported");
        }
        catch (DefinitionException exception) when (exception.Code == "path-limit") { return (null, code); }

        async ValueTask<Instruction?> Compile(JsonElement projection, DefinitionNode scope, bool item,
            AssignmentScope assigned, DefinitionPath target, bool guardResolved = false, bool objectMember = false)
        {
            await work.InverseAsync();
            if (!guardResolved && projection.TryGetProperty("when", out _)) return null;
            string op = projection.GetProperty("op").GetString()!;
            var children = new List<(string, Instruction)>();
            DefinitionPath? path = null;
            DefinitionNode? schema = null;
            bool emitted = true;
            bool isAlias = false;
            if (op is "copy" or "repeat")
            {
                JsonElement reference = projection.GetProperty("reference");
                if (reference.GetProperty("scope").GetString() != (item ? "item" : "root")) return null;
                path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
                schema = SourceNode(scope, path, out emitted, out _);
                if (schema is null) return null;
                // Schema activation is evaluated after recovery so a file can supply its own mapped controllers.
                AssignmentScope position = assigned;
                foreach (string segment in path.Segments)
                {
                    await work.InverseAsync();
                    if (position.Assigned)
                    {
                        ambiguous = true;
                        return null;
                    }
                    if (!position.Children.TryGetValue(segment, out AssignmentScope? next))
                        position.Children.Add(segment, next = new AssignmentScope());
                    position = next;
                }
                bool scalarAlias = !item && objectMember && op == "copy" && !schema.IsNullable && schema.Variants.Count == 0 &&
                    schema.Type is DefinitionNodeType.Text or DefinitionNodeType.Boolean or DefinitionNodeType.WholeNumber or DefinitionNodeType.Number;
                DefinitionPath parent = DefinitionPath.FromSegments(target.Segments.Take(target.Segments.Count - 1));
                JsonElement? condition = projection.TryGetProperty("when", out JsonElement guard) ? guard : null;
                if (position.HasAssignments)
                {
                    await work.InverseAsync();
                    // A shared output parent fixes inherited guards; only identical scalar copies may agree as aliases.
                    if (!scalarAlias || !position.Assigned || position.Children.Count != 0 ||
                        !scalarAssignments.TryGetValue(path, out var previous) || previous.Parent != parent ||
                        (previous.Condition is { } prior ? condition is not { } current || !JsonElement.DeepEquals(prior, current) : condition is not null))
                    {
                        ambiguous = true;
                        return null;
                    }
                    isAlias = true;
                }
                else if (scalarAlias) scalarAssignments.Add(path, (parent, condition));
                position.Assigned = true;
                if (artifact.Format == DefinitionArtifactFormat.Dotenv && (op != "copy" || target.Segments.Count != 1 ||
                    schema.IsNullable || schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)) return null;
            }
            switch (op)
            {
                case "copy": break;
                case "literal":
                    if (artifact.Format == DefinitionArtifactFormat.Dotenv && projection.GetProperty("value").ValueKind
                        is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null) return null;
                    break;
                case "index":
                    if (!item) return null;
                    break;
                case "object":
                    if (artifact.Format == DefinitionArtifactFormat.Dotenv && target.Segments.Count != 0) return null;
                    foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                    {
                        if (member.Value.TryGetProperty("when", out JsonElement memberCondition))
                        {
                            if (item || context is null) return null;
                            bool? active = await Active(memberCondition);
                            if (active is null) return null;
                            if (!active.Value) continue;
                        }
                        Instruction? child = await Compile(member.Value, scope, item, assigned,
                            DefinitionPath.FromSegments(target.Segments.Append(member.Name)), contextValidation is not null, objectMember: true);
                        if (child is null) return null;
                        children.Add((member.Name, child));
                    }
                    break;
                case "list":
                    if (artifact.Format == DefinitionArtifactFormat.Dotenv) return null;
                    int index = 0;
                    foreach (JsonElement entry in projection.GetProperty("items").EnumerateArray())
                    {
                        string ordinal = (index++).ToString(CultureInfo.InvariantCulture);
                        Instruction? child = await Compile(entry, scope, item, assigned,
                            DefinitionPath.FromSegments(target.Segments.Append(ordinal)));
                        // Omitted list elements shift every subsequent position.
                        if (child is null || !child.AlwaysEmitted) return null;
                        children.Add((ordinal, child));
                    }
                    break;
                case "repeat":
                    if (schema!.Type != DefinitionNodeType.Sequence || schema.IsNullable) return null;
                    var rowAssignments = new AssignmentScope();
                    Instruction? row = await Compile(projection.GetProperty("item"), schema.Items!, true, rowAssignments,
                        DefinitionPath.FromSegments(target.Segments.Append("0")));
                    if (row is null || !row.AlwaysEmitted || !rowAssignments.HasAssignments ||
                        row.Path?.Segments.Count != 0 && (schema.Items!.Type != DefinitionNodeType.Mapping || schema.Items.IsNullable))
                        return null;
                    children.Add(("", row));
                    break;
                default: return null;
            }
            return new(projection, path, schema, children.AsReadOnly(), emitted, isAlias);
        }

        bool Reserve(DefinitionPath path)
        {
            AssignmentScope position = assignments;
            foreach (string segment in path.Segments)
            {
                if (position.Assigned) return false;
                if (!position.Children.TryGetValue(segment, out AssignmentScope? next))
                    position.Children.Add(segment, next = new AssignmentScope());
                position = next;
            }
            if (position.HasAssignments) return false;
            position.Assigned = true;
            return true;
        }

        async ValueTask<bool> CaptureContext(DefinitionPath path)
        {
            await work.InverseAsync();
            foreach (string _ in path.Segments) await work.InverseAsync();
            if (contextValues.TryGetValue(path, out JsonElement captured))
                return contextValidation is not null && contextValidation.TryGetEffectiveValue(path, out JsonElement observed)
                    && JsonElement.DeepEquals(captured, observed);
            DefinitionNode? schema = SourceNode(definition.Configuration, path, out _, out bool publicPath);
            if (schema is null || !publicPath || path.Segments.Count == 0 ||
                schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence ||
                unknown?.Contains(path.Segments[0]) == true ||
                contextValidation is null || !contextValidation.TryGetEffectiveValue(path, out JsonElement value) ||
                !DefinitionValidator.ValueValid(schema, value) || !Reserve(path)) return false;
            contextValues.Add(path, value);
            return true;
        }

        async ValueTask<bool?> Active(JsonElement condition)
        {
            await work.InverseAsync();
            if (!await ResolveContext()) return null;
            if (condition.TryGetProperty("operands", out JsonElement operands))
            {
                foreach (JsonElement operand in operands.EnumerateArray())
                    if (await Active(operand) is null) return null;
            }
            else
            {
                JsonElement reference = condition.GetProperty("reference");
                if (reference.GetProperty("scope").GetString() != "root" ||
                    !await CaptureContext(DefinitionPath.Parse(reference.GetProperty("path").GetString()!))) return null;
            }
            DefinitionValidationResult resolved = contextValidation!;
            return await DefinitionValidator.EvaluateConditionAsync(condition, resolved.Values, unknown,
                deferredLocations: resolved.DeferredLocations, work: work, unresolvedLocations: resolved.UndecidedLocations);
        }

        async ValueTask<bool> ResolveContext()
        {
            if (context is null) return false;
            if (contextValidation is null)
            {
                contextValidation = await DefinitionValidator.ValidateImportAsync(definition, context, work, work.CancellationToken);
                unknown = contextValidation.UndecidedRoots
                    .Concat(contextValidation.Fields.Where(field => field.HasError).Select(field => field.Definition.Name))
                    .ToHashSet(StringComparer.Ordinal);
            }
            return !contextValidation.Diagnostics.Any(item => item.Code is "field-unknown" or "answer-omission" or "validation-limit");
        }
    }

    internal async ValueTask<DefinitionImportEvidence> RecoverAsync(Definition definition, JsonElement input,
        DefinitionImportWork work, Func<DefinitionImportFinding, ValueTask> finding)
    {
        var assignments = new Dictionary<DefinitionPath, JsonElement>();
        var observations = new Dictionary<DefinitionPath, DefinitionImportObservation>();
        foreach ((DefinitionPath path, JsonElement value) in _context)
        {
            assignments.Add(path, value);
            observations.Add(path, new(DefinitionImportObservationState.ContextDerivedValue, true, true, null));
            await finding(new(DefinitionImportFindingKind.ContextDerivedValue, "context-derived", null, SchemaLocation(definition, path)));
        }
        await Walk(_root, input, true, [], DefinitionPath.Parse(""), assignments, null);
        Dictionary<string, JsonElement> values;
        if (assignments.Keys.All(path => path.Segments.Count == 1))
            values = assignments.ToDictionary(pair => pair.Key.Segments[0], pair => pair.Value, StringComparer.Ordinal);
        else
        {
            JsonElement recovered = Build(assignments);
            if (recovered.ValueKind != JsonValueKind.Object) throw new DefinitionException("input-invalid");
            values = recovered.EnumerateObject().ToDictionary(member => member.Name, member => member.Value, StringComparer.Ordinal);
        }
        AnswerSnapshot snapshot = work.CooperativeYield is { } cooperativeYield
            ? await AnswerSnapshot.CreateAsync(0, values, null, cooperativeYield, work.CancellationToken)
            : new(0, values);
        return new(snapshot, assignments.Keys.ToArray(), observations);

        async ValueTask Walk(Instruction instruction, JsonElement value, bool present, int[] source,
            DefinitionPath scope, Dictionary<DefinitionPath, JsonElement> target, int? row)
        {
            await work.InverseAsync();
            string op = instruction.Projection.GetProperty("op").GetString()!;
            DefinitionPath? concrete = instruction.Path is null ? null
                : DefinitionPath.FromSegments(scope.Segments.Concat(instruction.Path.Segments));
            var origin = new DefinitionImportSourceLocation(source);
            if (op == "copy")
            {
                if (present && Artifact.Format == DefinitionArtifactFormat.Dotenv)
                {
                    if (DefinitionImport.TryDotenvValue(instruction.Schema!, value.GetString()!, out JsonElement converted))
                        value = converted;
                    else await finding(new(DefinitionImportFindingKind.InvalidSuppliedValue, "value-invalid",
                        origin, SchemaLocation(definition, concrete!)));
                }
                if (instruction.IsAlias && observations.TryGetValue(concrete!, out DefinitionImportObservation? prior))
                {
                    if ((prior.State == DefinitionImportObservationState.ObservedValue) != present ||
                        present && (!target.TryGetValue(instruction.Path!, out JsonElement first) ||
                            !JsonElement.DeepEquals(first, value) ||
                            !await DefinitionValidator.LocalValueValidAsync(instruction.Schema!, value, work: work)))
                        await finding(new(DefinitionImportFindingKind.StructuralOutputChange, "alias-conflict",
                            origin, SchemaLocation(definition, concrete!)));
                    return;
                }
                observations[concrete!] = new(present ? DefinitionImportObservationState.ObservedValue
                    : DefinitionImportObservationState.ObservedAbsence, true, true, origin);
                if (present)
                {
                    target.Add(instruction.Path!, value);
                    await Account(instruction.Schema!, value, concrete!, source);
                }
                return;
            }
            if (op == "repeat")
            {
                if (!present)
                {
                    observations[concrete!] = new(DefinitionImportObservationState.ObservedAbsence, true, true, origin);
                    return;
                }
                if (value.ValueKind != JsonValueKind.Array) { await Structural(); return; }
                observations[concrete!] = new(DefinitionImportObservationState.ObservedValue, false, true, origin);
                var rows = new JsonArray();
                int index = 0;
                foreach (JsonElement element in value.EnumerateArray())
                {
                    var rowValues = new Dictionary<DefinitionPath, JsonElement>();
                    DefinitionPath rowPath = DefinitionPath.FromSegments(concrete!.Segments.Append(index.ToString(CultureInfo.InvariantCulture)));
                    observations[rowPath] = new(DefinitionImportObservationState.ObservedValue, false, false,
                        new DefinitionImportSourceLocation([.. source, index]));
                    await Walk(instruction.Children[0].Child, element, true, [.. source, index], rowPath, rowValues, index);
                    rows.Add(JsonNode.Parse(Build(rowValues).GetRawText()));
                    index++;
                }
                target.Add(instruction.Path!, JsonSerializer.SerializeToElement(rows));
                return;
            }
            if (op is "literal" or "index")
            {
                if (op == "literal")
                {
                    JsonElement expected = instruction.Projection.GetProperty("value");
                    if (Artifact.Format == DefinitionArtifactFormat.Dotenv && expected.ValueKind != JsonValueKind.String)
                        expected = JsonSerializer.SerializeToElement(expected.GetRawText());
                    if (!present || !JsonElement.DeepEquals(value, expected)) await Structural();
                }
                else if (!present || value.ValueKind != JsonValueKind.Number ||
                    !value.TryGetInt32(out int ordinal) || ordinal != row) await Structural();
                return;
            }
            if (op == "object")
            {
                if (!present || value.ValueKind != JsonValueKind.Object) { await Structural(); return; }
                var members = value.EnumerateObject().Select((member, index) => (member, index))
                    .ToDictionary(entry => entry.member.Name, StringComparer.Ordinal);
                foreach ((string name, Instruction child) in instruction.Children)
                {
                    bool exists = members.Remove(name, out var entry);
                    await Walk(child, exists ? entry.member.Value : default, exists,
                        exists ? [.. source, entry.index] : source, scope, target, row);
                }
                foreach (var entry in members.Values)
                    await finding(new(DefinitionImportFindingKind.UnrecognizedInput, "input-unrecognized",
                        new DefinitionImportSourceLocation([.. source, entry.index]), null));
                return;
            }
            if (!present || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != instruction.Children.Count)
            {
                await Structural();
                return;
            }
            for (int index = 0; index < instruction.Children.Count; index++)
                await Walk(instruction.Children[index].Child, value[index], true, [.. source, index], scope, target, row);

            ValueTask Structural() => finding(new(DefinitionImportFindingKind.StructuralOutputChange, "output-structure",
                origin, concrete is null ? null : SchemaLocation(definition, concrete)));
        }

        async ValueTask Account(DefinitionNode schema, JsonElement value, DefinitionPath path, int[] source)
        {
            await work.InverseAsync();
            observations[path] = new(DefinitionImportObservationState.ObservedValue, true, true,
                new DefinitionImportSourceLocation(source));
            if (schema.Variants.Count > 0)
            {
                if (DefinitionValidator.SelectVariant(schema, value) is not { } branch) return;
                schema = branch;
            }
            if (schema.Type == DefinitionNodeType.Mapping && value.ValueKind == JsonValueKind.Object)
            {
                int index = 0;
                var mapEntries = new List<string>();
                foreach (JsonProperty member in value.EnumerateObject())
                {
                    if (!schema.Properties.TryGetValue(member.Name, out DefinitionNode? child))
                    {
                        child = schema.AdditionalProperties;
                        if (child is not null) mapEntries.Add(member.Name);
                    }
                    if (child is null)
                        await finding(new(DefinitionImportFindingKind.UnrecognizedInput, "value-unrecognized",
                            new DefinitionImportSourceLocation([.. source, index]), null));
                    else await Account(child, member.Value, DefinitionPath.FromSegments(path.Segments.Append(member.Name)),
                        [.. source, index]);
                    index++;
                }
                if (schema.AdditionalProperties is not null)
                {
                    mapEntries.Sort(StringComparer.Ordinal);
                    observations[path] = observations[path] with { MapEntries = mapEntries.AsReadOnly() };
                }
            }
            else if (schema.Type == DefinitionNodeType.Sequence && value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    await Account(schema.Items!, item, DefinitionPath.FromSegments(path.Segments.Append(
                        index.ToString(CultureInfo.InvariantCulture))), [.. source, index]);
                    index++;
                }
            }
        }
    }

    private static JsonElement Build(Dictionary<DefinitionPath, JsonElement> assignments)
    {
        if (assignments.TryGetValue(DefinitionPath.Parse(""), out JsonElement whole)) return whole;
        var root = new JsonObject();
        foreach ((DefinitionPath path, JsonElement value) in assignments)
        {
            JsonObject container = root;
            for (int index = 0; index < path.Segments.Count - 1; index++)
            {
                string name = path.Segments[index];
                if (container[name] is not JsonObject child) container[name] = child = new JsonObject();
                container = child;
            }
            container[path.Segments[^1]] = JsonNode.Parse(value.GetRawText());
        }
        return JsonSerializer.SerializeToElement(root);
    }

    internal static DefinitionImportSchemaLocation? SchemaLocation(Definition definition, DefinitionPath path)
    {
        if (path.Segments.Count == 0) return null;
        FieldDefinition? field = definition.Fields.FirstOrDefault(candidate => candidate.Name == path.Segments[0]);
        if (field is null) return null;
        DefinitionNode schema = field.Node;
        var relative = new List<string>();
        foreach (string segment in path.Segments.Skip(1))
        {
            if (schema.Type != DefinitionNodeType.Mapping || !schema.Properties.TryGetValue(segment, out DefinitionNode? child)) break;
            relative.Add(segment);
            schema = child;
        }
        return new(field.Id, relative);
    }

    internal static DefinitionNode? SourceNode(DefinitionNode root, DefinitionPath path,
        out bool required, out bool publicPath)
    {
        DefinitionNode node = root;
        required = true;
        publicPath = FieldDefinition.SensitivityOf(root) == FieldSensitivity.Public;
        foreach (string segment in path.Segments)
        {
            if (node.Type != DefinitionNodeType.Mapping) return null;
            required &= node.Required.Contains(segment, StringComparer.Ordinal);
            if (node.Properties.TryGetValue(segment, out DefinitionNode? child)) node = child;
            else if (node.AdditionalProperties is { } additional) node = additional;
            else return null;
            publicPath &= FieldDefinition.SensitivityOf(node) == FieldSensitivity.Public;
            required &= !(node.Keywords.TryGetProperty("x-configurator", out var metadata) && metadata.TryGetProperty("applicableWhen", out _));
        }
        return node;
    }

    internal static bool IsPrefix(DefinitionPath prefix, DefinitionPath path) =>
        prefix.Segments.Count <= path.Segments.Count &&
        prefix.Segments.SequenceEqual(path.Segments.Take(prefix.Segments.Count), StringComparer.Ordinal);
}
