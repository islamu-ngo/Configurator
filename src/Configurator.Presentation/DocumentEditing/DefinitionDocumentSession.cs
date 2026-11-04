namespace Configurator.Presentation;

using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Configurator.Core.Definitions;

public enum DefinitionDocumentRepresentation { Canonical, Environment }
public enum DefinitionDocumentApplyMode { Merge, ReplaceRepresentedScope }
public enum DefinitionDocumentChangeKind { Set, Reset, Omit }

/// <summary>Only admitted schema paths and codec coordinates may appear in document metadata.</summary>
public sealed record DefinitionDocumentDiagnostic(string Code, int Line = 1, int Column = 1);
public sealed record DefinitionDocumentCoverage(DefinitionPath Path, bool Editable);
public sealed record DefinitionDocumentChange(DefinitionPath Path, DefinitionDocumentChangeKind Kind);
public sealed record DefinitionDocumentApplyResult(bool Succeeded, string Code, long InputRevision,
    bool ObserverFailed = false);

/// <summary>
/// Owns one detached, single-use replacement. Neither serialization nor inspection exposes answers.
/// Disposal and consumption are serialized by the owning form gate.
/// </summary>
public sealed class DefinitionDocumentCandidate : IDisposable
{
    private DefinitionAcceptedEditingState? _replacement;
    private readonly DefinitionDocumentSession _session;
    private readonly CancellationTokenRegistration _cancellation;
    internal DefinitionDocumentCandidate(DefinitionDocumentSession session, long generation,
        DefinitionAcceptedEditingState replacement, IEnumerable<DefinitionDocumentChange> changes,
        CancellationToken cancellationToken)
    {
        _session = session;
        Generation = generation;
        _replacement = replacement;
        Changes = Array.AsReadOnly(changes.ToArray());
        CancellationToken = cancellationToken;
        _cancellation = cancellationToken.Register(static state => ((DefinitionDocumentCandidate)state!).Dispose(), this);
    }

    internal long Generation { get; }
    internal CancellationToken CancellationToken { get; }
    internal DefinitionAcceptedEditingState? Replacement => _replacement;
    internal IReadOnlySet<int> ChangedFields { get; init; } = new HashSet<int>();
    internal IReadOnlySet<string> RetiredRows { get; init; } = new HashSet<string>();
    internal bool BelongsTo(DefinitionDocumentSession session) => ReferenceEquals(_session, session);
    internal DefinitionAcceptedEditingState? Consume()
    {
        DefinitionAcceptedEditingState? result = _replacement;
        _replacement = null;
        _cancellation.Unregister();
        return result;
    }
    public IReadOnlyList<DefinitionDocumentChange> Changes { get; }
    public void Dispose()
    {
        lock (_session.Gate)
        {
            _replacement?.Retire();
            _replacement = null;
            _cancellation.Unregister();
        }
    }
    public override string ToString() => $"{nameof(DefinitionDocumentCandidate)}:Changes={Changes.Count}";
}

/// <summary>Owns provisional authority, not text. Disposing the result retires its candidate.</summary>
public sealed class DefinitionDocumentParseResult : IDisposable
{
    internal DefinitionDocumentParseResult(DefinitionDocumentCandidate? candidate,
        IEnumerable<DefinitionDocumentDiagnostic> diagnostics, bool truncated = false)
    {
        Candidate = candidate;
        DefinitionDocumentDiagnostic[] items = diagnostics.Take(101).ToArray();
        Diagnostics = Array.AsReadOnly(items.Take(100).ToArray());
        DiagnosticsTruncated = truncated || items.Length > 100;
    }
    [JsonIgnore]
    public DefinitionDocumentCandidate? Candidate { get; }
    public bool Succeeded => Candidate is not null && Diagnostics.Count == 0;
    public IReadOnlyList<DefinitionDocumentDiagnostic> Diagnostics { get; }
    public bool DiagnosticsTruncated { get; }
    public void Dispose() => Candidate?.Dispose();
    public override string ToString() => $"{nameof(DefinitionDocumentParseResult)}:Succeeded={Succeeded}:Diagnostics={Diagnostics.Count}";
    internal static DefinitionDocumentParseResult Reject(string code) => new(null, [new(code)]);
}

