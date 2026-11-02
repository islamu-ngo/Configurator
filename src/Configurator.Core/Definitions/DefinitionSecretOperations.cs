namespace Configurator.Core.Definitions;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Pure secret-target eligibility and immutable answer transformations; callers retain custody and operation authority.</summary>
public static class DefinitionSecretOperations
{
    /// <summary>Resolves a local generation target using validation and omissions from the same immutable answer state.</summary>
    public static DefinitionNode? Resolve(Definition definition, IEnumerable<DefinitionPath> omissions,
        DefinitionValidationResult validation, DefinitionPath path, out DefinitionLocationSegment[] location)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(omissions);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(path);
        location = [];
        if (path.Segments.Count == 0) return null;
        if (omissions.Any(omitted => omitted.Segments.Count < path.Segments.Count
            && path.Segments.Take(omitted.Segments.Count).SequenceEqual(omitted.Segments))) return null;
        FieldDefinition? field = definition.Fields.FirstOrDefault(field => field.Name == path.Segments[0]);
        if (field is null) return null;
        var segments = new List<DefinitionLocationSegment>();
        DefinitionNode schema = definition.Configuration;
        JsonElement value = schema.Variants.Count == 0 ? default : JsonSerializer.SerializeToElement(validation.Values);
        for (int index = 0; index <= path.Segments.Count; index++)
        {
            if (ReadOnly(schema)) return null;
            if (schema.Variants.Count != 0)
            {
                DefinitionNode? variant = DefinitionValidator.SelectVariant(schema, value);
                if (variant is null || ReadOnly(variant)) return null;
                schema = variant;
            }
            if (index == path.Segments.Count)
            {
                location = segments.ToArray();
                return schema.Secret is { Acquisition: DefinitionSecretAcquisition.Local, Generation: not null }
                    && validation.Nodes.Any(state => state.FieldId == field.Id
                        && state.Location.Segments.SequenceEqual(segments) && state.IsActive) ? schema : null;
            }
            string segment = path.Segments[index];
            if (schema.Type == DefinitionNodeType.Mapping)
            {
                bool declared = schema.Properties.TryGetValue(segment, out DefinitionNode? child);
                if (!declared) child = schema.AdditionalProperties;
                if (child is null) return null;
                if (index > 0)
                {
                    if (declared) segments.Add(DefinitionLocationSegment.Property(segment));
                    else
                    {
                        if (value.ValueKind != JsonValueKind.Object) return null;
                        string[] keys = value.EnumerateObject().Where(member => !schema.Properties.ContainsKey(member.Name))
                            .Select(member => member.Name).ToArray();
                        int ordinal = Array.IndexOf(keys, segment);
                        if (ordinal < 0) return null;
                        segments.Add(DefinitionLocationSegment.MapEntry(ordinal));
                    }
                }
                if (index == 0) validation.Values.TryGetValue(segment, out value);
                else value = value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out JsonElement member) ? member : default;
                schema = child;
            }
            else if (schema.Type == DefinitionNodeType.Sequence && schema.Items is not null
                && value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row)
                && row < value.GetArrayLength())
            { schema = schema.Items; value = value[row]; segments.Add(DefinitionLocationSegment.ArrayRow(row)); }
            else return null;
        }
        return null;

        static bool ReadOnly(DefinitionNode schema) =>
            schema.Keywords.TryGetProperty("readOnly", out JsonElement value) && value.ValueKind == JsonValueKind.True;
    }

    /// <summary>Creates a new snapshot for an eligible target, retaining unrelated answers and omissions.</summary>
    public static AnswerSnapshot Replace(AnswerSnapshot answers, DefinitionValidationResult validation,
        DefinitionPath path, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Segments.Count == 0 || value.ValueKind == JsonValueKind.Undefined)
            throw new DefinitionException("secret-target-invalid");
        JsonNode root = JsonSerializer.SerializeToNode(answers.Values)!;
        JsonNode parent = root;
        for (int index = 0; index < path.Segments.Count - 1; index++)
        {
            if (parent is JsonArray array && path.TryGetArrayIndex(index, out int row) && row < array.Count && array[row] is { } item)
                parent = item;
            else if (parent is JsonObject mapping)
            {
                string segment = path.Segments[index];
                if (!mapping.TryGetPropertyValue(segment, out JsonNode? child))
                {
                    child = validation.TryGetEffectiveValue(DefinitionPath.FromSegments(path.Segments.Take(index + 1)), out JsonElement effective)
                        ? JsonNode.Parse(effective.GetRawText()) : new JsonObject();
                    mapping[segment] = child;
                }
                parent = child ?? throw new DefinitionException("secret-target-invalid");
            }
            else throw new DefinitionException("secret-target-invalid");
        }
        JsonNode? replacement = JsonNode.Parse(value.GetRawText());
        if (parent is JsonObject obj) obj[path.Segments[^1]] = replacement;
        else if (parent is JsonArray sequence && path.TryGetArrayIndex(path.Segments.Count - 1, out int row) && row < sequence.Count)
            sequence[row] = replacement;
        else throw new DefinitionException("secret-target-invalid");
        return new(checked(answers.InputRevision + 1), root.AsObject().ToDictionary(item => item.Key,
            item => JsonSerializer.SerializeToElement(item.Value), StringComparer.Ordinal),
            answers.Omitted.Where(omitted => !omitted.Segments.Take(path.Segments.Count).SequenceEqual(path.Segments)));
    }
}
