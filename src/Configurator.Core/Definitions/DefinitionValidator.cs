namespace Configurator.Core.Definitions;

using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Secrets;

public static class DefinitionValidator
{
    public static DefinitionValidationResult Validate(Definition definition, AnswerSnapshot answers,
        CancellationToken cancellationToken = default) =>
        DefinitionImportWork.CompleteSynchronously(DefinitionValidationSession.RunAsync(definition, answers, false, cancellationToken));

    /// <summary>Defers missing secret leaves only. Supplied invalid values remain blocking.</summary>
    public static DefinitionValidationResult ValidateDraft(Definition definition, AnswerSnapshot answers,
        CancellationToken cancellationToken = default) =>
        DefinitionImportWork.CompleteSynchronously(DefinitionValidationSession.RunAsync(definition, answers, true, cancellationToken));

    internal static ValueTask<DefinitionValidationResult> ValidateSuppliedAsync(Definition definition,
        DefinitionImportEvidence evidence, DefinitionImportWork work, Func<DefinitionDiagnostic, ValueTask> diagnosticObserved,
        CancellationToken cancellationToken) =>
        DefinitionValidationSession.RunAsync(definition, evidence.Answers, false, cancellationToken, evidence, work, diagnosticObserved);

    internal static ValueTask<DefinitionValidationResult> ValidateImportAsync(Definition definition, AnswerSnapshot answers,
        DefinitionImportWork work, CancellationToken cancellationToken) =>
        DefinitionValidationSession.RunAsync(definition, answers, false, cancellationToken, work: work);

    internal static async ValueTask<JsonElement> ElementAsync(JsonNode? value, DefinitionImportWork? work)
    {
        if (work is null) return Element(value);
        CancellationToken cancellationToken = work.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (value is not (JsonObject or JsonArray)) return Element(value);
        Func<CancellationToken, ValueTask>? cooperativeYield = work.CooperativeYield;
        int materializedNodes = 0;
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
                await WriteAsync(writer, value);
            using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
            return document.RootElement.Clone();
        }
        finally { buffer.Clear(); }

        async ValueTask WriteAsync(Utf8JsonWriter writer, JsonNode? node)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Materialization is scheduling work, not another semantic evaluation.
            if (++materializedNodes % 1024 == 0 && cooperativeYield is not null)
            {
                await cooperativeYield(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            switch (node)
            {
                case JsonObject mapping:
                    writer.WriteStartObject();
                    foreach ((string name, JsonNode? child) in mapping)
                    {
                        writer.WritePropertyName(name);
                        await WriteAsync(writer, child);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonArray sequence:
                    writer.WriteStartArray();
                    foreach (JsonNode? child in sequence) await WriteAsync(writer, child);
                    writer.WriteEndArray();
                    break;
                case null: writer.WriteNullValue(); break;
                default: node.WriteTo(writer); break;
            }
        }
    }

    private static JsonElement Element(JsonNode? value)
    {
        using JsonDocument document = JsonDocument.Parse(value?.ToJsonString() ?? "null");
        return document.RootElement.Clone();
    }

    /// <summary>Checks a supplied value against an already admitted node, including local constraints and secret policy.</summary>
    public static bool ValueValid(DefinitionNode schema, JsonElement value)
    {
        if (!DefinitionImportWork.CompleteSynchronously(LocalValueValidAsync(schema, value))) return false;
        if (value.ValueKind == JsonValueKind.Null) return true;
        if (schema.Variants.Count > 0)
            return SelectVariant(schema, value) is { } branch && ValueValid(branch, value);
        if (schema.Type == DefinitionNodeType.Mapping)
        {
            if (schema.AdditionalProperties is null && value.EnumerateObject().Any(member => !schema.Properties.ContainsKey(member.Name))) return false;
            foreach ((string name, DefinitionNode child) in schema.Properties)
            {
                bool present = value.TryGetProperty(name, out JsonElement supplied);
                if (!present && schema.Required.Contains(name, StringComparer.Ordinal) || present && !ValueValid(child, supplied)) return false;
            }
            if (schema.AdditionalProperties is { } map)
                foreach (JsonProperty entry in value.EnumerateObject())
                    if (!schema.Properties.ContainsKey(entry.Name) && !ValueValid(map, entry.Value)) return false;
        }
        if (schema.Type == DefinitionNodeType.Sequence)
            foreach (JsonElement item in value.EnumerateArray())
                if (!ValueValid(schema.Items!, item)) return false;
        return true;
    }

    /// <summary>Returns the declared branch selected by a supplied discriminator, without inventing a branch.</summary>
    public static DefinitionNode? SelectVariant(DefinitionNode schema, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.Discriminator is null || value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(schema.Discriminator, out JsonElement discriminator)) return null;
        return DiscriminatedBranch(schema, discriminator);
    }

