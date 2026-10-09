namespace Configurator.Desktop.ViewModels;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;

public enum DesktopDisplayMode { Essential, Guided, Advanced }

/// <summary>Adapts a pinned local definition to native editing and protected output.</summary>
public sealed class SetupDesktopShellViewModel : ObservableObject, IDisposable
{
    private readonly Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactStatus>> _save;
    private readonly Func<IReadOnlyList<DefinitionArtifact>, string, CancellationToken,
        Task<ProtectedArtifactBundleResult>> _publishBundle;
    private readonly SetupPresentationSession _session = new(new StrongReferenceMessenger());
    private SetupPresentationWorkspace? _workspace;
    private string[]? _preparedBundleSelection;
    private long _preparedBundleInputRevision;
    private readonly HashSet<string> _selectedArtifactIds = new(StringComparer.Ordinal);
    private CancellationTokenSource? _saveCancellation;
    private CancellationTokenSource? _importCancellation;
    private DefinitionFormModel? _form;
    private string _definitionPath = string.Empty;
    private string _outputDirectory;
    private string _fileName = string.Empty;
    private string _existingFilePath = string.Empty;
    private string _status = "definition-required";
    private string? _artifactId;
    private DesktopDisplayMode _displayMode = DesktopDisplayMode.Guided;
    private long _generation;
    private bool _isSaving;
    private bool _disposed;

    public SetupDesktopShellViewModel()
        : this(NativeSaveAsync, Directory.GetCurrentDirectory(), new ProtectedArtifactWriter().IsAvailable) { }

    internal SetupDesktopShellViewModel(
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactStatus>> save,
        string outputDirectory, bool protectedOutputAvailable,
        Func<IReadOnlyList<DefinitionArtifact>, string, CancellationToken,
            Task<ProtectedArtifactBundleResult>>? publishBundle = null)
    {
        _save = save;
        _publishBundle = publishBundle ?? ((artifacts, directory, cancellation) =>
            ProtectedArtifactBundlePublisher.PublishAsync(new ProtectedArtifactWriter(),
                artifacts, directory, cancellation));
        _outputDirectory = outputDirectory;
        ProtectedSaveAvailable = protectedOutputAvailable;
    }

    public DefinitionFormModel? Form => _form;
    public IReadOnlyList<SetupArtifactDefinition> Artifacts => _form?.Definition.Artifacts ?? [];
    public IReadOnlyList<string> SelectedArtifactIds => Artifacts.Where(item => _selectedArtifactIds.Contains(item.Id))
        .Select(item => item.Id).ToArray();
    public bool ProtectedSaveAvailable { get; }
    public string ProtectedCapability => ProtectedSaveAvailable
        ? "Protected create-only save is available on this host."
        : "Protected save is unavailable on this host; prepared output cannot be saved here.";
    public bool IsSaving { get => _isSaving; private set { SetProperty(ref _isSaving, value); NotifySave(); } }
    public bool CanPrepare => _form is not null && !IsSaving;
    public bool CanSave => ProtectedSaveAvailable && !IsSaving &&
        _workspace?.Result is DefinitionPreparationMetadata { Succeeded: true } &&
        !_workspace.Output.IsEmpty;
    public bool CanSaveBundle => ProtectedSaveAvailable && !IsSaving && _preparedBundleSelection is not null;
    public DefinitionPreparationMetadata? Preparation => _workspace?.Result as DefinitionPreparationMetadata;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string DefinitionPin => _form is null ? "No definition loaded" :
        $"{_form.Definition.Title} | {_form.Definition.ApplicationId} | publisher: {_form.Definition.Publisher}\n" +
        $"Family: {_form.Definition.FamilyId:D} | revision: {_form.Definition.Revision} | SHA-256: {_form.Definition.Digest.Value}\n" +
        "Publisher metadata is not a trust assertion. Local preparation does not apply configuration.";
    public string ArtifactConsequences => Artifacts.FirstOrDefault(item => item.Id == ArtifactId) is { } artifact
        ? $"Format: {artifact.Format} | apply: {artifact.ApplySemantics} | restart: {artifact.Restart}\n" +
          "Restricted output. Save creates a new owner-only file; it never overwrites or applies it."
        : "Select a declared artifact.";

