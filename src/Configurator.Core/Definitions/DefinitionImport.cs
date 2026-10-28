namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Environment;

public sealed record DefinitionImportResult(bool Succeeded, SetupAnswerSnapshot? Answers, string Code)
{
    public IReadOnlyList<string> ProvidedFields { get; init; } = [];
    public DefinitionDiagnostic? Diagnostic { get; init; }
    public override string ToString() => $"{nameof(DefinitionImportResult)}:{Code}";
}

public static class DefinitionImport
{
    public static DefinitionImportResult Assess(SetupDefinition definition, string artifactId,
        ReadOnlyMemory<byte> bytes, SetupAnswerSnapshot current, IReadOnlySet<string>? replaceableDefaults = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(current);
        if (!TryMappings(definition, artifactId, out SetupArtifactDefinition? artifact,
            out Dictionary<DefinitionPath, DefinitionPath> mappings, out string code)) return Failed(code);
        try
        {
            if (bytes.IsEmpty || bytes.Length > SetupCompositionLimits.DefaultAggregateSourceBytes)
                return Failed("input-invalid");
            Dictionary<string, JsonElement>? input = Parse(artifact!.Format, bytes);
            if (input is null) return Failed("input-invalid");
            var supplied = new Dictionary<DefinitionPath, JsonElement>();
            if (!Collect(JsonSerializer.SerializeToElement(input), DefinitionPath.Parse("")))
                return Failed("inverse-unsupported");
            if (supplied.Count == 0) return Failed("input-invalid");

            var values = current.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            var imported = new HashSet<string>(StringComparer.Ordinal);
            var assignments = new Dictionary<DefinitionPath, JsonElement>();
            foreach ((DefinitionPath path, JsonElement value) in supplied)
            {
                if (path.Segments.Count == 0)
                {
                    if (value.ValueKind != JsonValueKind.Object) return Failed("input-invalid");
                    foreach (JsonProperty member in value.EnumerateObject())
                        assignments.Add(DefinitionPath.FromSegments([member.Name]), member.Value);
                }
                else assignments.Add(path, value);
            }
            foreach ((DefinitionPath path, JsonElement value) in assignments.OrderBy(item => item.Key.ToPointer(), StringComparer.Ordinal))
            {
                string field = path.Segments[0];
                imported.Add(field);
                JsonElement answer = value;
                SetupFieldDefinition? descriptor = definition.Fields.FirstOrDefault(item => item.Name == field);
                DefinitionDiagnostic? conflict = descriptor is null ? null : new("answer-conflict", descriptor.Id);
                if (artifact.Format == "dotenv" && !TryDotenvValue(
                    SourceNode(definition.Configuration, path)!, value.GetString()!, out answer))
                    return Failed("inverse-unsupported");
                foreach (DefinitionPath omitted in current.Omitted)
                {
                    if (IsPrefix(omitted, path) || IsPrefix(path, omitted)
                        && TrySelect(answer, omitted, path.Segments.Count, out _))
                        return Failed("answer-conflict", conflict);
                }
                bool replaceable = descriptor is not null && replaceableDefaults?.Contains(descriptor.Id) == true;
                if (path.Segments.Count == 1)
                {
                    if (values.TryGetValue(field, out JsonElement previous)
                        && !JsonElement.DeepEquals(previous, answer) && !replaceable)
                        return Failed("answer-conflict", conflict);
                    values[field] = answer;
                    continue;
                }
                JsonObject root;
                if (!values.TryGetValue(field, out JsonElement existing)) root = new();
                else if (existing.ValueKind == JsonValueKind.Object) root = JsonNode.Parse(existing.GetRawText())!.AsObject();
                else if (replaceable) root = new();
                else return Failed("answer-conflict", conflict);
                JsonObject container = root;
                for (int index = 1; index < path.Segments.Count - 1; index++)
                {
                    string segment = path.Segments[index];
                    if (container.TryGetPropertyValue(segment, out JsonNode? child))
                    {
                        if (child is JsonObject mapping) { container = mapping; continue; }
                        if (!replaceable) return Failed("answer-conflict", conflict);
                    }
                    var created = new JsonObject();
                    container[segment] = created;
                    container = created;
                }
                string leaf = path.Segments[^1];
                if (container.TryGetPropertyValue(leaf, out JsonNode? prior)
                    && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(prior), answer) && !replaceable)
                    return Failed("answer-conflict", conflict);
                container[leaf] = JsonNode.Parse(answer.GetRawText());
                values[field] = JsonSerializer.SerializeToElement(root);
            }

            var candidate = new SetupAnswerSnapshot(checked(current.InputRevision + 1), values, current.Omitted);
            DefinitionValidationResult validation = DefinitionValidator.Validate(definition, candidate);
            var rejected = validation.Fields.FirstOrDefault(field =>
                imported.Contains(field.Definition.Name) && (!field.IsActive || field.HasError));
            if (rejected is not null)
                return Failed("import-field-invalid", validation.Diagnostics.FirstOrDefault(item => item.FieldId == rejected.Id)
                    ?? new("import-field-invalid", rejected.Id));
            if (validation.Diagnostics.FirstOrDefault(item => item.Code != "value-required") is { } diagnostic)
                return Failed("import-field-invalid", definition.Fields.Any(field => field.Id == diagnostic.FieldId) ? diagnostic : null);
            foreach ((DefinitionPath path, JsonElement value) in assignments)
            {
                if (!validation.TryGetEffectiveValue(path, out JsonElement effective) || !RetainsMembers(value, effective))
                {
                    SetupFieldDefinition? descriptor = definition.Fields.FirstOrDefault(field => field.Name == path.Segments[0]);
                    return Failed("import-field-invalid", descriptor is null ? null : new("import-field-invalid", descriptor.Id));
                }
            }
            return new(true, candidate, "import-ready")
            {
                ProvidedFields = Array.AsReadOnly(definition.Fields.Where(field => imported.Contains(field.Name))
                    .Select(field => field.Id).ToArray())
            };

            bool Collect(JsonElement value, DefinitionPath target)
            {
                if (mappings.TryGetValue(target, out DefinitionPath? source))
                {
                    supplied.Add(source, value);
                    return true;
                }
                if (value.ValueKind != JsonValueKind.Object || !mappings.Keys.Any(path => IsPrefix(target, path)))
                    return false;
                foreach (JsonProperty member in value.EnumerateObject())
                    if (!Collect(member.Value, DefinitionPath.FromSegments(target.Segments.Append(member.Name))))
                        return false;
                return true;
            }
        }
        catch (Exception exception) when (exception is SetupCompositionException or JsonException
            or SetupDefinitionException or OverflowException or InvalidOperationException)
        {
            return Failed("input-invalid");
        }
    }

    public static bool CanImport(SetupDefinition definition, string artifactId) =>
        TryMappings(definition, artifactId, out _, out _, out _);

    private static bool TryMappings(SetupDefinition definition, string artifactId,
        out SetupArtifactDefinition? artifact, out Dictionary<DefinitionPath, DefinitionPath> mappings, out string code)
    {
        artifact = definition.Artifacts.FirstOrDefault(item => item.Id == artifactId);
        mappings = new();
        code = "import-unsupported";
        if (artifact is null || artifact.Format is not ("dotenv" or "json" or "yaml"))
            return false;

        code = "inverse-unsupported";
        var collected = new Dictionary<DefinitionPath, DefinitionPath>();
        bool ambiguous = false;
        bool dotenv = artifact.Format == "dotenv";
        bool supported;
        try { supported = Visit(artifact.Data.GetProperty("projection"), DefinitionPath.Parse("")); }
        catch (SetupDefinitionException exception) when (exception.Code == "path-limit") { return false; }
        if (ambiguous) code = "inverse-ambiguous";
        if (!supported || collected.Count == 0) return false;
        mappings = collected;
        return true;

        bool Visit(JsonElement projection, DefinitionPath target)
        {
            if (projection.TryGetProperty("when", out _)) return false;
            if (projection.GetProperty("op").GetString() == "object")
            {
                if (dotenv && target.Segments.Count != 0) return false;
                foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                    if (!Visit(member.Value, DefinitionPath.FromSegments(target.Segments.Append(member.Name)))) return false;
                return true;
            }
            if (projection.GetProperty("op").GetString() != "copy") return false;
            JsonElement reference = projection.GetProperty("reference");
            if (reference.GetProperty("scope").GetString() != "root") return false;
            DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
            DefinitionNode? node = SourceNode(definition.Configuration, path);
            if (node is null || dotenv && (target.Segments.Count != 1 || node.IsNullable
                || node.JsonType is not ("string" or "boolean" or "integer" or "number"))) return false;
            if (collected.Values.Any(other => IsPrefix(other, path) || IsPrefix(path, other)))
            {
                ambiguous = true;
                return false;
            }
            collected.Add(target, path);
            return true;
        }
    }

    private static DefinitionNode? SourceNode(DefinitionNode root, DefinitionPath path)
    {
        DefinitionNode node = root;
        foreach (string segment in path.Segments)
        {
            if (node.Type != DefinitionNodeType.Mapping) return null;
            if (node.Properties.TryGetValue(segment, out DefinitionNode? child)) node = child;
            else if (node.AdditionalProperties is { } additional) node = additional;
            else return null;
        }
        return node;
    }

    private static bool IsPrefix(DefinitionPath prefix, DefinitionPath path) =>
        prefix.Segments.Count <= path.Segments.Count
        && prefix.Segments.SequenceEqual(path.Segments.Take(prefix.Segments.Count), StringComparer.Ordinal);

    private static bool TrySelect(JsonElement value, DefinitionPath path, int start, out JsonElement selected)
    {
        selected = value;
        for (int index = start; index < path.Segments.Count; index++)
        {
            if (selected.ValueKind == JsonValueKind.Object)
            {
                if (!selected.TryGetProperty(path.Segments[index], out selected)) return false;
            }
            else if (selected.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int position)
                && position < selected.GetArrayLength()) selected = selected[position];
            else return false;
        }
        return true;
    }

    // Inspect only the validator's resolved tree: source applicability is owned by Core validation.
    private static bool RetainsMembers(JsonElement supplied, JsonElement effective)
    {
        if (supplied.ValueKind == JsonValueKind.Object)
            return effective.ValueKind == JsonValueKind.Object && supplied.EnumerateObject().All(member =>
                effective.TryGetProperty(member.Name, out JsonElement child) && RetainsMembers(member.Value, child));
        if (supplied.ValueKind == JsonValueKind.Array)
            return effective.ValueKind == JsonValueKind.Array && supplied.GetArrayLength() == effective.GetArrayLength()
                && supplied.EnumerateArray().Zip(effective.EnumerateArray()).All(pair => RetainsMembers(pair.First, pair.Second));
        return true;
    }

    private static bool TryDotenvValue(DefinitionNode field, string text, out JsonElement value)
    {
        value = JsonSerializer.SerializeToElement(text);
        if (field.Type == DefinitionNodeType.Text) return true;
        if (field.Type == DefinitionNodeType.Boolean && text is "true" or "false")
        {
            value = JsonSerializer.SerializeToElement(text == "true");
            return true;
        }
        if (field.Type == DefinitionNodeType.WholeNumber && long.TryParse(text, NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out long integer))
        {
            value = JsonSerializer.SerializeToElement(integer);
            return value.GetRawText() == text;
        }
        if (field.Type == DefinitionNodeType.Number && decimal.TryParse(text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal number))
        {
            value = JsonSerializer.SerializeToElement(number);
            return value.GetRawText() == text;
        }
        return false;
    }

    private static Dictionary<string, JsonElement>? Parse(string format, ReadOnlyMemory<byte> bytes)
    {
        if (format == "dotenv")
        {
            DotenvParseResult parsed = DotenvCodec.Parse(bytes);
            if (!parsed.Succeeded) return null;
            var entries = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (DotenvEntry entry in parsed.Document!.Entries)
                if (entry.Value is null || !entries.TryAdd(entry.Key, JsonSerializer.SerializeToElement(entry.Value)))
                    return null;
            return entries;
        }
        CompositionMap normalized = format == "yaml"
            ? SetupCompositionYamlParser.Parse(bytes, SetupCompositionLimits.Default, CancellationToken.None, keyPolicy: CompositionKeyPolicy.Exact)
            : SetupCompositionNormalizer.ParseJson(bytes, SetupCompositionLimits.Default, CancellationToken.None, keyPolicy: CompositionKeyPolicy.Exact);
        byte[] normalizedBytes = SetupCompositionNormalizer.WriteJson(normalized, CancellationToken.None);
        try
        {
            using JsonDocument document = JsonDocument.Parse(normalizedBytes);
            return document.RootElement.EnumerateObject().ToDictionary(
                item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
        }
        finally { CryptographicOperations.ZeroMemory(normalizedBytes); }
    }

    private static DefinitionImportResult Failed(string code, DefinitionDiagnostic? diagnostic = null) =>
        new(false, null, code) { Diagnostic = diagnostic };
}
