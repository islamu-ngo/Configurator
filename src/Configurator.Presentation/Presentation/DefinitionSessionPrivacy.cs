namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Secret custody is explicitly enabled for each browser workspace; native forms remain complete.</summary>
public enum DefinitionSessionPrivacy
{
    NativeComplete,
    BrowserNoSecrets,
    BrowserSecretsEnabled
}

internal static class DefinitionSecretPolicy
{
    internal static bool IsSecret(JsonElement metadata, bool inherited = false) => inherited ||
        metadata.TryGetProperty("sensitivity", out JsonElement sensitivity) && sensitivity.GetString() == "secret";

    internal static bool ContainsSecret(JsonElement metadata, JsonElement value, bool inherited = false)
    {
        if (IsSecret(metadata, inherited))
            return value.ValueKind != JsonValueKind.Null;
        if (!metadata.TryGetProperty("properties", out JsonElement properties)) return false;
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Any(item => ContainsProperties(properties, item));
        return ContainsProperties(properties, value);
    }

    private static bool ContainsProperties(JsonElement properties, JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && properties.EnumerateArray().Any(property =>
            value.TryGetProperty(property.GetProperty("id").GetString()!, out JsonElement child) &&
            ContainsSecret(property, child));

    internal static JsonNode? RemoveSecrets(JsonElement metadata, JsonNode? node, bool inherited = false)
    {
        if (IsSecret(metadata, inherited)) return null;
        if (!metadata.TryGetProperty("properties", out JsonElement properties)) return node;
        if (node is JsonArray rows)
        {
            foreach (JsonNode? row in rows) RemoveProperties(properties, row);
        }
        else RemoveProperties(properties, node);
        return node;
    }

    private static void RemoveProperties(JsonElement properties, JsonNode? node)
    {
        if (node is not JsonObject mapping) return;
        foreach (JsonElement property in properties.EnumerateArray())
        {
            string id = property.GetProperty("id").GetString()!;
            if (!mapping.TryGetPropertyValue(id, out JsonNode? child)) continue;
            JsonNode? retained = RemoveSecrets(property, child);
            if (retained is null) mapping.Remove(id);
        }
    }
}
