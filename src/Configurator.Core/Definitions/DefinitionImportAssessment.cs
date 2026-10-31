namespace Configurator.Core.Definitions;

using System.Text.Json;
using Configurator.Core.Composition;

public enum DefinitionImportCompatibility { Compatible, ChangesRequired, Incomplete, Unsupported, InvalidInput }
public enum DefinitionImportFindingKind
{
    MissingRequired, InvalidSuppliedValue, UnrecognizedInput, StructuralOutputChange, Unassessed, ContextDerivedValue
}

/// <summary>Input ordinals identify assessed content, never workspace rows or private member names.</summary>
public sealed record DefinitionImportSourceLocation
{
    internal DefinitionImportSourceLocation(IEnumerable<int> ordinals) => Ordinals = Array.AsReadOnly(ordinals.ToArray());
    public IReadOnlyList<int> Ordinals { get; }
}

/// <summary>Declared schema coordinates stop before dynamic entries; they grant no correction authority.</summary>
public sealed record DefinitionImportSchemaLocation
{
    internal DefinitionImportSchemaLocation(string fieldId, IEnumerable<string> relativePath)
    {
        FieldId = fieldId;
        RelativePath = Array.AsReadOnly(relativePath.ToArray());
    }
    public string FieldId { get; }
    public IReadOnlyList<string> RelativePath { get; }
}

public sealed record DefinitionImportFinding(DefinitionImportFindingKind Kind, string Code,
    DefinitionImportSourceLocation? Source, DefinitionImportSchemaLocation? Schema);

public sealed record DefinitionImportCapability
{
    internal DefinitionImportCapability(bool supported, string code)
    {
        InspectionSupported = supported;
        StrictAutofillSupported = supported;
        Code = code;
    }
    public bool InspectionSupported { get; }
    public bool StrictAutofillSupported { get; }
    public string Code { get; }
    public string SupportedDomain => "bounded-projection";
    public IReadOnlyList<string> CoverageLimitations { get; } = Array.AsReadOnly(new[] { "represented-content-only" });
}

/// <summary>Immutable value-free facts with no reference or lazy access to private evidence.</summary>
public sealed record DefinitionImportReport
{
    internal DefinitionImportReport(string definitionDigest, string artifactId, string code,
        DefinitionImportCompatibility compatibility, bool completed, bool coverage, bool inventory,
        bool autofill, IEnumerable<DefinitionImportFinding> findings, int workUnits = 0,
        int inverseNodes = 0, int validationNodes = 0)
    {
        DefinitionDigest = definitionDigest;
        ArtifactId = artifactId;
        Code = code;
        Compatibility = compatibility;
        ExecutionCompleted = completed;
        CoverageComplete = coverage;
        InventoryComplete = inventory;
        AutofillEligible = autofill;
        Findings = Array.AsReadOnly(findings.ToArray());
        WorkUnits = workUnits;
        InverseNodes = inverseNodes;
        ValidationNodes = validationNodes;
    }
    public string DefinitionDigest { get; }
    public string ArtifactId { get; }
    public string Code { get; }
    public bool ExecutionCompleted { get; }
    public DefinitionImportCompatibility Compatibility { get; }
    public bool CoverageComplete { get; }
    public bool InventoryComplete { get; }
    public bool AutofillEligible { get; }
    public IReadOnlyList<DefinitionImportFinding> Findings { get; }
    public int RetainedFindingCount => Findings.Count;
    public int? TotalFindingCount => InventoryComplete ? Findings.Count : null;
    public int WorkUnits { get; }
    public int InverseNodes { get; }
    public int ValidationNodes { get; }
}

/// <summary>Owns private evidence; only the detached report participates in ordinary serialization.</summary>
public sealed class DefinitionImportAssessment : IDisposable
{
    private SetupDefinition? _definition;
    private DefinitionImportEvidence? _evidence;
    private readonly string? _refusal;
    private bool _disposed;

    internal DefinitionImportAssessment(DefinitionImportReport report, SetupDefinition? definition = null,
        DefinitionImportEvidence? evidence = null, string? refusal = null)
    {
        Report = report;
        _definition = definition;
        _evidence = evidence;
        _refusal = refusal;
    }
    public DefinitionImportReport Report { get; }
    public DefinitionImportResult AssessAutofill(SetupAnswerSnapshot current,
        IReadOnlySet<string>? replaceableDefaults = null, CancellationToken cancellationToken = default) =>
        DefinitionImportWork.CompleteSynchronously(AssessAutofillCoreAsync(current, replaceableDefaults, null, cancellationToken));

