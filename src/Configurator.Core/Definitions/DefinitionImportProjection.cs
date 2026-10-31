namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Compiles only projections with nonoverlapping, presence-preserving assignments.</summary>
internal sealed class DefinitionImportProjection
{
    private sealed record Instruction(JsonElement Projection, DefinitionPath? Path, DefinitionNode? Schema,
        IReadOnlyList<(string Name, Instruction Child)> Children, bool AlwaysEmitted);

    private readonly Instruction _root;
    private readonly DefinitionPath? _selector;
    private readonly JsonElement _selectorValue;
    internal SetupArtifactDefinition Artifact { get; }

    private DefinitionImportProjection(SetupArtifactDefinition artifact, Instruction root,
        DefinitionPath? selector, JsonElement selectorValue)
    {
        Artifact = artifact;
        _root = root;
        _selector = selector;
        _selectorValue = selectorValue;
    }

    internal static async ValueTask<(DefinitionImportProjection? Projection, string Code)> CompileAsync(
        SetupDefinition definition, string artifactId, DefinitionImportWork work)
    {
        string code = "import-unsupported";
        SetupArtifactDefinition? artifact = definition.Artifacts.FirstOrDefault(item => item.Id == artifactId);
        if (artifact is null || artifact.Format is not ("json" or "yaml" or "dotenv")) return (null, code);
        code = "inverse-unsupported";
        bool ambiguous = false;
        DefinitionPath? selector = null;
        JsonElement selectorValue = default;
        var assignments = new List<DefinitionPath>();
        try
        {
            JsonElement projection = artifact.Projection;
            if (artifact.Format == "dotenv" && projection.GetProperty("op").GetString() != "object") return (null, code);
            if (projection.TryGetProperty("when", out JsonElement condition))
            {
                if (condition.GetProperty("op").GetString() != "equals" ||
                    condition.GetProperty("reference").GetProperty("scope").GetString() != "root") return (null, code);
                selector = DefinitionPath.Parse(condition.GetProperty("reference").GetProperty("path").GetString()!);
                if (selector.Segments.Count != 1) return (null, code);
                DefinitionNode? selected = SourceNode(definition.Configuration, selector, out _, out bool publicPath);
                selectorValue = condition.GetProperty("value");
                if (selected is null || !publicPath || selected.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence ||
                    selected.Keywords.TryGetProperty("x-configurator", out var metadata) && metadata.TryGetProperty("applicableWhen", out _) ||
                    !DefinitionValidator.ValueValid(selected, selectorValue)) return (null, code);
                assignments.Add(selector);
            }
            Instruction? root = await Compile(projection, definition.Configuration, false, assignments, DefinitionPath.Parse(""), true);
            if (ambiguous) code = "inverse-ambiguous";
            if (root is null) return (null, code);
            // The existing YAML codec admits object documents, not scalar/sequence documents.
            if (artifact.Format == "yaml" && projection.GetProperty("op").GetString() is not ("object" or "copy")) return (null, code);
            return (new(artifact, root, selector, selectorValue), "inverse-supported");
        }
        catch (SetupDefinitionException exception) when (exception.Code == "path-limit") { return (null, code); }

        async ValueTask<Instruction?> Compile(JsonElement projection, DefinitionNode scope, bool item,
            List<DefinitionPath> assigned, DefinitionPath target, bool root = false)
        {
            await work.InverseAsync();
            if (!root && projection.TryGetProperty("when", out _)) return null;
            string op = projection.GetProperty("op").GetString()!;
            var children = new List<(string, Instruction)>();
            DefinitionPath? path = null;
            DefinitionNode? schema = null;
            bool emitted = true;
            if (op is "copy" or "repeat")
            {
                JsonElement reference = projection.GetProperty("reference");
                if (reference.GetProperty("scope").GetString() != (item ? "item" : "root")) return null;
                path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
                schema = SourceNode(scope, path, out emitted, out _);
                if (schema is null) return null;
                foreach (DefinitionPath prior in assigned)
                {
                    await work.InverseAsync();
                    if (!IsPrefix(prior, path) && !IsPrefix(path, prior)) continue;
                    ambiguous = true;
                    return null;
                }
                assigned.Add(path);
                if (artifact.Format == "dotenv" && (op != "copy" || target.Segments.Count != 1 ||
                    schema.IsNullable || schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)) return null;
            }
            switch (op)
            {
                case "copy": break;
                case "literal":
                    if (artifact.Format == "dotenv" && projection.GetProperty("value").ValueKind
                        is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null) return null;
                    break;
                case "index":
                    if (!item) return null;
                    break;
                case "object":
                    if (artifact.Format == "dotenv" && target.Segments.Count != 0) return null;
                    if (artifact.Format == "dotenv" && target.Segments.Count != 0) return null;
                    foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                    {
                        Instruction? child = await Compile(member.Value, scope, item, assigned,
                            DefinitionPath.FromSegments(target.Segments.Append(member.Name)));
                        if (child is null) return null;
                        children.Add((member.Name, child));
                    }
                    break;
                case "list":
                    if (artifact.Format == "dotenv") return null;
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
                    var rowAssignments = new List<DefinitionPath>();
                    Instruction? row = await Compile(projection.GetProperty("item"), schema.Items!, true, rowAssignments,
                        DefinitionPath.FromSegments(target.Segments.Append("0")));
                    if (row is null || !row.AlwaysEmitted || rowAssignments.Count == 0 ||
                        row.Path?.Segments.Count != 0 && (schema.Items!.Type != DefinitionNodeType.Mapping || schema.Items.IsNullable))
                        return null;
                    children.Add(("", row));
                    break;
                default: return null;
            }
            return new(projection, path, schema, children.AsReadOnly(), emitted);
        }
    }

