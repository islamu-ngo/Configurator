namespace Configurator.Terminal;

using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Presentation;

internal sealed record SetupTerminalArtifactResult
{
    internal SetupTerminalArtifactResult(IEnumerable<ProtectedArtifactBundleOutcome> outcomes,
        ProtectedArtifactBundleCompletionCode completionCode = ProtectedArtifactBundleCompletionCode.Completed,
        bool cleanupFailed = false)
    {
        Outcomes = Array.AsReadOnly(outcomes.ToArray());
        CompletionCode = completionCode;
        CleanupFailed = cleanupFailed || Outcomes.Any(item => item.Result is { CleanupFailed: true });
    }
    internal IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes { get; }
    internal ProtectedArtifactBundleCompletionCode CompletionCode { get; }
    internal bool CleanupFailed { get; }
    internal bool Succeeded => CompletionCode == ProtectedArtifactBundleCompletionCode.Completed && !CleanupFailed && Outcomes.Count > 0
        && Outcomes.All(item => item.Result is { Succeeded: true });
    public override string ToString() => $"{nameof(SetupTerminalArtifactResult)}:Succeeded={Succeeded}:Count={Outcomes.Count}";
}

internal sealed record SetupTerminalPreparation(long InputRevision, int ArtifactCount)
{
    public override string ToString() => $"{nameof(SetupTerminalPreparation)}:Count={ArtifactCount}";
}

/// <summary>Tracks selected outputs and current readiness while the workspace fences native publication.</summary>
internal sealed class SetupTerminalArtifactOperation : ISetupPresentationOperation, IDisposable
{
    private readonly IProtectedArtifactWriter _writer;
    private readonly string _baseDirectory;
    private readonly Func<CancellationToken, Task>? _beforePublication;
    private readonly Action<ProtectedArtifactBundleOutcome>? _outcomeObserver;
    private readonly Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>,
        CancellationToken, Task<SetupTerminalArtifactResult>> _publishBundle;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly object _receiptGate = new();
    private readonly Queue<ProtectedArtifactPublicationReceipt> _history = new();
    private ProtectedArtifactPublicationReceipt? _currentPublication;
    private bool _publishing;
    private string _artifactId;
    private bool _disposed;

