namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Configurator.Core.Composition;
using static Configurator.Core.Definitions.DefinitionAdmission;

internal static class DefinitionNodeReader
{
    internal static DefinitionNode Read(JsonElement schema, SortedSet<string> capabilities, bool publicAncestors,
        bool secretAncestors, JsonElement schemaRoot, HashSet<string> references, CompositionBudget budget, int depth,
        string schemaPath = "/configuration")
    {
        try
        {
            return ReadNodeCore(schema, capabilities, publicAncestors, secretAncestors, schemaRoot, references,
                budget, depth, schemaPath);
        }
        catch (DefinitionException exception) { throw exception.AtSchemaPath(schemaPath); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or ArgumentException or OverflowException)
        {
            throw new DefinitionException("definition-invalid").AtSchemaPath(schemaPath);
        }
    }

    private static DefinitionNode ReadNodeCore(JsonElement schema, SortedSet<string> capabilities, bool publicAncestors,
        bool secretAncestors, JsonElement schemaRoot, HashSet<string> references, CompositionBudget budget, int depth,
        string schemaPath)
    {
        budget.Node(depth);
        if (schema.TryGetProperty("$defs", out JsonElement definitions))
        {
            if (definitions.ValueKind != JsonValueKind.Object || definitions.EnumerateObject().Count() > 1024)
                throw new DefinitionException("schema-reference");
            foreach (JsonProperty definition in definitions.EnumerateObject())
                _ = Read(definition.Value, capabilities, publicAncestors, secretAncestors,
                    schemaRoot, references, budget, depth + 1,
                    schemaPath + "/$defs/" + definition.Name.Replace("~", "~0", StringComparison.Ordinal)
                        .Replace("/", "~1", StringComparison.Ordinal));
            capabilities.Add("schema.localRef");
        }
        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            Only(schema, "$ref", "title", "description", "readOnly", "writeOnly", "deprecated", "examples", "default", "x-configurator", "$defs");
            string pointer = reference.GetString()!;
            if (!pointer.StartsWith("#/$defs/", StringComparison.Ordinal) || pointer.Contains('%') ||
                references.Count >= 32 || !references.Add(pointer)) throw new DefinitionException("schema-reference");
            try
            {
                JsonElement target = schemaRoot;
                DefinitionPath path = DefinitionPath.Parse(pointer[1..]);
                for (int index = 0; index < path.Segments.Count; index++)
                {
                    if (target.ValueKind == JsonValueKind.Object && target.TryGetProperty(path.Segments[index], out target)) continue;
                    if (target.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int position) &&
                        position < target.GetArrayLength()) { target = target[position]; continue; }
                    throw new DefinitionException("schema-reference");
                }
                JsonObject merged = JsonNode.Parse(target.GetRawText())!.AsObject();
                foreach (JsonProperty annotation in schema.EnumerateObject())
                {
                    if (annotation.Name is "$ref" or "$defs") continue;
                    if (annotation.Name == "writeOnly" && merged["writeOnly"]?.GetValue<bool>() == true) continue;
                    if (annotation.Name == "x-configurator")
                    {
                        JsonObject incoming = JsonNode.Parse(annotation.Value.GetRawText())!.AsObject();
                        JsonObject previous = merged["x-configurator"] as JsonObject ?? new();
                        string oldSensitivity = previous.ContainsKey("secret") || merged["writeOnly"]?.GetValue<bool>() == true
                            ? "secret" : previous["sensitivity"]?.GetValue<string>() ?? "sensitive";
                        string newSensitivity = incoming["sensitivity"]?.GetValue<string>() ?? oldSensitivity;
                        if (incoming.ContainsKey("sensitivity") && incoming["sensitivity"] is null)
                            throw new DefinitionException("sensitivity-invalid");
                        if (incoming.ContainsKey("sensitivity") && newSensitivity is not ("public" or "sensitive" or "secret"))
                            throw new DefinitionException("sensitivity-invalid");
                        if (incoming.ContainsKey("secret") && incoming["sensitivity"]?.GetValue<string>() == "public")
                            throw new DefinitionException("secret-policy-invalid");
                        static int Rank(string value) => value switch { "public" => 0, "sensitive" => 1, "secret" => 2, _ => -1 };
                        if (incoming.ContainsKey("sensitivity") && Rank(newSensitivity) < Rank(oldSensitivity))
                            throw new DefinitionException("sensitivity-downgrade");
                        if (previous.ContainsKey("secret") && incoming.ContainsKey("secret")
                            && !JsonNode.DeepEquals(previous["secret"], incoming["secret"]))
                            throw new DefinitionException("secret-policy-conflict");
                        if (incoming.ContainsKey("secret")) newSensitivity = "secret";
                        foreach ((string name, JsonNode? value) in incoming)
                            if (name != "sensitivity") previous[name] = value?.DeepClone();
                        previous["sensitivity"] = oldSensitivity == "secret" || newSensitivity == "secret" ? "secret"
                            : oldSensitivity == "sensitive" || newSensitivity == "sensitive" ? "sensitive" : "public";
                        merged["x-configurator"] = previous;
                    }
                    else merged[annotation.Name] = JsonNode.Parse(annotation.Value.GetRawText());
                }
                capabilities.Add("schema.localRef");
                using JsonDocument expanded = JsonDocument.Parse(merged.ToJsonString());
                return Read(expanded.RootElement, capabilities, publicAncestors, secretAncestors,
                    schemaRoot, references, budget, depth, "/configuration" + pointer[1..]);
            }
            finally { references.Remove(pointer); }
        }
        Only(schema, "type", "title", "description", "enum", "const", "properties", "required", "additionalProperties",
            "items", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems", "minProperties",
            "maxProperties", "format", "default", "readOnly", "writeOnly", "deprecated", "examples", "x-configurator",
            "if", "then", "else", "dependentRequired", "$defs", "oneOf", "exclusiveMinimum", "exclusiveMaximum", "multipleOf", "uniqueItems");
        JsonElement type = schema.GetProperty("type");
        bool nullable = type.ValueKind == JsonValueKind.Array;
        string spelling;
        if (nullable)
        {
            string?[] types = type.EnumerateArray().Select(item => item.GetString()).ToArray();
            if (types.Length != 2 || types.Count(item => item == "null") != 1) throw new DefinitionException("type-invalid");
            spelling = types.Single(item => item != "null")!;
            capabilities.Add("schema.nullable");
        }
        else spelling = type.GetString()!;
        DefinitionNodeType nodeType = spelling switch
        {
            "string" => DefinitionNodeType.Text,
            "boolean" => DefinitionNodeType.Boolean,
            "integer" => DefinitionNodeType.WholeNumber,
            "number" => DefinitionNodeType.Number,
            "object" => DefinitionNodeType.Mapping,
            "array" => DefinitionNodeType.Sequence,
            _ => throw new DefinitionException("type-unsupported")
        };
        capabilities.Add("schema." + spelling);
        foreach (string name in new[] { "title", "description", "format" })
            if (schema.TryGetProperty(name, out var text)) Text(text, name == "description" ? 4096 : 256);
        foreach (string name in new[] { "readOnly", "writeOnly", "deprecated" })
            if (schema.TryGetProperty(name, out var flag)) _ = flag.GetBoolean();
        bool writeOnly = schema.TryGetProperty("writeOnly", out var write) && write.GetBoolean();
        JsonElement extension = default;
        bool hasExtension = schema.TryGetProperty("x-configurator", out extension);
        string sensitivity = "sensitive";
        if (hasExtension)
        {
            Only(extension, "sensitivity", "initialValue", "suggestedValue", "notBlank", "detail", "guidance", "secret", "choices", "units",
                "uniqueBy", "applicableWhen", "assertFormat");
            if (extension.TryGetProperty("sensitivity", out var declared))
            {
                sensitivity = declared.GetString()!;
                Choice(extension, "sensitivity", DefinitionVocabulary.Sensitivities);
            }
            if (extension.TryGetProperty("secret", out JsonElement secret))
            {
                if (nodeType != DefinitionNodeType.Text || sensitivity == "public")
                    throw new DefinitionException("secret-policy-invalid");
                _ = DefinitionSecretAdmission.Read(secret, schema);
                sensitivity = "secret";
                capabilities.Add("schema.secret");
            }
        }
        bool publicContext = publicAncestors && sensitivity == "public" && !writeOnly;
        bool secretContext = secretAncestors || sensitivity == "secret" || writeOnly;
        var properties = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal);
        var required = new List<string>();
        DefinitionNode? items = null;
        DefinitionNode? map = null;
        var variants = new List<DefinitionNode>();
        if (schema.TryGetProperty("oneOf", out JsonElement alternatives))
        {
            if (nodeType != DefinitionNodeType.Mapping || alternatives.ValueKind != JsonValueKind.Array ||
                alternatives.GetArrayLength() is < 2 or > 16 || schema.TryGetProperty("properties", out _) ||
                schema.TryGetProperty("required", out _) || schema.TryGetProperty("additionalProperties", out _))
                throw new DefinitionException("schema-variant");
            int alternativeIndex = 0;
            foreach (JsonElement alternative in alternatives.EnumerateArray())
                variants.Add(Read(alternative, capabilities, publicContext, secretContext, schemaRoot, references, budget,
                    depth + 1, schemaPath + "/oneOf/" + (alternativeIndex++).ToString(CultureInfo.InvariantCulture)));
            capabilities.Add("schema.variant");
        }
        if (nodeType == DefinitionNodeType.Mapping)
        {
            if (variants.Count == 0 && (!schema.TryGetProperty("additionalProperties", out var additional) ||
                additional.ValueKind is not (JsonValueKind.False or JsonValueKind.Object)))
                throw new DefinitionException("map-semantics-unsupported");
            if (schema.TryGetProperty("additionalProperties", out var typedMap) && typedMap.ValueKind == JsonValueKind.Object)
            {
                map = Read(typedMap, capabilities, publicContext, secretContext, schemaRoot, references, budget,
                    depth + 1, schemaPath + "/additionalProperties");
                capabilities.Add("schema.map");
            }
            if (schema.TryGetProperty("properties", out var children))
                foreach (JsonProperty child in children.EnumerateObject())
                    properties.Add(child.Name, Read(child.Value, capabilities, publicContext, secretContext,
                        schemaRoot, references, budget, depth + 1,
                        schemaPath + "/properties/" + child.Name.Replace("~", "~0", StringComparison.Ordinal)
                            .Replace("/", "~1", StringComparison.Ordinal)));
            if (schema.TryGetProperty("required", out var requiredNames))
            {
                foreach (JsonElement name in requiredNames.EnumerateArray())
                {
                    string key = name.GetString()!;
                    if (!properties.ContainsKey(key) || required.Contains(key, StringComparer.Ordinal))
                        throw new DefinitionException("required-invalid");
                    required.Add(key);
                }
            }
        }
        else if (schema.TryGetProperty("properties", out _) || schema.TryGetProperty("required", out _)
            || schema.TryGetProperty("additionalProperties", out _)) throw new DefinitionException("constraint-context");
        if (schema.TryGetProperty("if", out var guard))
        {
            if (nodeType != DefinitionNodeType.Mapping || !schema.TryGetProperty("then", out var consequence))
                throw new DefinitionException("relationship-invalid");
            Only(guard, "required", "properties");
            if (guard.TryGetProperty("required", out var presentNames)) Names(presentNames);
            if (guard.TryGetProperty("properties", out var guards))
            {
                if (guards.ValueKind != JsonValueKind.Object) throw new DefinitionException("relationship-invalid");
                foreach (JsonProperty property in guards.EnumerateObject())
                {
                    if (!properties.ContainsKey(property.Name)) throw new DefinitionException("relationship-invalid");
                    Only(property.Value, "const", "enum");
                    if (property.Value.TryGetProperty("const", out var expected))
                    {
                        if (!DefinitionValidator.ValueValid(properties[property.Name], expected))
                            throw new DefinitionException("condition-type");
                    }
                    else if (property.Value.TryGetProperty("enum", out var guardChoices) && guardChoices.ValueKind == JsonValueKind.Array
                        && guardChoices.GetArrayLength() > 0)
                    {
                        foreach (JsonElement choice in guardChoices.EnumerateArray())
                            if (!DefinitionValidator.ValueValid(properties[property.Name], choice))
                                throw new DefinitionException("condition-type");
                    }
                    else throw new DefinitionException("relationship-invalid");
                }
            }
            if (!guard.EnumerateObject().Any()) throw new DefinitionException("relationship-invalid");
            RequiredSet(consequence);
            if (schema.TryGetProperty("else", out var alternative)) RequiredSet(alternative);
            capabilities.Add("relationship.presence");
        }
        else if (schema.TryGetProperty("then", out _) || schema.TryGetProperty("else", out _))
            throw new DefinitionException("relationship-invalid");
        if (schema.TryGetProperty("dependentRequired", out var dependencies))
        {
            if (nodeType != DefinitionNodeType.Mapping || dependencies.ValueKind != JsonValueKind.Object)
                throw new DefinitionException("relationship-invalid");
            foreach (JsonProperty dependency in dependencies.EnumerateObject())
            {
                if (!properties.ContainsKey(dependency.Name)) throw new DefinitionException("relationship-invalid");
                Names(dependency.Value);
            }
            capabilities.Add("relationship.presence");
        }
        void RequiredSet(JsonElement assertion)
        {
            Only(assertion, "required");
            Names(assertion.GetProperty("required"));
        }
        void Names(JsonElement names)
        {
            if (names.ValueKind != JsonValueKind.Array || names.GetArrayLength() is < 1 or > 1024)
                throw new DefinitionException("relationship-invalid");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement name in names.EnumerateArray())
                if (!properties.ContainsKey(name.GetString()!) || !seen.Add(name.GetString()!))
                    throw new DefinitionException("relationship-invalid");
        }
        if (nodeType == DefinitionNodeType.Sequence) items = Read(schema.GetProperty("items"), capabilities, publicContext, secretContext,
            schemaRoot, references, budget, depth + 1, schemaPath + "/items");
        else if (schema.TryGetProperty("items", out _)) throw new DefinitionException("constraint-context");
        foreach ((string low, string high, DefinitionNodeType owner) in new[]
        {
            ("minLength", "maxLength", DefinitionNodeType.Text),
            ("minItems", "maxItems", DefinitionNodeType.Sequence),
            ("minProperties", "maxProperties", DefinitionNodeType.Mapping),
            ("minimum", "maximum", DefinitionNodeType.Number),
            ("exclusiveMinimum", "exclusiveMaximum", DefinitionNodeType.Number)
        })
        {
            decimal? lower = Bound(low);
            decimal? upper = Bound(high);
            if (lower > upper) throw new DefinitionException("constraint-invalid");
            decimal? Bound(string name)
            {
                if (!schema.TryGetProperty(name, out var value)) return null;
                if (owner == DefinitionNodeType.Number ? nodeType is not (DefinitionNodeType.Number or DefinitionNodeType.WholeNumber) : nodeType != owner)
                    throw new DefinitionException("constraint-context");
                decimal number = value.GetDecimal();
                if (owner != DefinitionNodeType.Number && (number < 0 || number != decimal.Truncate(number) || number > int.MaxValue))
                    throw new DefinitionException("constraint-invalid");
                capabilities.Add("schema.bounds");
                return number;
            }
        }
        if (schema.TryGetProperty("multipleOf", out JsonElement multiple))
        {
            if (nodeType is not (DefinitionNodeType.Number or DefinitionNodeType.WholeNumber) || multiple.GetDecimal() <= 0)
                throw new DefinitionException("constraint-context");
            capabilities.Add("schema.multipleOf");
        }
        decimal? lowerBound = null, upperBound = null;
        bool lowerExclusive = false, upperExclusive = false;
        foreach (string name in new[] { "minimum", "exclusiveMinimum", "maximum", "exclusiveMaximum" })
        {
            if (!schema.TryGetProperty(name, out JsonElement bound)) continue;
            decimal number = bound.GetDecimal();
            if (name is "minimum" or "exclusiveMinimum")
            {
                if (!lowerBound.HasValue || number > lowerBound.Value)
                {
                    lowerBound = number;
                    lowerExclusive = name == "exclusiveMinimum";
                }
                else if (number == lowerBound.Value) lowerExclusive |= name == "exclusiveMinimum";
            }
            else if (!upperBound.HasValue || number < upperBound.Value)
            {
                upperBound = number;
                upperExclusive = name == "exclusiveMaximum";
            }
            else if (number == upperBound.Value) upperExclusive |= name == "exclusiveMaximum";
        }
        if (lowerBound > upperBound || lowerBound.HasValue && lowerBound == upperBound && (lowerExclusive || upperExclusive))
            throw new DefinitionException("constraint-invalid");
        if (schema.TryGetProperty("uniqueItems", out JsonElement uniqueItems))
        {
            if (nodeType != DefinitionNodeType.Sequence) throw new DefinitionException("constraint-context");
            if (uniqueItems.GetBoolean()) capabilities.Add("schema.uniqueItems");
        }
        var keywords = new JsonObject();
        foreach (JsonProperty property in schema.EnumerateObject())
            if (property.Name is not ("properties" or "required" or "items" or "additionalProperties" or "$defs" or "oneOf"))
                keywords.Add(property.Name, JsonNode.Parse(property.Value.GetRawText()));
        var node = new DefinitionNode(nodeType, nullable, properties, required, items, map, variants,
            keywords: JsonSerializer.SerializeToElement(keywords));
        ChargeKeywords(node.Keywords, depth);
        void ChargeKeywords(JsonElement value, int level)
        {
            budget.Node(level);
            if (value.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    budget.Scalar(property.Name);
                    ChargeKeywords(property.Value, level + 1);
                }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement child in value.EnumerateArray()) ChargeKeywords(child, level + 1);
            else if (value.ValueKind == JsonValueKind.String) budget.Scalar(value.GetString()!);
        }
        if (schema.TryGetProperty("format", out _) && nodeType != DefinitionNodeType.Text)
            throw new DefinitionException("constraint-context");
        if (schema.TryGetProperty("enum", out var choices))
        {
            if (nodeType is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence || choices.GetArrayLength() is < 1 or > 256)
                throw new DefinitionException("enum-invalid");
            var values = new HashSet<string>(StringComparer.Ordinal);
            JsonValueKind? kind = null;
            foreach (JsonElement value in choices.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.Null)
                {
                    if (!nullable || !values.Add("null")) throw new DefinitionException("enum-invalid");
                    continue;
                }
                JsonValueKind scalarKind = value.ValueKind == JsonValueKind.False ? JsonValueKind.True : value.ValueKind;
                if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null
                    || kind.HasValue && kind != scalarKind || !values.Add(value.GetRawText())
                    || !DefinitionValidator.ValueValid(node, value)) throw new DefinitionException("enum-invalid");
                kind = scalarKind;
            }
            capabilities.Add("schema.enum");
        }
        if (schema.TryGetProperty("const", out var constant))
        {
            if (constant.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                || !DefinitionValidator.ValueValid(node, constant)) throw new DefinitionException("const-invalid");
            capabilities.Add("schema.const");
        }
        if (hasExtension)
        {
            if (extension.TryGetProperty("assertFormat", out JsonElement asserted))
            {
                bool enabled = asserted.GetBoolean();
                if (nodeType != DefinitionNodeType.Text || enabled && (!schema.TryGetProperty("format", out JsonElement format) ||
                    format.GetString() is not ("hostname" or "email-address" or "uri" or "http-url")))
                    throw new DefinitionException("format-unsupported");
                if (enabled) capabilities.Add("schema.assertFormat");
            }
            if (extension.TryGetProperty("uniqueBy", out var uniqueBy))
            {
                if (nodeType != DefinitionNodeType.Sequence || items?.Type != DefinitionNodeType.Mapping)
                    throw new DefinitionException("unique-key-invalid");
                DefinitionPath path = DefinitionPath.Parse(uniqueBy.GetString()!);
                if (path.Segments.Count == 0) throw new DefinitionException("unique-key-invalid");
                DefinitionNode current = items;
                foreach (string name in path.Segments)
                    if (current.Type != DefinitionNodeType.Mapping
                        || !current.Required.Contains(name, StringComparer.Ordinal)
                        || !current.Properties.TryGetValue(name, out current!))
                        throw new DefinitionException("unique-key-invalid");
                if (current.Type != DefinitionNodeType.Text || current.IsNullable)
                    throw new DefinitionException("unique-key-invalid");
                capabilities.Add("schema.uniqueBy");
            }
            if (extension.TryGetProperty("notBlank", out var notBlank))
            {
                if (nodeType != DefinitionNodeType.Text) throw new DefinitionException("constraint-context");
                if (notBlank.GetBoolean()) capabilities.Add("schema.notBlank");
            }
            if (extension.TryGetProperty("detail", out _)) Choice(extension, "detail", "basic", "standard", "advanced");
            if (extension.TryGetProperty("units", out var units)) Text(units, 128);
            if (extension.TryGetProperty("guidance", out var guidance)) ValidateGuidance(guidance);
            if (extension.TryGetProperty("choices", out var labels))
            {
                if (!schema.TryGetProperty("enum", out var enumValues) || labels.GetArrayLength() != enumValues.GetArrayLength())
                    throw new DefinitionException("choices-invalid");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement label in labels.EnumerateArray())
                {
                    Only(label, "value", "label", "explanation");
                    string value = label.GetProperty("value").GetRawText();
                    if (!seen.Add(value) || !enumValues.EnumerateArray().Any(item => item.GetRawText() == value))
                        throw new DefinitionException("choices-invalid");
                    Text(label.GetProperty("label"), 256);
                    if (label.TryGetProperty("explanation", out var explanation)) Text(explanation, 2048);
                }
            }
        }
        bool protectedTree = !publicContext || properties.Values.Concat(variants).Any(Protected) ||
            items is not null && Protected(items) || map is not null && Protected(map);
        foreach (string name in new[] { "default", "const", "examples" })
            if (schema.TryGetProperty(name, out var value))
            {
                if (protectedTree) throw new DefinitionException("public-value-protected");
                if (name == "examples")
                {
                    if (value.GetArrayLength() > 256 || value.EnumerateArray().Any(item => !DefinitionValidator.ValueValid(node, item)))
                        throw new DefinitionException("public-value-invalid");
                }
                else if (!DefinitionValidator.ValueValid(node, value)) throw new DefinitionException("public-value-invalid");
            }
        foreach (string name in new[] { "initialValue", "suggestedValue" })
            if (hasExtension && extension.TryGetProperty(name, out var value))
            {
                if (protectedTree || !DefinitionValidator.ValueValid(node, value)) throw new DefinitionException("public-value-invalid");
                capabilities.Add(name == "initialValue" ? "values.initial" : "values.suggested");
            }
        return node;
    }

    private static bool Protected(DefinitionNode node) =>
        node.Keywords.TryGetProperty("writeOnly", out var write) && write.GetBoolean()
        || !node.Keywords.TryGetProperty("x-configurator", out var extension)
        || !extension.TryGetProperty("sensitivity", out var sensitivity) || sensitivity.GetString() != "public"
        || node.Properties.Values.Concat(node.Variants).Any(Protected) || node.Items is not null && Protected(node.Items)
        || node.AdditionalProperties is not null && Protected(node.AdditionalProperties);

}
