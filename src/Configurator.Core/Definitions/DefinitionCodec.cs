namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Configurator.Core.Composition;

public static class DefinitionCodec
{
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = CompositionLimits.DefaultNestingDepth,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static Definition ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static Definition ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    private static Definition Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        DefinitionSourceMap sourceMap = DefinitionSourceMap.Empty;
        try
        {
            byte[] canonical = yaml ? DefinitionProfile.CanonicalizeYaml(bytes, out sourceMap)
                : DefinitionProfile.CanonicalizeJson(bytes, out sourceMap);
            using JsonDocument parsed = JsonDocument.Parse(canonical);
            var capabilities = new SortedSet<string>(StringComparer.Ordinal);
            DefinitionNode configuration = Admit(parsed.RootElement, capabilities);
            JsonElement declared = parsed.RootElement.GetProperty("requiredCapabilities");
            if (declared.ValueKind != JsonValueKind.Array
                || !declared.EnumerateArray().Select(item => item.GetString()).SequenceEqual(capabilities, StringComparer.Ordinal))
                throw new DefinitionException("capabilities-invalid");
            return new Definition(parsed.RootElement, canonical, configuration, sourceMap);
        }
        catch (DefinitionException exception) { throw exception.WithLocation(sourceMap, ""); }
        catch (CompositionException) { throw new DefinitionException("definition-limit"); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or ArgumentException or OverflowException)
        {
            throw new DefinitionException("definition-invalid");
        }
    }

    /// <summary>Fills computed profile/capabilities only, then uses public runtime admission.</summary>
    internal static Definition AdmitCompiled(JsonObject runtime)
    {
        runtime["profile"] = DefinitionProfile.Id;
        runtime["requiredCapabilities"] = new JsonArray();
        byte[] bytes = DefinitionProfile.CanonicalizeJson(JsonSerializer.SerializeToUtf8Bytes(runtime));
        using JsonDocument document = JsonDocument.Parse(bytes);
        var capabilities = new SortedSet<string>(StringComparer.Ordinal);
        _ = Admit(document.RootElement, capabilities);
        runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        return ParseJson(JsonSerializer.SerializeToUtf8Bytes(runtime));
    }

    private static DefinitionNode Admit(JsonElement document, SortedSet<string> capabilities)
    {
        Only(document, "schemaVersion", "profile", "familyId", "revision", "project", "applicationVersions",
            "entryPoints", "locales", "configuration", "artifacts", "requiredCapabilities", "presentation", "localization", "annotations");
        if (document.GetProperty("schemaVersion").ValueKind != JsonValueKind.String
            || document.GetProperty("schemaVersion").GetString() != DefinitionProfile.RuntimeSchemaVersion
            || document.GetProperty("profile").GetString() != DefinitionProfile.Id)
            throw new DefinitionException("definition-version");
        if (!Guid.TryParseExact(document.GetProperty("familyId").GetString(), "D", out Guid family)
            || family == Guid.Empty || document.GetProperty("revision").GetInt64() < 1)
            throw new DefinitionException("identity-invalid");
        JsonElement project = document.GetProperty("project");
        Only(project, "id", "name", "publisher", "description", "repository", "definitionLicense");
        foreach (string name in new[] { "id", "name", "publisher" }) Text(project.GetProperty(name), 256);
        foreach (string name in new[] { "description", "definitionLicense" })
            if (project.TryGetProperty(name, out var text)) Text(text, name == "description" ? 4096 : 256);
        if (project.TryGetProperty("repository", out var repository)) PublicUrl(repository, fragment: false);
        foreach (string name in new[] { "applicationVersions", "entryPoints", "locales" })
            Strings(document.GetProperty(name), 128, 128, nonempty: true);
        JsonElement configurationSource = document.GetProperty("configuration");
        DefinitionNode configuration = ReadNode(configurationSource, capabilities, true, false,
            configurationSource, new(StringComparer.Ordinal), new(CompositionLimits.Default), 1);
        if (configuration.Type != DefinitionNodeType.Mapping || configuration.IsNullable)
            throw new DefinitionException("configuration-object-required");
        if (configuration.Properties.Count > 1024) throw new DefinitionException("document-limit");
        ValidateApplicability(configuration, capabilities);
        if (document.TryGetProperty("annotations", out var annotations) && annotations.ValueKind != JsonValueKind.Object)
            throw new DefinitionException("annotations-invalid");
        ValidateLocalization(document);
        JsonElement artifacts = document.GetProperty("artifacts");
        if (artifacts.GetArrayLength() is < 1 or > 64) throw new DefinitionException("artifact-limit");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            Only(artifact, "id", "fileName", "format", "targetUpdateMode", "restartRequirement", "projection");
            string id = Text(artifact.GetProperty("id"), 128);
            if (!char.IsAsciiLetter(id[0]) || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                || !ids.Add(id)) throw new DefinitionException("artifact-identifier");
            Choice(artifact, "format", DefinitionVocabulary.Formats);
            Choice(artifact, "targetUpdateMode", DefinitionVocabulary.TargetUpdateModes);
            Choice(artifact, "restartRequirement", DefinitionVocabulary.RestartRequirements);
            string file = Text(artifact.GetProperty("fileName"), 128);
            string stem = file.Split('.')[0];
            if (file is "." or ".." || file[^1] is '.' or ' '
                || !file.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                || stem.ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL"
                || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && char.IsAsciiDigit(stem[3])
                || !files.Add(file)) throw new DefinitionException("filename-invalid");
            ValidateProjection(artifact.GetProperty("projection"), configuration, null, capabilities, 0);
            if (artifact.GetProperty("format").GetString() == "dotenv")
                ValidateDotenv(artifact.GetProperty("projection"), configuration);
            if (artifact.GetProperty("format").GetString() == "markdown")
                ValidateMarkdown(artifact.GetProperty("projection"), configuration);
        }
        if (document.TryGetProperty("presentation", out var presentation))
            ValidatePresentation(presentation, configuration, artifacts);
        return configuration;

    }

    private static DefinitionNode ReadNode(JsonElement schema, SortedSet<string> capabilities, bool publicAncestors,
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
                _ = ReadNode(definition.Value, capabilities, publicAncestors, secretAncestors,
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
                return ReadNode(expanded.RootElement, capabilities, publicAncestors, secretAncestors,
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
                variants.Add(ReadNode(alternative, capabilities, publicContext, secretContext, schemaRoot, references, budget,
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
                map = ReadNode(typedMap, capabilities, publicContext, secretContext, schemaRoot, references, budget,
                    depth + 1, schemaPath + "/additionalProperties");
                capabilities.Add("schema.map");
            }
            if (schema.TryGetProperty("properties", out var children))
                foreach (JsonProperty child in children.EnumerateObject())
                    properties.Add(child.Name, ReadNode(child.Value, capabilities, publicContext, secretContext,
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
        if (nodeType == DefinitionNodeType.Sequence) items = ReadNode(schema.GetProperty("items"), capabilities, publicContext, secretContext,
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

    private static void ValidateApplicability(DefinitionNode root, SortedSet<string> capabilities)
    {
        if (root.Keywords.TryGetProperty("x-configurator", out var rootMetadata)
            && rootMetadata.TryGetProperty("applicableWhen", out _)) throw new DefinitionException("condition-context");
        if (root.Variants.Count > 0)
        {
            foreach (DefinitionNode branch in root.Variants) ValidateApplicability(branch, capabilities);
            return;
        }
        ValidateScope(root, null);

        void ValidateScope(DefinitionNode scope, DefinitionNode? item)
        {
            foreach (DefinitionNode branch in scope.Variants)
                ValidateScope(branch, ReferenceEquals(scope, item) ? branch : item);
            var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach ((string name, DefinitionNode node) in scope.Properties)
            {
                string[] references = [];
                if (node.Keywords.TryGetProperty("x-configurator", out var metadata)
                    && metadata.TryGetProperty("applicableWhen", out var condition))
                    ValidateCondition(condition, root, capabilities, item);
                if (ReferenceEquals(scope, root) || ReferenceEquals(scope, item))
                    references = DefinitionValidator.SchemaReferences(node, item is null ? "root" : "item")
                        .Where(scope.Properties.ContainsKey).ToArray();
                dependencies.Add(name, references);
                ValidateScope(node, item);
            }
            if (scope.Items is { } row) ValidateScope(row, row);
            if (scope.AdditionalProperties is { } map) ValidateScope(map, item);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in scope.Properties.Keys) Visit(name);

            void Visit(string name)
            {
                if (visited.Contains(name)) return;
                if (!visiting.Add(name)) throw new DefinitionException("activation-cycle");
                foreach (string reference in dependencies[name]) Visit(reference);
                visiting.Remove(name);
                visited.Add(name);
            }
        }
    }

    private static void ValidateCondition(JsonElement condition, DefinitionNode root, SortedSet<string> capabilities,
        DefinitionNode? item = null)
    {
        string operation = condition.GetProperty("op").GetString()!;
        if (operation is "all" or "any" or "not")
        {
            Only(condition, "op", "operands");
            JsonElement operands = condition.GetProperty("operands");
            if (operands.GetArrayLength() < 1 || operation == "not" && operands.GetArrayLength() != 1)
                throw new DefinitionException("condition-operands");
            foreach (JsonElement operand in operands.EnumerateArray()) ValidateCondition(operand, root, capabilities, item);
            return;
        }
        if (operation is not ("exists" or "equals" or "notEquals" or "in"))
            throw new DefinitionException("condition-operation");
        Only(condition, operation == "exists" ? ["op", "reference"]
            : operation == "in" ? ["op", "reference", "values"] : ["op", "reference", "value"]);
        JsonElement reference = condition.GetProperty("reference");
        if (DefinitionPath.Parse(reference.GetProperty("path").GetString()!).Segments.Count == 0)
            throw new DefinitionException("condition-context");
        DefinitionNode controller = Reference(reference, root, item);
        foreach (JsonElement expected in operation == "exists" ? Array.Empty<JsonElement>()
            : operation == "in" ? condition.GetProperty("values").EnumerateArray().ToArray()
            : new[] { condition.GetProperty("value") })
        {
            bool compatible = expected.ValueKind == JsonValueKind.Null ? controller.IsNullable : controller.Type switch
            {
                DefinitionNodeType.Text => expected.ValueKind == JsonValueKind.String,
                DefinitionNodeType.Boolean => expected.ValueKind is JsonValueKind.True or JsonValueKind.False,
                DefinitionNodeType.WholeNumber => expected.ValueKind == JsonValueKind.Number && expected.TryGetInt64(out _),
                DefinitionNodeType.Number => expected.ValueKind == JsonValueKind.Number && expected.TryGetDecimal(out _),
                DefinitionNodeType.Mapping => expected.ValueKind == JsonValueKind.Object,
                DefinitionNodeType.Sequence => expected.ValueKind == JsonValueKind.Array,
                _ => false
            };
            if (!compatible) throw new DefinitionException("condition-type");
        }
        if (operation == "in" && condition.GetProperty("values").GetArrayLength() == 0)
            throw new DefinitionException("condition-operands");
        capabilities.Add(reference.GetProperty("scope").GetString() == "item" ? "predicate.item" : "predicate.root");
    }

    private static void ValidateProjection(JsonElement projection, DefinitionNode root, DefinitionNode? item,
        SortedSet<string> capabilities, int depth)
    {
        if (depth > CompositionLimits.DefaultNestingDepth) throw new DefinitionException("projection-limit");
        string op = projection.GetProperty("op").GetString()!;
        string[] members = op switch
        {
            "literal" => ["op", "value"],
            "copy" => ["op", "reference"],
            "omit" or "index" => ["op"],
            "object" => ["op", "members"],
            "list" => ["op", "items"],
            "repeat" => ["op", "reference", "item"],
            _ => throw new DefinitionException("projection-operation")
        };
        Only(projection, [.. members, "when"]);
        if (projection.TryGetProperty("when", out var condition)) ValidateCondition(condition, root, capabilities, item);
        capabilities.Add("projection." + op);
        switch (op)
        {
            case "literal": _ = projection.GetProperty("value"); break;
            case "copy": _ = Reference(projection.GetProperty("reference"), root, item); break;
            case "index": if (item is null) throw new DefinitionException("index-context"); break;
            case "object":
                foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
                    ValidateProjection(member.Value, root, item, capabilities, depth + 1);
                break;
            case "list":
                foreach (JsonElement child in projection.GetProperty("items").EnumerateArray())
                    ValidateProjection(child, root, item, capabilities, depth + 1);
                break;
            case "repeat":
                DefinitionNode source = Reference(projection.GetProperty("reference"), root, item);
                if (source.Type != DefinitionNodeType.Sequence || source.IsNullable)
                    throw new DefinitionException("repeat-source");
                ValidateProjection(projection.GetProperty("item"), root, source.Items, capabilities, depth + 1);
                break;
        }
    }

    private static DefinitionNode Reference(JsonElement reference, DefinitionNode root, DefinitionNode? item)
    {
        Only(reference, "scope", "path");
        DefinitionNode node = reference.GetProperty("scope").GetString() switch
        {
            "root" => root,
            "item" => item ?? throw new DefinitionException("reference-context"),
            _ => throw new DefinitionException("reference-scope")
        };
        foreach (string segment in DefinitionPath.Parse(reference.GetProperty("path").GetString()!).Segments)
        {
            if (node.Type == DefinitionNodeType.Mapping && node.Properties.TryGetValue(segment, out var property)) node = property;
            else if (node.Type == DefinitionNodeType.Sequence && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && segment == index.ToString(CultureInfo.InvariantCulture)) node = node.Items!;
            else throw new DefinitionException("reference-invalid");
        }
        return node;
    }

    private static void ValidateDotenv(JsonElement projection, DefinitionNode root)
    {
        if (projection.GetProperty("op").GetString() == "omit") return;
        if (projection.GetProperty("op").GetString() != "object") throw new DefinitionException("dotenv-shape");
        foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
        {
            string key = member.Name;
            if (key.Length == 0 || !(char.IsAsciiLetter(key[0]) || key[0] == '_')
                || !key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) throw new DefinitionException("dotenv-key");
            JsonElement value = member.Value;
            switch (value.GetProperty("op").GetString())
            {
                case "omit": break;
                case "copy":
                    DefinitionNode source = Reference(value.GetProperty("reference"), root, null);
                    if (source.IsNullable || source.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)
                        throw new DefinitionException("dotenv-shape");
                    break;
                case "literal":
                    if (value.GetProperty("value").ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                        throw new DefinitionException("dotenv-shape");
                    break;
                default: throw new DefinitionException("dotenv-shape");
            }
        }
    }

    private static void ValidateMarkdown(JsonElement projection, DefinitionNode root)
    {
        bool valid = projection.GetProperty("op").GetString() switch
        {
            "omit" => true,
            "literal" => projection.GetProperty("value").ValueKind == JsonValueKind.Array &&
                projection.GetProperty("value").EnumerateArray().All(value => value.ValueKind == JsonValueKind.String),
            "list" => projection.GetProperty("items").EnumerateArray().All(value => TextResult(value, null)),
            "copy" => TextSequence(Reference(projection.GetProperty("reference"), root, null)),
            "repeat" => TextResult(projection.GetProperty("item"),
                Reference(projection.GetProperty("reference"), root, null).Items),
            _ => false
        };
        if (!valid) throw new DefinitionException("markdown-shape");

        static bool TextSequence(DefinitionNode node) => !node.IsNullable && node.Type == DefinitionNodeType.Sequence
            && node.Items is { Type: DefinitionNodeType.Text, IsNullable: false };

        bool TextResult(JsonElement value, DefinitionNode? item) => value.GetProperty("op").GetString() switch
        {
            "omit" => true,
            "literal" => value.GetProperty("value").ValueKind == JsonValueKind.String,
            "copy" => Reference(value.GetProperty("reference"), root, item)
                is { Type: DefinitionNodeType.Text, IsNullable: false },
            _ => false
        };
    }

    private static void ValidatePresentation(JsonElement presentation, DefinitionNode root, JsonElement artifacts)
    {
        Only(presentation, "sections", "journeys", "defaultJourney", "navigation", "outputSelector");
        if (presentation.TryGetProperty("navigation", out _)) Choice(presentation, "navigation", "wizard", "sections");
        var sections = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement section in presentation.GetProperty("sections").EnumerateArray())
        {
            Only(section, "id", "title", "description", "pointers");
            if (!sections.Add(Text(section.GetProperty("id"), 64))) throw new DefinitionException("presentation-duplicate");
            Text(section.GetProperty("title"), 128);
            if (section.TryGetProperty("description", out var description)) Text(description, 2048);
            var pointers = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement pointer in section.GetProperty("pointers").EnumerateArray())
            {
                if (!pointers.Add(pointer.GetString()!)) throw new DefinitionException("presentation-duplicate");
                _ = Reference(JsonSerializer.SerializeToElement(new { scope = "root", path = pointer.GetString() }), root, null);
            }
        }
        if (sections.Count is < 1 or > 64) throw new DefinitionException("presentation-limit");
        var journeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement journey in presentation.GetProperty("journeys").EnumerateArray())
        {
            Only(journey, "id", "title", "description", "sections");
            if (!journeys.Add(Text(journey.GetProperty("id"), 64))) throw new DefinitionException("presentation-duplicate");
            Text(journey.GetProperty("title"), 128);
            if (journey.TryGetProperty("description", out var description)) Text(description, 2048);
            Strings(journey.GetProperty("sections"), 64, 64, true);
            foreach (JsonElement section in journey.GetProperty("sections").EnumerateArray())
                if (!sections.Contains(section.GetString()!)) throw new DefinitionException("presentation-reference");
        }
        if (journeys.Count is < 1 or > 16 || !journeys.Contains(presentation.GetProperty("defaultJourney").GetString()!))
            throw new DefinitionException("presentation-default");
        if (presentation.TryGetProperty("outputSelector", out var selector))
            ValidateOutputSelector(selector, root, artifacts);
    }

    private static void ValidateOutputSelector(JsonElement selector, DefinitionNode root, JsonElement artifacts)
    {
        Only(selector, "pointer", "options");
        DefinitionPath pointer = DefinitionPath.Parse(selector.GetProperty("pointer").GetString()!);
        if (pointer.Segments.Count != 1 || !PublicEditable(root)
            || !root.Properties.TryGetValue(pointer.Segments[0], out DefinitionNode? field)
            || field.Type != DefinitionNodeType.Text || field.IsNullable || !PublicEditable(field)
            || field.Keywords.GetProperty("x-configurator").TryGetProperty("applicableWhen", out _)
            || !field.Keywords.TryGetProperty("enum", out JsonElement enumeration))
            throw new DefinitionException("output-selector-field");
        var remaining = enumeration.EnumerateArray().Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        JsonElement options = selector.GetProperty("options");
        if (remaining.Count is < 1 or > 64 || options.GetArrayLength() is < 1 or > 64)
            throw new DefinitionException("output-selector-limit");
        foreach (JsonElement option in options.EnumerateArray())
        {
            Only(option, "value", "title", "artifactIds");
            string value = option.GetProperty("value").GetString()!;
            if (!remaining.Remove(value)) throw new DefinitionException("output-selector-value");
            Text(option.GetProperty("title"), 128);
            JsonElement artifactIds = option.GetProperty("artifactIds");
            Strings(artifactIds, 64, 128, nonempty: true);
            foreach (JsonElement id in artifactIds.EnumerateArray())
            {
                JsonElement artifact = artifacts.EnumerateArray().FirstOrDefault(
                    artifact => artifact.GetProperty("id").GetString() == id.GetString());
                if (artifact.ValueKind == JsonValueKind.Undefined
                    || !artifact.GetProperty("projection").TryGetProperty("when", out JsonElement guard)
                    || guard.GetProperty("op").GetString() != "equals"
                    || guard.GetProperty("reference").GetProperty("scope").GetString() != "root"
                    || guard.GetProperty("reference").GetProperty("path").GetString() != pointer.ToPointer()
                    || guard.GetProperty("value").ValueKind != JsonValueKind.String
                    || guard.GetProperty("value").GetString() != value)
                    throw new DefinitionException("output-selector-artifact");
            }
        }
        if (remaining.Count != 0) throw new DefinitionException("output-selector-value");

        static bool PublicEditable(DefinitionNode node) =>
            node.Keywords.TryGetProperty("x-configurator", out JsonElement metadata)
            && metadata.TryGetProperty("sensitivity", out JsonElement sensitivity)
            && sensitivity.GetString() == "public"
            && (!node.Keywords.TryGetProperty("writeOnly", out JsonElement writeOnly) || !writeOnly.GetBoolean())
            && (!node.Keywords.TryGetProperty("readOnly", out JsonElement readOnly) || !readOnly.GetBoolean());
    }

    private static void ValidateLocalization(JsonElement document)
    {
        var locales = new SortedSet<string>(StringComparer.Ordinal) { "en" };
        if (document.TryGetProperty("localization", out var localization))
        {
            Only(localization, "defaultLocale", "translations");
            locales.Clear();
            locales.Add(Text(localization.GetProperty("defaultLocale"), 128));
            foreach (JsonProperty translation in localization.GetProperty("translations").EnumerateObject())
            {
                Text(JsonSerializer.SerializeToElement(translation.Name), 128);
                locales.Add(translation.Name);
                Only(translation.Value, "direction", "overrides");
                if (translation.Value.TryGetProperty("direction", out _)) Choice(translation.Value, "direction", "ltr", "rtl");
                if (translation.Value.TryGetProperty("overrides", out var overrides))
                    foreach (JsonProperty entry in overrides.EnumerateObject())
                    {
                        IReadOnlyList<string> segments = DefinitionPath.Parse(entry.Name).Segments;
                        if (segments.Count == 0 || segments[^1] is not ("title" or "description" or "instructions"
                            or "label" or "explanation" or "units"))
                            throw new DefinitionException("localization-target");
                        JsonElement target = document;
                        foreach (string segment in segments)
                        {
                            if (target.ValueKind == JsonValueKind.Object && target.TryGetProperty(segment, out var child)) target = child;
                            else if (target.ValueKind == JsonValueKind.Array
                                && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                                && index >= 0 && index < target.GetArrayLength()
                                && segment == index.ToString(CultureInfo.InvariantCulture)) target = target[index];
                            else throw new DefinitionException("localization-target");
                        }
                        if (target.ValueKind != JsonValueKind.String) throw new DefinitionException("localization-target");
                        Text(entry.Value, 4096);
                    }
            }
        }
        if (!document.GetProperty("locales").EnumerateArray().Select(value => value.GetString()).SequenceEqual(locales, StringComparer.Ordinal))
            throw new DefinitionException("locales-invalid");
    }

    private static void ValidateGuidance(JsonElement guidance)
    {
        Only(guidance, "instructions", "links", "searchTerms");
        if (guidance.TryGetProperty("instructions", out var instructions))
        {
            string text = instructions.GetString()!;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4096
                || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))
                || text.Contains("${", StringComparison.Ordinal) || text.Contains("{{", StringComparison.Ordinal)
                || text.Contains("}}", StringComparison.Ordinal)) throw new DefinitionException("guidance-invalid");
        }
        if (guidance.TryGetProperty("links", out var links))
        {
            if (links.GetArrayLength() > 4) throw new DefinitionException("guidance-limit");
            foreach (JsonElement link in links.EnumerateArray())
            {
                Only(link, "title", "url");
                Text(link.GetProperty("title"), 128);
                PublicUrl(link.GetProperty("url"), fragment: true);
            }
        }
        if (guidance.TryGetProperty("searchTerms", out var terms)) Strings(terms, 16, 64, false);
    }

    private static void PublicUrl(JsonElement value, bool fragment)
    {
        string url = Text(value, 2048);
        if (url.Any(char.IsWhiteSpace) || url.Contains("${", StringComparison.Ordinal) || url.Contains("{{", StringComparison.Ordinal)
            || url.Contains("}}", StringComparison.Ordinal) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.Host.Length == 0 || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || !fragment && uri.Fragment.Length != 0)
            throw new DefinitionException("public-url-invalid");
    }

    private static void Strings(JsonElement array, int maximum, int textMaximum, bool nonempty)
    {
        if (array.GetArrayLength() > maximum || nonempty && array.GetArrayLength() == 0)
            throw new DefinitionException("metadata-limit");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement value in array.EnumerateArray())
            if (!seen.Add(Text(value, textMaximum))) throw new DefinitionException("metadata-duplicate");
    }

    internal static string Text(JsonElement value, int maximum)
    {
        string text = value.GetString() ?? throw new DefinitionException("text-invalid");
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)
            || text.Contains('<', StringComparison.Ordinal) || text.Contains('>', StringComparison.Ordinal))
            throw new DefinitionException("text-invalid");
        return text;
    }

    private static void Choice(JsonElement value, string name, params string[] allowed)
    {
        if (!allowed.Contains(value.GetProperty(name).GetString(), StringComparer.Ordinal))
            throw new DefinitionException("vocabulary-invalid");
    }

    private static void Only(JsonElement value, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            throw new DefinitionException("member-unsupported");
    }
}