    internal SetupTerminalArtifactOperation(
        DefinitionFormModel form, IProtectedArtifactWriter writer, string baseDirectory,
        Func<CancellationToken, Task>? beforePublication = null,
        Action<ProtectedArtifactBundleOutcome>? outcomeObserver = null,
        Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>,
            CancellationToken, Task<SetupTerminalArtifactResult>>? publishBundle = null)
    {
        Form = form;
        _writer = writer;
        _baseDirectory = baseDirectory;
        _beforePublication = beforePublication;
        _outcomeObserver = outcomeObserver;
        _publishBundle = publishBundle ?? (async (artifacts, directory, observe, token) =>
        {
            ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
                writer, artifacts, directory, observe, token).ConfigureAwait(false);
            return new(result.Outcomes, result.CompletionCode, result.CleanupFailed);
        });
        _artifactId = form.Definition.Artifacts[0].Id;
        _selected.Add(_artifactId);
        Form.InputChanged += InputChanged;
    }

    internal DefinitionFormModel Form { get; }
    internal IReadOnlyList<ProtectedArtifactPublicationReceipt> PublicationHistory
    {
        get { lock (_receiptGate) return Array.AsReadOnly(_history.ToArray()); }
    }
    internal ProtectedArtifactPublicationReceipt? CurrentPublication { get { lock (_receiptGate) return _currentPublication; } }
    internal ProtectedArtifactPublicationReceipt? LatestPublication
    {
        get { lock (_receiptGate) return _currentPublication ?? _history.LastOrDefault(); }
    }
    internal bool IsPublishing { get { lock (_receiptGate) return _publishing; } }
    internal event EventHandler? PublicationChanged;
    internal event EventHandler? Invalidated;
    internal SetupTerminalPreparation? Preparation { get; private set; }
    internal bool IsPrepared => !IsPublishing && Preparation?.InputRevision == Form.InputRevision;
    internal bool ProtectedOutputAvailable => _writer.IsAvailable;
    internal IReadOnlyList<string> SelectedArtifactIds => Form.Definition.Artifacts
        .Where(item => _selected.Contains(item.Id)).Select(item => item.Id).ToArray();
    internal string ArtifactId
    {
        get => _artifactId;
        set
        {
            if (_artifactId == value) return;
            _ = Form.Definition.Artifacts.Single(item => item.Id == value);
            _artifactId = value;
        }
    }

    internal bool CurrentArtifactSelected => _selected.Contains(_artifactId);

    internal void ToggleCurrentArtifact()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_selected.Add(_artifactId)) _selected.Remove(_artifactId);
        Invalidate();
    }

    internal Task<bool> PrepareAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPublishing) return Task.FromResult(false);
        Invalidate();
        cancellationToken.ThrowIfCancellationRequested();
        if (Form.HasInputErrors) return Task.FromResult(false);
        var snapshot = Form.Snapshot();
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(
            Form.Definition, SelectedArtifactIds, snapshot, cancellationToken);
        if (!bundle.Succeeded || snapshot.InputRevision != Form.InputRevision) return Task.FromResult(false);
        Preparation = new(snapshot.InputRevision, bundle.Artifacts.Count);
        return Task.FromResult(true);
    }

    public async Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        string[] selected = SelectedArtifactIds.ToArray();
        Guid attemptId = Guid.CreateVersion7();
        lock (_receiptGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_publishing) throw new InvalidOperationException("publication-still-running");
            _publishing = true;
            _currentPublication = new(attemptId, selected.Select(id => new ProtectedArtifactBundleOutcome(id, null)), false);
        }
        try
        {
            SetupTerminalArtifactResult result = await PublishAsync(selected, cancellationToken).ConfigureAwait(false);
            ProtectedArtifactPublicationReceipt receipt = SettlePublication(attemptId, result.Outcomes, result.CompletionCode, result.CleanupFailed);
            return new(new SetupTerminalArtifactResult(receipt.Outcomes, receipt.CompletionCode ?? result.CompletionCode,
                receipt.CleanupFailed), ReadOnlyMemory<byte>.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SettlePublication(attemptId, null, ProtectedArtifactBundleCompletionCode.Cancelled);
            throw;
        }
        catch
        {
            SettlePublication(attemptId, null, null, failed: true);
            throw;
        }
    }

    private async Task<SetupTerminalArtifactResult> PublishAsync(string[] selected, CancellationToken cancellationToken)
    {
        SetupTerminalPreparation? prepared = Preparation;
        Preparation = null;
        cancellationToken.ThrowIfCancellationRequested();
        if (prepared?.InputRevision != Form.InputRevision || selected.Length == 0)
            return Outcome(selected, ProtectedArtifactStatus.InvalidRequest);
        if (!_writer.IsAvailable)
            return Outcome(selected, ProtectedArtifactStatus.UnsupportedPlatform);

        var snapshot = Form.Snapshot();
        if (snapshot.InputRevision != prepared.InputRevision)
            return Outcome(selected, ProtectedArtifactStatus.InvalidRequest);
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(Form.Definition, selected, snapshot, cancellationToken);
        if (!bundle.Succeeded || Form.InputRevision != snapshot.InputRevision)
            return Outcome(selected, ProtectedArtifactStatus.InvalidRequest);
        if (_beforePublication is not null) await _beforePublication(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return await _publishBundle(bundle.Artifacts, _baseDirectory, RecordOutcome, cancellationToken).ConfigureAwait(false);
    }

    private static SetupTerminalArtifactResult Outcome(IEnumerable<string> selected, ProtectedArtifactStatus status) =>
        new(selected.Select(id => new ProtectedArtifactBundleOutcome(id, ProtectedArtifactCommitResult.NotInstalled(status))));

    private void RecordOutcome(ProtectedArtifactBundleOutcome outcome)
    {
        lock (_receiptGate)
        {
            if (!_disposed && _currentPublication is { } current)
                _currentPublication = new(current.AttemptId, current.Outcomes.Select(item =>
                    item.ArtifactId == outcome.ArtifactId ? outcome : item), false, isUnknown: current.IsUnknown);
        }
        PublicationChanged?.Invoke(this, EventArgs.Empty);
        _outcomeObserver?.Invoke(outcome);
    }

    private ProtectedArtifactPublicationReceipt SettlePublication(Guid attemptId,
        IReadOnlyList<ProtectedArtifactBundleOutcome>? outcomes, ProtectedArtifactBundleCompletionCode? completionCode,
        bool cleanupFailed = false, bool failed = false)
    {
        ProtectedArtifactPublicationReceipt receipt;
        lock (_receiptGate)
        {
            receipt = new(attemptId, outcomes ?? _currentPublication?.Outcomes ?? [], true, completionCode, cleanupFailed,
                isUnknown: failed, failed: failed);
            _publishing = false;
            _currentPublication = null;
            if (_disposed) return receipt;
            _history.Enqueue(receipt);
            if (_history.Count > 16) _history.Dequeue();
        }
        try { PublicationChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception)
        {
            receipt = new(attemptId, receipt.Outcomes, true, ProtectedArtifactBundleCompletionCode.ObserverFailed, cleanupFailed,
                isUnknown: failed, failed: failed);
            lock (_receiptGate)
            {
                ProtectedArtifactPublicationReceipt[] retained = _history.Select(item => item.AttemptId == attemptId ? receipt : item).ToArray();
                _history.Clear();
                foreach (ProtectedArtifactPublicationReceipt item in retained) _history.Enqueue(item);
            }
        }
        return receipt;
    }

    internal void MarkPublicationUnknown()
    {
        lock (_receiptGate)
            if (_currentPublication is { } current)
                _currentPublication = new(current.AttemptId, current.Outcomes, false, isUnknown: true);
    }

    private void InputChanged(object? sender, EventArgs args) => Invalidate();

    internal void Invalidate()
    {
        Preparation = null;
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    internal void ClearPrivateState()
    {
        if (!Form.ClearPrivateState()) Invalidate();
    }

    public void Dispose()
    {
        lock (_receiptGate)
        {
            if (_disposed) return;
            _disposed = true;
            _history.Clear();
            _currentPublication = null;
            PublicationChanged = null;
        }
        Form.InputChanged -= InputChanged;
        try { ClearPrivateState(); }
        finally { Invalidated = null; }
    }
}
