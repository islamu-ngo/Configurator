namespace Configurator.Core.Definitions;

using System.Collections.ObjectModel;
using System.Text.Json;

public sealed class DefinitionValidationResult
{
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
