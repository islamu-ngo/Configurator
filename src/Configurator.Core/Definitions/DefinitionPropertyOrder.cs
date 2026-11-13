namespace Configurator.Core.Definitions;

using System.Text.Json;

/// <summary>
/// Resolves authored object property display order. Canonical JSON still sorts keys ordinally, so order is
/// carried as data (<c>x-configurator.propertyOrder</c> and per-property <c>x-configurator.order</c>) and
/// never changes canonical bytes or digests by itself.
/// </summary>
internal static class DefinitionPropertyOrder
{
    /// <summary>Placeholder in <c>propertyOrder</c> for every property not listed explicitly.</summary>
    internal const string Rest = "*";

    internal static IReadOnlyList<string> Resolve(JsonElement keywords,
        IReadOnlyDictionary<string, DefinitionNode> properties)
    {
        string[] rest = properties
            .Select(property => (property.Key, Order: Rank(property.Value.Keywords)))
            .OrderBy(property => property.Order.HasValue ? 0 : 1)
            .ThenBy(property => property.Order ?? 0)
            .ThenBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => property.Key).ToArray();
        if (!keywords.TryGetProperty("x-configurator", out JsonElement extension)
            || extension.ValueKind != JsonValueKind.Object
            || !extension.TryGetProperty("propertyOrder", out JsonElement declared))
            return Array.AsReadOnly(rest);
        if (declared.ValueKind != JsonValueKind.Array || declared.GetArrayLength() == 0
            || declared.GetArrayLength() > properties.Count + 1)
            throw new DefinitionException("property-order-invalid");
        var listed = new List<string>(declared.GetArrayLength());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement entry in declared.EnumerateArray())
        {
            string? name = entry.ValueKind == JsonValueKind.String ? entry.GetString() : null;
            if (name is null || !seen.Add(name) || name != Rest && !properties.ContainsKey(name))
                throw new DefinitionException("property-order-invalid");
            listed.Add(name);
        }
        string[] remaining = rest.Where(name => !seen.Contains(name)).ToArray();
        var ordered = new List<string>(properties.Count);
        foreach (string name in listed)
            if (name == Rest) ordered.AddRange(remaining);
            else ordered.Add(name);
        if (!seen.Contains(Rest)) ordered.AddRange(remaining);
        return ordered.AsReadOnly();
    }

    /// <summary>Reads the optional per-property rank; admission rejects non-integer ranks before this point.</summary>
    internal static int? Rank(JsonElement keywords) =>
        keywords.TryGetProperty("x-configurator", out JsonElement extension)
        && extension.ValueKind == JsonValueKind.Object
        && extension.TryGetProperty("order", out JsonElement order)
        && order.ValueKind == JsonValueKind.Number && order.TryGetInt32(out int rank) ? rank : null;
}
