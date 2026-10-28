namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Composition;
using Configurator.Core.Artifacts;

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
    /// <summary>Explicitly reads a resolved private value; serialized validation facts remain value-free.</summary>
    public bool TryGetEffectiveValue(DefinitionPath location, out JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(location);
        JsonElement reference = JsonSerializer.SerializeToElement(new { scope = "root", path = location.ToPointer() });
        if (DefinitionValidator.TryResolveReference(reference, Values, null, out JsonElement resolved))
        {
            value = resolved.Clone();
            return true;
        }
        value = default;
        return false;
    }
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
    public static DefinitionValidationResult Validate(SetupDefinition definition, SetupAnswerSnapshot answers,
        CancellationToken cancellationToken = default) => ValidateCore(definition, answers, false, cancellationToken);

    /// <summary>Defers missing secret leaves only. Supplied invalid values remain blocking.</summary>
    public static DefinitionValidationResult ValidateDraft(SetupDefinition definition, SetupAnswerSnapshot answers,
        CancellationToken cancellationToken = default) => ValidateCore(definition, answers, true, cancellationToken);

    private static DefinitionValidationResult ValidateCore(SetupDefinition definition, SetupAnswerSnapshot answers,
        bool draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        var effective = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var diagnostics = new List<DefinitionDiagnostic>();
        var states = new Dictionary<string, SetupFieldState>(StringComparer.Ordinal);
        var nodes = new List<DefinitionNodeState>();
        var deferred = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        var invalidControllers = new HashSet<string>(StringComparer.Ordinal);
        var fields = definition.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var budget = new CompositionBudget(SetupCompositionLimits.Default);
        var missing = new List<(string Root, string Id, string[] Path)>();
        try
        {
            if (answers.Values.Keys.Any(name => !fields.ContainsKey(name)))
                diagnostics.Add(new("field-unknown", ""));
            foreach (DefinitionPath omission in answers.Omitted)
            {
                DefinitionNode current = definition.Configuration;
                for (int index = 0; index < omission.Segments.Count; index++)
                {
                    if (current.Type == DefinitionNodeType.Mapping &&
                        current.Properties.TryGetValue(omission.Segments[index], out DefinitionNode? child))
                        current = child;
                    else if (current.Type == DefinitionNodeType.Sequence && omission.TryGetArrayIndex(index, out int row) &&
                        row < SetupCompositionLimits.DefaultSequenceEntries) current = current.Items!;
                    else { diagnostics.Add(new("answer-omission", "")); break; }
                }
            }
            foreach (SetupFieldDefinition field in definition.Fields) Activate(field);
            SettleRequirements();
            if (DefinitionArtifactComposer.PreparationDiagnostics.IsEnabled("ValidationCompleted"))
                DefinitionArtifactComposer.PreparationDiagnostics.Write("ValidationCompleted", draft);
            return new(definition.Fields.Select(field => states[field.Name]), diagnostics, effective, deferred, unresolved, nodes);
        }
        catch (SetupCompositionException)
        {
            return new([], [new("validation-limit", "")], new(StringComparer.Ordinal));
        }

        void Activate(SetupFieldDefinition field)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (states.ContainsKey(field.Name)) return;
            if (!visiting.Add(field.Name)) throw new SetupDefinitionException("activation-cycle");
            foreach (string dependency in field.Dependencies)
                if (fields.TryGetValue(dependency, out SetupFieldDefinition? controller)) Activate(controller);
            var unknown = deferred.Concat(unresolved).Concat(invalidControllers).ToHashSet(StringComparer.Ordinal);
            bool? applies = field.ActiveWhen is { } condition ? EvaluateCondition(condition, effective, unknown) : true;
            if (applies is null) unresolved.Add(field.Name);
            bool active = applies == true;
            bool supplied = answers.Values.TryGetValue(field.Name, out JsonElement source);
            JsonNode? value = supplied ? JsonNode.Parse(source.GetRawText()) : null;
            int before = diagnostics.Count;
            var resolved = Resolve(field.Node, value, supplied, [field.Name], field.Id, [], active,
                active && field.Required, SetupFieldDefinition.SensitivityOf(definition.Configuration));
            if (active && resolved.Present) effective.Add(field.Name, Element(resolved.Value));
            bool error = diagnostics.Count != before;
            if (error) invalidControllers.Add(field.Name);
            states.Add(field.Name, new(field, active, active && field.Required, active && resolved.Present, error));
            visiting.Remove(field.Name);
        }

        (bool Present, JsonNode? Value, bool Valid) Resolve(DefinitionNode schema,
            JsonNode? value, bool present, string[] location, string fieldId,
            string[] safePath, bool active, bool required, SetupFieldSensitivity inherited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            budget.Node(location.Length);
            int start = nodes.Count;
            int diagnosticsBefore = diagnostics.Count;
            var sensitivity = (SetupFieldSensitivity)Math.Max((int)inherited, (int)SetupFieldDefinition.SensitivityOf(schema));
            if (!active || answers.Omitted.Contains(DefinitionPath.FromSegments(location))) present = false;
            else if (!present && schema.Keywords.TryGetProperty("x-configurator", out JsonElement metadata)
                && metadata.TryGetProperty("initialValue", out JsonElement initial))
            {
                value = JsonNode.Parse(initial.GetRawText());
                present = true;
            }
            bool missingSecret = active && !present && draft && sensitivity == SetupFieldSensitivity.Secret
                && schema.Type is not (DefinitionNodeType.Mapping or DefinitionNodeType.Sequence);
            if (missingSecret) deferred.Add(location[0]);
            bool valid = !active || !present;
            if (active && !present && required && !missingSecret)
                missing.Add((location[0], fieldId, safePath));
            bool containerPresent = active && present && value is JsonObject;
            if (schema.Type == DefinitionNodeType.Mapping)
            {
                var mapping = value as JsonObject;
                bool childrenValid = true;
                if (containerPresent && mapping!.Any(member => !schema.Properties.ContainsKey(member.Key)))
                {
                    diagnostics.Add(new("value-invalid", fieldId, safePath));
                    childrenValid = false;
                }
                foreach ((string name, DefinitionNode childSchema) in schema.Properties)
                {
                    bool childPresent = containerPresent && mapping!.ContainsKey(name);
                    JsonNode? childValue = childPresent ? mapping![name] : null;
                    var child = Resolve(childSchema, childValue, childPresent, [.. location, name], fieldId,
                        [.. safePath, name], containerPresent, containerPresent && schema.Required.Contains(name, StringComparer.Ordinal), sensitivity);
                    childrenValid &= child.Valid;
                    if (containerPresent)
                    {
                        if (!child.Present) mapping!.Remove(name);
                        else if (!ReferenceEquals(mapping![name], child.Value)) mapping![name] = child.Value;
                    }
                }
                if (active && present) valid = childrenValid && LocalValueValid(schema, Element(value));
            }
            else if (schema.Type == DefinitionNodeType.Sequence && active && present
                && value is JsonArray sequence)
            {
                valid = true;
                for (int index = 0; index < sequence.Count; index++)
                {
                    string ordinal = index.ToString(CultureInfo.InvariantCulture);
                    var child = Resolve(schema.Items!, sequence[index], true, [.. location, ordinal], fieldId,
                        [.. safePath, ordinal], true, true, sensitivity);
                    valid &= child.Valid;
                    if (!ReferenceEquals(sequence[index], child.Value)) sequence[index] = child.Value;
                }
                JsonElement resolvedSequence = Element(value);
                valid &= LocalValueValid(schema, resolvedSequence);
                int duplicate = DuplicateIndex(schema, resolvedSequence);
                if (duplicate >= 0)
                {
                    JsonElement uniqueBy = schema.Keywords.GetProperty("x-configurator").GetProperty("uniqueBy");
                    diagnostics.Add(new("value-duplicate", fieldId, [.. safePath,
                        duplicate.ToString(CultureInfo.InvariantCulture), .. DefinitionPath.Parse(uniqueBy.GetString()!).Segments]));
                }
            }
            else if (active && present) valid = LocalValueValid(schema, Element(value));
            if (active && present && !valid && diagnostics.Count == diagnosticsBefore)
                diagnostics.Add(new("value-invalid", fieldId, safePath));
            bool childObligation = nodes.Skip(start).Any(node => node.IsObligation);
            bool obligation = active && required && (schema.Type != DefinitionNodeType.Mapping || !childObligation);
            bool obligationReady = present && (schema.Type is DefinitionNodeType.Mapping or DefinitionNodeType.Sequence
                ? LocalValueValid(schema, Element(value)) : valid);
            nodes.Insert(start, new(fieldId, safePath, active, required, active && present, valid,
                obligation, obligationReady, !present, SetupFieldDefinition.EffectiveSensitivity(schema, inherited)));
            return (active && present, value, valid);

        }

        void SettleRequirements()
        {
            var unknown = deferred.Concat(unresolved).Concat(invalidControllers).ToHashSet(StringComparer.Ordinal);
            var relationshipRequired = new HashSet<string>(StringComparer.Ordinal);
            var witnesses = new HashSet<string>(StringComparer.Ordinal);
            CloseUncertainty();
            foreach (var absent in missing)
                if (!unknown.Contains(absent.Root))
                {
                    witnesses.Add(absent.Root);
                    diagnostics.Add(new("value-required", absent.Id, absent.Path));
                }
            foreach (string name in relationshipRequired)
                if (!effective.ContainsKey(name) && !unknown.Contains(name) && states[name].IsActive)
                {
                    if (draft && SetupFieldDefinition.EffectiveSensitivity(fields[name].Node) == SetupFieldSensitivity.Secret)
                        deferred.Add(name);
                    else
                    {
                        witnesses.Add(name);
                        diagnostics.Add(new("value-required", fields[name].Id));
                    }
                }
            unknown.UnionWith(witnesses);
            unknown.UnionWith(deferred);
            CloseUncertainty();
            foreach (SetupFieldDefinition field in definition.Fields)
            {
                SetupFieldState previous = states[field.Name];
                bool? applicable = field.ActiveWhen is { } condition ? EvaluateCondition(condition, effective, unknown) : true;
                bool active = applicable == true;
                if (applicable is null) unresolved.Add(field.Name);
                if (!active) effective.Remove(field.Name);
                bool required = active && (field.Required || relationshipRequired.Contains(field.Name));
                states[field.Name] = new(field, active, required, active && previous.IsProvided,
                    diagnostics.Any(diagnostic => diagnostic.FieldId == field.Id));
            }
            unresolved.UnionWith(unknown.Except(deferred).Except(invalidControllers));
            for (int index = 0; index < nodes.Count; index++)
            {
                DefinitionNodeState node = nodes[index];
                SetupFieldState root = states[fields.Values.First(field => field.Id == node.FieldId).Name];
                bool active = root.IsActive && node.IsActive;
                bool required = node.RelativePath.Count == 0 ? root.IsRequired : node.IsRequired;
                bool error = diagnostics.Any(diagnostic => diagnostic.FieldId == node.FieldId &&
                    diagnostic.RelativePath.SequenceEqual(node.RelativePath, StringComparer.Ordinal));
                bool rootObligation = required && node.RelativePath.Count == 0 &&
                    (root.Definition.Node.Type != DefinitionNodeType.Mapping ||
                     !nodes.Any(child => child.FieldId == node.FieldId && child.RelativePath.Count > 0 && child.IsObligation));
                nodes[index] = new(node.FieldId, node.RelativePath, active, required, active && node.IsProvided,
                    !error && node.IsReady, active && (node.IsObligation || rootObligation),
                    node.IsObligationReady, node.IsMissing, node.Sensitivity);
            }

            void CloseUncertainty()
            {
                for (int scan = 0; scan <= fields.Count; scan++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = unknown.Count;
                    relationshipRequired.Clear();
                    foreach (SetupFieldDefinition field in definition.Fields)
                        if (field.ActiveWhen is { } condition && EvaluateCondition(condition, effective, unknown) is null)
                            unknown.Add(field.Name);
                    JsonElement keywords = definition.Configuration.Keywords;
                    if (keywords.TryGetProperty("if", out JsonElement guard))
                    {
                        bool? holds = true;
                        foreach (JsonElement name in guard.GetProperty("required").EnumerateArray())
                        {
                            string key = name.GetString()!;
                            if (!unknown.Contains(key) && !effective.ContainsKey(key)) { holds = false; break; }
                            if (unknown.Contains(key)) holds = null;
                        }
                        Require(keywords.GetProperty("then").GetProperty("required"), holds);
                    }
                    if (keywords.TryGetProperty("dependentRequired", out JsonElement dependencies))
                        foreach (JsonProperty dependency in dependencies.EnumerateObject())
                            Require(dependency.Value, unknown.Contains(dependency.Name) ? null : effective.ContainsKey(dependency.Name));
                    if (unknown.Count == count) return;
                }
                throw new SetupDefinitionException("validation-limit");
            }

            void Require(JsonElement targets, bool? holds)
            {
                if (holds == false) return;
                foreach (JsonElement target in targets.EnumerateArray())
                {
                    string name = target.GetString()!;
                    if (holds == true) relationshipRequired.Add(name);
                    else if (!effective.ContainsKey(name) && states[name].IsActive) unknown.Add(name);
                }
            }
        }
    }

    private static JsonElement Element(JsonNode? value)
    {
        using JsonDocument document = JsonDocument.Parse(value?.ToJsonString() ?? "null");
        return document.RootElement.Clone();
    }

    internal static bool ValueValid(DefinitionNode schema, JsonElement value)
    {
        if (!LocalValueValid(schema, value)) return false;
        if (value.ValueKind == JsonValueKind.Null) return true;
        if (schema.Type == DefinitionNodeType.Mapping)
        {
            if (value.EnumerateObject().Any(member => !schema.Properties.ContainsKey(member.Name))) return false;
            foreach ((string name, DefinitionNode child) in schema.Properties)
            {
                bool present = value.TryGetProperty(name, out JsonElement supplied);
                if (!present && schema.Required.Contains(name, StringComparer.Ordinal) || present && !ValueValid(child, supplied)) return false;
            }
        }
        if (schema.Type == DefinitionNodeType.Sequence)
            foreach (JsonElement item in value.EnumerateArray())
                if (!ValueValid(schema.Items!, item)) return false;
        return true;
    }

    private static bool LocalValueValid(DefinitionNode schema, JsonElement value)
    {
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
        if (keywords.TryGetProperty("enum", out JsonElement choices)
            && !choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(value, choice))) return false;
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            int length = text.EnumerateRunes().Count();
            if (keywords.TryGetProperty("minLength", out JsonElement minimum) && length < minimum.GetInt32()
                || keywords.TryGetProperty("maxLength", out JsonElement maximum) && length > maximum.GetInt32()
                || keywords.TryGetProperty("x-configurator", out JsonElement extension)
                    && extension.TryGetProperty("notBlank", out JsonElement notBlank) && notBlank.GetBoolean() && string.IsNullOrWhiteSpace(text)) return false;
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDecimal(out decimal number)
                || keywords.TryGetProperty("minimum", out JsonElement minimum) && number < minimum.GetDecimal()
                || keywords.TryGetProperty("maximum", out JsonElement maximum) && number > maximum.GetDecimal()) return false;
        }
        if (value.ValueKind == JsonValueKind.Array
            && (keywords.TryGetProperty("minItems", out JsonElement minItems) && value.GetArrayLength() < minItems.GetInt32()
                || keywords.TryGetProperty("maxItems", out JsonElement maxItems) && value.GetArrayLength() > maxItems.GetInt32()
                || DuplicateIndex(schema, value) >= 0)) return false;
        if (value.ValueKind == JsonValueKind.Object)
        {
            int count = value.EnumerateObject().Count();
            if (keywords.TryGetProperty("minProperties", out JsonElement minimum) && count < minimum.GetInt32()
                || keywords.TryGetProperty("maxProperties", out JsonElement maximum) && count > maximum.GetInt32()) return false;
        }
        return true;
    }

    private static int DuplicateIndex(DefinitionNode schema, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || !schema.Keywords.TryGetProperty("x-configurator", out var metadata)
            || !metadata.TryGetProperty("uniqueBy", out var uniqueBy)) return -1;
        DefinitionPath path = DefinitionPath.Parse(uniqueBy.GetString()!);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            JsonElement key = item;
            foreach (string name in path.Segments)
                if (key.ValueKind != JsonValueKind.Object || !key.TryGetProperty(name, out key)) return -1;
            if (key.ValueKind != JsonValueKind.String) return -1;
            if (!keys.Add(key.GetString()!)) return index;
            index++;
        }
        return -1;
    }

    internal static bool ConditionMatches(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values) =>
        EvaluateCondition(condition, values, null) == true;

    internal static bool? EvaluateCondition(JsonElement condition, IReadOnlyDictionary<string, JsonElement> values,
        IReadOnlySet<string>? unresolved, JsonElement? item = null)
    {
        string operation = condition.GetProperty("op").GetString()!;
        if (operation is "all" or "any" or "not")
        {
            bool?[] results = condition.GetProperty("operands").EnumerateArray()
                .Select(operand => EvaluateCondition(operand, values, unresolved, item)).ToArray();
            if (operation == "not") return !results[0];
            return operation == "all" ? results.Contains(false) ? false : results.Contains(null) ? null : true
                : results.Contains(true) ? true : results.Contains(null) ? null : false;
        }
        JsonElement reference = condition.GetProperty("reference");
        DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
        if (reference.GetProperty("scope").GetString() == "root" && unresolved is not null
            && (path.Segments.Count == 0 ? unresolved.Count != 0 : unresolved.Contains(path.Segments[0]))) return null;
        bool present = TryResolveReference(reference, values, item, out JsonElement value);
        if (operation == "exists") return present;
        if (!present) return false;
        return operation switch
        {
            "equals" => JsonElement.DeepEquals(value, condition.GetProperty("value")),
            "notEquals" => !JsonElement.DeepEquals(value, condition.GetProperty("value")),
            "in" => condition.GetProperty("values").EnumerateArray().Any(candidate => JsonElement.DeepEquals(value, candidate)),
            _ => throw new SetupDefinitionException("condition-operation")
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
    {
        if (condition.TryGetProperty("reference", out JsonElement reference))
        {
            DefinitionPath path = DefinitionPath.Parse(reference.GetProperty("path").GetString()!);
            if (reference.GetProperty("scope").GetString() == "root" && path.Segments.Count != 0)
                return [path.Segments[0]];
            return [];
        }
        return condition.GetProperty("operands").EnumerateArray().SelectMany(References).ToArray();
    }
}
