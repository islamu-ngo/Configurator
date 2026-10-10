namespace Configurator.Browser.Components.Configuration;

using System.Security.Cryptography;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Browser.Services;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

public partial class ConfigurationEditor
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Parameter, EditorRequired] public SetupDefinition Definition { get; set; } = null!;
    private SetupDefinition? _pinned;
    private DefinitionFormModel? _form;
    private readonly SetupPresentationSession _session = new(new WeakReferenceMessenger());
    private readonly List<PreparedArtifact> _prepared = [];
    private readonly List<string> _diagnostics = [];
    private readonly Dictionary<string, string> _invalidInputs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mountedFields = new(StringComparer.Ordinal);
    private string _status = "Supply ordinary settings. Secret entry is disabled.";
    private string _statusCode = "workspace-ready";
    private string _importArtifact = string.Empty;
    private string? _selectedSection;
    private string? _focusField;
    private int _focusRow = -1;
    private long _focusGeneration;
    private bool _focusConsent;
    private ElementReference _consentControl;
    private long _generation;
    private long _fileKey;
    private CancellationTokenSource? _readCancellation;
    private byte[]? _readBuffer;
    private InputFile? _fileInput;
    private bool _restoreFocus;
    private ElementReference _startButton;
    private bool _focusStart;
    private bool _disposed;
    private bool _ready;
    private bool _beginPending;
    private BrowserOfflinePolicy? _offline;
    private long _offlineLease;
    private DefinitionValidationResult? _validation;
    private BrowserNavigationState? _navigation;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_pinned, Definition)) return;
        Invalidate();
        _form?.Dispose();
        _invalidInputs.Clear();
        _mountedFields.Clear();
        _selectedSection = null;
        _focusField = null;
        _pinned = Definition;
        _ready = false;
        _beginPending = true;
        _form = new DefinitionFormModel(Definition, DefinitionSessionPrivacy.BrowserNoSecrets);
        _form.InputChanged += InputChanged;
        _importArtifact = Definition.Artifacts.Count > 0 ? Definition.Artifacts[0].Id : string.Empty;
        _navigation ??= Services.GetService(typeof(BrowserNavigationState)) as BrowserNavigationState;
        _navigation?.Register(this, Invalidate, DiscardAsync);
    }

    private static string Group(SetupFieldDefinition field) => field.Group ?? "Other";
    private string SectionId(string group) => "configuration-section-" + Array.FindIndex(Definition.Fields.Select(Group).Distinct(StringComparer.Ordinal).ToArray(), item => item == group);
    private ConfigurationSection[] Sections(IReadOnlyList<SetupFieldState> fields) =>
        fields.GroupBy(state => Group(state.Definition)).Where(group => group.Any(state => state.IsVisible(_form!.DisplayMode) || _invalidInputs.ContainsValue(state.Id)))
            .Select(group => new ConfigurationSection(group.Key, group.Count(state => state.IsActive),
                group.Count(state => state.HasError || _invalidInputs.ContainsValue(state.Id)))).ToArray();
    private string? SelectedSection(IReadOnlyList<ConfigurationSection> sections) =>
        sections.Any(item => item.Name == _selectedSection) ? _selectedSection : sections.Count > 0 ? sections[0].Name : null;
    private void SelectSection(string section) { _selectedSection = section; _focusField = null; }
    private void CorrectField(string fieldId)
    {
        SetupFieldDefinition? field = Definition.Fields.FirstOrDefault(item => item.Id == fieldId || fieldId.StartsWith(item.Id + "/", StringComparison.Ordinal));
        if (field is null) return;
        _selectedSection = Group(field);
        _focusField = field.Id;
        string suffix = fieldId.Length > field.Id.Length ? fieldId[(field.Id.Length + 1)..] : string.Empty;
        _focusRow = int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int row) ? row : -1;
        ++_focusGeneration;
        _focusConsent = !_form!.CanEdit([field.Id]);
    }
    private async Task EnableSecretsAsync()
    {
        DefinitionFormModel form = _form!;
        await ConsentAsync(new ChangeEventArgs { Value = true });
        if (!_disposed && ReferenceEquals(form, _form) && form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled)
            _focusConsent = true;
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        Invalidate();
        _status = "Answers changed. Previous output has been discarded.";
        _statusCode = "answers-changed";
        StateHasChanged();
    }
    private void ChangeMode(ChangeEventArgs args)
    {
        if (!Enum.TryParse(args.Value?.ToString(), out SetupDisplayMode mode) || !Enum.IsDefined(mode)) return;
        Invalidate();
        _form!.DisplayMode = mode;
        _status = "Field view changed. Previous output has been discarded; validation still covers every active field.";
        _statusCode = "display-mode-changed";
    }
    private void ChangeImportFormat(ChangeEventArgs args)
    {
        string? artifactId = args.Value?.ToString();
        if (!Definition.Artifacts.Any(item => item.Id == artifactId)) return;
        Invalidate();
        _importArtifact = artifactId!;
        _status = "Import format changed. Previous output and pending import have been discarded.";
        _statusCode = "import-format-changed";
    }
    private async Task ConsentAsync(ChangeEventArgs args)
    {
        bool enabled = args.Value is true;
        DefinitionFormModel form = _form!;
        long generation = _generation + 1;
        await InvalidateOutputAsync();
        if (_disposed || generation != _generation || !ReferenceEquals(form, _form)) return;
        form.SetSecretEntryEnabled(enabled);
        _status = enabled ? "Secret entry enabled for this workspace. Values remain masked." : "Secret entry revoked. Secret answers and output discarded; ordinary settings retained.";
        _statusCode = enabled ? "secret-entry-enabled" : "secret-entry-revoked";
    }

    private async Task PrepareAsync(bool draft)
    {
        DefinitionFormModel form = _form!;
        long generation = _generation + 1;
        await InvalidateOutputAsync();
        if (_disposed || generation != _generation || !ReferenceEquals(form, _form)) return;
        SetupAnswerSnapshot answers;
        try { answers = form.Snapshot(); }
        catch (SetupDefinitionException)
        {
            _status = "Answers exceed the shared preparation limit. Reduce input sizes before preparing configuration.";
            _statusCode = "answer-budget-exceeded";
            _diagnostics.Add("answer-budget-exceeded");
            return;
        }
        if (form.HasAnswerBudgetError)
        {
            _status = "An input exceeded the shared preparation limit. Reduce or clear the rejected field before preparing configuration.";
            _statusCode = "answer-budget-exceeded";
            _diagnostics.Add("answer-budget-exceeded");
            return;
        }
        _validation = draft ? DefinitionValidator.ValidateDraft(Definition, answers) : form.Validate();
        if (form.HasInputErrors || _invalidInputs.Count > 0)
        {
            _diagnostics.Add("input-type-invalid");
            _status = "Correct invalid field values before preparing configuration.";
            _statusCode = "input-type-invalid";
            return;
        }
        HashSet<string> admittedArtifacts;
        if (draft)
        {
            using DefinitionDraftBundleResult bundle = DefinitionDraftArtifactComposer.ComposeBundle(Definition, Definition.Artifacts.Select(item => item.Id), answers);
            if (bundle.Artifacts.Count == 0 || bundle.Diagnostics.Any(item => item.Code is not ("draft-dependency-unresolved" or "artifact-omitted")))
            {
                _diagnostics.AddRange(bundle.Diagnostics.Select(item => $"{item.FieldId}: {item.Code}"));
                _status = "No configuration prepared. Correct required settings or reduce output size.";
                _statusCode = "preparation-rejected";
                return;
            }
            admittedArtifacts = bundle.Artifacts.Select(item => item.ArtifactId).ToHashSet(StringComparer.Ordinal);
            _diagnostics.AddRange(bundle.Diagnostics.Select(item => item.Code));
        }
        else
        {
            using DefinitionBundleResult bundle = DefinitionBundleComposer.ComposeActive(Definition, answers);
            if (!bundle.Succeeded)
            {
                _diagnostics.AddRange(bundle.Diagnostics.Select(item => $"{item.FieldId}: {item.Code}"));
                _status = "No configuration prepared. Correct required settings or reduce output size.";
                _statusCode = "preparation-rejected";
                return;
            }
            admittedArtifacts = bundle.Artifacts.Select(item => item.ArtifactId).ToHashSet(StringComparer.Ordinal);
        }
        foreach (var artifact in Definition.Artifacts)
        {
            if (!admittedArtifacts.Contains(artifact.Id)) continue;
            var operation = new DefinitionPresentationOperation(Definition, artifact.Id, answers, prepareDraft: draft);
            SetupWorkspaceId.TryCreate(Guid.NewGuid().ToString("N"), out var workspaceId);
            var workspace = _session.CreateWorkspace(workspaceId, operation);
            workspace.Activate();
            await workspace.ExecuteAsync(Guid.NewGuid());
            if (_disposed || generation != _generation) { workspace.Dispose(); return; }
            if (workspace.Result is not DefinitionPreparationMetadata { Succeeded: true } metadata)
            {
                if (workspace.Result is DefinitionPreparationMetadata failure) _diagnostics.AddRange(failure.DiagnosticCodes);
                _diagnostics.AddRange(form.Validate().Diagnostics.Select(item => $"{item.FieldId}: {item.Code}"));
                workspace.Dispose();
                continue;
            }
            _prepared.Add(new PreparedArtifact(metadata, workspace, Javascript));
        }
        _status = _prepared.Count == 0 ? "No configuration prepared. Correct required settings." : $"Prepared {_prepared.Count} private artifact(s). Reveal, copy or download each explicitly.";
        _statusCode = _prepared.Count == 0 ? "preparation-rejected" : "preparation-ready";
    }
    private void InputValidityChanged((string Id, string FieldId, bool Invalid) value)
    {
        if (value.Invalid) _invalidInputs[value.Id] = value.FieldId;
        else _invalidInputs.Remove(value.Id);
    }
    private async Task CopyAsync(PreparedArtifact artifact)
    {
        bool copied = await artifact.Export.CopyAsync();
        if (!_prepared.Contains(artifact)) return;
        _status = copied ? "Current artifact copied. Clipboard contents remain outside this workspace." : "Clipboard unavailable or denied. Use Download instead.";
        _statusCode = copied ? "copied" : "clipboard-unavailable";
    }
    private async Task DownloadAsync(PreparedArtifact artifact)
    {
        bool downloaded = await artifact.Export.DownloadAsync(artifact.Metadata.FileName!, artifact.Metadata.MediaType!);
        if (!_prepared.Contains(artifact)) return;
        _status = downloaded ? $"Download requested as {artifact.Metadata.FileName}. If your browser changes the filename, rename it to this declared name. Protect the downloaded file on your device." : "Download could not start. Prepare current output again.";
    }
    private async Task RevealAsync(PreparedArtifact artifact)
    {
        if (artifact.Revealed)
        {
            await artifact.Export.InvalidateAsync();
            if (!_prepared.Contains(artifact)) return;
            artifact.Export.SetOutput(artifact.Workspace.Output);
            artifact.Revealed = false;
            _status = "Private preview hidden.";
            return;
        }
        artifact.Revealed = true;
        StateHasChanged();
        bool revealed = await artifact.Export.RevealAsync(artifact.PreviewElement);
        if (!_prepared.Contains(artifact)) return;
        artifact.Revealed = revealed;
        _status = revealed ? "Private preview revealed. Hide or clear it when finished." : "Preview unavailable. Use Download instead.";
    }
    private async Task ImportAsync(InputFileChangeEventArgs args)
    {
        try
        {
            await using Stream stream = args.File.OpenReadStream(1024 * 1024);
            await LoadAsync(stream);
        }
        catch (IOException) { _status = "Import rejected: file exceeds the limit or cannot be read."; _statusCode = "import-rejected"; }
        finally { ++_fileKey; _restoreFocus = true; }
    }
    public async Task LoadAsync(Stream stream)
    {
        DefinitionFormModel form = _form!;
        long generation = _generation + 1;
        await InvalidateOutputAsync();
        if (_disposed || generation != _generation || !ReferenceEquals(form, _form)) return;
        var cancellation = new CancellationTokenSource();
        _readCancellation = cancellation;
        byte[] buffer = new byte[1024 * 1024 + 1];
        _readBuffer = buffer;
        try
        {
            int length = 0;
            while (length < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(length), cancellation.Token);
                if (read == 0) break;
                length += read;
            }
            if (_disposed || generation != _generation || cancellation.IsCancellationRequested) return;
            if (length > 1024 * 1024) { _status = "Import rejected: maximum size is 1 MiB."; _statusCode = "import-too-large"; return; }
            var result = await form.ImportAsync(_importArtifact, _ => Task.FromResult<ReadOnlyMemory<byte>>(buffer.AsMemory(0, length)), cancellation.Token);
            if (_disposed || !ReferenceEquals(form, _form) || generation != _generation && !result.Succeeded) return;
            _status = result.Succeeded ? "Artifact imported into this local workspace." : result.Code switch
            {
                "answer-conflict" => "Import rejected (answer-conflict). Existing answers differ from the file. Clear the conflicting fields before importing; existing answers are preserved.",
                "inverse-unsupported" => "Import rejected (inverse-unsupported). This declared artifact has no supported reverse mapping. Recreate its settings with the typed controls.",
                "import-secret-consent-required" => "Import rejected (import-secret-consent-required). Explicitly enable secret entry for this workspace or remove secret values from the file.",
                _ => $"Import rejected ({result.Code}). Check the declared format and field values. Existing answers are preserved."
            };
            _statusCode = result.Succeeded ? "imported" : result.Code;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or InvalidOperationException or SetupDefinitionException)
        {
            if (generation == _generation) { _status = "Import rejected or cancelled."; _statusCode = "import-rejected"; }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            if (ReferenceEquals(_readBuffer, buffer)) _readBuffer = null;
            if (ReferenceEquals(_readCancellation, cancellation)) _readCancellation = null;
            cancellation.Dispose();
            if (!_disposed) await InvokeAsync(StateHasChanged);
        }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || _beginPending) { _beginPending = false; await BeginAsync(); }
        if (_focusStart) { _focusStart = false; await _startButton.FocusAsync(preventScroll: true); }
        if (_focusConsent) { _focusConsent = false; await _consentControl.FocusAsync(); }
        if (_restoreFocus && _fileInput?.Element is { } element) { _restoreFocus = false; await element.FocusAsync(preventScroll: true); }
    }
    public async Task BeginAsync()
    {
        if (_disposed) return;
        _navigation?.Register(this, Invalidate, DiscardAsync);
        long generation = _generation;
        DefinitionFormModel form = _form!;
        _offline ??= Services.GetService(typeof(BrowserOfflinePolicy)) as BrowserOfflinePolicy;
        if (_offline is not null) await _offline.InitializeAsync();
        if (_disposed || generation != _generation) return;
        ValueTask<bool> beginning = _offline?.BeginWorkspaceAsync() ?? ValueTask.FromResult(true);
        long lease = _offline?.WorkspaceGeneration ?? 0;
        bool ready = await beginning;
        if (_disposed || generation != _generation || !ReferenceEquals(form, _form)
            || _offline is not null && lease != _offline.WorkspaceGeneration)
        {
            if (_offline is not null) await _offline.EndWorkspaceAsync(lease);
            return;
        }
        _ready = ready;
        if (_ready) _offlineLease = lease;
        if (!_ready) { _status = "Offline app preparation is incomplete. Finish loading public app assets before entering answers."; _statusCode = "app-preparation-incomplete"; }
        if (!_disposed) StateHasChanged();
    }
    public void Invalidate()
    {
        ++_generation;
        _readCancellation?.Cancel();
        if (_readBuffer is not null) CryptographicOperations.ZeroMemory(_readBuffer);
        _readBuffer = null;
        PreparedArtifact[] previous = _prepared.ToArray();
        _prepared.Clear();
        _diagnostics.Clear();
        _validation = null;
        foreach (var prepared in previous) _ = prepared.DisposeAsync().AsTask();
    }
    private async Task InvalidateOutputAsync()
    {
        ++_generation;
        _readCancellation?.Cancel();
        if (_readBuffer is not null) CryptographicOperations.ZeroMemory(_readBuffer);
        PreparedArtifact[] previous = _prepared.ToArray();
        _prepared.Clear();
        _diagnostics.Clear();
        _validation = null;
        foreach (var prepared in previous) await prepared.DisposeAsync();
    }
    private async Task ClearAsync()
    {
        Task discarding = DiscardAsync();
        long generation = _generation;
        DefinitionFormModel form = _form!;
        await discarding;
        if (_disposed || generation != _generation || !ReferenceEquals(form, _form) || _ready) return;
        ++_fileKey;
        _status = "Answers and output cleared. Secret entry is disabled. Start a new local workspace to continue.";
        _statusCode = "workspace-cleared";
        _focusStart = true;
    }
    public async Task DiscardAsync()
    {
        DefinitionFormModel form = _form!;
        long lease = _offlineLease;
        _ready = false;
        form.Dispose();
        _navigation?.Release(this);
        if (!_disposed && ReferenceEquals(form, _form))
        {
            _form = new DefinitionFormModel(Definition, DefinitionSessionPrivacy.BrowserNoSecrets);
            _form.InputChanged += InputChanged;
            _invalidInputs.Clear();
            _mountedFields.Clear();
            _focusField = null;
        }
        await InvalidateOutputAsync();
        if (_offline is not null) await _offline.EndWorkspaceAsync(lease);
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _mountedFields.Clear();
        _navigation?.Release(this);
        _ready = false;
        long lease = _offlineLease;
        _form?.Dispose();
        _session.Dispose();
        await InvalidateOutputAsync();
        if (_offline is not null) await _offline.EndWorkspaceAsync(lease);
        GC.SuppressFinalize(this);
    }
    private sealed class PreparedArtifact : IAsyncDisposable
    {
        public PreparedArtifact(DefinitionPreparationMetadata metadata, SetupPresentationWorkspace workspace, IJSRuntime javascript)
        {
            Metadata = metadata;
            Workspace = workspace;
            Export = new BrowserArtifactExport(javascript);
            Export.SetOutput(workspace.Output);
        }
        public DefinitionPreparationMetadata Metadata { get; }
        public SetupPresentationWorkspace Workspace { get; }
        public BrowserArtifactExport Export { get; }
        public ElementReference PreviewElement;
        public bool Revealed { get; set; }
        public async ValueTask DisposeAsync() { Workspace.Dispose(); await Export.DisposeAsync(); }
    }
}
