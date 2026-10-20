namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Configurator.Core.Composition;

public sealed class SetupFieldState
{
    internal SetupFieldState(SetupFieldDefinition field, bool active, bool required, bool provided, bool error)
    {
        Definition = field;
        IsActive = active;
        IsRequired = required;
        IsProvided = provided;
        HasError = error;
    }

    public SetupFieldDefinition Definition { get; }
    public string Id => Definition.Id;
    public bool IsActive { get; }
    public bool IsRequired { get; }
    public bool IsProvided { get; }
    public bool HasError { get; }
}

public sealed class DefinitionValidationResult
{
    internal DefinitionValidationResult(IEnumerable<SetupFieldState> fields,
        IEnumerable<DefinitionDiagnostic> diagnostics, Dictionary<string, JsonElement> values,
        IReadOnlySet<string>? deferred = null, IReadOnlySet<string>? unresolved = null,
        IEnumerable<DefinitionNodeState>? nodes = null)
    {
        Fields = Array.AsReadOnly(fields.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.OrderBy(item => item.FieldId, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray());
        Values = new ReadOnlyDictionary<string, JsonElement>(values);
        DeferredFields = deferred ?? new HashSet<string>(StringComparer.Ordinal);
        UnresolvedFields = unresolved ?? new HashSet<string>(StringComparer.Ordinal);
        Nodes = Array.AsReadOnly(nodes?.ToArray() ?? []);
    }

    public IReadOnlyList<SetupFieldState> Fields { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<DefinitionNodeState> Nodes { get; }
    public bool IsReady => Diagnostics.Count == 0;
    internal IReadOnlyDictionary<string, JsonElement> Values { get; }
    internal IReadOnlySet<string> DeferredFields { get; }
    internal IReadOnlySet<string> UnresolvedFields { get; }
    public override string ToString() => $"{nameof(DefinitionValidationResult)}:Ready={IsReady}:Diagnostics={Diagnostics.Count}";
}

/// <summary>Value-free validation facts for one literal editor path, including required-work obligations.</summary>
public sealed class DefinitionNodeState
{
    internal DefinitionNodeState(string fieldId, IReadOnlyList<string> relativePath,
        bool active, bool required, bool provided, bool valid, bool obligation, bool obligationReady, bool missing,
        SetupFieldSensitivity sensitivity)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
        IsActive = active;
        IsRequired = active && required;
        IsProvided = provided;
        IsReady = valid;
        HasError = active && !valid;
        IsObligation = active && obligation;
        IsObligationReady = active && obligationReady;
        IsMissing = missing;
        Sensitivity = sensitivity;
    }

    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
    public bool IsActive { get; }
    public bool IsRequired { get; }
    public bool IsProvided { get; }
    public bool IsReady { get; }
    public bool HasError { get; }
    public bool IsObligation { get; }
    public bool IsObligationReady { get; }
    public bool IsMissing { get; }
    public SetupFieldSensitivity Sensitivity { get; }
}

public static class DefinitionValidator
{
    public static DefinitionValidationResult Validate(SetupDefinition definition, SetupAnswerSnapshot answers)
        => ValidateCore(definition, answers, draft: false);

    /// <summary>Validates draft answers, deferring only empty or absent secret leaves. Ordinary answer errors remain blocking.</summary>
    public static DefinitionValidationResult ValidateDraft(SetupDefinition definition, SetupAnswerSnapshot answers)
        => ValidateCore(definition, answers, draft: true);

