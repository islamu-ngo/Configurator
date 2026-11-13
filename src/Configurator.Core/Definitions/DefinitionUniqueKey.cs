namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>
/// Reads <c>x-configurator.uniqueBy</c>: one item-relative JSON Pointer, or an array of up to eight pointers
/// forming a composite row key. Rows conflict only when every component matches.
/// </summary>
internal static class DefinitionUniqueKey
{
    internal const int MaximumComponents = 8;

    internal static IReadOnlyList<DefinitionPath> Paths(JsonElement uniqueBy)
    {
        IEnumerable<JsonElement> pointers = uniqueBy.ValueKind switch
        {
            JsonValueKind.String => [uniqueBy],
            JsonValueKind.Array when uniqueBy.GetArrayLength() is >= 1 and <= MaximumComponents => uniqueBy.EnumerateArray(),
            _ => throw new DefinitionException("unique-key-invalid")
        };
        var paths = new List<DefinitionPath>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement pointer in pointers)
        {
            if (pointer.ValueKind != JsonValueKind.String || !seen.Add(pointer.GetString()!))
                throw new DefinitionException("unique-key-invalid");
            DefinitionPath path = DefinitionPath.Parse(pointer.GetString()!);
            if (path.Segments.Count == 0) throw new DefinitionException("unique-key-invalid");
            paths.Add(path);
        }
        return paths.AsReadOnly();
    }

    /// <summary>Returns the declared key paths of an admitted sequence node, or none.</summary>
    internal static IReadOnlyList<DefinitionPath> Of(DefinitionNode schema) =>
        schema.Keywords.TryGetProperty("x-configurator", out JsonElement metadata)
        && metadata.TryGetProperty("uniqueBy", out JsonElement uniqueBy) ? Paths(uniqueBy) : Array.Empty<DefinitionPath>();
}