    internal async ValueTask<DefinitionImportEvidence> RecoverAsync(SetupDefinition definition, JsonElement input,
        DefinitionImportWork work, Func<DefinitionImportFinding, ValueTask> finding)
    {
        var assignments = new Dictionary<DefinitionPath, JsonElement>();
        var observations = new Dictionary<DefinitionPath, DefinitionImportObservation>();
        if (_selector is not null)
        {
            assignments.Add(_selector, _selectorValue);
            observations.Add(_selector, new(DefinitionImportObservationState.ContextDerivedValue, true, true, null));
            await finding(new(DefinitionImportFindingKind.ContextDerivedValue, "context-derived", null, SchemaLocation(definition, _selector)));
        }
        await Walk(_root, input, true, [], DefinitionPath.Parse(""), assignments, null);
        Dictionary<string, JsonElement> values;
        if (assignments.Keys.All(path => path.Segments.Count == 1))
            values = assignments.ToDictionary(pair => pair.Key.Segments[0], pair => pair.Value, StringComparer.Ordinal);
        else
        {
            JsonElement recovered = Build(assignments);
            if (recovered.ValueKind != JsonValueKind.Object) throw new SetupDefinitionException("input-invalid");
            values = recovered.EnumerateObject().ToDictionary(member => member.Name, member => member.Value, StringComparer.Ordinal);
        }
        SetupAnswerSnapshot snapshot = work.CooperativeYield is { } cooperativeYield
            ? await SetupAnswerSnapshot.CreateAsync(0, values, null, cooperativeYield, work.CancellationToken)
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
                if (present && Artifact.Format == "dotenv")
                {
                    if (DefinitionImport.TryDotenvValue(instruction.Schema!, value.GetString()!, out JsonElement converted))
                        value = converted;
                    else await finding(new(DefinitionImportFindingKind.InvalidSuppliedValue, "value-invalid",
                        origin, SchemaLocation(definition, concrete!)));
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
                    if (Artifact.Format == "dotenv" && expected.ValueKind != JsonValueKind.String)
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

    internal static DefinitionImportSchemaLocation? SchemaLocation(SetupDefinition definition, DefinitionPath path)
    {
        if (path.Segments.Count == 0) return null;
        SetupFieldDefinition? field = definition.Fields.FirstOrDefault(candidate => candidate.Name == path.Segments[0]);
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
        publicPath = SetupFieldDefinition.SensitivityOf(root) == SetupFieldSensitivity.Public;
        foreach (string segment in path.Segments)
        {
            if (node.Type != DefinitionNodeType.Mapping) return null;
            required &= node.Required.Contains(segment, StringComparer.Ordinal);
            if (node.Properties.TryGetValue(segment, out DefinitionNode? child)) node = child;
            else if (node.AdditionalProperties is { } additional) node = additional;
            else return null;
            publicPath &= SetupFieldDefinition.SensitivityOf(node) == SetupFieldSensitivity.Public;
            required &= !(node.Keywords.TryGetProperty("x-configurator", out var metadata) && metadata.TryGetProperty("applicableWhen", out _));
        }
        return node;
    }

    internal static bool IsPrefix(DefinitionPath prefix, DefinitionPath path) =>
        prefix.Segments.Count <= path.Segments.Count &&
        prefix.Segments.SequenceEqual(path.Segments.Take(prefix.Segments.Count), StringComparer.Ordinal);
}
