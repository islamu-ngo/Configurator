using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Core.Definitions.Authoring;

/// <summary>A value-free account of one public schema concept. Document zero is the supplied root.</summary>
public sealed record DefinitionSchemaTransformation(string Classification, string Code, string Location, int DocumentOrdinal);

/// <summary>Snapshots a v2 configuration schema and its explicit transformation ledger.</summary>
public sealed class DefinitionSchemaImportResult
{
    internal DefinitionSchemaImportResult(JsonElement configuration, IEnumerable<DefinitionSchemaTransformation> transformations)
    {
        Configuration = configuration.Clone();
        Transformations = Array.AsReadOnly(transformations.ToArray());
    }
    public JsonElement Configuration { get; }
    public IReadOnlyList<DefinitionSchemaTransformation> Transformations { get; }
}

public sealed class DefinitionSchemaImportException : Exception
{
    internal DefinitionSchemaImportException(string code, IEnumerable<DefinitionSchemaTransformation>? transformations = null) : base(code)
    {
        Code = code;
        Transformations = Array.AsReadOnly((transformations ?? []).ToArray());
    }
    public string Code { get; }
    public IReadOnlyList<DefinitionSchemaTransformation> Transformations { get; }
}

/// <summary>Translates a closed JSON Schema subset without granting reference, filesystem or network authority.</summary>
public static class DefinitionSchemaImporter
{
    private const int MaximumBytes = 4 * 1024 * 1024;

