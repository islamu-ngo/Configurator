namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Text.Json;

public sealed class DefinitionValidationResult
{
    private IReadOnlyDictionary<string, IReadOnlyList<DefinitionDiagnostic>>? _diagnosticsByField;
    private IReadOnlyDictionary<string, FieldState>? _fieldsById;

    internal DefinitionValidationResult(IEnumerable<FieldState> fields,
        IEnumerable<DefinitionDiagnostic> diagnostics, Dictionary<string, JsonElement> values,
        IReadOnlySet<string>? deferred = null, IReadOnlySet<string>? unresolved = null,
        IEnumerable<DefinitionNodeState>? nodes = null, IReadOnlySet<DefinitionPath>? deferredLocations = null,
        DefinitionConfigurationContext? configurationContext = null, IReadOnlySet<DefinitionPath>? uncertainLocations = null,
        IReadOnlySet<DefinitionPath>? unresolvedLocations = null)
    {
        Fields = Array.AsReadOnly(fields.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.OrderBy(item => item.FieldId, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray());
        Values = new ReadOnlyDictionary<string, JsonElement>(values);
        DeferredFields = deferred ?? new HashSet<string>(StringComparer.Ordinal);
        UnresolvedFields = unresolved ?? new HashSet<string>(StringComparer.Ordinal);
        Nodes = Array.AsReadOnly(nodes?.ToArray() ?? []);
        DeferredLocations = deferredLocations ?? new HashSet<DefinitionPath>();
        ConfigurationContext = configurationContext;
        UncertainLocations = uncertainLocations ?? new HashSet<DefinitionPath>();
        UnresolvedLocations = unresolvedLocations ?? new HashSet<DefinitionPath>();
        UndecidedRoots = UnresolvedFields.Concat(DeferredLocations.Where(location => location.Segments.Count == 1)
            .Select(location => location.Segments[0])).ToHashSet(StringComparer.Ordinal);
        UndecidedLocations = UnresolvedLocations.Concat(DeferredLocations.Where(location => location.Segments.Count > 1)).ToHashSet();
    }

    public IReadOnlyList<FieldState> Fields { get; }
    public IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }

    /// <summary>Returns indexed validation diagnostics targeting <paramref name="fieldId"/> in O(1) lookup time.</summary>
    public IReadOnlyList<DefinitionDiagnostic> DiagnosticsForField(string fieldId)
    {
        ArgumentNullException.ThrowIfNull(fieldId);
        if (Diagnostics.Count == 0) return [];
        IReadOnlyDictionary<string, IReadOnlyList<DefinitionDiagnostic>> index = _diagnosticsByField ?? BuildDiagnosticsByField();
        return index.GetValueOrDefault(fieldId) ?? [];
    }

    /// <summary>Returns the <see cref="FieldState"/> for <paramref name="fieldId"/> in O(1) lookup time, or null when absent.</summary>
    public FieldState? FindField(string fieldId)
    {
        ArgumentNullException.ThrowIfNull(fieldId);
        if (Fields.Count == 0) return null;
        IReadOnlyDictionary<string, FieldState> index = _fieldsById ?? BuildFieldsById();
        return index.GetValueOrDefault(fieldId);
    }

    private IReadOnlyDictionary<string, IReadOnlyList<DefinitionDiagnostic>> BuildDiagnosticsByField()
    {
        var grouped = new Dictionary<string, List<DefinitionDiagnostic>>(StringComparer.Ordinal);
        foreach (DefinitionDiagnostic diagnostic in Diagnostics)
        {
            if (!grouped.TryGetValue(diagnostic.FieldId, out List<DefinitionDiagnostic>? bucket))
                grouped.Add(diagnostic.FieldId, bucket = []);
            bucket.Add(diagnostic);
        }
        var result = new Dictionary<string, IReadOnlyList<DefinitionDiagnostic>>(grouped.Count, StringComparer.Ordinal);
        foreach (var (key, list) in grouped)
            result.Add(key, Array.AsReadOnly(list.ToArray()));
        var ro = new ReadOnlyDictionary<string, IReadOnlyList<DefinitionDiagnostic>>(result);
        return Interlocked.CompareExchange(ref _diagnosticsByField, ro, null) ?? _diagnosticsByField;
    }

