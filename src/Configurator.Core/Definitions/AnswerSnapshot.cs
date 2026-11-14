namespace Configurator.Core.Definitions;

using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;

public sealed class AnswerSnapshot
{
    private readonly ReadOnlyDictionary<string, JsonElement> _values;
    private readonly FrozenSet<DefinitionPath> _omitted;

    public AnswerSnapshot(long inputRevision, IReadOnlyDictionary<string, JsonElement> values,
        IEnumerable<DefinitionPath>? omitted = null) : this(DefinitionImportWork.CompleteSynchronously(
            CreateStateAsync(inputRevision, values, omitted, null, CancellationToken.None)))
    {
    }

    private AnswerSnapshot((long Revision, Dictionary<string, JsonElement> Values, FrozenSet<DefinitionPath> Omitted) state)
    {
        InputRevision = state.Revision;
        _values = new(state.Values);
        _omitted = state.Omitted;
    }

    internal static async ValueTask<AnswerSnapshot> CreateAsync(long inputRevision,
        IReadOnlyDictionary<string, JsonElement> values, IEnumerable<DefinitionPath>? omitted,
        Func<CancellationToken, ValueTask> cooperativeYield, CancellationToken cancellationToken) =>
        new(await CreateStateAsync(inputRevision, values, omitted, cooperativeYield, cancellationToken));

    private static async ValueTask<(long Revision, Dictionary<string, JsonElement> Values, FrozenSet<DefinitionPath> Omitted)>
        CreateStateAsync(long inputRevision, IReadOnlyDictionary<string, JsonElement> values,
            IEnumerable<DefinitionPath>? omitted, Func<CancellationToken, ValueTask>? cooperativeYield,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegative(inputRevision);
        cancellationToken.ThrowIfCancellationRequested();
        if (values.Count > Composition.CompositionLimits.DefaultMappingEntries)
            throw new DefinitionException("answer-limit");
        var snapshot = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var budget = new Composition.CompositionBudget(Composition.CompositionLimits.Default);
        int bytes = 2;
        try
        {
            budget.Node(1);
            foreach ((string key, JsonElement value) in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DefinitionProfile.ValidateScalar(key);
                budget.Scalar(key);
                if (value.ValueKind == JsonValueKind.Undefined) throw new DefinitionException("answer-invalid");
                byte[] canonical = cooperativeYield is null
                    ? DefinitionJson.Canonicalize(value, budget, Composition.CompositionKeyPolicy.Exact, initialDepth: 2)
                    : await DefinitionJson.CanonicalizeAsync(value, cooperativeYield, budget,
                        Composition.CompositionKeyPolicy.Exact, initialDepth: 2, cancellationToken: cancellationToken);
                try
                {
                    bytes += canonical.Length + JsonSerializer.SerializeToUtf8Bytes(key, CoreJsonContext.Default.String).Length + 2;
                    if (bytes > Composition.CompositionLimits.DefaultAggregateSourceBytes)
                        throw new DefinitionException("answer-limit");
                    using JsonDocument parsed = JsonDocument.Parse(canonical);
                    snapshot.Add(key, parsed.RootElement.Clone());
                }
                finally { CryptographicOperations.ZeroMemory(canonical); }
            }
            var suppressed = new HashSet<DefinitionPath>();
            foreach (DefinitionPath path in omitted ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (suppressed.Count == Composition.CompositionLimits.DefaultSequenceEntries)
                    throw new DefinitionException("answer-limit");
                ArgumentNullException.ThrowIfNull(path);
                budget.Scalar(path.ToPointer());
                bytes += JsonSerializer.SerializeToUtf8Bytes(path.ToPointer(), CoreJsonContext.Default.String).Length + 1;
                if (bytes > Composition.CompositionLimits.DefaultAggregateSourceBytes)
                    throw new DefinitionException("answer-limit");
                if (!suppressed.Add(path) || HasSuppliedValue(path))
                    throw new DefinitionException("answer-omission");
            }
            foreach (DefinitionPath path in suppressed)
            {
                string reference = path.ToPointer();
                for (int position = 1; position < reference.Length; position++)
                    if (reference[position] == '/' && suppressed.Contains(DefinitionPath.Parse(reference[..position])))
                        throw new DefinitionException("answer-omission");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return (inputRevision, snapshot, suppressed.ToFrozenSet());
        }
        catch (Composition.CompositionException)
        {
            throw new DefinitionException("answer-limit");
        }
        bool HasSuppliedValue(DefinitionPath path)
        {
            if (path.Segments.Count == 0) return true;
            if (!snapshot.TryGetValue(path.Segments[0], out JsonElement value)) return false;
            for (int index = 1; index < path.Segments.Count; index++)
            {
                if (value.ValueKind == JsonValueKind.Object)
                {
                    if (!value.TryGetProperty(path.Segments[index], out value)) return false;
                }
                else if (value.ValueKind == JsonValueKind.Array && path.TryGetArrayIndex(index, out int position)
                    && position < value.GetArrayLength()) value = value[position];
                else return false;
            }
            return true;
        }
    }

    public long InputRevision { get; }
    public IReadOnlyDictionary<string, JsonElement> Values => _values;
    public IReadOnlySet<DefinitionPath> Omitted => _omitted;
    public override string ToString() => $"{nameof(AnswerSnapshot)}:Revision={InputRevision}:Count={_values.Count}";
}
