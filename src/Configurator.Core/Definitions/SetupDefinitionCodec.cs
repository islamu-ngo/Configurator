namespace Configurator.Core.Definitions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Configurator.Core.Composition;

public static class SetupDefinitionCodec
{
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = SetupCompositionLimits.DefaultNestingDepth,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static SetupDefinition ParseJson(ReadOnlyMemory<byte> bytes) => Parse(bytes, false);
    public static SetupDefinition ParseYaml(ReadOnlyMemory<byte> bytes) => Parse(bytes, true);

    private static SetupDefinition Parse(ReadOnlyMemory<byte> bytes, bool yaml)
    {
        try
        {
            byte[] canonical = yaml ? DefinitionProfile.CanonicalizeYaml(bytes) : DefinitionProfile.CanonicalizeJson(bytes);
            using JsonDocument parsed = JsonDocument.Parse(canonical);
            var capabilities = new SortedSet<string>(StringComparer.Ordinal);
            DefinitionNode configuration = Admit(parsed.RootElement, capabilities);
            JsonElement declared = parsed.RootElement.GetProperty("requiredCapabilities");
            if (declared.ValueKind != JsonValueKind.Array
                || !declared.EnumerateArray().Select(item => item.GetString()).SequenceEqual(capabilities, StringComparer.Ordinal))
                throw new SetupDefinitionException("capabilities-invalid");
            return new SetupDefinition(parsed.RootElement, canonical, configuration);
        }
        catch (SetupDefinitionException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or ArgumentException or OverflowException)
        {
            throw new SetupDefinitionException("definition-invalid");
        }
    }

