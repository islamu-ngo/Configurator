namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Text.Json;

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
    public bool IsVisible(SetupDisplayMode mode) => mode switch
    {
        SetupDisplayMode.AllDeclared => true,
        SetupDisplayMode.Advanced => IsActive,
        SetupDisplayMode.Guided => IsActive && (IsRequired || HasError
            || Definition.Sensitivity != SetupFieldSensitivity.Public || Definition.Consequence is not null
            || Definition.Disclosure != SetupFieldDisclosure.Advanced),
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}

public sealed class DefinitionValidationResult
{
    internal DefinitionValidationResult(IEnumerable<SetupFieldState> fields,
        IEnumerable<DefinitionDiagnostic> diagnostics, Dictionary<string, JsonElement> values)
    {
        Fields = Array.AsReadOnly(fields.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.OrderBy(item => item.FieldId, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray());
        Values = new ReadOnlyDictionary<string, JsonElement>(values);
    }

    public IReadOnlyList<SetupFieldState> Fields { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }
    public bool IsReady => Diagnostics.Count == 0;
    internal IReadOnlyDictionary<string, JsonElement> Values { get; }
    public override string ToString() => $"{nameof(DefinitionValidationResult)}:Ready={IsReady}:Diagnostics={Diagnostics.Count}";
}

public static class DefinitionValidator
{
    public static DefinitionValidationResult Validate(SetupDefinition definition, SetupAnswerSnapshot answers)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        var definitions = definition.Fields.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var active = new Dictionary<string, bool>(StringComparer.Ordinal);
        var effective = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var diagnostics = new List<DefinitionDiagnostic>();
        if (answers.Values.Keys.Any(key => !definitions.ContainsKey(key)))
            diagnostics.Add(new("field-unknown", ""));
        foreach (SetupFieldDefinition field in definition.Fields) Activate(field);
        void Activate(SetupFieldDefinition field)
        {
            if (active.ContainsKey(field.Id)) return;
            foreach (string name in new[] { "activeWhen", "requiredWhen" })
                if (field.Data.TryGetProperty(name, out var condition))
                    foreach (string reference in References(condition)) Activate(definitions[reference]);
            bool enabled = !field.Data.TryGetProperty("activeWhen", out var expression)
                || ConditionMatches(expression, effective);
            active.Add(field.Id, enabled);
            if (!enabled) return;
            if (answers.Values.TryGetValue(field.Id, out var value)) effective.Add(field.Id, value);
            else if (field.Default is { } fallback) effective.Add(field.Id, fallback);
        }

        var states = new List<SetupFieldState>();
        foreach (SetupFieldDefinition field in definition.Fields)
        {
            bool enabled = active[field.Id];
            bool required = enabled && (field.Required || field.Data.TryGetProperty("requiredWhen", out var condition)
                && ConditionMatches(condition, effective));
            bool present = effective.TryGetValue(field.Id, out var value);
            bool invalid = enabled && present && !ValueValid(field.Data, value);
            bool missing = required && (!present || value.ValueKind == JsonValueKind.Null
                || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));
            if (invalid) diagnostics.Add(new("value-invalid", field.Id));
            if (missing) diagnostics.Add(new("value-required", field.Id));
            states.Add(new(field, enabled, required, present, invalid || missing));
        }
        return new(states, diagnostics, effective);
    }

    internal static bool ConditionMatches(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values)
    {
        string op = condition.GetProperty("op").GetString()!;
        if (op is "all" or "any" or "not")
        {
            JsonElement operands = condition.GetProperty("operands");
            return op switch
            {
                "all" => operands.EnumerateArray().All(item => ConditionMatches(item, values)),
                "any" => operands.EnumerateArray().Any(item => ConditionMatches(item, values)),
                _ => !ConditionMatches(operands[0], values)
            };
        }
        bool present = values.TryGetValue(condition.GetProperty("field").GetString()!, out var value)
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

    private static IEnumerable<string> References(JsonElement condition) =>
        condition.TryGetProperty("field", out var field) ? [field.GetString()!]
            : condition.GetProperty("operands").EnumerateArray().SelectMany(References);

    internal static bool ValueValid(JsonElement field, JsonElement value)
    {
        string type = field.GetProperty("type").GetString()!;
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
                || value.EnumerateArray().Any(item => !TypeMatches(field.GetProperty("itemType").GetString()!, item)))
                return false;
        }
        if (type == "object")
        {
            JsonElement[] properties = field.GetProperty("properties").EnumerateArray().ToArray();
            if (value.EnumerateObject().Any(property => !properties.Any(item =>
                item.GetProperty("id").GetString() == property.Name))) return false;
            foreach (JsonElement property in properties)
            {
                bool exists = value.TryGetProperty(property.GetProperty("id").GetString()!, out var child);
                if (exists ? !ValueValid(property, child)
                    : property.TryGetProperty("required", out var required) && required.GetBoolean()) return false;
            }
        }
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
