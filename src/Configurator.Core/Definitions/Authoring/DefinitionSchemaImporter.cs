using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Core.Definitions.Authoring;

/// <summary>Snapshots translated fields and the named preparation-domain differences requiring review.</summary>
public sealed class DefinitionSchemaImportResult
{
    internal DefinitionSchemaImportResult(JsonElement fields, IEnumerable<string> differences)
    {
        Fields = fields.Clone();
        Differences = Array.AsReadOnly(differences.Order(StringComparer.Ordinal).ToArray());
    }
    public JsonElement Fields { get; }
    public IReadOnlyList<string> Differences { get; }
}

public sealed class DefinitionSchemaImportException : Exception
{
    internal DefinitionSchemaImportException(string code, IEnumerable<string>? differences = null) : base(code)
    {
        Code = code;
        Differences = Array.AsReadOnly((differences ?? []).Order(StringComparer.Ordinal).ToArray());
    }
    public string Code { get; }
    public IReadOnlyList<string> Differences { get; }
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
            foreach ((string name, ReadOnlyMemory<byte> content) in localReferences ?? new Dictionary<string, ReadOnlyMemory<byte>>())
            {
                if (!IsLocalName(name) || documents.Count >= 64 || content.Length > MaximumBytes - bytes)
                    throw new DefinitionSchemaImportException("schema-reference-bound");
                bytes += content.Length;
                documents.Add(name, Parse(content, ref nodes));
            }
            var importer = new Importer(documents);
            JsonObject fields = importer.ObjectFields(documents[""], "", 0, new HashSet<string>(StringComparer.Ordinal));
            if (!acceptBoundedSemantics && importer.Differences.Count != 0)
                throw new DefinitionSchemaImportException("schema-bounded-semantics-required", importer.Differences);
            return new(JsonSerializer.SerializeToElement(fields), importer.Differences);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new DefinitionSchemaImportException("schema-document-invalid");
        }
    }

    private static JsonElement Parse(ReadOnlyMemory<byte> bytes, ref int nodes)
    {
        if (bytes.Length is < 1 or > MaximumBytes) throw new DefinitionSchemaImportException("schema-source-bound");
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        Inspect(document.RootElement, ref nodes);
        return document.RootElement.Clone();
    }

    private static void Inspect(JsonElement element, ref int nodes)
    {
        if (++nodes > 16_384) throw new DefinitionSchemaImportException("schema-node-bound");
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Length > 128 || !names.Add(property.Name.Normalize()))
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
        internal HashSet<string> Differences { get; } = new(StringComparer.Ordinal);
        private int _expandedNodes;
        private int _expandedBytes;
        private static readonly string[] Common = ["$schema", "$id", "$defs", "$ref", "title", "description", "$comment", "default", "examples", "readOnly", "writeOnly", "deprecated", "format"];
        private static readonly string[] ReferenceAnnotations = ["title", "description", "format", "writeOnly", "readOnly", "deprecated"];

        internal JsonObject ObjectFields(JsonElement schema, string document, int depth, HashSet<string> stack)
        {
            var resolved = Resolve(schema, document, stack);
            schema = resolved.Schema;
            document = resolved.Document;
            try
            {
                Validate(schema, ["type", "properties", "required", "additionalProperties"]);
                if (Text(schema, "type") != "object" || !schema.TryGetProperty("additionalProperties", out JsonElement additional)
                    || additional.ValueKind != JsonValueKind.False || !schema.TryGetProperty("properties", out JsonElement properties)
                    || properties.ValueKind != JsonValueKind.Object)
                    throw new DefinitionSchemaImportException("schema-closed-object-required");
                CheckExpansion(depth);
                var required = new HashSet<string>(StringComparer.Ordinal);
                if (schema.TryGetProperty("required", out JsonElement requiredArray))
                {
                    if (requiredArray.ValueKind != JsonValueKind.Array) throw new DefinitionSchemaImportException("schema-required-invalid");
                    foreach (JsonElement item in requiredArray.EnumerateArray())
                        if (item.ValueKind != JsonValueKind.String || !required.Add(item.GetString()!)
                            || !properties.TryGetProperty(item.GetString()!, out _))
                            throw new DefinitionSchemaImportException("schema-required-invalid");
                }
                var fields = new JsonObject();
                foreach (JsonProperty property in properties.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    Charge(JsonSerializer.SerializeToUtf8Bytes(property.Name).Length + 1);
                    if (depth > 0 && DefinitionAuthoringCompiler.FieldId(property.Name) != property.Name)
                        throw new DefinitionSchemaImportException("schema-property-unrepresentable");
                    JsonObject field = Field(property.Value, document, depth + 1, stack, required.Contains(property.Name));
                    if (schema.TryGetProperty("writeOnly", out JsonElement writeOnly) && writeOnly.ValueKind == JsonValueKind.True)
                        field["sensitivity"] = "secret";
                    fields.Add(property.Name, field);
                }
                if (fields.Count > 1024) throw new DefinitionSchemaImportException("schema-field-bound");
                return fields;
            }
            finally { Release(resolved.Keys, stack); }
        }

        private JsonObject Field(JsonElement schema, string document, int depth, HashSet<string> stack, bool required)
        {
            var resolved = Resolve(schema, document, stack);
            schema = resolved.Schema;
            document = resolved.Document;
            try
            {
                CheckExpansion(depth);
                if (schema.TryGetProperty("$id", out _)) throw new DefinitionSchemaImportException("schema-resource-scope-unsupported");
                string? type = Text(schema, "type");
                string[] keywords = type switch
                {
                    "object" => ["type", "properties", "required", "additionalProperties"],
                    "array" => ["type", "items", "minItems", "maxItems"],
                    "string" => ["type", "enum", "minLength", "maxLength"],
                    "integer" or "number" => ["type", "minimum", "maximum"],
                    "boolean" => ["type"],
                    _ => throw new DefinitionSchemaImportException("schema-type-unsupported")
                };
                Validate(schema, keywords);
                var result = new JsonObject
                {
                    ["type"] = type == "array" ? "list" : type,
                    ["required"] = required,
                    ["sensitivity"] = schema.TryGetProperty("writeOnly", out JsonElement writeOnly) && writeOnly.ValueKind == JsonValueKind.True ? "secret" : "sensitive"
                };
                if (Annotation(schema, "title", 256) is { } title)
                {
                    Charge(JsonSerializer.SerializeToUtf8Bytes(title).Length);
                    result["title"] = title;
                }
                string? help = Annotation(schema, "description", 4096);
                if (Text(schema, "format") is { } format)
                    help = (help is null ? "" : help + "; ") + "Target schema format annotation: " + format;
                if (help is not null)
                {
                    ValidateText(help, 4096);
                    Charge(JsonSerializer.SerializeToUtf8Bytes(help).Length);
                    result["help"] = help;
                }
                if (type == "object")
                    result["properties"] = ObjectFields(schema, document, depth + 1, stack);
                else if (type == "array")
                {
                    if (!schema.TryGetProperty("items", out JsonElement items)) throw new DefinitionSchemaImportException("schema-items-required");
                    JsonObject item = Field(items, document, depth + 1, stack, false);
                    string itemType = item["type"]!.GetValue<string>();
                    if (itemType is not ("string" or "boolean" or "integer" or "number" or "object"))
                        throw new DefinitionSchemaImportException("schema-item-type-unsupported");
                    if (item.Any(pair => pair.Key is not ("type" or "sensitivity" or "required" or "title" or "help" or "properties")))
                        throw new DefinitionSchemaImportException("schema-item-constraint-unsupported");
                    result["itemType"] = itemType;
                    if (itemType == "object") result["properties"] = item["properties"]!.DeepClone();
                    if (item["sensitivity"]!.GetValue<string>() == "secret") result["sensitivity"] = "secret";
                    CopyBound(schema, result, "minItems", integer: true);
                    CopyBound(schema, result, "maxItems", integer: true);
                }
                else if (type == "string")
                {
                    if (required) Differences.Add("schema-required-string-whitespace");
                    if (schema.TryGetProperty("enum", out JsonElement choices))
                    {
                        if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() is < 1 or > 256
                            || choices.EnumerateArray().Any(choice => choice.ValueKind != JsonValueKind.String)
                            || choices.EnumerateArray().Select(choice => choice.GetString()).Distinct(StringComparer.Ordinal).Count() != choices.GetArrayLength())
                            throw new DefinitionSchemaImportException("schema-enum-unsupported");
                        Charge(Encoding.UTF8.GetByteCount(choices.GetRawText()));
                        result["type"] = "enum";
                        result["choices"] = JsonNode.Parse(choices.GetRawText());
                    }
                    foreach (string bound in new[] { "minLength", "maxLength" })
                        if (schema.TryGetProperty(bound, out _))
                        {
                            Differences.Add("schema-string-utf16-length");
                            CopyBound(schema, result, bound, integer: true);
                        }
                }
                else if (type is "integer" or "number")
                {
                    Differences.Add("schema-numeric-bounded-domain");
                    CopyBound(schema, result, "minimum", integer: false);
                    CopyBound(schema, result, "maximum", integer: false);
                }
                foreach ((string minimum, string maximum) in new[] { ("minLength", "maxLength"), ("minItems", "maxItems"), ("minimum", "maximum") })
                    if (schema.TryGetProperty(minimum, out JsonElement lower) && schema.TryGetProperty(maximum, out JsonElement upper)
                        && lower.GetDecimal() > upper.GetDecimal())
                        throw new DefinitionSchemaImportException("schema-bound-invalid");
                return result;
            }
            finally { Release(resolved.Keys, stack); }
        }

        private (JsonElement Schema, string Document, List<string> Keys) Resolve(JsonElement schema, string document, HashSet<string> stack)
        {
            var keys = new List<string>();
            var annotations = new List<JsonElement>();
            while (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("$ref", out JsonElement reference))
            {
                Validate(schema, []);
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
            return (schema, document, keys);
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

        private static void Validate(JsonElement schema, IEnumerable<string> keywords)
        {
            if (schema.ValueKind != JsonValueKind.Object) throw new DefinitionSchemaImportException("schema-object-required");
            var allowed = new HashSet<string>(Common.Concat(keywords), StringComparer.Ordinal);
            if (schema.EnumerateObject().Any(property => !allowed.Contains(property.Name)))
                throw new DefinitionSchemaImportException("schema-keyword-unsupported");
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
