using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

namespace Configurator.Core.Tests.Definitions;

/// <summary>Resolves nested settings by JSON Pointer in definitions, validation results and answer values.</summary>
internal static class DefinitionPathLookup
{
    internal static FieldDefinition Field(this Definition definition, string location)
    {
        IReadOnlyList<string> segments = DefinitionPath.Parse(location).Segments;
        FieldDefinition field = definition.Fields.Single(candidate => candidate.Name == segments[0]);
        foreach (string segment in segments.Skip(1))
            field = field.Properties.Single(candidate => candidate.Name == segment);
        return field;
    }

    internal static DefinitionNodeState Node(this DefinitionValidationResult result, Definition definition, string location)
    {
        IReadOnlyList<string> segments = DefinitionPath.Parse(location).Segments;
        string fieldId = definition.Fields.Single(candidate => candidate.Name == segments[0]).Id;
        return result.Nodes.Single(node => node.FieldId == fieldId && node.RelativePath.SequenceEqual(segments.Skip(1), StringComparer.Ordinal));
    }

    /// <summary>Sets one nested answer, creating its enclosing objects.</summary>
    internal static void Set(this IDictionary<string, JsonElement> values, string location, object value)
    {
        IReadOnlyList<string> segments = DefinitionPath.Parse(location).Segments;
        JsonNode supplied = JsonSerializer.SerializeToNode(value)!;
        if (segments.Count == 1)
        {
            values[segments[0]] = JsonSerializer.SerializeToElement(supplied);
            return;
        }
        JsonObject root = values.TryGetValue(segments[0], out JsonElement existing) && existing.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(existing.GetRawText())!.AsObject() : [];
        JsonObject current = root;
        foreach (string segment in segments.Skip(1).SkipLast(1))
        {
            if (current[segment] is not JsonObject next) current[segment] = next = [];
            current = next;
        }
        current[segments[^1]] = supplied;
        values[segments[0]] = JsonSerializer.SerializeToElement(root);
    }

    internal static AnswerSnapshot Answers(long revision, params (string Location, object Value)[] answers)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach ((string location, object value) in answers) values.Set(location, value);
        return new AnswerSnapshot(revision, values);
    }
}
