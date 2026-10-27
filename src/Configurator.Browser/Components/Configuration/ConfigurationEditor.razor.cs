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
    [Inject] private BrowserOfflinePolicy Offline { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Parameter, EditorRequired] public SetupDefinition Definition { get; set; } = null!;
    private SetupDefinition? _pinned;
    private DefinitionFormModel? _form;
    private readonly SetupPresentationSession _session = new(new WeakReferenceMessenger());
    private readonly List<PreparedArtifact> _prepared = [];
    private readonly List<string> _diagnostics = [];
    private string _activeWorkflowStage = "configure";
    private string? _focusWorkflowStage;
    private int _requiredCount;
    private ElementReference _configureHeading;
    private ElementReference _sectionHeading;
    private ElementReference _reviewHeading;
    private ElementReference _prepareHeading;
    private string _status = "Supply ordinary settings. Secret entry is disabled.";
    private string _statusCode = "workspace-ready";
    private string _importArtifact = string.Empty;
    private string? _selectedSection;
    private string? _focusField;
    private IReadOnlyList<string> _focusPath = [];
    private long _focusGeneration;
    private bool _focusConsent;
    private bool _returnToReview;
    private ElementReference _consentControl;
    private long _generation;
    private CancellationTokenSource _mountCancellation = new();
    private Task _outputRetirement = Task.CompletedTask;
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
    private bool _compact;
    private long _offlineLease;
    private DefinitionValidationResult? _validation;
    private DefinitionDiagnostic? _importDiagnostic;
    private BrowserNavigationState? _navigation;
    private long _navigationLease;
    private WorkspaceSearchState? _search;
    private long _searchLease;
    private Dictionary<string, EditorSection> _editorSections = new(StringComparer.Ordinal);
    private DefinitionDiscoverySelection? _observedDiscoverySelection;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_pinned, Definition)) return;
        InvalidateMounts();
        Invalidate();
        _search?.Release(this, _searchLease);
        _form?.Dispose();
        _activeWorkflowStage = "configure";
        _returnToReview = false;
        _selectedSection = null;
        _focusField = null;
        _pinned = Definition;
        _compact = false;
        ImportableArtifacts = Definition.Artifacts
            .Where(artifact => DefinitionImport.CanImport(Definition, artifact.Id)).ToArray();
        _ready = false;
        _beginPending = true;
        _form = new DefinitionFormModel(Definition, DefinitionSessionPrivacy.BrowserNoSecrets);
        IndexSections();
        _requiredCount = _form.Projection.Progress.Required;
        _form.InputChanged += InputChanged;
        _form.ViewChanged += ViewChanged;
        _importArtifact = ImportableArtifacts.FirstOrDefault()?.Id ?? string.Empty;
        _navigation ??= Services.GetService(typeof(BrowserNavigationState)) as BrowserNavigationState;
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync) ?? 0;
        RegisterSearch();
    }

    private void RegisterSearch()
    {
        _search ??= Services.GetService(typeof(WorkspaceSearchState)) as WorkspaceSearchState;
        if (_search is null) return;
        DefinitionFormModel form = _form!;
        long lease = 0;
        _searchLease = lease = _search.Register(this, () => form.DiscoveryQuery,
            query => form.SetDiscovery(query), builder =>
            {
                builder.OpenComponent<ConfigurationDiscovery>(0);
                builder.AddAttribute(1, nameof(ConfigurationDiscovery.Form), form);
                builder.AddAttribute(2, nameof(ConfigurationDiscovery.Navigate),
                    EventCallback.Factory.Create<DefinitionDiagnostic>(this, diagnostic =>
                    {
                        if (_disposed || !ReferenceEquals(form, _form) || lease != _search.Generation) return;
                        _search.CompleteNavigation(this, lease);
                        CorrectField(diagnostic);
                    }));
                builder.CloseComponent();
            });
    }

    private DefinitionFormExperience SelectedExperience => _form!.Experiences.Single(item => item.Id == _form.SelectedExperienceId);
    private SetupArtifactDefinition[] ImportableArtifacts { get; set; } = [];
    private sealed record EditorSection(string Id, string Title, IReadOnlyList<string> Fields, string? Description);
    private void IndexSections()
    {
        _observedDiscoverySelection = _form!.DiscoverySelection;
        _editorSections = _form.Sections.ToDictionary(section => section.Id, section =>
            new EditorSection(section.Id, section.Title, section.Fields, section.Description),
            StringComparer.Ordinal);
    }
    private EditorSection[] EditorSections()
    {
        return _form!.Projection.Sections
            .Select(section => _editorSections[section.Id])
            .ToArray();
    }
    private bool EditorVisible(string fieldId) => _form!.IsEditorVisible([fieldId]);
    private string SectionId(string section) => "configuration-section-" + Array.FindIndex(EditorSections(), item => item.Id == section);
    private string? SelectedSection(IReadOnlyList<EditorSection> sections) =>
        sections.Any(item => item.Id == _selectedSection) ? _selectedSection : sections.Count > 0 ? sections[0].Id : null;
    private void SelectSection(string section)
    {
        _selectedSection = section;
        _focusField = null;
        SelectWorkflowStage("configure");
    }

    private void SelectWorkflowStage(string stage)
    {
        InvalidateMounts();
        _focusField = null;
        _activeWorkflowStage = stage;
        _focusWorkflowStage = stage;
        if (stage == "review") _validation = _form!.Validate();
    }
    private void CorrectField(DefinitionDiagnostic diagnostic)
    {
        string fieldId = diagnostic.FieldId;
        SetupFieldDefinition? field = Definition.Fields.FirstOrDefault(item => item.Id == fieldId);
        if (field is null) return;
        string[] path = new[] { fieldId }.Concat(diagnostic.RelativePath).ToArray();
        bool focused = _form!.FocusEditor(path, preserveDiscovery: true);
        bool denied = !_form.CanEdit(path);
        if (!focused && !denied) return;
        InvalidateMounts();
        _selectedSection = EditorSections().Single(section => section.Fields.Contains(fieldId)).Id;
        _focusField = denied ? null : field.Id;
        _focusPath = denied ? [] : path;
        _activeWorkflowStage = "configure";
        _focusWorkflowStage = null;
        ++_focusGeneration;
        _focusConsent = denied;
    }
    private void ChangeReviewField(DefinitionDiagnostic diagnostic)
    {
        _returnToReview = true;
        CorrectField(diagnostic);
    }
    private void ReturnToReview()
    {
        _returnToReview = false;
        SelectWorkflowStage("review");
    }
    private void CorrectImportField(DefinitionDiagnostic diagnostic)
    {
        if (diagnostic.Code == "import-secret-consent-required")
        {
            _form!.ResetDiscovery();
            _activeWorkflowStage = "configure";
            _focusWorkflowStage = null;
            _focusField = null;
            _focusConsent = true;
            return;
        }
        var entry = _form!.Projection.Entries.FirstOrDefault(item => item.FieldId == diagnostic.FieldId &&
            item.RelativePath.SequenceEqual(diagnostic.RelativePath));
        if (entry is { IsActive: false } && entry.ActiveControllerFields.Count > 0)
            CorrectField(new("import-activation", entry.ActiveControllerFields[0]));
        else CorrectField(diagnostic);
    }
    private string ImportCorrectionLabel(DefinitionDiagnostic diagnostic) =>
        diagnostic.Code == "import-secret-consent-required" ? "Review secret-entry consent"
            : "Review " + ConfigurationDiagnosticText.Title(Definition.Fields.Single(field => field.Id == diagnostic.FieldId));
    private async Task EnableSecretsAsync()
    {
        DefinitionFormModel form = _form!;
        await ConsentAsync(new ChangeEventArgs { Value = true });
        if (!_disposed && ReferenceEquals(form, _form) && form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled)
            _focusConsent = true;
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        InvalidateMounts();
        _focusField = null;
        Invalidate();
        int required = _form!.Projection.Progress.Required;
        _status = required == _requiredCount ? "Answers changed. Previous output has been discarded."
            : $"Required work changed from {_requiredCount} to {required} answers. Previous output has been discarded.";
        _requiredCount = required;
        _statusCode = "answers-changed";
        _search?.Refresh(this, _searchLease);
        StateHasChanged();
    }
    private void ViewChanged(object? sender, EventArgs args)
    {
        _search?.Refresh(this, _searchLease);
        // Discovery owns its input events and rerenders its own query results.
        if (!ReferenceEquals(_observedDiscoverySelection, _form!.DiscoverySelection))
        {
            _observedDiscoverySelection = _form.DiscoverySelection;
            return;
        }
        InvalidateMounts();
        _focusField = null;
        StateHasChanged();
    }
    private void InvalidateMounts()
    {
        _mountCancellation.Cancel();
        _mountCancellation.Dispose();
        _mountCancellation = new CancellationTokenSource();
    }
    private void ChangeDetail(ChangeEventArgs args)
    {
        switch (args.Value)
        {
            case "authored": _form!.SetDisclosureOverride(null); break;
            case "essential": _form!.SetDisclosureOverride(SetupFieldDisclosure.Essential); break;
            case "guided": _form!.SetDisclosureOverride(SetupFieldDisclosure.Guided); break;
            case "advanced": _form!.SetDisclosureOverride(SetupFieldDisclosure.Advanced); break;
        }
    }
    private void ChangeDensity(ChangeEventArgs args)
    {
        if (args.Value is not ("compact" or "comfortable")) return;
        _compact = args.Value is "compact";
    }
    private void ChangeExperience(ChangeEventArgs args)
    {
        if (args.Value is not string id || !_form!.SelectExperience(id)) return;
        _selectedSection = null;
        _focusField = null;
        _activeWorkflowStage = "configure";
        _focusWorkflowStage = null;
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
        long generation = _generation;
        form.SetSecretEntryEnabled(enabled);
        if (generation == _generation) Invalidate();
        Task retirement = _outputRetirement;
        _status = enabled ? "Secret entry enabled for this workspace. Values remain masked." : "Secret entry revoked. Secret answers and output discarded; ordinary settings retained.";
        _statusCode = enabled ? "secret-entry-enabled" : "secret-entry-revoked";
        await retirement;
    }

    private async Task PrepareAsync(bool draft)
    {
        if (_disposed || !_ready) return;
        DefinitionFormModel form = _form!;
        long generation = _generation + 1;
        await InvalidateOutputAsync();
        if (_disposed || !_ready || generation != _generation || !ReferenceEquals(form, _form)) return;
        _activeWorkflowStage = "prepare";
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
        if (form.HasInputErrors)
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
        if (_disposed || !_ready) return;
        DefinitionFormModel form = _form!;
        long generation = _generation + 1;
        await InvalidateOutputAsync();
        if (_disposed || !_ready || generation != _generation || !ReferenceEquals(form, _form)) return;
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
            if (_disposed || !_ready || generation != _generation || cancellation.IsCancellationRequested) return;
            if (length > 1024 * 1024) { _status = "Import rejected: maximum size is 1 MiB."; _statusCode = "import-too-large"; return; }
            var result = await form.ImportAsync(_importArtifact, _ => Task.FromResult<ReadOnlyMemory<byte>>(buffer.AsMemory(0, length)), cancellation.Token);
            if (_disposed || !_ready || !ReferenceEquals(form, _form) || generation != _generation && !result.Succeeded) return;
            _importDiagnostic = result.Diagnostic;
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
        if (_focusWorkflowStage is { } stage)
        {
            _focusWorkflowStage = null;
            ElementReference heading = stage switch
            {
                "review" => _reviewHeading,
                "prepare" => _prepareHeading,
                _ => string.IsNullOrEmpty(_sectionHeading.Id) ? _configureHeading : _sectionHeading
            };
            await heading.FocusAsync();
        }
        if (_focusStart) { _focusStart = false; await _startButton.FocusAsync(preventScroll: true); }
        if (_focusConsent) { _focusConsent = false; await _consentControl.FocusAsync(); }
        if (_restoreFocus && _fileInput?.Element is { } element) { _restoreFocus = false; await element.FocusAsync(preventScroll: true); }
    }
    public async Task BeginAsync()
    {
        if (_disposed) return;
        _ready = false;
        Invalidate();
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync) ?? 0;
        long generation = _generation;
        DefinitionFormModel form = _form!;
        await Offline.InitializeAsync();
        if (_disposed || generation != _generation) return;
        ValueTask<bool> beginning = Offline.BeginWorkspaceAsync();
        long lease = Offline.WorkspaceGeneration;
        bool ready = await beginning;
        if (_disposed || generation != _generation || !ReferenceEquals(form, _form)
            || lease != Offline.WorkspaceGeneration)
        {
            await Offline.EndWorkspaceAsync(lease);
            return;
        }
        _ready = ready;
        if (_ready)
        {
            _offlineLease = lease;
            _status = form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled
                ? "Secret entry enabled for this workspace. Values remain masked."
                : "Supply ordinary settings. Secret entry is disabled.";
            _statusCode = "workspace-ready";
        }
        else { _status = "Offline app preparation is incomplete. Finish loading public app assets before entering answers."; _statusCode = "app-preparation-incomplete"; }
        if (!_disposed) StateHasChanged();
    }
    public void Invalidate() => _ = InvalidateOutputAsync();
    private Task InvalidateOutputAsync()
    {
        ++_generation;
        _readCancellation?.Cancel();
        if (_readBuffer is not null) CryptographicOperations.ZeroMemory(_readBuffer);
        _readBuffer = null;
        PreparedArtifact[] previous = _prepared.ToArray();
        _prepared.Clear();
        _diagnostics.Clear();
        _validation = null;
        _importDiagnostic = null;
        // Start every retirement before awaiting any JS cleanup. Each owner
        // revokes its workspace, bytes and generation synchronously.
        Task[] retirements = previous.Select(prepared => prepared.DisposeAsync().AsTask()).ToArray();
        return _outputRetirement = ObserveRetirementAsync(retirements);
    }
    private async Task ObserveRetirementAsync(Task[] retirements)
    {
        try { await Task.WhenAll(retirements); }
        catch (Exception)
        {
            // Cleanup is an external boundary: report failure without values
            // and never let a retired owner mutate current policy or bytes.
            if (!_disposed) await InvokeAsync(() =>
            {
                _diagnostics.Add("output-cleanup-failed");
                StateHasChanged();
            });
        }
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
        InvalidateMounts();
        DefinitionFormModel form = _form!;
        long lease = _offlineLease;
        long navigationLease = _navigationLease;
        Task retirement = _outputRetirement;
        _ready = false;
        _search?.Release(this, _searchLease);
        form.Dispose();
        if (!_disposed && ReferenceEquals(form, _form))
        {
            _form = new DefinitionFormModel(Definition, DefinitionSessionPrivacy.BrowserNoSecrets);
            _compact = false;
            IndexSections();
            _form.InputChanged += InputChanged;
            _form.ViewChanged += ViewChanged;
            RegisterSearch();
            _activeWorkflowStage = "configure";
            _requiredCount = _form.Projection.Progress.Required;
            _focusField = null;
        }
        await Task.WhenAll(retirement, InvalidateOutputAsync());
        await Offline.EndWorkspaceAsync(lease);
        _navigation?.Release(this, navigationLease);
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        long navigationLease = _navigationLease;
        Task retirement = _outputRetirement;
        _disposed = true;
        _mountCancellation.Cancel();
        _mountCancellation.Dispose();
        _search?.Release(this, _searchLease);
        _ready = false;
        long lease = _offlineLease;
        _form?.Dispose();
        _session.Dispose();
        await Task.WhenAll(retirement, InvalidateOutputAsync());
        await Offline.EndWorkspaceAsync(lease);
        _navigation?.Release(this, navigationLease);
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
        public async ValueTask DisposeAsync()
        {
            try { Workspace.Dispose(); }
            finally { await Export.DisposeAsync(); }
        }
    }
}
