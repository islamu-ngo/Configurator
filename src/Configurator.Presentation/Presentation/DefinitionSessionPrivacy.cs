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
        if (IsSecret(schema, inherited)) return true;
        if (schema.Variants.Count > 0)
            return DefinitionValidator.SelectVariant(schema, value) is { } branch
                ? ContainsSecret(branch, value)
                : schema.Variants.Any(variant => ContainsSecret(variant, value));
        if (value.ValueKind == JsonValueKind.Array && schema.Items is { } items)
            return value.EnumerateArray().Any(item => ContainsSecret(items, item));
        return value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any(property =>
            (schema.Properties.TryGetValue(property.Name, out DefinitionNode? child)
                ? child : schema.AdditionalProperties) is { } declared && ContainsSecret(declared, property.Value));
    }

    internal static JsonNode? RemoveSecrets(DefinitionNode schema, JsonNode? node, bool inherited = false)
    {
        if (IsSecret(schema, inherited)) return null;
        if (schema.Variants.Count > 0 && node is not null)
        {
            JsonElement value = JsonSerializer.SerializeToElement(node);
            if (DefinitionValidator.SelectVariant(schema, value) is { } branch)
                return RemoveSecrets(branch, node);
            foreach (DefinitionNode variant in schema.Variants)
                node = RemoveSecrets(variant, node);
            return node;
        }
        if (node is JsonArray rows && schema.Items is { } items)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                JsonNode? current = rows[index];
                JsonNode? retained = RemoveSecrets(items, current);
                if (!ReferenceEquals(retained, current)) rows[index] = retained;
            }
        }
        else if (node is JsonObject mapping)
            foreach (string name in mapping.Select(property => property.Key).ToArray())
                if ((schema.Properties.TryGetValue(name, out DefinitionNode? childSchema)
                    ? childSchema : schema.AdditionalProperties) is { } declared &&
                    (IsSecret(declared) || mapping[name] is not null && RemoveSecrets(declared, mapping[name]) is null))
                    mapping.Remove(name);
        return node;
    }
}
