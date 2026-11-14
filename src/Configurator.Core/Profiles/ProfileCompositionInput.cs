namespace Configurator.Core.Profiles;

using System.Buffers;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;

/// <summary>
/// Owns one immutable composition scope over a live profile and current public/replacement answers.
/// Disposal immediately retires this scope and its outputs, but does not dispose the borrowed profile.
/// The caller owns historical definition admission, consent, revision and stable-row authority.
/// </summary>
public sealed class ProfileCompositionInput : IDisposable
{
    private readonly UnlockedProfile _profile;
    private readonly AnswerSnapshot _answers;
    private readonly FrozenSet<DefinitionPath> _excluded;
    private readonly object _access = new();
    private readonly HashSet<IDisposable> _outputs = [];
    private int _disposed;

    public ProfileCompositionInput(UnlockedProfile profile, Definition target,
        AnswerSnapshot publicAndReplacementAnswers, IReadOnlySet<DefinitionPath>? excludedRetainedPaths = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(publicAndReplacementAnswers);
        profile.EnsureAlive();
        _ = DefinitionSchemaDiff.Compare(profile.Definition, target);
        if (excludedRetainedPaths?.Count > CompositionLimits.DefaultSequenceEntries)
            throw new DefinitionException("answer-limit");
        _excluded = excludedRetainedPaths is null ? FrozenSet<DefinitionPath>.Empty : excludedRetainedPaths.ToFrozenSet();
        if (_excluded.Any(path => path is null)) throw new DefinitionException("answer-omission");
        _profile = profile;
        Target = target;
        _answers = publicAndReplacementAnswers;
        profile.EnsureAlive();
    }

    public Guid ProfileId => _profile.ProfileId;
    public Guid SourceSnapshotId => _profile.SnapshotId;
    public long InputRevision => _answers.InputRevision;
    public ArtifactDigest DefinitionDigest => Target.Digest;
    public long DefinitionRevision => Target.Revision;
    internal Definition Target { get; }

