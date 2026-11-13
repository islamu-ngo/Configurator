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
    /// <summary>Whether the schema or any static descendant, including every variant branch, is non-public.</summary>
    internal static bool IsProtected(DefinitionNode schema) =>
        DefinitionSensitivityPolicy.SensitivityOf(schema) != FieldSensitivity.Public ||
        schema.Properties.Values.Concat(schema.Variants)
            .Concat(schema.Items is null ? [] : new[] { schema.Items })
            .Concat(schema.AdditionalProperties is null ? [] : new[] { schema.AdditionalProperties })
            .Any(IsProtected);

    /// <summary>Whether the node itself declares a non-public sensitivity; undeclared nodes default to sensitive.</summary>
    internal static bool DeclaresProtected(DefinitionNode schema) =>
        DefinitionSensitivityPolicy.SensitivityOf(schema) != FieldSensitivity.Public;

    /// <summary>
    /// Prunes every declared non-public subtree of a public value in place and keeps public members.
    /// Returns <see langword="true"/> when the whole value must be removed instead: the node is declared non-public,
    /// its shape does not match a protected schema, a selected branch or list row is protected as a whole, or no
    /// branch matches while any branch is protected. Whole-list removal keeps row positions from shifting.
    /// </summary>
    internal static bool RemoveProtected(DefinitionNode schema, JsonNode? node)
    {
        if (DeclaresProtected(schema)) return true;
        if (node is null) return false;
        if (schema.Variants.Count > 0)
            return DefinitionValidator.SelectVariant(schema, JsonSerializer.SerializeToElement(node)) is { } branch
                ? RemoveProtected(branch, node) : IsProtected(schema);
        switch (node)
        {
            case JsonArray rows when schema.Items is { } items:
                foreach (JsonNode? row in rows)
                    if (RemoveProtected(items, row)) return true;
                return false;
            case JsonObject mapping when schema.Type == DefinitionNodeType.Mapping:
                foreach (string name in mapping.Select(property => property.Key).ToArray())
                    if ((schema.Properties.GetValueOrDefault(name) ?? schema.AdditionalProperties) is not { } declared ||
                        RemoveProtected(declared, mapping[name]))
                        mapping.Remove(name);
                return false;
            case JsonArray or JsonObject:
                return IsProtected(schema);
            default:
                return schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence && IsProtected(schema);
        }
    }

    internal static bool IsSecret(DefinitionNode schema, bool inherited = false) => inherited ||
        DefinitionSensitivityPolicy.SensitivityOf(schema) == FieldSensitivity.Secret;

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