    /// <summary>Fills computed profile/capabilities only, then uses public runtime admission.</summary>
    internal static SetupDefinition AdmitCompiled(JsonObject runtime)
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
        if (document.GetProperty("schemaVersion").GetInt32() != DefinitionProfile.RuntimeSchemaVersion
            || document.GetProperty("profile").GetString() != DefinitionProfile.Id)
            throw new SetupDefinitionException("definition-version");
        if (!Guid.TryParseExact(document.GetProperty("familyId").GetString(), "D", out Guid family)
            || family == Guid.Empty || document.GetProperty("revision").GetInt64() < 1)
            throw new SetupDefinitionException("identity-invalid");
        JsonElement project = document.GetProperty("project");
        Only(project, "id", "name", "publisher", "description", "repository", "definitionLicense");
        foreach (string name in new[] { "id", "name", "publisher" }) Text(project.GetProperty(name), 256);
        foreach (string name in new[] { "description", "definitionLicense" })
            if (project.TryGetProperty(name, out var text)) Text(text, name == "description" ? 4096 : 256);
        if (project.TryGetProperty("repository", out var repository)) PublicUrl(repository, fragment: false);
        foreach (string name in new[] { "applicationVersions", "entryPoints", "locales" })
            Strings(document.GetProperty(name), 128, 128, nonempty: true);
        DefinitionNode configuration = ReadNode(document.GetProperty("configuration"), capabilities, true, false);
        if (configuration.Type != DefinitionNodeType.Mapping || configuration.IsNullable)
            throw new SetupDefinitionException("configuration-object-required");
        if (configuration.Properties.Count > 1024) throw new SetupDefinitionException("document-limit");
        foreach (DefinitionNode child in configuration.Properties.Values) RejectNestedRelationships(child);
        ValidateApplicability(configuration, capabilities);
        if (document.TryGetProperty("annotations", out var annotations) && annotations.ValueKind != JsonValueKind.Object)
            throw new SetupDefinitionException("annotations-invalid");
        ValidateLocalization(document);
        if (document.TryGetProperty("presentation", out var presentation)) ValidatePresentation(presentation, configuration);
        JsonElement artifacts = document.GetProperty("artifacts");
        if (artifacts.GetArrayLength() is < 1 or > 64) throw new SetupDefinitionException("artifact-limit");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            Only(artifact, "id", "fileName", "format", "targetUpdateMode", "restartRequirement", "projection");
            string id = Text(artifact.GetProperty("id"), 128);
            if (!char.IsAsciiLetter(id[0]) || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                || !ids.Add(id)) throw new SetupDefinitionException("artifact-identifier");
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
                || !files.Add(file)) throw new SetupDefinitionException("filename-invalid");
            ValidateProjection(artifact.GetProperty("projection"), configuration, null, capabilities, 0);
            if (artifact.GetProperty("format").GetString() == "dotenv")
                ValidateDotenv(artifact.GetProperty("projection"), configuration);
        }
        return configuration;

        static void RejectNestedRelationships(DefinitionNode node)
        {
            if (node.Keywords.TryGetProperty("if", out _) || node.Keywords.TryGetProperty("dependentRequired", out _))
                throw new SetupDefinitionException("relationship-context");
            foreach (DefinitionNode child in node.Properties.Values) RejectNestedRelationships(child);
            if (node.Items is { } items) RejectNestedRelationships(items);
        }
    }

    private static DefinitionNode ReadNode(JsonElement schema, SortedSet<string> capabilities, bool publicAncestors, bool secretAncestors)
    {
        Only(schema, "type", "title", "description", "enum", "const", "properties", "required", "additionalProperties",
            "items", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems", "minProperties",
            "maxProperties", "format", "default", "readOnly", "writeOnly", "deprecated", "examples", "x-configurator",
            "if", "then", "dependentRequired");
        JsonElement type = schema.GetProperty("type");
        bool nullable = type.ValueKind == JsonValueKind.Array;
        string spelling;
        if (nullable)
        {
            string?[] types = type.EnumerateArray().Select(item => item.GetString()).ToArray();
            if (types.Length != 2 || types.Count(item => item == "null") != 1) throw new SetupDefinitionException("type-invalid");
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
            _ => throw new SetupDefinitionException("type-unsupported")
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
            Only(extension, "sensitivity", "initialValue", "suggestedValue", "notBlank", "detail", "guidance", "credential", "choices", "units",
                "uniqueBy", "applicableWhen");
            if (extension.TryGetProperty("sensitivity", out var declared))
            {
                sensitivity = declared.GetString()!;
                Choice(extension, "sensitivity", DefinitionVocabulary.Sensitivities);
            }
        }
        bool publicContext = publicAncestors && sensitivity == "public" && !writeOnly;
        bool secretContext = secretAncestors || sensitivity == "secret" || writeOnly;
        var properties = new Dictionary<string, DefinitionNode>(StringComparer.Ordinal);
        var required = new List<string>();
        DefinitionNode? items = null;
        if (nodeType == DefinitionNodeType.Mapping)
        {
            if (!schema.TryGetProperty("additionalProperties", out var additional) || additional.ValueKind != JsonValueKind.False)
                throw new SetupDefinitionException("map-semantics-unsupported");
            if (schema.TryGetProperty("properties", out var children))
                foreach (JsonProperty child in children.EnumerateObject())
                    properties.Add(child.Name, ReadNode(child.Value, capabilities, publicContext, secretContext));
            if (schema.TryGetProperty("required", out var requiredNames))
            {
                foreach (JsonElement name in requiredNames.EnumerateArray())
                {
                    string key = name.GetString()!;
                    if (!properties.ContainsKey(key) || required.Contains(key, StringComparer.Ordinal))
                        throw new SetupDefinitionException("required-invalid");
                    required.Add(key);
                }
            }
        }
        else if (schema.TryGetProperty("properties", out _) || schema.TryGetProperty("required", out _)
            || schema.TryGetProperty("additionalProperties", out _)) throw new SetupDefinitionException("constraint-context");
        if (schema.TryGetProperty("if", out var guard))
        {
            if (nodeType != DefinitionNodeType.Mapping || !schema.TryGetProperty("then", out var consequence))
                throw new SetupDefinitionException("relationship-invalid");
            RequiredSet(guard);
            RequiredSet(consequence);
            capabilities.Add("relationship.presence");
        }
        else if (schema.TryGetProperty("then", out _)) throw new SetupDefinitionException("relationship-invalid");
        if (schema.TryGetProperty("dependentRequired", out var dependencies))
        {
            if (nodeType != DefinitionNodeType.Mapping || dependencies.ValueKind != JsonValueKind.Object)
                throw new SetupDefinitionException("relationship-invalid");
            foreach (JsonProperty dependency in dependencies.EnumerateObject())
            {
                if (!properties.ContainsKey(dependency.Name)) throw new SetupDefinitionException("relationship-invalid");
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
                throw new SetupDefinitionException("relationship-invalid");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement name in names.EnumerateArray())
                if (!properties.ContainsKey(name.GetString()!) || !seen.Add(name.GetString()!))
                    throw new SetupDefinitionException("relationship-invalid");
        }
        if (nodeType == DefinitionNodeType.Sequence) items = ReadNode(schema.GetProperty("items"), capabilities, publicContext, secretContext);
        else if (schema.TryGetProperty("items", out _)) throw new SetupDefinitionException("constraint-context");
        foreach ((string low, string high, DefinitionNodeType owner) in new[]
        {
            ("minLength", "maxLength", DefinitionNodeType.Text),
            ("minItems", "maxItems", DefinitionNodeType.Sequence),
            ("minProperties", "maxProperties", DefinitionNodeType.Mapping),
            ("minimum", "maximum", DefinitionNodeType.Number)
        })
        {
            decimal? lower = Bound(low);
            decimal? upper = Bound(high);
            if (lower > upper) throw new SetupDefinitionException("constraint-invalid");
            decimal? Bound(string name)
            {
                if (!schema.TryGetProperty(name, out var value)) return null;
                if (owner == DefinitionNodeType.Number ? nodeType is not (DefinitionNodeType.Number or DefinitionNodeType.WholeNumber) : nodeType != owner)
                    throw new SetupDefinitionException("constraint-context");
                decimal number = value.GetDecimal();
                if (owner != DefinitionNodeType.Number && (number < 0 || number != decimal.Truncate(number) || number > int.MaxValue))
                    throw new SetupDefinitionException("constraint-invalid");
                capabilities.Add("schema.bounds");
                return number;
            }
        }
        var keywords = new JsonObject();
        foreach (JsonProperty property in schema.EnumerateObject())
            if (property.Name is not ("properties" or "required" or "items" or "additionalProperties"))
                keywords.Add(property.Name, JsonNode.Parse(property.Value.GetRawText()));
        var node = new DefinitionNode(nodeType, nullable, properties, required, items, keywords: JsonSerializer.SerializeToElement(keywords));
        if (schema.TryGetProperty("format", out _) && nodeType != DefinitionNodeType.Text)
            throw new SetupDefinitionException("constraint-context");
        if (schema.TryGetProperty("enum", out var choices))
        {
            if (nodeType is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence || choices.GetArrayLength() is < 1 or > 256)
                throw new SetupDefinitionException("enum-invalid");
            var values = new HashSet<string>(StringComparer.Ordinal);
            JsonValueKind? kind = null;
            foreach (JsonElement value in choices.EnumerateArray())
            {
                JsonValueKind scalarKind = value.ValueKind == JsonValueKind.False ? JsonValueKind.True : value.ValueKind;
                if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null
                    || kind.HasValue && kind != scalarKind || !values.Add(value.GetRawText())
                    || !DefinitionValidator.ValueValid(node, value)) throw new SetupDefinitionException("enum-invalid");
                kind = scalarKind;
            }
            capabilities.Add("schema.enum");
        }
        if (schema.TryGetProperty("const", out var constant))
        {
            if (constant.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                || !DefinitionValidator.ValueValid(node, constant)) throw new SetupDefinitionException("const-invalid");
            capabilities.Add("schema.const");
        }
        if (hasExtension)
        {
            if (extension.TryGetProperty("uniqueBy", out var uniqueBy))
            {
                if (nodeType != DefinitionNodeType.Sequence || items?.Type != DefinitionNodeType.Mapping)
                    throw new SetupDefinitionException("unique-key-invalid");
                DefinitionPath path = DefinitionPath.Parse(uniqueBy.GetString()!);
                if (path.Segments.Count == 0) throw new SetupDefinitionException("unique-key-invalid");
                DefinitionNode current = items;
                foreach (string name in path.Segments)
                    if (current.Type != DefinitionNodeType.Mapping
                        || !current.Required.Contains(name, StringComparer.Ordinal)
                        || !current.Properties.TryGetValue(name, out current!))
                        throw new SetupDefinitionException("unique-key-invalid");
                if (current.Type != DefinitionNodeType.Text || current.IsNullable)
                    throw new SetupDefinitionException("unique-key-invalid");
                capabilities.Add("schema.uniqueBy");
            }
            if (extension.TryGetProperty("notBlank", out var notBlank))
            {
                if (nodeType != DefinitionNodeType.Text) throw new SetupDefinitionException("constraint-context");
                if (notBlank.GetBoolean()) capabilities.Add("schema.notBlank");
            }
            if (extension.TryGetProperty("detail", out _)) Choice(extension, "detail", "basic", "standard", "advanced");
            if (extension.TryGetProperty("units", out var units)) Text(units, 128);
            if (extension.TryGetProperty("guidance", out var guidance)) ValidateGuidance(guidance);
            if (extension.TryGetProperty("credential", out var credential))
            {
                Only(credential, "kind", "acquisition");
                Choice(credential, "kind", "password", "opaque-token", "symmetric-key", "private-key", "connection-string");
                Choice(credential, "acquisition", "existing", "create", "issuer");
                string kind = credential.GetProperty("kind").GetString()!;
                string acquisition = credential.GetProperty("acquisition").GetString()!;
                if (nodeType != DefinitionNodeType.Text || !secretContext
                    || acquisition == "create" && kind is not ("password" or "symmetric-key")
                    || acquisition == "issuer" && kind == "symmetric-key") throw new SetupDefinitionException("credential-invalid");
            }
            if (extension.TryGetProperty("choices", out var labels))
            {
                if (!schema.TryGetProperty("enum", out var enumValues) || labels.GetArrayLength() != enumValues.GetArrayLength())
                    throw new SetupDefinitionException("choices-invalid");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement label in labels.EnumerateArray())
                {
                    Only(label, "value", "label", "explanation");
                    string value = label.GetProperty("value").GetRawText();
                    if (!seen.Add(value) || !enumValues.EnumerateArray().Any(item => item.GetRawText() == value))
                        throw new SetupDefinitionException("choices-invalid");
                    Text(label.GetProperty("label"), 256);
                    if (label.TryGetProperty("explanation", out var explanation)) Text(explanation, 2048);
                }
            }
        }
        bool protectedTree = !publicContext || properties.Values.Any(Protected) || items is not null && Protected(items);
        foreach (string name in new[] { "default", "const", "examples" })
            if (schema.TryGetProperty(name, out var value))
            {
                if (protectedTree) throw new SetupDefinitionException("public-value-protected");
                if (name == "examples")
                {
                    if (value.GetArrayLength() > 256 || value.EnumerateArray().Any(item => !DefinitionValidator.ValueValid(node, item)))
                        throw new SetupDefinitionException("public-value-invalid");
                }
                else if (!DefinitionValidator.ValueValid(node, value)) throw new SetupDefinitionException("public-value-invalid");
            }
        foreach (string name in new[] { "initialValue", "suggestedValue" })
            if (hasExtension && extension.TryGetProperty(name, out var value))
            {
                if (protectedTree || !DefinitionValidator.ValueValid(node, value)) throw new SetupDefinitionException("public-value-invalid");
                capabilities.Add(name == "initialValue" ? "values.initial" : "values.suggested");
            }
        return node;
    }

    private static bool Protected(DefinitionNode node) =>
        node.Keywords.TryGetProperty("writeOnly", out var write) && write.GetBoolean()
        || !node.Keywords.TryGetProperty("x-configurator", out var extension)
        || !extension.TryGetProperty("sensitivity", out var sensitivity) || sensitivity.GetString() != "public"
        || node.Properties.Values.Any(Protected) || node.Items is not null && Protected(node.Items);

    private static void ValidateApplicability(DefinitionNode root, SortedSet<string> capabilities)
    {
        if (root.Keywords.TryGetProperty("x-configurator", out var rootMetadata)
            && rootMetadata.TryGetProperty("applicableWhen", out _)) throw new SetupDefinitionException("condition-context");
        var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach ((string name, DefinitionNode node) in root.Properties)
        {
            string[] references = [];
            if (node.Keywords.TryGetProperty("x-configurator", out var metadata)
                && metadata.TryGetProperty("applicableWhen", out var condition))
            {
                ValidateCondition(condition, root, capabilities);
                references = DefinitionValidator.References(condition).Distinct(StringComparer.Ordinal).ToArray();
            }
            dependencies.Add(name, references);
            foreach (DefinitionNode child in node.Properties.Values.Concat(node.Items is null ? [] : new[] { node.Items }))
                RejectNested(child);
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in root.Properties.Keys) Visit(name);

        void Visit(string name)
        {
            if (visited.Contains(name)) return;
            if (!visiting.Add(name)) throw new SetupDefinitionException("activation-cycle");
            foreach (string reference in dependencies[name]) Visit(reference);
            visiting.Remove(name);
            visited.Add(name);
        }
        static void RejectNested(DefinitionNode node)
        {
            if (node.Keywords.TryGetProperty("x-configurator", out var extension)
                && extension.TryGetProperty("applicableWhen", out _)) throw new SetupDefinitionException("condition-context");
            foreach (DefinitionNode child in node.Properties.Values.Concat(node.Items is null ? [] : new[] { node.Items }))
                RejectNested(child);
        }
    }

    private static void ValidateCondition(JsonElement condition, DefinitionNode root, SortedSet<string> capabilities)
    {
        string operation = condition.GetProperty("op").GetString()!;
        if (operation is "all" or "any" or "not")
        {
            Only(condition, "op", "operands");
            JsonElement operands = condition.GetProperty("operands");
            if (operands.GetArrayLength() < 1 || operation == "not" && operands.GetArrayLength() != 1)
                throw new SetupDefinitionException("condition-operands");
            foreach (JsonElement operand in operands.EnumerateArray()) ValidateCondition(operand, root, capabilities);
            return;
        }
        if (operation is not ("exists" or "equals" or "notEquals" or "in"))
            throw new SetupDefinitionException("condition-operation");
        Only(condition, operation == "exists" ? ["op", "reference"]
            : operation == "in" ? ["op", "reference", "values"] : ["op", "reference", "value"]);
        JsonElement reference = condition.GetProperty("reference");
        if (reference.GetProperty("scope").GetString() != "root"
            || DefinitionPath.Parse(reference.GetProperty("path").GetString()!).Segments.Count == 0)
            throw new SetupDefinitionException("condition-context");
        DefinitionNode controller = Reference(reference, root, null);
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
            if (!compatible) throw new SetupDefinitionException("condition-type");
        }
        if (operation == "in" && condition.GetProperty("values").GetArrayLength() == 0)
            throw new SetupDefinitionException("condition-operands");
        capabilities.Add("predicate.root");
    }

    private static void ValidateProjection(JsonElement projection, DefinitionNode root, DefinitionNode? item,
        SortedSet<string> capabilities, int depth)
    {
        if (depth > SetupCompositionLimits.DefaultNestingDepth) throw new SetupDefinitionException("projection-limit");
        string op = projection.GetProperty("op").GetString()!;
        string[] members = op switch
        {
            "literal" => ["op", "value"],
            "copy" => ["op", "reference"],
            "omit" or "index" => ["op"],
            "object" => ["op", "members"],
            "list" => ["op", "items"],
            "repeat" => ["op", "reference", "item"],
            _ => throw new SetupDefinitionException("projection-operation")
        };
        Only(projection, [.. members, "when"]);
        if (projection.TryGetProperty("when", out var condition)) ValidateCondition(condition, root, capabilities);
        capabilities.Add("projection." + op);
        switch (op)
        {
            case "literal": _ = projection.GetProperty("value"); break;
            case "copy": _ = Reference(projection.GetProperty("reference"), root, item); break;
            case "index": if (item is null) throw new SetupDefinitionException("index-context"); break;
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
                if (source.Type != DefinitionNodeType.Sequence) throw new SetupDefinitionException("repeat-source");
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
            "item" => item ?? throw new SetupDefinitionException("reference-context"),
            _ => throw new SetupDefinitionException("reference-scope")
        };
        foreach (string segment in DefinitionPath.Parse(reference.GetProperty("path").GetString()!).Segments)
        {
            if (node.Type == DefinitionNodeType.Mapping && node.Properties.TryGetValue(segment, out var property)) node = property;
            else if (node.Type == DefinitionNodeType.Sequence && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && segment == index.ToString(CultureInfo.InvariantCulture)) node = node.Items!;
            else throw new SetupDefinitionException("reference-invalid");
        }
        return node;
    }

    private static void ValidateDotenv(JsonElement projection, DefinitionNode root)
    {
        if (projection.GetProperty("op").GetString() != "object") throw new SetupDefinitionException("dotenv-shape");
        foreach (JsonProperty member in projection.GetProperty("members").EnumerateObject())
        {
            string key = member.Name;
            if (key.Length == 0 || !(char.IsAsciiLetter(key[0]) || key[0] == '_')
                || !key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) throw new SetupDefinitionException("dotenv-key");
            JsonElement value = member.Value;
            switch (value.GetProperty("op").GetString())
            {
                case "omit": break;
                case "copy":
                    DefinitionNode source = Reference(value.GetProperty("reference"), root, null);
                    if (source.IsNullable || source.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence)
                        throw new SetupDefinitionException("dotenv-shape");
                    break;
                case "literal":
                    if (value.GetProperty("value").ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                        throw new SetupDefinitionException("dotenv-shape");
                    break;
                default: throw new SetupDefinitionException("dotenv-shape");
            }
        }
    }

    private static void ValidatePresentation(JsonElement presentation, DefinitionNode root)
    {
        Only(presentation, "sections", "journeys", "defaultJourney", "navigation");
        if (presentation.TryGetProperty("navigation", out _)) Choice(presentation, "navigation", "wizard", "sections");
        var sections = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement section in presentation.GetProperty("sections").EnumerateArray())
        {
            Only(section, "id", "title", "description", "pointers");
            if (!sections.Add(Text(section.GetProperty("id"), 64))) throw new SetupDefinitionException("presentation-duplicate");
            Text(section.GetProperty("title"), 128);
            if (section.TryGetProperty("description", out var description)) Text(description, 2048);
            var pointers = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement pointer in section.GetProperty("pointers").EnumerateArray())
            {
                if (!pointers.Add(pointer.GetString()!)) throw new SetupDefinitionException("presentation-duplicate");
                _ = Reference(JsonSerializer.SerializeToElement(new { scope = "root", path = pointer.GetString() }), root, null);
            }
        }
        if (sections.Count is < 1 or > 64) throw new SetupDefinitionException("presentation-limit");
        var journeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement journey in presentation.GetProperty("journeys").EnumerateArray())
        {
            Only(journey, "id", "title", "description", "sections");
            if (!journeys.Add(Text(journey.GetProperty("id"), 64))) throw new SetupDefinitionException("presentation-duplicate");
            Text(journey.GetProperty("title"), 128);
            if (journey.TryGetProperty("description", out var description)) Text(description, 2048);
            Strings(journey.GetProperty("sections"), 64, 64, true);
            foreach (JsonElement section in journey.GetProperty("sections").EnumerateArray())
                if (!sections.Contains(section.GetString()!)) throw new SetupDefinitionException("presentation-reference");
        }
        if (journeys.Count is < 1 or > 16 || !journeys.Contains(presentation.GetProperty("defaultJourney").GetString()!))
            throw new SetupDefinitionException("presentation-default");
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
                            throw new SetupDefinitionException("localization-target");
                        JsonElement target = document;
                        foreach (string segment in segments)
                        {
                            if (target.ValueKind == JsonValueKind.Object && target.TryGetProperty(segment, out var child)) target = child;
                            else if (target.ValueKind == JsonValueKind.Array
                                && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                                && index >= 0 && index < target.GetArrayLength()
                                && segment == index.ToString(CultureInfo.InvariantCulture)) target = target[index];
                            else throw new SetupDefinitionException("localization-target");
                        }
                        if (target.ValueKind != JsonValueKind.String) throw new SetupDefinitionException("localization-target");
                        Text(entry.Value, 4096);
                    }
            }
        }
        if (!document.GetProperty("locales").EnumerateArray().Select(value => value.GetString()).SequenceEqual(locales, StringComparer.Ordinal))
            throw new SetupDefinitionException("locales-invalid");
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
                || text.Contains("}}", StringComparison.Ordinal)) throw new SetupDefinitionException("guidance-invalid");
        }
        if (guidance.TryGetProperty("links", out var links))
        {
            if (links.GetArrayLength() > 4) throw new SetupDefinitionException("guidance-limit");
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
            throw new SetupDefinitionException("public-url-invalid");
    }

    private static void Strings(JsonElement array, int maximum, int textMaximum, bool nonempty)
    {
        if (array.GetArrayLength() > maximum || nonempty && array.GetArrayLength() == 0)
            throw new SetupDefinitionException("metadata-limit");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement value in array.EnumerateArray())
            if (!seen.Add(Text(value, textMaximum))) throw new SetupDefinitionException("metadata-duplicate");
    }

    internal static string Text(JsonElement value, int maximum)
    {
        string text = value.GetString() ?? throw new SetupDefinitionException("text-invalid");
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)
            || text.Contains('<', StringComparison.Ordinal) || text.Contains('>', StringComparison.Ordinal))
            throw new SetupDefinitionException("text-invalid");
        return text;
    }

    private static void Choice(JsonElement value, string name, params string[] allowed)
    {
        if (!allowed.Contains(value.GetProperty(name).GetString(), StringComparer.Ordinal))
            throw new SetupDefinitionException("vocabulary-invalid");
    }

    private static void Only(JsonElement value, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            throw new SetupDefinitionException("member-unsupported");
    }
}
