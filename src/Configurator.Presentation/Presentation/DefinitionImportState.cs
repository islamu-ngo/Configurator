namespace Configurator.Presentation;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core.Definitions;

/// <summary>Detached operation facts never grant access to recovered private values.</summary>
public sealed record DefinitionImportComparisonResult(bool Succeeded, string Code, DefinitionImportReport? Report = null);

public sealed record DefinitionImportSchemaExplanation(
    string FieldId, string Title, string? Help, IReadOnlyList<string> Path);

public sealed partial class DefinitionFormModel
{
    private DefinitionImportAssessment? _importComparison;
    private long _importGeneration;
    private long _importComparisonRevision;
    private string? _importAutofillCode;

    public event EventHandler? ImportComparisonChanged;

    public DefinitionImportReport? ImportComparison
    {
        get { lock (_gate) return _importComparison?.Report; }
    }

    public string? ImportAutofillCode
    {
        get { lock (_gate) return _importAutofillCode; }
    }

    public bool CanAdoptImportComparison
    {
        get { lock (_gate) return _importComparison is not null && _importAutofillCode == "import-ready"; }
    }

    /// <summary>Inspects one local file without committing drafts, changing answers or retiring prepared output.</summary>
    public async Task<DefinitionImportComparisonResult> CompareAsync(string artifactId,
        Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        Func<CancellationToken, ValueTask>? cooperativeYield = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);
        long revision;
        long generation;
        using (BeginMutation())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RetireImportComparison();
            if (SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
                return new(false, "import-secret-consent-required");
            revision = InputRevision;
            generation = _importGeneration;
        }
        ReadOnlyMemory<byte> bytes = default;
        DefinitionImportAssessment? assessment = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bytes = await read(cancellationToken);
            lock (_gate)
                if (!ImportIsCurrent(generation, revision, cancellationToken))
                    return new(false, "import-stale");
            SetupAnswerSnapshot snapshot;
            HashSet<string> defaults;
            lock (_gate)
            {
                if (!ImportIsCurrent(generation, revision, cancellationToken))
                    return new(false, "import-stale");
                snapshot = Snapshot();
                defaults = ReplaceableImportDefaults();
            }
            assessment = cooperativeYield is null
                ? DefinitionImport.Inspect(Definition, artifactId, bytes, cancellationToken)
                : await DefinitionImport.InspectAsync(Definition, artifactId, bytes, YieldCurrent, cancellationToken);
            DefinitionImportResult? autofill = assessment.Report.ExecutionCompleted
                ? cooperativeYield is null
                    ? assessment.AssessAutofill(snapshot, defaults, cancellationToken)
                    : await assessment.AssessAutofillAsync(snapshot, defaults, YieldCurrent, cancellationToken)
                : null;
            using (BeginMutation())
            {
                if (!ImportIsCurrent(generation, revision, cancellationToken))
                    return new(false, "import-stale");
                DefinitionImportReport report = assessment.Report;
                if (!report.ExecutionCompleted)
                    return new(false, report.Code, report);
                _importAutofillCode = CheckAutofillStorage(autofill!).Code;
                _importComparisonRevision = revision;
                _importComparison = assessment;
                assessment = null;
                NotifyImportComparison();
                return new(true, report.Code, report);
            }
        }
        catch (OperationCanceledException)
        {
            return new(false, cancellationToken.IsCancellationRequested ? "import-cancelled" : "import-stale");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(false, "import-read-failed");
        }
        finally
        {
            assessment?.Dispose();
            if (MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> storage))
                CryptographicOperations.ZeroMemory(storage.AsSpan());
        }

        async ValueTask YieldCurrent(CancellationToken token)
        {
            await cooperativeYield!(token);
            lock (_gate)
                if (!ImportIsCurrent(generation, revision, token)) throw new OperationCanceledException();
        }
    }

    /// <summary>Invalidates pending inspection and releases the single retained private proposal.</summary>
    public void ClearImportComparison()
    {
        using (BeginMutation()) RetireImportComparison();
    }

    public DefinitionImportResult AdoptImportComparison()
    {
        using (BeginMutation())
        {
            if (_disposed || _importComparison is null || _importComparisonRevision != InputRevision)
                return new(false, null, "import-stale");
            if (SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
                return new(false, null, "import-secret-consent-required");
            DefinitionImportResult result = AssessCurrentAutofill(_importComparison);
            if (!result.Succeeded)
            {
                _importAutofillCode = result.Code;
                NotifyImportComparison();
                return result;
            }
            return AdoptImportedResult(result, _importComparisonRevision, _importGeneration, CancellationToken.None);
        }
    }

    /// <summary>Reassesses outside the mutation gate, then adopts only the still-current proposal.</summary>
    public async Task<DefinitionImportResult> AdoptImportComparisonAsync(
        Func<CancellationToken, ValueTask> cooperativeYield, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cooperativeYield);
        DefinitionImportAssessment assessment;
        SetupAnswerSnapshot snapshot;
        HashSet<string> defaults;
        long revision;
        long generation;
        using (BeginMutation())
        {
            if (_disposed || _importComparison is null || _importComparisonRevision != InputRevision)
                return new(false, null, "import-stale");
            if (SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets)
                return new(false, null, "import-secret-consent-required");
            assessment = _importComparison;
            revision = InputRevision;
            generation = _importGeneration;
            snapshot = Snapshot();
            defaults = ReplaceableImportDefaults();
        }
        DefinitionImportResult result;
        try
        {
            result = await assessment.AssessAutofillAsync(snapshot, defaults, YieldCurrent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new(false, null, cancellationToken.IsCancellationRequested ? "import-cancelled" : "import-stale");
        }
        using (BeginMutation())
        {
            if (!ImportIsCurrent(generation, revision, cancellationToken) ||
                !ReferenceEquals(assessment, _importComparison))
                return new(false, null, "import-stale");
            result = CheckAutofillStorage(result);
            if (!result.Succeeded)
            {
                _importAutofillCode = result.Code;
                NotifyImportComparison();
                return result;
            }
            return AdoptImportedResult(result, revision, generation, cancellationToken);
        }

        async ValueTask YieldCurrent(CancellationToken token)
        {
            await cooperativeYield(token);
            lock (_gate)
                if (!ImportIsCurrent(generation, revision, token) ||
                    !ReferenceEquals(assessment, _importComparison)) throw new OperationCanceledException();
        }
    }

    /// <summary>Returns declared schema guidance only; imported ordinals never resolve current workspace entities.</summary>
    public DefinitionImportSchemaExplanation? ExplainImportFinding(DefinitionImportFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        lock (_gate)
        {
            if (_disposed || _importComparison is null ||
                !_importComparison.Report.Findings.Any(current => ReferenceEquals(current, finding)) ||
                finding.Schema is not { } location) return null;
            SetupFieldDefinition? field = Definition.Fields.FirstOrDefault(item => item.Id == location.FieldId);
            if (field is null) return null;
            DefinitionNode schema = field.Node;
            var path = new List<string> { field.Id };
            foreach (string name in location.RelativePath)
            {
                if (!schema.Properties.TryGetValue(name, out DefinitionNode? child)) break;
                path.Add(name);
                schema = child;
            }
            return new(field.Id, field.Title, field.Help, path.AsReadOnly());
        }
    }

    private DefinitionImportResult AssessCurrentAutofill(DefinitionImportAssessment assessment)
    {
        DefinitionImportResult result = assessment.AssessAutofill(Snapshot(), ReplaceableImportDefaults());
        return CheckAutofillStorage(result);
    }

    private DefinitionImportResult CheckAutofillStorage(DefinitionImportResult result)
    {
        if (!result.Succeeded) return result;
        if (!HasImportStorage(result.Answers!))
            return new(false, null, "import-workspace-unsupported");
        DefinitionFieldInput? pending = Fields.FirstOrDefault(field =>
            result.ProvidedFields.Contains(field.Definition.Id) && field.InputDiagnosticCode is not null);
        return pending is null ? result : new(false, null, "answer-conflict")
        {
            Diagnostic = new("answer-conflict", pending.Definition.Id)
        };
    }

    private HashSet<string> ReplaceableImportDefaults() =>
        Fields.Where(field => field.IsPristineDefault &&
            !_omitted.Any(path => path.Segments[0] == field.Definition.Id))
            .Select(field => field.Definition.Id).ToHashSet(StringComparer.Ordinal);

    private bool HasImportStorage(SetupAnswerSnapshot answers)
    {
        var names = Fields.Select(field => field.Definition.Name).ToHashSet(StringComparer.Ordinal);
        return answers.Values.Keys.All(names.Contains);
    }

    private bool ImportIsCurrent(long generation, long revision, CancellationToken cancellationToken) =>
        !_disposed && !cancellationToken.IsCancellationRequested && _importGeneration == generation &&
        InputRevision == revision && SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets;

    private void RetireImportComparison()
    {
        _importGeneration = checked(_importGeneration + 1);
        DefinitionImportAssessment? previous = _importComparison;
        _importComparison = null;
        _importAutofillCode = null;
        previous?.Dispose();
        if (previous is not null) NotifyImportComparison();
    }

    private void NotifyImportComparison()
    {
        OnPropertyChanged(nameof(ImportComparison));
        OnPropertyChanged(nameof(ImportAutofillCode));
        OnPropertyChanged(nameof(CanAdoptImportComparison));
        Notify(() => ImportComparisonChanged?.Invoke(this, EventArgs.Empty));
    }
}