    private static DefinitionValidationResult ValidateCore(SetupDefinition definition, SetupAnswerSnapshot answers, bool draft)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        var definitions = definition.Fields.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var active = new Dictionary<string, bool>(StringComparer.Ordinal);
        var effective = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var diagnostics = new List<DefinitionDiagnostic>();
        var deferred = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        if (answers.Values.Keys.Any(key => !definitions.ContainsKey(key)))
            diagnostics.Add(new("field-unknown", ""));
        foreach (SetupFieldDefinition field in definition.Fields) Activate(field);
        void Activate(SetupFieldDefinition field)
        {
            if (active.ContainsKey(field.Id)) return;
            foreach (string name in new[] { "activeWhen", "requiredWhen" })
                if (field.Data.TryGetProperty(name, out var condition))
                    foreach (string reference in References(condition)) Activate(definitions[reference]);
            bool? activation = field.Data.TryGetProperty("activeWhen", out var expression)
                ? EvaluateCondition(expression, effective, draft ? UnknownFields() : null) : true;
            if (activation is null) unresolved.Add(field.Id);
            bool enabled = activation != false;
            active.Add(field.Id, enabled);
            if (!enabled) return;
            if (answers.Values.TryGetValue(field.Id, out var value)) effective.Add(field.Id, value);
            else if (field.Default is { } fallback) effective.Add(field.Id, fallback);
            if (draft)
            {
                var missingLeaves = new HashSet<string>(StringComparer.Ordinal);
                bool present = effective.TryGetValue(field.Id, out var answer);
                _ = ValueValid(field.Data, answer, present, missingLeaves, field.Id, false);
                if (missingLeaves.Count > 0) deferred.Add(field.Id);
                if (!field.Required && Missing(present, answer) && field.Data.TryGetProperty("requiredWhen", out var requiredWhen)
                    && EvaluateCondition(requiredWhen, effective, UnknownFields()) is null)
                    unresolved.Add(field.Id);
            }
        }

        HashSet<string> UnknownFields() => deferred.Concat(unresolved).ToHashSet(StringComparer.Ordinal);