    private IReadOnlyDictionary<string, FieldState> BuildFieldsById()
    {
        var map = new Dictionary<string, FieldState>(Fields.Count, StringComparer.Ordinal);
        foreach (FieldState field in Fields)
            map[field.Id] = field;
        var ro = new ReadOnlyDictionary<string, FieldState>(map);
        return Interlocked.CompareExchange(ref _fieldsById, ro, null) ?? _fieldsById;
    }
    public IReadOnlyList<DefinitionNodeState> Nodes { get; }
    public DefinitionConfigurationContext? ConfigurationContext { get; }
    public bool IsReady => Diagnostics.Count == 0;
    internal IReadOnlyDictionary<string, JsonElement> Values { get; }
    /// <summary>Top-level members holding at least one deferred secret.</summary>
    internal IReadOnlySet<string> DeferredFields { get; }
    /// <summary>Top-level members whose entire value is undecided.</summary>
    internal IReadOnlySet<string> UnresolvedFields { get; }
    internal IReadOnlySet<DefinitionPath> DeferredLocations { get; }
    internal IReadOnlySet<DefinitionPath> UncertainLocations { get; }
    /// <summary>Nested values left undecided by a missing member or an undecided condition, excluding deferred secrets.</summary>
    internal IReadOnlySet<DefinitionPath> UnresolvedLocations { get; }
    /// <summary>
    /// What a condition cannot yet read: whole top-level members (unresolved or deferred themselves) and nested
    /// values (unresolved or deferred). Pass both to condition evaluation; nested causes never hide siblings.
    /// </summary>
    internal IReadOnlySet<string> UndecidedRoots { get; }
    internal IReadOnlySet<DefinitionPath> UndecidedLocations { get; }

    /// <summary>Indicates whether any field or deferred secret dependency has not yet resolved.</summary>
    public bool HasUndecidedDependencies => UndecidedRoots.Count > 0 || UndecidedLocations.Count > 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="path"/> or its subtree depends on an undecided value.</summary>
    public bool IsPathUndecided(DefinitionPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Segments.Count == 0) return false;
        if (UndecidedRoots.Contains(path.Segments[0])) return true;
        foreach (DefinitionPath location in UndecidedLocations)
        {
            int shared = Math.Min(location.Segments.Count, path.Segments.Count);
            if (location.Segments.Take(shared).SequenceEqual(path.Segments.Take(shared), StringComparer.Ordinal))
                return true;
        }
        return false;
    }
    /// <summary>Explicitly reads a resolved private value; serialized validation facts remain value-free.</summary>
    public bool TryGetEffectiveValue(DefinitionPath location, out JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Segments.Count == 0)
        {
            var root = new System.Text.Json.Nodes.JsonObject();
            foreach ((string name, JsonElement supplied) in Values)
                root.Add(name, System.Text.Json.Nodes.JsonNode.Parse(supplied.GetRawText()));
            value = JsonSerializer.SerializeToElement(root, CoreJsonContext.Default.JsonObject);
            return true;
        }
        if (!Values.TryGetValue(location.Segments[0], out JsonElement current))
        {
            value = default;
            return false;
        }
        for (int index = 1; index < location.Segments.Count; index++)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(location.Segments[index], out current))
                {
                    value = default;
                    return false;
                }
            }
            else if (current.ValueKind == JsonValueKind.Array && location.TryGetArrayIndex(index, out int position)
                && position < current.GetArrayLength())
            {
                current = current[position];
            }
            else
            {
                value = default;
                return false;
            }
        }
        value = current.Clone();
        return true;
    }
    public override string ToString() => $"{nameof(DefinitionValidationResult)}:Ready={IsReady}:Diagnostics={Diagnostics.Count}";
}