    /// <summary>Cooperatively assesses a candidate without mutating the workspace. In-flight work retains its immutable evidence across disposal.</summary>
    public ValueTask<DefinitionImportResult> AssessAutofillAsync(SetupAnswerSnapshot current,
        IReadOnlySet<string>? replaceableDefaults, Func<CancellationToken, ValueTask> cooperativeYield,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cooperativeYield);
        return AssessAutofillCoreAsync(current, replaceableDefaults, cooperativeYield, cancellationToken);
    }

    private async ValueTask<DefinitionImportResult> AssessAutofillCoreAsync(SetupAnswerSnapshot current,
        IReadOnlySet<string>? replaceableDefaults, Func<CancellationToken, ValueTask>? cooperativeYield,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(current);
        cancellationToken.ThrowIfCancellationRequested();
        DefinitionImportEvidence? evidence = _evidence;
        SetupDefinition? definition = _definition;
        if (cooperativeYield is not null)
        {
            await cooperativeYield(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (evidence is null || definition is null || _refusal is not null)
            return new(false, null, _refusal ?? Report.Code);
        return await DefinitionImport.MergeAsync(definition, evidence, current, replaceableDefaults, cancellationToken,
            cooperativeYield: cooperativeYield);
    }

    public void Dispose()
    {
        _disposed = true;
        _evidence = null;
        _definition = null;
    }
    public override string ToString() => $"{nameof(DefinitionImportAssessment)}:{Report.Code}";
}

internal enum DefinitionImportObservationState { ObservedValue, ObservedAbsence, Unobserved, ContextDerivedValue }

internal sealed record DefinitionImportObservation(DefinitionImportObservationState State,
    bool CompleteSubtree, bool CompleteContainer, DefinitionImportSourceLocation? Source,
    IReadOnlyList<string>? MapEntries = null);

/// <summary>Private immutable observations reference one owned recovered snapshot, not a second answer store.</summary>
internal sealed class DefinitionImportEvidence
{
    private readonly Dictionary<DefinitionPath, DefinitionImportObservation> _observations;
    internal SetupAnswerSnapshot Answers { get; }
    internal IReadOnlyList<DefinitionPath> Assignments { get; }

    /// <summary>Takes ownership of recovery's completed private observation dictionary.</summary>
    internal DefinitionImportEvidence(SetupAnswerSnapshot answers, IEnumerable<DefinitionPath> assignments,
        Dictionary<DefinitionPath, DefinitionImportObservation> ownedObservations)
    {
        Answers = answers;
        Assignments = Array.AsReadOnly(assignments.ToArray());
        _observations = ownedObservations;
    }

    internal DefinitionImportObservationState State(DefinitionPath path)
    {
        if (_observations.TryGetValue(path, out DefinitionImportObservation? exact)) return exact.State;
        for (int count = path.Segments.Count - 1; count >= 0; count--)
        {
            DefinitionPath ancestor = DefinitionPath.FromSegments(path.Segments.Take(count));
            if (!_observations.TryGetValue(ancestor, out DefinitionImportObservation? observed) || !observed.CompleteSubtree) continue;
            if (observed.State == DefinitionImportObservationState.ObservedAbsence)
                return DefinitionImportObservationState.ObservedAbsence;
            if (!TryValue(ancestor, out JsonElement value)) return DefinitionImportObservationState.Unobserved;
            for (int index = count; index < path.Segments.Count; index++)
            {
                if (value.ValueKind == JsonValueKind.Object)
                {
                    if (!value.TryGetProperty(path.Segments[index], out value))
                        return DefinitionImportObservationState.ObservedAbsence;
                }
                else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row))
                {
                    if (row >= value.GetArrayLength()) return DefinitionImportObservationState.ObservedAbsence;
                    value = value[row];
                }
                else return DefinitionImportObservationState.Unobserved;
            }
            return observed.State;
        }
        return DefinitionImportObservationState.Unobserved;
    }

    internal bool Complete(DefinitionPath path)
    {
        if (_observations.TryGetValue(path, out DefinitionImportObservation? exact)) return exact.CompleteContainer;
        for (int count = path.Segments.Count - 1; count >= 0; count--)
            if (_observations.TryGetValue(DefinitionPath.FromSegments(path.Segments.Take(count)), out var ancestor)
                && ancestor.CompleteSubtree) return State(path) != DefinitionImportObservationState.Unobserved;
        return false;
    }

    internal bool TryValue(DefinitionPath path, out JsonElement value)
    {
        if (path.Segments.Count == 0)
        {
            value = JsonSerializer.SerializeToElement(Answers.Values);
            return true;
        }
        if (!Answers.Values.TryGetValue(path.Segments[0], out value)) return false;
        for (int index = 1; index < path.Segments.Count; index++)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(path.Segments[index], out value)) return false;
            }
            else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row) &&
                row < value.GetArrayLength()) value = value[row];
            else return false;
        }
        return true;
    }

    internal DefinitionImportSourceLocation? Source(DefinitionPath path)
    {
        for (int count = path.Segments.Count; count >= 0; count--)
        {
            DefinitionPath ancestor = DefinitionPath.FromSegments(path.Segments.Take(count));
            if (!_observations.TryGetValue(ancestor, out var observed)) continue;
            if (observed.Source is null || count == path.Segments.Count || !TryValue(ancestor, out JsonElement value))
                return observed.Source;
            var ordinals = observed.Source.Ordinals.ToList();
            for (int index = count; index < path.Segments.Count; index++)
            {
                if (value.ValueKind == JsonValueKind.Object)
                {
                    int ordinal = 0;
                    bool found = false;
                    foreach (JsonProperty member in value.EnumerateObject())
                    {
                        if (member.Name == path.Segments[index])
                        {
                            value = member.Value;
                            ordinals.Add(ordinal);
                            found = true;
                            break;
                        }
                        ordinal++;
                    }
                    if (!found) break;
                }
                else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int row) &&
                    row < value.GetArrayLength())
                {
                    ordinals.Add(row);
                    value = value[row];
                }
                else break;
            }
            return new(ordinals);
        }
        return null;
    }

    internal async ValueTask<DefinitionImportSourceLocation?> SourceAsync(DefinitionPath root,
        IReadOnlyList<DefinitionLocationSegment> segments, DefinitionImportWork work)
    {
        var concrete = new List<string>(root.Segments);
        foreach (DefinitionLocationSegment segment in segments)
        {
            await work.VisitAsync();
            if (segment.Kind == DefinitionLocationSegmentKind.Property)
                concrete.Add(segment.PropertyName!);
            else if (segment.Kind == DefinitionLocationSegmentKind.ArrayRow)
                concrete.Add(segment.Index!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else
            {
                if (!_observations.TryGetValue(DefinitionPath.FromSegments(concrete), out DefinitionImportObservation? observation) ||
                    observation.MapEntries is not { } entries || segment.Index!.Value >= entries.Count) return null;
                concrete.Add(entries[segment.Index.Value]);
            }
        }
        return Source(DefinitionPath.FromSegments(concrete));
    }
}

