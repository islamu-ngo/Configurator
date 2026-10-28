namespace Configurator.Presentation;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

/// <summary>Secret custody is explicitly enabled for each browser workspace; native forms remain complete.</summary>
public enum DefinitionSessionPrivacy
{
    NativeComplete,
    BrowserNoSecrets,
    BrowserSecretsEnabled
}

internal static class DefinitionSecretPolicy
{
    internal static bool IsSecret(DefinitionNode schema, bool inherited = false) => inherited ||
        schema.Keywords.TryGetProperty("writeOnly", out JsonElement writeOnly) && writeOnly.ValueKind == JsonValueKind.True ||
        schema.Keywords.TryGetProperty("x-configurator", out JsonElement metadata) &&
        metadata.TryGetProperty("sensitivity", out JsonElement sensitivity) && sensitivity.GetString() == "secret";

    internal static bool ContainsSecret(DefinitionNode schema, JsonElement value, bool inherited = false)
    {
        if (IsSecret(schema, inherited))
            return value.ValueKind != JsonValueKind.Null;
        if (value.ValueKind == JsonValueKind.Array && schema.Items is { } items)
            return value.EnumerateArray().Any(item => ContainsSecret(items, item));
        return value.ValueKind == JsonValueKind.Object && schema.Properties.Any(property =>
            value.TryGetProperty(property.Key, out JsonElement child) && ContainsSecret(property.Value, child));
    }

    internal static JsonNode? RemoveSecrets(DefinitionNode schema, JsonNode? node, bool inherited = false)
    {
        if (IsSecret(schema, inherited)) return null;
        if (node is JsonArray rows && schema.Items is { } items)
        {
            for (int index = 0; index < rows.Count; index++)
                rows[index] = RemoveSecrets(items, rows[index]);
        }
        else if (node is JsonObject mapping)
            foreach ((string name, DefinitionNode childSchema) in schema.Properties)
                if (mapping.TryGetPropertyValue(name, out JsonNode? child) &&
                    RemoveSecrets(childSchema, child) is null) mapping.Remove(name);
        return node;
    }
}