    /// <summary>Imports supplied bounded public documents, refusing unsupported assertions and unacknowledged domain differences.</summary>
    public static DefinitionSchemaImportResult Import(ReadOnlyMemory<byte> schema,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>>? localReferences = null,
        bool acceptBoundedSemantics = false)
    {
        try
        {
            var documents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            int bytes = schema.Length;
            int nodes = 0;
            documents.Add("", Parse(schema, ref nodes));
            foreach ((string name, ReadOnlyMemory<byte> content) in (localReferences ?? new Dictionary<string, ReadOnlyMemory<byte>>()).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!IsLocalName(name) || documents.Count >= 64 || content.Length > MaximumBytes - bytes)
                    throw new DefinitionSchemaImportException("schema-reference-bound");
                bytes += content.Length;
                documents.Add(name, Parse(content, ref nodes));
            }
            var importer = new Importer(documents);
            try
            {
                JsonObject configuration = importer.Field(documents[""], "", 0, new HashSet<string>(StringComparer.Ordinal));
                // Compile through the ordinary Core admission path rather than duplicating its semantic evaluator.
                var source = new JsonObject
                {
                    ["$schema"] = DefinitionAuthoringCodec.SchemaId,
                    ["identity"] = new JsonObject { ["familyId"] = "69cdb122-41d2-4fab-9b82-7929a889c726", ["revision"] = 1 },
                    ["project"] = new JsonObject { ["id"] = "schema.import", ["name"] = "Schema import" },
                    ["applicationVersions"] = new JsonArray("1.0.0"),
                    ["configuration"] = configuration.DeepClone(),
                    ["outputs"] = new JsonObject { ["config.json"] = new JsonObject { ["format"] = "json" } }
                };
                try { _ = DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(source))); }
                catch (SetupDefinitionException)
                {
                    importer.Report("unsupported", "schema-profile-unsupported", "", "");
                    throw new DefinitionSchemaImportException("schema-profile-unsupported");
                }
                if (!acceptBoundedSemantics && importer.Transformations.Any(item =>
                    item.Code is "schema-numeric-bounded-domain" or "schema-nullable-variant-domain"))
                    throw new DefinitionSchemaImportException("schema-bounded-semantics-required");
                return new(JsonSerializer.SerializeToElement(configuration), importer.Transformations);
            }
            catch (DefinitionSchemaImportException exception)
            {
                if (exception.Code != "schema-bounded-semantics-required"
                    && !importer.Transformations.Any(item => item.Classification == "unsupported"))
                    importer.Report("unsupported", exception.Code, "", "");
                throw new DefinitionSchemaImportException(exception.Code, importer.Transformations);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new DefinitionSchemaImportException("schema-document-invalid");
        }
        catch (SetupDefinitionException) { throw new DefinitionSchemaImportException("schema-document-invalid"); }
    }

    private static JsonElement Parse(ReadOnlyMemory<byte> bytes, ref int nodes)
    {
        if (bytes.Length is < 1 or > MaximumBytes) throw new DefinitionSchemaImportException("schema-source-bound");
        using JsonDocument document = JsonDocument.Parse(DefinitionProfile.CanonicalizeJson(bytes));
        Inspect(document.RootElement, ref nodes);
        return document.RootElement.Clone();
    }

    private static void Inspect(JsonElement element, ref int nodes)
    {
        if (++nodes > 16_384) throw new DefinitionSchemaImportException("schema-node-bound");
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Length > 128 || !names.Add(property.Name))
                    throw new DefinitionSchemaImportException("schema-member-invalid");
                Inspect(property.Value, ref nodes);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in element.EnumerateArray()) Inspect(item, ref nodes);
    }

    internal static bool IsLocalName(string name) => name.Length is > 0 and <= 256
        && !name.StartsWith('/') && !name.Contains('\\') && !name.Contains(':') && !name.Contains('?')
        && !name.Contains('#') && !name.Contains('%') && name.Split('/').All(part => part is not ("" or "." or "..")
            && part.All(character => character >= ' ' && character != 0x7f));

    private sealed class Importer(IReadOnlyDictionary<string, JsonElement> documents)
    {
        internal List<DefinitionSchemaTransformation> Transformations { get; } = [];
        private int _expandedNodes;
        private int _expandedBytes;
        private static readonly string[] Common = ["$schema", "$id", "$defs", "$ref", "title", "description", "$comment", "default", "examples", "readOnly", "writeOnly", "deprecated", "format"];
        private static readonly string[] ReferenceAnnotations = ["title", "description", "format", "writeOnly", "readOnly", "deprecated"];

        internal void Report(string classification, string code, string document, string location)
        {
            if (location.Length > 4096 || Transformations.Count >= 16_384)
                throw new DefinitionSchemaImportException("schema-transformation-bound");
            var transformation = new DefinitionSchemaTransformation(classification, code, location,
                documents.Keys.TakeWhile(name => name != document).Count());
            if (!Transformations.Contains(transformation)) Transformations.Add(transformation);
        }

        private static string At(string location, string token) => location + "/"
            + token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

        internal JsonObject Field(JsonElement schema, string document, int depth, HashSet<string> stack,
            string location = "", bool secretAncestor = false)
        {
            var resolved = Resolve(schema, document, stack, location);
            schema = resolved.Schema;
            document = resolved.Document;
            location = resolved.Location;
            try
            {
                CheckExpansion(depth);
                if (schema.TryGetProperty("$id", out _)) throw new DefinitionSchemaImportException("schema-resource-scope-unsupported");
                string[] keywords = ["type", "properties", "required", "additionalProperties", "items", "minItems", "maxItems",
                    "uniqueItems", "enum", "const", "minLength", "maxLength", "minimum", "maximum", "exclusiveMinimum",
                    "exclusiveMaximum", "multipleOf", "minProperties", "maxProperties", "oneOf", "dependentRequired", "if", "then", "else"];
                Validate(schema, keywords, document, location);
                bool secret = secretAncestor || schema.TryGetProperty("writeOnly", out JsonElement writeOnly) && writeOnly.ValueKind == JsonValueKind.True;
                var result = new JsonObject
                {
                    ["x-configurator"] = new JsonObject { ["sensitivity"] = secret ? "secret" : "sensitive" }
                };
                foreach (string name in new[] { "title", "description", "format" })
                {
                    if (Annotation(schema, name, name == "description" ? 4096 : 256) is not { } annotation) continue;
                    Charge(JsonSerializer.SerializeToUtf8Bytes(annotation).Length);
                    result[name] = annotation;
                }
                foreach (string name in new[] { "readOnly", "deprecated", "writeOnly" })
                    if (schema.TryGetProperty(name, out JsonElement annotation))
                        result[name] = JsonNode.Parse(annotation.GetRawText());
                foreach (string name in new[] { "type", "required", "enum", "const", "uniqueItems", "dependentRequired" })
                {
                    if (!schema.TryGetProperty(name, out JsonElement value)) continue;
                    Charge(Encoding.UTF8.GetByteCount(value.GetRawText()));
                    result[name] = JsonNode.Parse(value.GetRawText());
                }
                foreach (string bound in new[] { "minLength", "maxLength", "minItems", "maxItems", "minProperties", "maxProperties" })
                    CopyBound(schema, result, bound, integer: true);
                foreach (string bound in new[] { "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf" })
                    CopyBound(schema, result, bound, integer: false);
                if (schema.TryGetProperty("type", out JsonElement type)
                    && (type.ValueKind == JsonValueKind.String && type.GetString() is "integer" or "number"
                        || type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(item =>
                            item.ValueKind == JsonValueKind.String && item.GetString() is "integer" or "number")))
                    Report("converted", "schema-numeric-bounded-domain", document, At(location, "type"));
                bool container = schema.TryGetProperty("type", out JsonElement declaredType) &&
                    (declaredType.ValueKind == JsonValueKind.String && declaredType.GetString() is "object" or "array" ||
                     declaredType.ValueKind == JsonValueKind.Array && declaredType.EnumerateArray().Any(item =>
                         item.ValueKind == JsonValueKind.String && item.GetString() is "object" or "array"));
                if (!secret && (container || schema.TryGetProperty("const", out _)))
                {
                    // Structure and supplied public constants carry no collected answer.
                    // Leaf answers remain sensitive, and writeOnly ancestry never lowers.
                    result["x-configurator"]!["sensitivity"] = "public";
                    Report("converted", container ? "schema-structure-classified-public" : "schema-constant-classified-public",
                        document, At(location, container ? "type" : "const"));
                }
                if (declaredType.ValueKind == JsonValueKind.String && declaredType.GetString() == "null")
                {
                    if (schema.TryGetProperty("enum", out JsonElement nullChoices) &&
                        nullChoices.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.Null) ||
                        schema.TryGetProperty("const", out JsonElement nullConstant) && nullConstant.ValueKind != JsonValueKind.Null)
                        throw new DefinitionSchemaImportException("schema-null-constraint-unsupported");
                    result["type"] = new JsonArray("string", "null");
                    result["enum"] = new JsonArray((JsonNode?)null);
                    Report("converted", "schema-null-type-converted", document, At(location, "type"));
                }
                if (schema.TryGetProperty("oneOf", out _) && declaredType.ValueKind == JsonValueKind.Array &&
                    declaredType.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == "null"))
                    Report("converted", "schema-nullable-variant-domain", document, At(location, "oneOf"));
                if (schema.TryGetProperty("$defs", out JsonElement definitions))
                {
                    if (definitions.ValueKind != JsonValueKind.Object) throw new DefinitionSchemaImportException("schema-object-required");
                    var retainedDefinitions = new JsonObject();
                    foreach (JsonProperty definition in definitions.EnumerateObject())
                        retainedDefinitions[definition.Name] = Field(definition.Value, document, depth + 1, stack,
                            At(At(location, "$defs"), definition.Name), secret);
                    result["$defs"] = retainedDefinitions;
                }
                if (schema.TryGetProperty("properties", out JsonElement properties))
                {
                    if (properties.ValueKind != JsonValueKind.Object || properties.EnumerateObject().Count() > 1024)
                        throw new DefinitionSchemaImportException("schema-field-bound");
                    var fields = new JsonObject();
                    foreach (JsonProperty property in properties.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        Charge(JsonSerializer.SerializeToUtf8Bytes(property.Name).Length + 1);
                        fields[property.Name] = Field(property.Value, document, depth + 1, stack,
                            At(At(location, "properties"), property.Name), secret);
                    }
                    result["properties"] = fields;
                }
                if (schema.TryGetProperty("additionalProperties", out JsonElement additional))
                {
                    if (additional.ValueKind == JsonValueKind.False) result["additionalProperties"] = false;
                    else if (additional.ValueKind == JsonValueKind.Object)
                        result["additionalProperties"] = Field(additional, document, depth + 1, stack, At(location, "additionalProperties"), secret);
                    else
                    {
                        Report("unsupported", "schema-untyped-map-unsupported", document, At(location, "additionalProperties"));
                        throw new DefinitionSchemaImportException("schema-untyped-map-unsupported");
                    }
                }
                if (schema.TryGetProperty("items", out JsonElement items))
                {
                    JsonObject item = Field(items, document, depth + 1, stack, At(location, "items"), secret);
                    result["items"] = item;
                    if (item["x-configurator"]!["sensitivity"]!.GetValue<string>() == "secret")
                        result["x-configurator"]!["sensitivity"] = "secret";
                }
                if (schema.TryGetProperty("oneOf", out JsonElement branches))
                {
                    if (branches.ValueKind != JsonValueKind.Array || branches.GetArrayLength() is < 2 or > 16)
                    {
                        Report("unsupported", "schema-variant-unsupported", document, At(location, "oneOf"));
                        throw new DefinitionSchemaImportException("schema-variant-unsupported");
                    }
                    var variants = new JsonArray();
                    int index = 0;
                    foreach (JsonElement branch in branches.EnumerateArray())
                        variants.Add(Field(branch, document, depth + 1, stack,
                            At(At(location, "oneOf"), (index++).ToString(System.Globalization.CultureInfo.InvariantCulture)), secret));
                    result["oneOf"] = variants;
                }
                foreach (string name in new[] { "if", "then", "else" })
                    if (schema.TryGetProperty(name, out JsonElement condition))
                        result[name] = Condition(condition, document, At(location, name), depth + 1);
                return result;
            }
            catch (DefinitionSchemaImportException exception)
            {
                if (!Transformations.Any(item => item.Classification == "unsupported"))
                    Report("unsupported", exception.Code, document, location);
                throw;
            }
            finally { Release(resolved.Keys, stack); }
        }

        private (JsonElement Schema, string Document, List<string> Keys, string Location) Resolve(JsonElement schema, string document, HashSet<string> stack, string location)
        {
            var keys = new List<string>();
            var annotations = new List<JsonElement>();
            while (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("$ref", out JsonElement reference))
            {
                Validate(schema, [], document, location);
                Report("converted", "schema-reference-expanded", document, At(location, "$ref"));
                if (schema.TryGetProperty("$id", out _))
                    throw new DefinitionSchemaImportException("schema-resource-scope-unsupported");
                if (schema.EnumerateObject().Any(property => ReferenceAnnotations.Contains(property.Name, StringComparer.Ordinal)))
                    annotations.Add(schema);
                string text = reference.ValueKind == JsonValueKind.String ? reference.GetString()! : throw new DefinitionSchemaImportException("schema-reference-invalid");
                string[] parts = text.Split('#');
                if (parts.Length > 2 || parts[0].Length > 0 && !IsLocalName(parts[0]))
                    throw new DefinitionSchemaImportException("schema-reference-unsupported");
                if (parts[0].Length > 0)
                {
                    if (schema.TryGetProperty("$id", out _) || documents[document].TryGetProperty("$id", out _))
                        throw new DefinitionSchemaImportException("schema-resource-scope-unsupported");
                    string prefix = document.Contains('/') ? document[..(document.LastIndexOf('/') + 1)] : "";
                    document = prefix + parts[0];
                }
                if (!documents.TryGetValue(document, out schema)) throw new DefinitionSchemaImportException("schema-reference-missing");
                string fragment = parts.Length == 2 ? parts[1] : "";
                if (fragment.Contains('%') || fragment.Length > 0 && !fragment.StartsWith("/$defs/", StringComparison.Ordinal))
                    throw new DefinitionSchemaImportException("schema-reference-unsupported");
                string key = document + "#" + fragment;
                if (!stack.Add(key) || stack.Count > 32) throw new DefinitionSchemaImportException("schema-reference-cycle");
                keys.Add(key);
                location = fragment;
                bool schemaLocation = true;
                foreach (string token in fragment.Split('/').Skip(1))
                {
                    for (int index = 0; index < token.Length; index++)
                        if (token[index] == '~' && (++index >= token.Length || token[index] is not ('0' or '1')))
                            throw new DefinitionSchemaImportException("schema-reference-unsupported");
                    string name = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                    if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty(name, out schema))
                        throw new DefinitionSchemaImportException("schema-reference-missing");
                    // Map member names are data; only schema locations introduce resource scopes.
                    schemaLocation = !schemaLocation || name is not ("$defs" or "properties");
                    if (schemaLocation && schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("$id", out _))
                        throw new DefinitionSchemaImportException("schema-resource-scope-unsupported");
                }
            }
            if (annotations.Count != 0)
            {
                if (schema.ValueKind != JsonValueKind.Object) throw new DefinitionSchemaImportException("schema-object-required");
                JsonObject merged = JsonNode.Parse(schema.GetRawText())!.AsObject();
                foreach (JsonElement annotation in annotations.AsEnumerable().Reverse())
                    foreach (string name in ReferenceAnnotations)
                        if (annotation.TryGetProperty(name, out JsonElement value))
                        {
                            if (name == "writeOnly" && merged["writeOnly"]?.GetValue<bool>() == true) continue;
                            merged[name] = JsonNode.Parse(value.GetRawText());
                        }
                schema = JsonSerializer.SerializeToElement(merged);
            }
            return (schema, document, keys, location);
        }

        private static void Release(IEnumerable<string> keys, HashSet<string> stack)
        {
            foreach (string key in keys) stack.Remove(key);
        }

        private void CheckExpansion(int depth)
        {
            if (depth > 24 || ++_expandedNodes > 8192) throw new DefinitionSchemaImportException("schema-expansion-bound");
            Charge(128);
        }

        private void Charge(int bytes)
        {
            if (bytes > MaximumBytes - _expandedBytes) throw new DefinitionSchemaImportException("schema-expansion-bound");
            _expandedBytes += bytes;
        }

        private static string? Annotation(JsonElement schema, string name, int maximum)
        {
            string? text = Text(schema, name);
            if (text is null) return null;
            text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            if (text.Length == 0) return null;
            ValidateText(text, maximum);
            return text;
        }

        private static void ValidateText(string text, int maximum)
        {
            try { _ = SetupDefinitionCodec.Text(JsonSerializer.SerializeToElement(text), maximum); }
            catch (SetupDefinitionException) { throw new DefinitionSchemaImportException("schema-annotation-bound"); }
        }

        private JsonObject Condition(JsonElement schema, string document, string location, int depth)
        {
            CheckExpansion(depth);
            Validate(schema, ["required", "properties", "const", "enum"], document, location);
            foreach (string name in new[] { "$ref", "$id", "$defs" })
                if (schema.TryGetProperty(name, out _))
                {
                    Report("unsupported", "schema-condition-unsupported", document, At(location, name));
                    throw new DefinitionSchemaImportException("schema-condition-unsupported");
                }
            var result = new JsonObject();
            foreach (string name in new[] { "required", "const", "enum" })
                if (schema.TryGetProperty(name, out JsonElement value))
                {
                    Charge(Encoding.UTF8.GetByteCount(value.GetRawText()));
                    result[name] = JsonNode.Parse(value.GetRawText());
                }
            if (schema.TryGetProperty("properties", out JsonElement properties))
            {
                if (properties.ValueKind != JsonValueKind.Object) throw new DefinitionSchemaImportException("schema-object-required");
                var fields = new JsonObject();
                foreach (JsonProperty property in properties.EnumerateObject())
                    fields[property.Name] = Condition(property.Value, document, At(At(location, "properties"), property.Name), depth + 1);
                result["properties"] = fields;
            }
            return result;
        }

        private void Validate(JsonElement schema, IEnumerable<string> keywords, string document, string location)
        {
            if (schema.ValueKind != JsonValueKind.Object) throw new DefinitionSchemaImportException("schema-object-required");
            var allowed = new HashSet<string>(Common.Concat(keywords), StringComparer.Ordinal);
            bool unsupported = false;
            foreach (JsonProperty property in schema.EnumerateObject())
            {
                string classification = property.Name switch
                {
                    "default" or "examples" or "$comment" => "discarded",
                    "format" or "readOnly" or "deprecated" => "advisory",
                    "$schema" => "converted",
                    _ => allowed.Contains(property.Name) ? "retained" : "unsupported"
                };
                string code = allowed.Contains(property.Name) ? "schema-" + property.Name.TrimStart('$') + "-" + classification
                    : "schema-keyword-unsupported";
                Report(classification, code, document, At(location, property.Name));
                unsupported |= classification == "unsupported";
            }
            if (unsupported) throw new DefinitionSchemaImportException("schema-keyword-unsupported");
            if (schema.TryGetProperty("$schema", out JsonElement dialect)
                && dialect.GetString() != "https://json-schema.org/draft/2020-12/schema")
                throw new DefinitionSchemaImportException("schema-dialect-unsupported");
            foreach (string name in new[] { "writeOnly", "readOnly", "deprecated" })
                if (schema.TryGetProperty(name, out JsonElement annotation) && annotation.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new DefinitionSchemaImportException("schema-annotation-invalid");
        }

        private static string? Text(JsonElement schema, string name) => schema.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : throw new DefinitionSchemaImportException("schema-annotation-invalid") : null;

        private static void CopyBound(JsonElement schema, JsonObject target, string name, bool integer)
        {
            if (!schema.TryGetProperty(name, out JsonElement value)) return;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out _)
                || integer && (!value.TryGetInt32(out int count) || count < 0))
                throw new DefinitionSchemaImportException("schema-bound-unrepresentable");
            try
            {
                byte[] canonical = DefinitionJson.Canonicalize(value,
                    new Composition.CompositionBudget(Composition.SetupCompositionLimits.Default));
                target[name] = JsonNode.Parse(canonical);
            }
            catch (SetupDefinitionException) { throw new DefinitionSchemaImportException("schema-bound-unrepresentable"); }
        }
    }
}