    internal void EnsureAlive()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _profile.EnsureAlive();
    }

    internal void Register(IDisposable output, CancellationToken cancellationToken)
    {
        lock (_access)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAlive();
            _outputs.Add(output);
        }
    }

    internal void Release(IDisposable output)
    {
        lock (_access) _outputs.Remove(output);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        IDisposable[] outputs;
        lock (_access)
        {
            outputs = _outputs.ToArray();
            _outputs.Clear();
        }
        foreach (IDisposable output in outputs) output.Dispose();
    }

    public override string ToString() => "ProfileCompositionInput:Restricted";

    /// <summary>
    /// Creates transient private evaluation data, never an ordinary answer snapshot. Owned UTF-8 buffers clear
    /// on disposal; JsonDocument disposal and managed validator/projection copies do not guarantee memory erasure.
    /// </summary>
    internal Evaluation Evaluate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAlive();
        byte[] payload = _profile.CopyPayload();
        JsonDocument? document = null;
        byte[]? combined = null;
        JsonDocument? valuesDocument = null;
        try
        {
            document = JsonDocument.Parse(payload, ProfilePayloadCodec.DocumentOptions);
            JsonElement storedAnswers = document.RootElement.GetProperty("answers");
            JsonElement retained = storedAnswers.GetProperty("values");
            var omitted = _answers.Omitted.ToHashSet();
            if (storedAnswers.TryGetProperty("omitted", out JsonElement storedOmissions))
                foreach (JsonElement item in storedOmissions.EnumerateArray())
                {
                    DefinitionPath path = DefinitionPath.Parse(item.GetString()!);
                    if (IsFixedTargetPath(path) && !Replaced(path) &&
                        !omitted.Any(current => DefinitionImportProjection.IsPrefix(current, path) ||
                            DefinitionImportProjection.IsPrefix(path, current)))
                        omitted.Add(path);
                }

            byte[] candidate = Combine(retained, null, omitted, cancellationToken);
            ProfileUpgradePlan plan;
            try
            {
                using JsonDocument candidateDocument = JsonDocument.Parse(candidate, ProfilePayloadCodec.DocumentOptions);
                plan = ProfileUpgradePlanner.Plan(_profile, Target,
                    Borrow(candidateDocument, omitted), cancellationToken);
            }
            finally { CryptographicOperations.ZeroMemory(candidate); }

            HashSet<string> eligible = plan.Fields.Where(field => field.Eligibility == ProfileCarryEligibility.Eligible)
                .Select(field => DefinitionPath.Parse(field.FieldPath).Segments[0]).ToHashSet(StringComparer.Ordinal);
            combined = Combine(retained, eligible, omitted, cancellationToken);
            valuesDocument = JsonDocument.Parse(combined, ProfilePayloadCodec.DocumentOptions);
            EnsureAlive();
            cancellationToken.ThrowIfCancellationRequested();
            return new Evaluation(payload, document, combined, valuesDocument, Borrow(valuesDocument, omitted));
        }
        catch
        {
            valuesDocument?.Dispose();
            if (combined is not null) CryptographicOperations.ZeroMemory(combined);
            document?.Dispose();
            CryptographicOperations.ZeroMemory(payload);
            throw;
        }
    }

    private DefinitionEvaluationInput Borrow(JsonDocument values, IReadOnlySet<DefinitionPath> omitted) =>
        new(InputRevision, values.RootElement.EnumerateObject().ToDictionary(
            property => property.Name, property => property.Value, StringComparer.Ordinal), omitted);

    /// <summary>Retires transient validation/JSON owners before asynchronous encryption begins.</summary>
    internal byte[] CreateSealingPayload(Guid newSnapshotId, CancellationToken cancellationToken)
    {
        using Evaluation evaluation = Evaluate(cancellationToken);
        DefinitionValidationResult validation = DefinitionValidator.Validate(Target, evaluation.Answers, cancellationToken);
        if (!validation.IsReady) throw new DefinitionException("profile-not-ready");
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in evaluation.Payload.EnumerateObject())
                {
                    switch (property.Name)
                    {
                        case "snapshotId": writer.WriteString("snapshotId", newSnapshotId); break;
                        case "definition":
                            writer.WritePropertyName("definition");
                            writer.WriteRawValue(Target.CanonicalBytes.Span, skipInputValidation: true);
                            break;
                        case "answers":
                            writer.WriteStartObject("answers");
                            writer.WriteString("answerContractVersion", AnswerSnapshotCodec.ContractVersion);
                            writer.WriteStartObject("values");
                            foreach ((string name, JsonElement value) in evaluation.Answers.Values)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                writer.WritePropertyName(name);
                                value.WriteTo(writer);
                            }
                            writer.WriteEndObject();
                            writer.WriteStartArray("omitted");
                            foreach (DefinitionPath path in evaluation.Answers.Omitted.OrderBy(path => path.ToPointer(), StringComparer.Ordinal))
                                writer.WriteStringValue(path.ToPointer());
                            writer.WriteEndArray();
                            writer.WriteEndObject();
                            break;
                        default: property.WriteTo(writer); break;
                    }
                }
                writer.WriteEndObject();
            }
            if (buffer.WrittenCount > ProfileContainerPolicy.MaximumPayloadBytes)
                throw new ProfileException(ProfileFailureCode.LimitExceeded);
            EnsureAlive();
            return buffer.WrittenSpan.ToArray();
        }
        finally { buffer.Clear(); }
    }

    private sealed class CombinedWriter(Utf8JsonWriter writer, IReadOnlySet<DefinitionPath> excluded,
        IReadOnlySet<DefinitionPath> omitted, CancellationToken cancellationToken)
    {
        internal void Write(string name, JsonElement? old, JsonElement? supplied, DefinitionPath path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (excluded.Any(retired => DefinitionImportProjection.IsPrefix(retired, path)) ||
                omitted.Any(omission => DefinitionImportProjection.IsPrefix(omission, path)))
                old = null;
            if (old is null && supplied is null) return;
            writer.WritePropertyName(name);
            if (old is { ValueKind: JsonValueKind.Object } previous &&
                (supplied is null || supplied.Value.ValueKind == JsonValueKind.Object))
            {
                writer.WriteStartObject();
                foreach (string child in previous.EnumerateObject().Select(property => property.Name).Union(
                    supplied?.EnumerateObject().Select(property => property.Name) ?? [], StringComparer.Ordinal))
                {
                    JsonElement? oldChild = previous.TryGetProperty(child, out JsonElement oldValue) ? oldValue : null;
                    JsonElement? newChild = supplied is { } next && next.TryGetProperty(child, out JsonElement newValue)
                        ? newValue : null;
                    Write(child, oldChild, newChild, DefinitionPath.FromSegments(path.Segments.Append(child)));
                }
                writer.WriteEndObject();
            }
            else (supplied ?? old)!.Value.WriteTo(writer);
        }
    }

    private byte[] Combine(JsonElement retained, HashSet<string>? eligible,
        HashSet<DefinitionPath> omitted, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                var combinedWriter = new CombinedWriter(writer, _excluded, omitted, cancellationToken);
                writer.WriteStartObject();
                foreach (string name in _answers.Values.Keys.Union(retained.EnumerateObject().Select(property => property.Name),
                    StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    JsonElement? supplied = _answers.Values.TryGetValue(name, out JsonElement current) ? current : null;
                    JsonElement? old = (eligible is null || eligible.Contains(name)) &&
                        _profile.Definition.Configuration.Properties.TryGetValue(name, out DefinitionNode? source) &&
                        Target.Configuration.Properties.TryGetValue(name, out DefinitionNode? target) &&
                        Fixed(source) && Fixed(target) && retained.TryGetProperty(name, out JsonElement value) ? value : null;
                    combinedWriter.Write(name, old, supplied, DefinitionPath.FromSegments([name]));
                }
                writer.WriteEndObject();

            }
            if (buffer.WrittenCount > CompositionLimits.DefaultAggregateSourceBytes)
                throw new DefinitionException("answer-limit");
            if (omitted.Count > CompositionLimits.DefaultSequenceEntries)
                throw new DefinitionException("answer-limit");
            using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory, ProfilePayloadCodec.DocumentOptions);
            var budget = new CompositionBudget(CompositionLimits.Default);
            byte[] canonical = DefinitionJson.Canonicalize(document.RootElement, budget, CompositionKeyPolicy.Exact);
            try
            {
                int bytes = canonical.Length;
                foreach (DefinitionPath path in omitted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string pointer = path.ToPointer();
                    budget.Scalar(pointer);
                    byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(pointer, CoreJsonContext.Default.String);
                    try { bytes += encoded.Length + 1; }
                    finally { CryptographicOperations.ZeroMemory(encoded); }
                    if (bytes > CompositionLimits.DefaultAggregateSourceBytes)
                        throw new DefinitionException("answer-limit");
                }
                return canonical;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(canonical);
                throw;
            }
        }
        catch (CompositionException) { throw new DefinitionException("answer-limit"); }
        finally { buffer.Clear(); }
    }

    private static bool Fixed(DefinitionNode node) => node.Type != DefinitionNodeType.Sequence &&
        node.AdditionalProperties is null && node.Variants.Count == 0 && node.Properties.Values.All(Fixed);

    private bool IsFixedTargetPath(DefinitionPath path)
    {
        DefinitionNode node = Target.Configuration;
        foreach (string segment in path.Segments)
        {
            if (node.Variants.Count != 0 || !node.Properties.TryGetValue(segment, out DefinitionNode? child)) return false;
            node = child;
        }
        return path.Segments.Count > 0;
    }

    private bool Replaced(DefinitionPath path)
    {
        if (path.Segments.Count == 0 || !_answers.Values.TryGetValue(path.Segments[0], out JsonElement value)) return false;
        for (int index = 1; index < path.Segments.Count; index++)
        {
            // A supplied collection is a whole replacement, never an ordinal binding to its predecessor.
            if (value.ValueKind != JsonValueKind.Object) return true;
            if (!value.TryGetProperty(path.Segments[index], out value)) return false;
        }
        return true;
    }

    internal sealed class Evaluation(byte[] payloadBytes, JsonDocument payload, byte[] valueBytes,
        JsonDocument values, DefinitionEvaluationInput answers) : IDisposable
    {
        internal JsonElement Payload => payload.RootElement;
        internal DefinitionEvaluationInput Answers => answers;

        public void Dispose()
        {
            values.Dispose();
            payload.Dispose();
            CryptographicOperations.ZeroMemory(valueBytes);
            CryptographicOperations.ZeroMemory(payloadBytes);
        }
    }
}