/// <summary>
/// A revision-bound public-scope editor lease. The caller owns visible text and history.
/// Reopen after accepted edits or consent changes; replace the lease to change representation.
/// </summary>
public sealed class DefinitionDocumentSession : IDisposable
{
    private readonly DefinitionFormModel _form;
    private DefinitionDocumentCandidate? _candidate;
    private long _generation;
    private bool _disposed;

    internal DefinitionDocumentSession(DefinitionFormModel form, DefinitionDocumentRepresentation representation,
        string? artifactId, long privacyGeneration)
    {
        _form = form;
        Representation = representation;
        InputRevision = form.InputRevision;
        DefinitionDigest = form.Definition.Digest;
        PrivacyGeneration = privacyGeneration;
        Privacy = form.SessionPrivacy;
        if (representation == DefinitionDocumentRepresentation.Environment)
            EnvironmentScope = DefinitionImport.CaptureEditableScope(form.Definition, artifactId!, form.Snapshot());
        Coverage = DefinitionDocumentEditing.CaptureCoverage(form.Definition.Configuration,
            form.ConfigurationContext, form.Snapshot());
        Scope = representation == DefinitionDocumentRepresentation.Canonical
            ? Array.AsReadOnly(Coverage.Where(item => item.Editable).Select(item => item.Path).ToArray())
            : Array.AsReadOnly(EnvironmentScope!.Mappings.Select(item => item.Path).ToArray());
    }

    internal object Gate => _form.DocumentGate;
    internal long PrivacyGeneration { get; }
    internal DefinitionSessionPrivacy Privacy { get; }
    internal IReadOnlyList<DefinitionPath> Scope { get; }
    internal bool IsCurrent(long generation) => !_disposed && generation == _generation
        && _form.DocumentIsCurrent(this);
    public DefinitionDocumentRepresentation Representation { get; }
    public long InputRevision { get; }
    public Configurator.Core.ArtifactDigest DefinitionDigest { get; }
    public IReadOnlyList<DefinitionDocumentCoverage> Coverage { get; }
    public DefinitionEditableProjection? EnvironmentScope { get; }

    /// <summary>Call synchronously for every text/format edit, before scheduling a parse.</summary>
    public void InvalidateText()
    {
        lock (Gate)
        {
            _generation = checked(_generation + 1);
            _candidate?.Dispose();
            _candidate = null;
        }
    }

    /// <summary>Returns caller-owned restricted UTF-8 bytes; no source text is retained by this session.</summary>
    public byte[] ReadSource(CancellationToken cancellationToken = default)
    {
        lock (Gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? failure = AdmissionFailure();
            if (failure is not null) throw new InvalidOperationException(failure);
            byte[] source = _form.ReadDocumentSource(this, cancellationToken);
            if (!cancellationToken.IsCancellationRequested) return source;
            CryptographicOperations.ZeroMemory(source);
            cancellationToken.ThrowIfCancellationRequested();
            return source;
        }
    }

    public DefinitionDocumentParseResult Parse(ReadOnlyMemory<byte> source,
        DefinitionDocumentApplyMode mode = DefinitionDocumentApplyMode.Merge,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        long generation;
        lock (Gate)
        {
            string? failure = AdmissionFailure();
            if (failure is not null) return DefinitionDocumentParseResult.Reject(failure);
            InvalidateText();
            generation = _generation;
        }
        if (source.Length > (Representation == DefinitionDocumentRepresentation.Environment
            ? Configurator.Core.Dotenv.DotenvCodec.MaximumFileUtf8Bytes
            : Configurator.Core.Composition.CompositionLimits.DefaultAggregateSourceBytes))
            return DefinitionDocumentParseResult.Reject("document-limit");
        // Snapshot caller memory before admission; a published candidate never refers to it.
        byte[] owned = source.ToArray();
        try { return ParseOwned(owned, mode, generation, cancellationToken); }
        finally { CryptographicOperations.ZeroMemory(owned); }
    }