    internal static DefinitionNode? DiscriminatedBranch(DefinitionNode schema, JsonElement discriminator) =>
        schema.Variants.FirstOrDefault(branch =>
            JsonElement.DeepEquals(branch.Properties[schema.Discriminator!].Keywords.GetProperty("const"), discriminator));

    internal static async ValueTask<bool> LocalValueValidAsync(DefinitionNode schema, JsonElement value, bool complete = true,
        DefinitionImportWork? work = null)
    {
        if (work is not null) await work.VisitAsync();
        bool compatible = value.ValueKind == JsonValueKind.Null ? schema.IsNullable : schema.Type switch
        {
            DefinitionNodeType.Text => value.ValueKind == JsonValueKind.String,
            DefinitionNodeType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            DefinitionNodeType.WholeNumber => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            DefinitionNodeType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            DefinitionNodeType.Mapping => value.ValueKind == JsonValueKind.Object,
            DefinitionNodeType.Sequence => value.ValueKind == JsonValueKind.Array,
            _ => false
        };
        if (!compatible) return false;
        JsonElement keywords = schema.Keywords;
        if (keywords.TryGetProperty("const", out JsonElement fixedValue) && !JsonElement.DeepEquals(value, fixedValue)) return false;
        if (keywords.TryGetProperty("enum", out JsonElement choices))
        {
            bool found = false;
            foreach (JsonElement choice in choices.EnumerateArray())
            {
                if (work is not null) await work.VisitAsync();
                if (!JsonElement.DeepEquals(value, choice)) continue;
                found = true;
                break;
            }
            if (!found) return false;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            if (schema.Secret is { } secret && SecretValueValidator.Validate(secret, text).Count != 0) return false;
            int length = text.EnumerateRunes().Count();
            if (keywords.TryGetProperty("minLength", out JsonElement minimum) && length < minimum.GetInt32()
                || keywords.TryGetProperty("maxLength", out JsonElement maximum) && length > maximum.GetInt32()
                || keywords.TryGetProperty("x-configurator", out JsonElement extension)
                    && extension.TryGetProperty("notBlank", out JsonElement notBlank) && notBlank.GetBoolean() && string.IsNullOrWhiteSpace(text)) return false;
            if (keywords.TryGetProperty("x-configurator", out JsonElement formatMetadata) &&
                formatMetadata.TryGetProperty("assertFormat", out JsonElement asserted) && asserted.GetBoolean() &&
                !FormatMatches(keywords.GetProperty("format").GetString()!, text)) return false;
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDecimal(out decimal number)
                || keywords.TryGetProperty("minimum", out JsonElement minimum) && number < minimum.GetDecimal()
                || keywords.TryGetProperty("maximum", out JsonElement maximum) && number > maximum.GetDecimal()
                || keywords.TryGetProperty("exclusiveMinimum", out JsonElement exclusiveMinimum) && number <= exclusiveMinimum.GetDecimal()
                || keywords.TryGetProperty("exclusiveMaximum", out JsonElement exclusiveMaximum) && number >= exclusiveMaximum.GetDecimal()
                || keywords.TryGetProperty("multipleOf", out JsonElement multiple) && number % multiple.GetDecimal() != 0) return false;
        }
        if (value.ValueKind == JsonValueKind.Array && keywords.TryGetProperty("uniqueItems", out JsonElement unique) &&
            unique.GetBoolean())
        {
            var seen = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (work is not null) await work.VisitAsync();
                byte[] canonical = DefinitionJson.Canonicalize(item, keyPolicy: CompositionKeyPolicy.Exact);
                string identity;
                try { identity = Convert.ToHexString(SHA256.HashData(canonical)); }
                finally { CryptographicOperations.ZeroMemory(canonical); }
                if (!seen.TryGetValue(identity, out List<JsonElement>? bucket)) seen.Add(identity, bucket = []);
                if (bucket.Any(previous => JsonElement.DeepEquals(previous, item))) return false;
                bucket.Add(item);
            }
        }
        if (complete && value.ValueKind == JsonValueKind.Array
            && (keywords.TryGetProperty("minItems", out JsonElement minItems) && value.GetArrayLength() < minItems.GetInt32()
                || keywords.TryGetProperty("maxItems", out JsonElement maxItems) && value.GetArrayLength() > maxItems.GetInt32()
                || await DuplicateIndexAsync(schema, value, work) >= 0)) return false;
        if (value.ValueKind == JsonValueKind.Object && !PropertyCountValid(schema, value.EnumerateObject().Count(), complete)) return false;
        return true;
    }

    internal static bool PropertyCountValid(DefinitionNode schema, int count, bool complete = true) =>
        (!complete || !schema.Keywords.TryGetProperty("minProperties", out JsonElement minimum) || count >= minimum.GetInt32()) &&
        (!schema.Keywords.TryGetProperty("maxProperties", out JsonElement maximum) || count <= maximum.GetInt32());

    private static bool FormatMatches(string format, string text)
    {
        if (format == "hostname") return Hostname(text);
        if (format == "email-address")
        {
            int separator = text.IndexOf('@');
            if (separator is < 1 or > 64 || separator != text.LastIndexOf('@') || text.Length > 254) return false;
            string local = text[..separator];
            return local[0] != '.' && local[^1] != '.' && !local.Contains("..", StringComparison.Ordinal) &&
                local.All(character => char.IsAsciiLetterOrDigit(character) ||
                    ".!#$%&'*+-/=?^_`{|}~".Contains(character)) && Hostname(text[(separator + 1)..]);
        }
        if (text.Any(character => character <= ' ' || character >= 127) ||
            !Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || !uri.IsWellFormedOriginalString()) return false;
        return format == "uri" || format == "http-url" && (uri.Scheme is "http" or "https") &&
            uri.Host.Length != 0 && uri.UserInfo.Length == 0;

        static bool Hostname(string host)
        {
            if (host.EndsWith('.')) host = host[..^1];
            return host.Length is > 0 and <= 253 && host.Split('.').All(label =>
                label.Length is > 0 and <= 63 && char.IsAsciiLetterOrDigit(label[0]) &&
                char.IsAsciiLetterOrDigit(label[^1]) &&
                label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
        }
    }

    internal static async ValueTask<int> DuplicateIndexAsync(DefinitionNode schema, JsonElement value, DefinitionImportWork? work = null)
    {
        if (value.ValueKind != JsonValueKind.Array || !schema.Keywords.TryGetProperty("x-configurator", out var metadata)
            || !metadata.TryGetProperty("uniqueBy", out var uniqueBy)) return -1;
        DefinitionPath path = DefinitionPath.Parse(uniqueBy.GetString()!);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (work is not null) await work.VisitAsync();
            JsonElement key = item;
            foreach (string name in path.Segments)
            {
                if (work is not null) await work.VisitAsync();
                if (key.ValueKind != JsonValueKind.Object || !key.TryGetProperty(name, out key)) return -1;
            }
            if (key.ValueKind != JsonValueKind.String) return -1;
            if (!keys.Add(key.GetString()!)) return index;
            index++;
        }
        return -1;
    }

    internal static bool ConditionMatches(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values) =>
        EvaluateCondition(condition, values, null) == true;

    internal static bool? EvaluateCondition(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string>? unresolved, JsonElement? item = null, IReadOnlySet<string>? itemUnknown = null,
        IReadOnlySet<DefinitionPath>? deferredLocations = null, DefinitionPath? itemLocation = null) =>
        DefinitionImportWork.CompleteSynchronously(
            EvaluateConditionAsync(condition, values, unresolved, item, itemUnknown, deferredLocations, itemLocation));

    internal static async ValueTask<bool?> EvaluateConditionAsync(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string>? unresolved, JsonElement? item = null, IReadOnlySet<string>? itemUnknown = null,
        IReadOnlySet<DefinitionPath>? deferredLocations = null, DefinitionPath? itemLocation = null,
        DefinitionImportEvidence? observations = null, DefinitionImportWork? work = null,
        DefinitionPath? observationBase = null, IReadOnlySet<DefinitionPath>? invalidLocations = null)
    {
        if (work is not null) await work.VisitAsync();
        string operation = condition.GetProperty("op").GetString()!;
        if (operation is "all" or "any" or "not")
        {
            var results = new List<bool?>();
            foreach (JsonElement operand in condition.GetProperty("operands").EnumerateArray())
                results.Add(await EvaluateConditionAsync(operand, values, unresolved, item, itemUnknown,
                    deferredLocations, itemLocation, observations, work, observationBase, invalidLocations));
            if (operation == "not") return !results[0];
            return operation == "all" ? results.Contains(false) ? false : results.Contains(null) ? null : true
                : results.Contains(true) ? true : results.Contains(null) ? null : false;
        }
        JsonElement reference = condition.GetProperty("reference");
        DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
        if (observations is not null)
        {
            DefinitionPath? prefix = reference.GetProperty("scope").GetString() == "item" ? itemLocation : observationBase;
            DefinitionPath concrete = prefix is null ? path : DefinitionPath.FromSegments(prefix.Segments.Concat(path.Segments));
            if (observations.State(concrete) == DefinitionImportObservationState.Unobserved ||
                invalidLocations?.Any(location => DefinitionImportProjection.IsPrefix(location, concrete)) == true)
                return null;
            if (operation != "exists" && !observations.Complete(concrete)) return null;
        }
        if (reference.GetProperty("scope").GetString() == "root" && unresolved is not null
            && (path.Segments.Count == 0 ? unresolved.Count != 0 : unresolved.Contains(path.Segments[0]))) return null;
        if (reference.GetProperty("scope").GetString() == "item" && itemUnknown is not null
            && (path.Segments.Count == 0 ? itemUnknown.Count != 0 : itemUnknown.Contains(path.Segments[0]))) return null;
        if (reference.GetProperty("scope").GetString() == "item" && deferredLocations is not null && itemLocation is not null)
        {
            DefinitionPath concrete = DefinitionPath.FromSegments(itemLocation.Segments.Concat(path.Segments));
            if (deferredLocations.Any(location => location.Segments.Take(Math.Min(location.Segments.Count, concrete.Segments.Count))
                .SequenceEqual(concrete.Segments.Take(Math.Min(location.Segments.Count, concrete.Segments.Count)), StringComparer.Ordinal)))
                return null;
        }
        bool present = TryResolveReference(reference, values, item, out JsonElement value);
        if (operation == "exists") return present;
        if (!present) return false;
        return operation switch
        {
            "equals" => JsonElement.DeepEquals(value, condition.GetProperty("value")),
            "notEquals" => !JsonElement.DeepEquals(value, condition.GetProperty("value")),
            "in" => condition.GetProperty("values").EnumerateArray().Any(candidate => JsonElement.DeepEquals(value, candidate)),
            _ => throw new DefinitionException("condition-operation")
        };
    }

    internal static bool TryResolveReference(JsonElement reference, IReadOnlyDictionary<string, JsonElement> values,
        JsonElement? item, out JsonElement value)
    {
        DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
        int start = 0;
        if (reference.GetProperty("scope").GetString() == "item")
        {
            if (item is null) { value = default; return false; }
            value = item.Value;
        }
        else if (path.Segments.Count == 0)
        {
            value = JsonSerializer.SerializeToElement(values);
            return true;
        }
        else
        {
            if (!values.TryGetValue(path.Segments[0], out value)) return false;
            start = 1;
        }
        for (int index = start; index < path.Segments.Count; index++)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(path.Segments[index], out value)) return false;
            }
            else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int position)
                && position < value.GetArrayLength()) value = value[position];
            else { value = default; return false; }
        }
        return true;
    }

    internal static IEnumerable<string> References(JsonElement condition)
        => ScopedReferences(condition, "root");

    internal static IEnumerable<string> SchemaReferences(DefinitionNode schema, string scope) =>
        DefinitionImportWork.CompleteSynchronously(SchemaReferencesAsync(schema, scope));

    internal static async ValueTask<IEnumerable<string>> SchemaReferencesAsync(DefinitionNode schema, string scope,
        DefinitionImportWork? work = null)
    {
        if (work is not null) await work.VisitAsync();
        IEnumerable<string> own = schema.Keywords.TryGetProperty("x-configurator", out var extension)
            && extension.TryGetProperty("applicableWhen", out var condition) ? await ScopedReferencesAsync(condition, scope, work) : [];
        IEnumerable<DefinitionNode> children = schema.Properties.Values.Concat(schema.Variants)
            .Concat(schema.AdditionalProperties is null ? [] : new[] { schema.AdditionalProperties })
            .Concat(scope == "root" && schema.Items is not null ? new[] { schema.Items } : []);
        var references = new List<string>(own);
        foreach (DefinitionNode child in children)
            references.AddRange(await SchemaReferencesAsync(child, scope, work));
        return references.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static IEnumerable<string> ScopedReferences(JsonElement condition, string scope) =>
        DefinitionImportWork.CompleteSynchronously(ScopedReferencesAsync(condition, scope));

    private static async ValueTask<IEnumerable<string>> ScopedReferencesAsync(JsonElement condition, string scope,
        DefinitionImportWork? work = null)
    {
        if (work is not null) await work.VisitAsync();
        if (condition.TryGetProperty("reference", out JsonElement reference))
        {
            DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
            if (reference.GetProperty("scope").GetString() == scope && path.Segments.Count != 0)
                return [path.Segments[0]];
            return [];
        }
        var references = new List<string>();
        foreach (JsonElement operand in condition.GetProperty("operands").EnumerateArray())
            references.AddRange(await ScopedReferencesAsync(operand, scope, work));
        return references.ToArray();
    }

    internal static ValueTask<bool?> EvaluateGuardAsync(JsonElement guard, JsonObject values, IReadOnlySet<string> unknown,
        DefinitionImportEvidence? observations = null, DefinitionImportWork? work = null,
        DefinitionPath? observationBase = null, IReadOnlySet<DefinitionPath>? invalidLocations = null) =>
        EvaluateGuardAsync(guard, values.ToDictionary(property => property.Key, property => Element(property.Value),
            StringComparer.Ordinal), unknown, observations, work, observationBase, invalidLocations);

    internal static ValueTask<bool?> EvaluateGuardAsync(JsonElement guard, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string> unknown, DefinitionImportEvidence? observations = null, DefinitionImportWork? work = null,
        DefinitionPath? observationBase = null, IReadOnlySet<DefinitionPath>? invalidLocations = null)
    {
        var operands = new JsonArray();
        JsonObject Reference(string name) => new()
        {
            ["scope"] = "root",
            ["path"] = DefinitionPath.FromSegments([name]).ToPointer()
        };
        if (guard.TryGetProperty("required", out var required))
            foreach (JsonElement name in required.EnumerateArray())
                operands.Add(new JsonObject { ["op"] = "exists", ["reference"] = Reference(name.GetString()!) });
        if (guard.TryGetProperty("properties", out var properties))
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                bool constant = property.Value.TryGetProperty("const", out var expected);
                var assertion = new JsonObject
                {
                    ["op"] = constant ? "equals" : "in",
                    ["reference"] = Reference(property.Name),
                    [constant ? "value" : "values"] = JsonNode.Parse(
                        (constant ? expected : property.Value.GetProperty("enum")).GetRawText())
                };
                operands.Add(new JsonObject
                {
                    ["op"] = "any",
                    ["operands"] = new JsonArray(
                    new JsonObject
                    {
                        ["op"] = "not",
                        ["operands"] = new JsonArray(
                        new JsonObject { ["op"] = "exists", ["reference"] = Reference(property.Name) })
                    }, assertion)
                });
            }
        return EvaluateConditionAsync(Element(new JsonObject { ["op"] = "all", ["operands"] = operands }), values, unknown,
            observations: observations, work: work, observationBase: observationBase, invalidLocations: invalidLocations);
    }
}