internal sealed class DefinitionImportLimitException : Exception
{
    internal DefinitionImportLimitException() : base("assessment-limit") { }
}

/// <summary>One combined traversal budget, independent of parser admission and display paging.</summary>
internal sealed class DefinitionImportWork(CancellationToken cancellationToken,
    Func<CancellationToken, ValueTask>? cooperativeYield = null)
{
    private const int YieldQuantum = 1024;
    internal CancellationToken CancellationToken => cancellationToken;
    internal Func<CancellationToken, ValueTask>? CooperativeYield => cooperativeYield;
    internal static T CompleteSynchronously<T>(ValueTask<T> operation)
    {
        if (!operation.IsCompleted) throw new InvalidOperationException("synchronous-operation-incomplete");
        return operation.GetAwaiter().GetResult();
    }

    internal int Units { get; private set; }
    internal int InverseNodes { get; private set; }
    internal int ValidationNodes { get; private set; }
    internal async ValueTask VisitAsync()
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Units == SetupCompositionLimits.DefaultParserEvents) throw new DefinitionImportLimitException();
        Units++;
        if (cooperativeYield is not null && Units % YieldQuantum == 0)
        {
            await cooperativeYield(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
    internal async ValueTask InverseAsync()
    {
        await VisitAsync();
        if (InverseNodes == SetupCompositionLimits.DefaultNormalizedNodes) throw new DefinitionImportLimitException();
        InverseNodes++;
    }
    internal async ValueTask ValidationAsync()
    {
        await VisitAsync();
        if (ValidationNodes == SetupCompositionLimits.DefaultNormalizedNodes) throw new DefinitionImportLimitException();
        ValidationNodes++;
        // Validation resolves paths, dependencies and constraints in addition to visiting each node.
        if (cooperativeYield is not null && ValidationNodes % 256 == 0)
        {
            await cooperativeYield(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
