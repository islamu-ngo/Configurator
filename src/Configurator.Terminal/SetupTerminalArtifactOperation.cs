namespace Configurator.Terminal;

using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Presentation;

internal sealed record SetupTerminalArtifactResult(IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes)
{
    internal bool Succeeded => Outcomes.Count > 0
        && Outcomes.All(item => item.Status == ProtectedArtifactStatus.Written);
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
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private string _artifactId;
    private bool _disposed;

    internal SetupTerminalArtifactOperation(
        DefinitionFormModel form, IProtectedArtifactWriter writer, string baseDirectory,
        Func<CancellationToken, Task>? beforePublication = null)
    {
        Form = form;
        _writer = writer;
        _baseDirectory = baseDirectory;
        _beforePublication = beforePublication;
        _artifactId = form.Definition.Artifacts[0].Id;
        _selected.Add(_artifactId);
        Form.InputChanged += InputChanged;
    }

    internal DefinitionFormModel Form { get; }
    internal event EventHandler? Invalidated;
    internal SetupTerminalPreparation? Preparation { get; private set; }
    internal bool IsPrepared => Preparation?.InputRevision == Form.InputRevision;
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
            Invalidate();
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
        Invalidate();
        cancellationToken.ThrowIfCancellationRequested();
        if (Form.HasInputErrors) return Task.FromResult(false);
        var snapshot = Form.Snapshot();
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(
            Form.Definition, SelectedArtifactIds, snapshot);
        if (!bundle.Succeeded || snapshot.InputRevision != Form.InputRevision) return Task.FromResult(false);
        Preparation = new(snapshot.InputRevision, bundle.Artifacts.Count);
        return Task.FromResult(true);
    }

    public async Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        SetupTerminalPreparation? prepared = Preparation;
        string[] selected = SelectedArtifactIds.ToArray();
        Preparation = null;
        cancellationToken.ThrowIfCancellationRequested();
        if (prepared?.InputRevision != Form.InputRevision || selected.Length == 0)
            return Outcome(selected, ProtectedArtifactStatus.InvalidRequest);
        if (!_writer.IsAvailable)
            return Outcome(selected, ProtectedArtifactStatus.UnsupportedPlatform);

        var snapshot = Form.Snapshot();
        if (snapshot.InputRevision != prepared.InputRevision)
            return Outcome(selected, ProtectedArtifactStatus.InvalidRequest);
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(Form.Definition, selected, snapshot);
        if (!bundle.Succeeded || Form.InputRevision != snapshot.InputRevision)
            return Outcome(selected, ProtectedArtifactStatus.InvalidRequest);
        if (_beforePublication is not null) await _beforePublication(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            _writer, bundle.Artifacts, _baseDirectory, cancellationToken);
        return new(new SetupTerminalArtifactResult(result.Outcomes), ReadOnlyMemory<byte>.Empty);
    }

    private static SetupPresentationOutcome Outcome(IEnumerable<string> selected, ProtectedArtifactStatus status) =>
        new(new SetupTerminalArtifactResult(selected.Select(id =>
            new ProtectedArtifactBundleOutcome(id, status)).ToArray()), ReadOnlyMemory<byte>.Empty);

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
        if (_disposed) return;
        _disposed = true;
        Form.InputChanged -= InputChanged;
        ClearPrivateState();
        Invalidated = null;
    }
}
