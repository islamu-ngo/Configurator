namespace Configurator.Core.Definitions.Authoring;

using System.Text.Json.Nodes;

/// <summary>
/// Lowers authoring-only schema conveniences into canonical runtime keywords before admission:
/// single-source <c>x-configurator.choices</c> synthesize <c>enum</c>, and authored mapping order becomes
/// explicit <c>x-configurator.propertyOrder</c> because canonical JSON sorts object keys.
/// </summary>
internal static class DefinitionSchemaLowering
{
    private const string Extension = "x-configurator";

    internal static void Lower(JsonObject schema, string schemaPath, DefinitionSourceMap sourceMap)
    {
        RequireAssertedFormat(schema);
        SynthesizeEnum(schema);
        InferFixedSecretSize(schema);
        if (schema["properties"] is JsonObject properties)
        {
            PreserveAuthoredOrder(schema, properties, schemaPath, sourceMap);
            foreach ((string name, JsonNode? child) in properties)
                if (child is JsonObject property) Lower(property, schemaPath + "/properties/" + Escape(name), sourceMap);
        }
        if (schema["$defs"] is JsonObject definitions)
            foreach ((string name, JsonNode? child) in definitions)
                if (child is JsonObject definition) Lower(definition, schemaPath + "/$defs/" + Escape(name), sourceMap);
        foreach (string member in new[] { "items", "additionalProperties" })
            if (schema[member] is JsonObject nested) Lower(nested, schemaPath + "/" + member, sourceMap);
        if (schema["oneOf"] is JsonArray variants)
            for (int index = 0; index < variants.Count; index++)
                if (variants[index] is JsonObject variant)
                    Lower(variant, schemaPath + "/oneOf/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), sourceMap);
    }

    private static void SynthesizeEnum(JsonObject schema)
    {
        if (schema.ContainsKey("enum") || schema[Extension] is not JsonObject metadata
            || metadata["choices"] is not JsonArray choices) return;
        if (!IsScalar(schema["type"]) || choices.Count is < 1 or > 256) throw new DefinitionException("choices-invalid");
        var values = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonNode? choice in choices)
        {
            if (choice is not JsonObject entry || !entry.TryGetPropertyValue("value", out JsonNode? value)
                || !seen.Add(value?.ToJsonString() ?? "null"))
                throw new DefinitionException("choices-invalid");
            values.Add(value?.DeepClone());
        }
        schema["enum"] = values;
    }

    /// <summary>
    /// New authored content may only use asserted formats. Runtime admission still treats unknown tokens as
    /// JSON Schema annotations so immutable historical releases remain admissible.
    /// </summary>
    private static void RequireAssertedFormat(JsonObject schema)
    {
        if (schema["format"] is JsonValue format && format.TryGetValue(out string? token)
            && !DefinitionFormatValidator.IsKnown(token))
            throw new DefinitionException("format-unsupported");
    }

    /// <summary>
    /// Generated fixed-size secrets imply their entered size: byte-sized tokens and keys pin <c>minBytes</c>,
    /// <c>maxBytes</c> and the encoded length, while generated passwords set a minimum length. Explicit values win.
    /// </summary>
    private static void InferFixedSecretSize(JsonObject schema)
    {
        if (schema[Extension]?["secret"] is not JsonObject secret || secret["generation"] is not JsonObject generation
            || secret["kind"] is not JsonValue kindValue || !kindValue.TryGetValue(out string? kind)) return;
        if (kind == "password" && generation["length"] is JsonValue length && length.TryGetValue(out int characters))
        {
            if (!schema.ContainsKey("minLength")) schema["minLength"] = characters;
            return;
        }
        if (kind is not ("opaque-token" or "symmetric-key") || generation["bytes"] is not JsonValue bytesValue
            || !bytesValue.TryGetValue(out int bytes) || secret["encoding"] is not JsonValue encodingValue
            || !encodingValue.TryGetValue(out string? encoding)) return;
        if (secret["constraints"] is not JsonObject constraints) secret["constraints"] = constraints = new JsonObject();
        if (!constraints.ContainsKey("minBytes")) constraints["minBytes"] = bytes;
        if (!constraints.ContainsKey("maxBytes")) constraints["maxBytes"] = bytes;
        int encoded = encoding switch
        {
            "hex" => bytes * 2,
            "base64" => 4 * ((bytes + 2) / 3),
            "base64url" => (bytes * 8 + 5) / 6,
            _ => -1
        };
        if (encoded < 0) return;
        if (!schema.ContainsKey("minLength")) schema["minLength"] = encoded;
        if (!schema.ContainsKey("maxLength")) schema["maxLength"] = encoded;
    }

    private static bool IsScalar(JsonNode? type) => type switch
    {
        JsonValue value when value.TryGetValue(out string? name) => name is "string" or "integer" or "number" or "boolean",
        JsonArray union => union.All(item => item is JsonValue value && value.TryGetValue(out string? name)
            && name is "string" or "integer" or "number" or "boolean" or "null"),
        _ => false
    };

    /// <summary>Records source mapping order only when no explicit order exists and it differs from ordinal order.</summary>
    private static void PreserveAuthoredOrder(JsonObject schema, JsonObject properties, string schemaPath,
        DefinitionSourceMap sourceMap)
    {
        if (properties.Count < 2 || schema[Extension] is JsonObject declared && declared.ContainsKey("propertyOrder")
            || properties.Any(property => property.Value?[Extension]?["order"] is not null))
            return;
        var positioned = new List<(string Name, DefinitionSourceSpan Span)>(properties.Count);
        foreach ((string name, _) in properties)
        {
            if (!sourceMap.TryGetSpan(schemaPath + "/properties/" + Escape(name), out DefinitionSourceSpan span)) return;
            positioned.Add((name, span));
        }
        string[] authored = positioned.OrderBy(entry => entry.Span.DocumentIndex).ThenBy(entry => entry.Span.StartLine)
            .ThenBy(entry => entry.Span.StartColumn).Select(entry => entry.Name).ToArray();
        if (authored.SequenceEqual(authored.Order(StringComparer.Ordinal), StringComparer.Ordinal)) return;
        if (schema[Extension] is not JsonObject metadata) schema[Extension] = metadata = new JsonObject();
        metadata["propertyOrder"] = new JsonArray(authored.Select(name => (JsonNode?)JsonValue.Create(name)).ToArray());
    }

    private static string Escape(string name) =>
        name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