    /// <summary>
    /// Checks consent before invoking the source. The delegate transfers ownership of returned bytes;
    /// they are cleared even when the read completes after retirement.
    /// An optional local cooperative yield lets adapters service queued edits before publication.
    /// It never replaces the final identity, revision, consent and generation checks.
    /// </summary>
    public async Task<DefinitionDocumentParseResult> ParseAsync(
        Func<CancellationToken, Task<byte[]>> read,
        DefinitionDocumentApplyMode mode = DefinitionDocumentApplyMode.Merge,
        Func<CancellationToken, ValueTask>? cooperativeYield = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        long generation;
        lock (Gate)
        {
            string? failure = AdmissionFailure();
            if (failure is not null) return DefinitionDocumentParseResult.Reject(failure);
            if (cancellationToken.IsCancellationRequested) return DefinitionDocumentParseResult.Reject("document-cancelled");
            InvalidateText();
            generation = _generation;
        }
        byte[]? owned = null;
        DefinitionDocumentParseResult? parsed = null;
        try
        {
            owned = await read(cancellationToken).ConfigureAwait(false);
            parsed = ParseOwned(owned, mode, generation, cancellationToken);
            CryptographicOperations.ZeroMemory(owned);
            owned = null;
            if (parsed.Succeeded && cooperativeYield is not null)
                await cooperativeYield(cancellationToken).ConfigureAwait(false);
            lock (Gate)
            {
                if (parsed.Succeeded && (!IsCurrent(generation) || cancellationToken.IsCancellationRequested))
                    return DefinitionDocumentParseResult.Reject("document-stale");
                DefinitionDocumentParseResult result = parsed;
                parsed = null;
                return result;
            }
        }
        catch (OperationCanceledException) { return DefinitionDocumentParseResult.Reject("document-cancelled"); }
        catch (Exception) { return DefinitionDocumentParseResult.Reject(parsed is null ? "document-read-failed" : "document-parse-failed"); }
        finally
        {
            parsed?.Dispose();
            if (owned is not null) CryptographicOperations.ZeroMemory(owned);
        }
    }

    private DefinitionDocumentParseResult ParseOwned(ReadOnlyMemory<byte> source, DefinitionDocumentApplyMode mode,
        long generation, CancellationToken cancellationToken)
    {
        DefinitionAcceptedEditingState? captured = null;
        try
        {
            lock (Gate)
            {
                if (!IsCurrent(generation)) return DefinitionDocumentParseResult.Reject("document-stale");
                cancellationToken.ThrowIfCancellationRequested();
                captured = _form.CaptureDocumentState(this);
            }
            DefinitionDocumentParseResult result = DefinitionDocumentEditing.Parse(
                _form.Definition, this, generation, captured, source, mode, cancellationToken);
            lock (Gate)
            {
                if (!IsCurrent(generation) || cancellationToken.IsCancellationRequested)
                {
                    result.Dispose();
                    return DefinitionDocumentParseResult.Reject("document-stale");
                }
                _candidate = result.Candidate;
                if (result.Succeeded) captured = null; // Candidate now owns the detached payload.
                return result;
            }
        }
        catch (OperationCanceledException) { return DefinitionDocumentParseResult.Reject("document-cancelled"); }
        catch (DefinitionException exception) { return DefinitionDocumentParseResult.Reject(exception.Code); }
        catch (InvalidOperationException) { return DefinitionDocumentParseResult.Reject("document-conflict"); }
        finally { captured?.Retire(); }
    }

    public DefinitionDocumentApplyResult Apply(DefinitionDocumentCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return _form.AdoptDocument(this, candidate, cancellationToken);
    }

    private string? AdmissionFailure() => !IsCurrent(_generation) ? "document-stale"
        : Privacy == DefinitionSessionPrivacy.BrowserNoSecrets ? "document-consent-required" : null;

    public void Dispose()
    {
        lock (Gate)
        {
            _disposed = true;
            InvalidateText();
        }
    }
    public override string ToString() => $"{nameof(DefinitionDocumentSession)}:{Representation}:Revision={InputRevision}";
}