    public string DefinitionPath
    {
        get => _definitionPath;
        set { if (SetProperty(ref _definitionPath, value)) Invalidate(); }
    }
    public string OutputDirectory
    {
        get => _outputDirectory;
        set { if (SetProperty(ref _outputDirectory, value)) Invalidate(); }
    }
    public string FileName
    {
        get => _fileName;
        set { if (SetProperty(ref _fileName, value)) Invalidate(); }
    }
    public string ExistingFilePath
    {
        get => _existingFilePath;
        set { if (SetProperty(ref _existingFilePath, value)) Invalidate(); }
    }
    public string? ArtifactId
    {
        get => _artifactId;
        set
        {
            if (!SetProperty(ref _artifactId, value)) return;
            Invalidate();
            FileName = Artifacts.FirstOrDefault(item => item.Id == value)?.FileName ?? string.Empty;
            OnPropertyChanged(nameof(ArtifactConsequences));
        }
    }
    public DesktopDisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (!SetProperty(ref _displayMode, value)) return;
            if (_form is not null)
                _form.DisplayMode = value == DesktopDisplayMode.Advanced ? SetupDisplayMode.Advanced : SetupDisplayMode.Guided;
        }
    }

    public async Task<bool> LoadDefinitionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unload();
        long generation = _generation;
        string path = DefinitionPath;
        byte[] source = [];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous);
            if (stream.Length > ProtectedArtifactWriter.MaximumBytes)
            {
                Status = "definition-invalid:source-limit";
                return false;
            }
            // The extra byte also bounds a file that grows after the length check.
            source = new byte[ProtectedArtifactWriter.MaximumBytes + 1];
            int count = 0;
            while (count < source.Length)
            {
                int read = await stream.ReadAsync(source.AsMemory(count), cancellationToken);
                if (read == 0) break;
                count += read;
            }
            if (count > ProtectedArtifactWriter.MaximumBytes)
            {
                Status = "definition-invalid:source-limit";
                return false;
            }
            if (_disposed || generation != _generation) return false;
            return LoadDefinition(source.AsMemory(0, count),
                Path.GetExtension(path).Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".yml", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!_disposed && generation == _generation) Status = "definition-unreadable";
            return false;
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && generation == _generation) Status = "load-cancelled";
            return false;
        }
        finally { CryptographicOperations.ZeroMemory(source); }
    }

    public bool LoadDefinition(ReadOnlyMemory<byte> source, bool yaml = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unload();
        try
        {
            SetupDefinition definition = yaml ? SetupDefinitionCodec.ParseYaml(source) : SetupDefinitionCodec.ParseJson(source);
            _form = new DefinitionFormModel(definition);
            _form.InputChanged += InputChanged;
            _form.DisplayMode = DisplayMode == DesktopDisplayMode.Advanced ? SetupDisplayMode.Advanced : SetupDisplayMode.Guided;
            ArtifactId = definition.Artifacts.Count > 0 ? definition.Artifacts[0].Id : null;
            if (ArtifactId is not null) _selectedArtifactIds.Add(ArtifactId);
            FileName = definition.Artifacts.Count > 0 ? definition.Artifacts[0].FileName : string.Empty;
            Status = "definition-loaded";
            NotifyDefinition();
            return true;
        }
        catch (SetupDefinitionException exception)
        {
            Status = $"definition-invalid:{exception.Code}";
            NotifyDefinition();
            return false;
        }
    }

    public bool Validate()
    {
        if (_form is null) { Status = "definition-required"; return false; }
        try
        {
            var result = _form.Validate();
            bool ready = result.IsReady && !_form.HasInputErrors;
            Status = ready ? "validation-ready" : "validation-invalid:" +
                string.Join(",", result.Diagnostics.Select(item => $"{item.FieldId}:{item.Code}")
                    .Concat(_form.Fields.Where(item => !item.TryValue(out _)).Select(item => $"{item.Definition.Id}:input-invalid")));
            return ready;
        }
        catch (SetupDefinitionException exception) { Status = $"validation-invalid:{exception.Code}"; return false; }
    }

    public async Task<bool> PrepareAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsSaving) return false;
        Invalidate();
        if (!Validate() || _form is null || ArtifactId is null) return false;
        if (!SetupWorkspaceId.TryCreate("desktop-definition", out var id))
            throw new InvalidOperationException("desktop-workspace-invalid");
        var workspace = _session.CreateWorkspace(id,
            new DefinitionPresentationOperation(_form.Definition, ArtifactId, _form.Snapshot()));
        _workspace = workspace;
        workspace.Activate();
        long generation = _generation;
        await workspace.ExecuteAsync(Guid.CreateVersion7());
        if (_disposed || generation != _generation || !ReferenceEquals(_workspace, workspace)) return false;
        Status = Preparation is { Succeeded: true } metadata
            ? $"artifact-ready:{metadata.ArtifactId}:{metadata.Digest?.Value}"
            : "preparation-invalid:" + string.Join(",", Preparation?.DiagnosticCodes ?? []);
        NotifySave();
        return Preparation?.Succeeded == true;
    }

    public void SelectArtifact(string artifactId, bool selected)
    {
        if (Artifacts.All(item => item.Id != artifactId)) return;
        if (selected ? _selectedArtifactIds.Add(artifactId) : _selectedArtifactIds.Remove(artifactId))
        {
            Invalidate();
            OnPropertyChanged(nameof(SelectedArtifactIds));
        }
    }

    public bool PrepareBundle()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsSaving) return false;
        Invalidate();
        if (!Validate() || _form is null) return false;
        string[] selection = SelectedArtifactIds.ToArray();
        using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(
            _form.Definition, selection, _form.Snapshot());
        if (!bundle.Succeeded)
        {
            Status = "bundle-invalid:" + string.Join(",", bundle.Diagnostics.Select(item => item.Code));
            return false;
        }
        _preparedBundleSelection = selection;
        _preparedBundleInputRevision = _form.InputRevision;
        Status = $"bundle-selected:{selection.Length}";
        NotifySave();
        return true;
    }

    public async Task<ProtectedArtifactBundleResult?> SaveBundleAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_preparedBundleSelection is null || _form is null || IsSaving) return null;
        string[] selection = _preparedBundleSelection;
        long inputRevision = _preparedBundleInputRevision;
        _preparedBundleSelection = null;
        IsSaving = true;
        long generation = _generation;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _saveCancellation = cancellation;
        try
        {
            if (!ProtectedSaveAvailable)
            {
                Status = "bundle-save-blocked:UnsupportedPlatform";
                return null;
            }
            if (cancellation.Token.IsCancellationRequested || _form.InputRevision != inputRevision)
                return null;
            using DefinitionBundleResult bundle = DefinitionBundleComposer.Compose(
                _form.Definition, selection, _form.Snapshot());
            if (!bundle.Succeeded)
            {
                if (generation == _generation && !_disposed)
                    Status = "bundle-invalid:" + string.Join(",", bundle.Diagnostics.Select(item => item.Code));
                return null;
            }
            if (generation != _generation || cancellation.Token.IsCancellationRequested) return null;
            ProtectedArtifactBundleResult result = await _publishBundle(bundle.Artifacts, OutputDirectory,
                cancellation.Token);
            if (generation == _generation && !_disposed)
                Status = result.Succeeded ? "bundle-save-complete" :
                    (result.Outcomes.Any(item => item.Status == ProtectedArtifactStatus.Written)
                        ? "bundle-save-partial:" : "bundle-save-blocked:") +
                    string.Join(",", result.Outcomes.Select(item => $"{item.ArtifactId}:{item.Status?.ToString() ?? "not-attempted"}"));
            return result;
        }
        catch (OperationCanceledException)
        {
            if (generation == _generation && !_disposed) Status = "bundle-save-cancelled";
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (generation == _generation && !_disposed) Status = "bundle-save-blocked:IoFailure";
            return null;
        }
        finally
        {
            _saveCancellation = null;
            IsSaving = false;
            NotifySave();
        }
    }

    public async Task<bool> ImportFileAsync(CancellationToken cancellationToken = default)
    {
        byte[] source = [];
        try
        {
            return await ImportAsync(async token =>
            {
                await using var stream = new FileStream(ExistingFilePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.Asynchronous);
                if (stream.Length > ProtectedArtifactWriter.MaximumBytes) throw new IOException("source-limit");
                source = new byte[ProtectedArtifactWriter.MaximumBytes + 1];
                int count = 0;
                while (count < source.Length)
                {
                    int read = await stream.ReadAsync(source.AsMemory(count), token);
                    if (read == 0) break;
                    count += read;
                }
                if (count > ProtectedArtifactWriter.MaximumBytes) throw new IOException("source-limit");
                return source.AsMemory(0, count);
            }, cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(source); }
    }

    public async Task<bool> ImportAsync(Func<CancellationToken, Task<ReadOnlyMemory<byte>>> read,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_form is null || ArtifactId is null || IsSaving) return false;
        DefinitionFormModel form = _form;
        string artifactId = ArtifactId;
        long generation = _generation;
        _importCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _importCancellation = cancellation;
        try
        {
            DefinitionImportResult result = await form.ImportAsync(artifactId, read, cancellation.Token);
            if (_disposed || !ReferenceEquals(_form, form)) return false;
            if (result.Succeeded)
            {
                // Applying one imported form revision raises exactly one input invalidation.
                if (_generation == generation + 1) Status = "import-ready";
                OnPropertyChanged(nameof(Form));
                return true;
            }
            if (generation == _generation) Status = result.Code;
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (generation == _generation && !_disposed) Status = "import-unreadable";
            return false;
        }
        finally
        {
            if (ReferenceEquals(_importCancellation, cancellation)) _importCancellation = null;
        }
    }

    public async Task<ProtectedArtifactStatus> SaveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsSaving || _workspace?.Result is not DefinitionPreparationMetadata { Succeeded: true })
            return ProtectedArtifactStatus.InvalidRequest;
        byte[] bytes = _workspace.Output.ToArray();
        ClearPreparation();
        IsSaving = true;
        long generation = _generation;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _saveCancellation = cancellation;
        try
        {
            var result = !ProtectedSaveAvailable ? ProtectedArtifactStatus.UnsupportedPlatform
                : !SafeFileName(FileName) || string.IsNullOrWhiteSpace(OutputDirectory) ? ProtectedArtifactStatus.UnsafeTarget
                : await _save(Path.Combine(OutputDirectory, FileName), bytes, cancellation.Token);
            if (generation != _generation || _disposed) return ProtectedArtifactStatus.InvalidRequest;
            Status = result == ProtectedArtifactStatus.Written ? "save-complete" : $"save-failed:{result}";
            return result;
        }
        catch (OperationCanceledException)
        {
            if (generation == _generation && !_disposed) Status = "save-cancelled";
            return ProtectedArtifactStatus.InvalidRequest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (generation == _generation && !_disposed) Status = "save-failed:IoFailure";
            return ProtectedArtifactStatus.IoFailure;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            _saveCancellation = null;
            IsSaving = false;
        }
    }

    public void Cancel()
    {
        Invalidate();
        Status = "operation-cancelled";
    }

    private void InputChanged(object? sender, EventArgs args) => Invalidate();
    private void Invalidate()
    {
        checked { _generation++; }
        _saveCancellation?.Cancel();
        _importCancellation?.Cancel();
        ClearPreparation();
        Status = _form is null ? "definition-required" : "inputs-changed";
    }
    private void ClearPreparation()
    {
        _preparedBundleSelection = null;
        if (_workspace is not null)
        {
            if (MemoryMarshal.TryGetArray(_workspace.Output, out ArraySegment<byte> segment))
                CryptographicOperations.ZeroMemory(segment.AsSpan());
            _workspace.Dispose();
            _workspace = null;
        }
        NotifySave();
    }
    private void Unload()
    {
        Invalidate();
        if (_form is not null) { _form.InputChanged -= InputChanged; _form.Dispose(); }
        _form = null;
        _artifactId = null;
        _selectedArtifactIds.Clear();
        _fileName = string.Empty;
        NotifyDefinition();
    }
    private void NotifySave()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveBundle));
        OnPropertyChanged(nameof(CanPrepare));
        OnPropertyChanged(nameof(Preparation));
    }
    private void NotifyDefinition()
    {
        foreach (string property in new[] { nameof(Form), nameof(Artifacts), nameof(DefinitionPin),
                     nameof(ArtifactId), nameof(FileName), nameof(ArtifactConsequences), nameof(CanPrepare) })
            OnPropertyChanged(property);
        OnPropertyChanged(nameof(SelectedArtifactIds));
    }
    private static bool SafeFileName(string value) =>
        value.Length is > 0 and <= 128 && value is not "-" and not "." and not ".." &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static async Task<ProtectedArtifactStatus> NativeSaveAsync(
        string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        using var preparation = await new ProtectedArtifactWriter()
            .PrepareAsync(SetupArtifactKind.Unknown, path, bytes, cancellationToken);
        return await preparation.CommitAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unload();
        _session.Dispose();
    }
    public override string ToString() => nameof(SetupDesktopShellViewModel);
}