        var states = new List<SetupFieldState>();
        var nodes = new List<DefinitionNodeState>();
        foreach (SetupFieldDefinition field in definition.Fields)
        {
            bool enabled = active[field.Id];
            bool? conditionalRequired = field.Data.TryGetProperty("requiredWhen", out var condition)
                ? EvaluateCondition(condition, effective, draft ? UnknownFields() : null) : false;
            if (draft && enabled && !field.Required && conditionalRequired is null
                && Missing(effective.TryGetValue(field.Id, out var candidate), candidate)) unresolved.Add(field.Id);
            bool required = enabled && (field.Required || conditionalRequired == true);
            bool present = effective.TryGetValue(field.Id, out var value);
            var missingLeaves = draft ? new HashSet<string>(StringComparer.Ordinal) : null;
            bool valid = draft ? ValueValid(field.Data, value, present, missingLeaves, field.Id, false)
                : !present || ValueValid(field.Data, value);
            bool invalid = enabled && present && !valid;
            bool missing = required && (!present || value.ValueKind == JsonValueKind.Null
                || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));
            if (draft && missingLeaves!.Contains(field.Id)) missing = false;
            if (draft && unresolved.Contains(field.Id)) missing = false;
            if (invalid)
            {
                AddInvalidDiagnostics(field.Data, value, field.Id, [], diagnostics,
                    draft, inheritedSecret: false);
            }
            if (missing) diagnostics.Add(new("value-required", field.Id));
            states.Add(new(field, enabled, required, present, invalid || missing));
            CollectNodes(field.Data, value, present, field.Id, [], enabled, required,
                relevant: true, SetupFieldSensitivity.Public, nodes);
        }
        return new(states, diagnostics, effective, deferred, unresolved, nodes);
    }

    private static void CollectNodes(JsonElement schema, JsonElement value, bool present,
        string fieldId, string[] path, bool active, bool required, bool relevant,
        SetupFieldSensitivity inherited, List<DefinitionNodeState> nodes)
    {
        SetupFieldSensitivity declared = schema.GetProperty("sensitivity").GetString() switch
        {
            "secret" => SetupFieldSensitivity.Secret,
            "sensitive" => SetupFieldSensitivity.Sensitive,
            _ => SetupFieldSensitivity.Public
        };
        var sensitivity = (SetupFieldSensitivity)Math.Max((int)inherited, (int)declared);
        string type = schema.GetProperty("type").GetString()!;
        int start = nodes.Count;
        bool requiredHere = relevant && required;
        bool valid = !relevant || ValueValid(schema, value, present, null, "", inherited == SetupFieldSensitivity.Secret)
            && (!requiredHere || !Missing(present, value));
        if (type == "object")
        {
            bool childrenRelevant = relevant && (required || present && value.ValueKind == JsonValueKind.Object);
            foreach (JsonElement child in schema.GetProperty("properties").EnumerateArray())
            {
                string id = child.GetProperty("id").GetString()!;
                JsonElement childValue = default;
                bool childPresent = present && value.ValueKind == JsonValueKind.Object && value.TryGetProperty(id, out childValue);
                CollectNodes(child, childValue, childPresent, fieldId, [.. path, id], active,
                    child.TryGetProperty("required", out var needed) && needed.GetBoolean(),
                    childrenRelevant, sensitivity, nodes);
            }
        }
        else if (type == "list" && present && value.ValueKind == JsonValueKind.Array)
        {
            JsonElement itemSchema = ItemSchema(schema);
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
                CollectNodes(itemSchema, item, present: true, fieldId,
                    [.. path, (index++).ToString(CultureInfo.InvariantCulture)],
                    active, required: false, relevant, sensitivity, nodes);
        }
        bool obligation = requiredHere && (type != "object" || !nodes.Skip(start).Any(node => node.IsObligation));
        bool obligationReady = type switch
        {
            "object" => present && value.ValueKind == JsonValueKind.Object,
            "list" => present && value.ValueKind == JsonValueKind.Array &&
                (!schema.TryGetProperty("minItems", out JsonElement minimum) || value.GetArrayLength() >= minimum.GetInt32()) &&
                (!schema.TryGetProperty("maxItems", out JsonElement maximum) || value.GetArrayLength() <= maximum.GetInt32()),
            _ => valid
        };
        var effectiveSensitivity = (SetupFieldSensitivity)Math.Max((int)inherited,
            (int)SetupFieldDefinition.EffectiveSensitivity(schema));
        nodes.Insert(start, new(fieldId, path, active, requiredHere, present, valid, obligation, obligationReady,
            Missing(present, value), effectiveSensitivity));
    }

    private static JsonElement ItemSchema(JsonElement list) => JsonSerializer.SerializeToElement(new
    {
        type = list.GetProperty("itemType").GetString(),
        sensitivity = "public",
        properties = list.TryGetProperty("properties", out JsonElement properties)
            ? properties : JsonSerializer.SerializeToElement(Array.Empty<object>())
    });

    private static void AddInvalidDiagnostics(JsonElement schema, JsonElement value, string fieldId,
        string[] path, List<DefinitionDiagnostic> diagnostics, bool draft, bool inheritedSecret)
    {
        int before = diagnostics.Count;
        string type = schema.GetProperty("type").GetString()!;
        bool secret = inheritedSecret || schema.GetProperty("sensitivity").GetString() == "secret";
        if (type == "object" && value.ValueKind == JsonValueKind.Object)
        {
            JsonElement[] properties = schema.GetProperty("properties").EnumerateArray().ToArray();
            if (value.EnumerateObject().Any(property => !properties.Any(child =>
                    child.GetProperty("id").GetString() == property.Name)))
                diagnostics.Add(new("value-invalid", fieldId, path));
            foreach (JsonElement child in properties)
            {
                string id = child.GetProperty("id").GetString()!;
                bool present = value.TryGetProperty(id, out JsonElement childValue);
                bool required = child.TryGetProperty("required", out var needed) && needed.GetBoolean();
                bool missing = required && Missing(present, childValue);
                bool deferred = draft && (secret || child.GetProperty("sensitivity").GetString() == "secret")
                    && child.GetProperty("type").GetString() is not ("object" or "list")
                    && Missing(present, childValue);
                if (deferred) continue;
                if (missing) diagnostics.Add(new("value-required", fieldId, [.. path, id]));
                else if (present && !ValueValid(child, childValue))
                    AddInvalidDiagnostics(child, childValue, fieldId, [.. path, id], diagnostics, draft, secret);
            }
        }
        else if (type == "list" && value.ValueKind == JsonValueKind.Array)
        {
            int count = value.GetArrayLength();
            if (schema.TryGetProperty("minItems", out var minimum) && count < minimum.GetInt32()
                || schema.TryGetProperty("maxItems", out var maximum) && count > maximum.GetInt32())
                diagnostics.Add(new("value-invalid", fieldId, path));
            int duplicate = DuplicateIndex(schema, value);
            if (duplicate >= 0)
                diagnostics.Add(new("value-duplicate", fieldId,
                    [.. path, duplicate.ToString(CultureInfo.InvariantCulture),
                        .. schema.GetProperty("uniqueBy").EnumerateArray().Select(segment => segment.GetString()!)]));
            JsonElement itemSchema = ItemSchema(schema);
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                bool deferred = draft && secret && itemSchema.GetProperty("type").GetString() != "object"
                    && Missing(present: true, item);
                if (!deferred && !ValueValid(itemSchema, item))
                    AddInvalidDiagnostics(itemSchema, item, fieldId,
                        [.. path, index.ToString(CultureInfo.InvariantCulture)], diagnostics, draft, secret);
                index++;
            }
        }
        if (diagnostics.Count == before &&
            !(type == "object" && value.ValueKind == JsonValueKind.Object ||
              type == "list" && value.ValueKind == JsonValueKind.Array))
            diagnostics.Add(new("value-invalid", fieldId, path));
    }

    private static bool Missing(bool present, JsonElement value) => !present || value.ValueKind == JsonValueKind.Null
        || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString());

    internal static bool ConditionMatches(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values)
        => EvaluateCondition(condition, values, null) == true;

    /// <summary>Unknown draft dependencies stay unresolved; strict evaluation uses its original Boolean truth values.</summary>
    internal static bool? EvaluateCondition(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string>? unresolved)
    {
        string op = condition.GetProperty("op").GetString()!;
        if (op is "all" or "any" or "not")
        {
            JsonElement operands = condition.GetProperty("operands");
            bool?[] results = operands.EnumerateArray().Select(item => EvaluateCondition(item, values, unresolved)).ToArray();
            if (op == "not") return !results[0];
            if (op == "all") return results.Contains(false) ? false : results.Contains(null) ? null : true;
            return results.Contains(true) ? true : results.Contains(null) ? null : false;
        }
        string fieldId = condition.GetProperty("field").GetString()!;
        if (unresolved?.Contains(fieldId) == true) return null;
        bool present = values.TryGetValue(fieldId, out var value)
            && value.ValueKind != JsonValueKind.Null;
        if (op == "present") return present;
        if (!present) return false;
        JsonElement expected = condition.GetProperty("value");
        return op switch
        {
            "equals" => JsonElement.DeepEquals(value, expected),
            "notEquals" => !JsonElement.DeepEquals(value, expected),
            "in" => expected.EnumerateArray().Any(item => JsonElement.DeepEquals(value, item)),
            _ => false
        };
    }

    internal static IEnumerable<string> References(JsonElement condition) =>
        condition.TryGetProperty("field", out var field) ? [field.GetString()!]
            : condition.GetProperty("operands").EnumerateArray().SelectMany(References);

    internal static bool ValueValid(JsonElement field, JsonElement value)
        => ValueValid(field, value, true, null, "", false);

    private static bool ValueValid(JsonElement field, JsonElement value, bool present,
        HashSet<string>? deferred, string path, bool secretAncestor)
    {
        string type = field.GetProperty("type").GetString()!;
        bool secret = secretAncestor || field.GetProperty("sensitivity").GetString() == "secret";
        if (deferred is not null && secret && type is not ("object" or "list")
            && (!present || value.ValueKind == JsonValueKind.Null
                || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
        {
            deferred.Add(path);
            return true;
        }
        if (!present) return !field.TryGetProperty("required", out var required) || !required.GetBoolean();
        if (field.TryGetProperty("required", out var requiredValue) && requiredValue.GetBoolean()
            && Missing(present, value)) return false;
        if (!TypeMatches(type, value)) return false;
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            if (field.TryGetProperty("minLength", out var min) && text.Length < min.GetInt32()
                || field.TryGetProperty("maxLength", out var max) && text.Length > max.GetInt32()) return false;
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDecimal(out decimal number)
                || field.TryGetProperty("minimum", out var min) && number < min.GetDecimal()
                || field.TryGetProperty("maximum", out var max) && number > max.GetDecimal()) return false;
        }
        if (type == "enum" && !field.GetProperty("choices").EnumerateArray()
            .Any(item => JsonElement.DeepEquals(item, value))) return false;
        if (type == "list")
        {
            int count = value.GetArrayLength();
            if (field.TryGetProperty("minItems", out var min) && count < min.GetInt32()
                || field.TryGetProperty("maxItems", out var max) && count > max.GetInt32()
                || value.GetArrayLength() > SetupCompositionLimits.DefaultSequenceEntries)
                return false;
            string itemType = field.GetProperty("itemType").GetString()!;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (deferred is not null && secret && itemType != "object"
                    && (item.ValueKind == JsonValueKind.Null
                        || item.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(item.GetString())))
                {
                    deferred.Add(path + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    index++;
                    continue;
                }
                if (!TypeMatches(itemType, item)
                    || itemType == "object" && !ObjectValid(field, item, deferred, path + "/" + index, secret)) return false;
                if (field.TryGetProperty("uniqueBy", out var uniqueBy))
                {
                    JsonElement key = item;
                    foreach (JsonElement segment in uniqueBy.EnumerateArray())
                        if (!key.TryGetProperty(segment.GetString()!, out key)) return false;
                    if (key.ValueKind != JsonValueKind.String || !keys.Add(key.GetString()!)) return false;
                }
                index++;
            }
        }
        if (type == "object")
            if (!ObjectValid(field, value, deferred, path, secret)) return false;
        if (!field.TryGetProperty("validator", out var validator)) return true;
        string? textValue = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return validator.GetString() switch
        {
            "none" => true,
            "hostname" => textValue is not null && textValue.Length <= 253
                && Uri.CheckHostName(textValue) == UriHostNameType.Dns,
            "absolute-uri" => textValue is not null && Uri.TryCreate(textValue, UriKind.Absolute, out var uri)
                && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo),
            "email-address" => textValue is not null && textValue.Length is >= 3 and <= 320
                && !textValue.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))
                && textValue.IndexOf('@', StringComparison.Ordinal) is > 0
                && textValue.IndexOf('@', StringComparison.Ordinal) == textValue.LastIndexOf('@')
                && !textValue.EndsWith('@'),
            "port-number" => value.ValueKind == JsonValueKind.Number
                && value.TryGetInt64(out long port) && port is >= 1 and <= 65535,
            "positive-integer" => value.ValueKind == JsonValueKind.Number
                && value.TryGetInt64(out long number) && number > 0,
            _ => false
        };
    }

    private static bool ObjectValid(JsonElement field, JsonElement value, HashSet<string>? deferred,
        string path, bool secretAncestor)
    {
        JsonElement[] properties = field.GetProperty("properties").EnumerateArray().ToArray();
        if (value.EnumerateObject().Any(property => !properties.Any(item =>
            item.GetProperty("id").GetString() == property.Name))) return false;
        foreach (JsonElement property in properties)
        {
            string id = property.GetProperty("id").GetString()!;
            bool exists = value.TryGetProperty(id, out var child);
            if (!ValueValid(property, child, exists, deferred, path + "/" + id, secretAncestor)) return false;
        }
        return true;
    }

    private static int DuplicateIndex(JsonElement field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || !field.TryGetProperty("uniqueBy", out var path)) return -1;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement row in value.EnumerateArray())
        {
            JsonElement key = row;
            foreach (JsonElement segment in path.EnumerateArray())
                if (key.ValueKind != JsonValueKind.Object || !key.TryGetProperty(segment.GetString()!, out key)) return -1;
            if (key.ValueKind != JsonValueKind.String) return -1;
            if (!keys.Add(key.GetString()!)) return index;
            index++;
        }
        return -1;
    }

    private static bool TypeMatches(string type, JsonElement value) => type switch
    {
        "string" or "enum" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
        "list" => value.ValueKind == JsonValueKind.Array,
        "object" => value.ValueKind == JsonValueKind.Object,
        _ => false
    };
}
