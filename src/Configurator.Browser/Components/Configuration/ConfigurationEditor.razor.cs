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
    private Task<IJSObjectReference>? _comparisonRendering;
    [Inject] private BrowserOfflinePolicy Offline { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Inject] private AppearanceThemeService Appearance { get; set; } = null!;
    [Parameter, EditorRequired] public SetupDefinition Definition { get; set; } = null!;
    [Parameter] public string? InitialLocale { get; set; }
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
    private string _importPurpose = "compare";
    private bool _importPending;
    private bool _importConsentRequired;
    private const int MaximumImportBytes = 1024 * 1024;
    private IReadOnlyDictionary<string, DefinitionImportCapability> _importCapabilities =
        new Dictionary<string, DefinitionImportCapability>();
    private string? _selectedSection;
    private string? _sectionLocale;
    private string DisplayDirection => Appearance.Current.Direction == "auto" ? _form!.Direction : Appearance.Current.Direction;
    protected override void OnInitialized() => Appearance.Changed += AppearanceChanged;
    private void AppearanceChanged(object? sender, EventArgs args)
    {
        if (!_disposed) _ = InvokeAsync(StateHasChanged);
    }
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
        if (_form is not null) _form.ImportComparisonChanged -= ImportComparisonChanged;
        _form?.Dispose();
        _activeWorkflowStage = "configure";
        _returnToReview = false;
        _selectedSection = null;
        _focusField = null;
        _pinned = Definition;
        _compact = false;
        ImportArtifacts = Definition.Artifacts.ToArray();
        _importCapabilities = ImportArtifacts.ToDictionary(artifact => artifact.Id,
            artifact => DefinitionImport.GetCapability(Definition, artifact.Id), StringComparer.Ordinal);
        _importPurpose = "compare";
        _ready = false;
        _beginPending = true;
        _form = new DefinitionFormModel(Definition, DefinitionSessionPrivacy.BrowserNoSecrets, BrowserLaunchSelection.EditingCapabilities);
        if (InitialLocale is not null && !_form.SelectLocale(InitialLocale))
            throw new InvalidOperationException("locale-unsupported");
        IndexSections();
        _requiredCount = _form.Projection.Progress.Required;
        _form.InputChanged += InputChanged;
        _form.ViewChanged += ViewChanged;
        _form.ImportComparisonChanged += ImportComparisonChanged;
        _importArtifact = ImportArtifacts.FirstOrDefault()?.Id ?? string.Empty;
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

    private DefinitionFormExperience SelectedExperience => _form!.DisplayExperiences.Single(item => item.Id == _form.SelectedExperienceId);
    private SetupArtifactDefinition[] ImportArtifacts { get; set; } = [];
    private DefinitionImportCapability? ImportCapability => _importCapabilities.GetValueOrDefault(_importArtifact);
    private sealed record EditorSection(string Id, string Title, IReadOnlyList<string> Fields, string? Description);
    private void IndexSections()
    {
        _observedDiscoverySelection = _form!.DiscoverySelection;
        _sectionLocale = _form.Locale;
        _editorSections = _form.DisplaySections.ToDictionary(section => section.Id, section =>
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
        IReadOnlyList<string> path = new[] { fieldId }.Concat(diagnostic.RelativePath).ToArray();
        if (diagnostic.Location is { } location && !_form!.TryResolveCorrection(fieldId, location, out path)) return;
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
    private void ReviewImportConsent() => _focusConsent = true;
    private void ImportComparisonChanged(object? sender, EventArgs args)
    {
        if (_disposed || !_ready || !ReferenceEquals(sender, _form) || _form!.ImportComparison is null) return;
        // Publishing private comparison state renews custody, never answer-editor state.
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync) ?? 0;
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
        if (_sectionLocale != _form!.Locale) IndexSections();
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
    private void ChangeLocale(ChangeEventArgs args)
    {
        if (args.Value is string locale) _form!.SelectLocale(locale);
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
        InvalidateImport();
        _importArtifact = artifactId!;
        _status = "Artifact selection changed. Comparison discarded; answers and prepared output retained.";
        _statusCode = "import-format-changed";
    }
    private void ChangeImportPurpose(ChangeEventArgs args)
    {
        if (args.Value is not ("compare" or "fill")) return;
        InvalidateImport();
        _importPurpose = (string)args.Value;
        _status = "Import purpose changed. Comparison discarded; answers and prepared output retained.";
        _statusCode = "import-purpose-changed";
    }
    private void ClearComparison()
    {
        InvalidateImport();
        _status = "Comparison cleared or cancelled. Answers and prepared output retained.";
        _statusCode = "import-cleared";
        _restoreFocus = true;
    }
    private void ClearComparison(DefinitionImportReport report)
    {
        if (ReferenceEquals(_form?.ImportComparison, report)) ClearComparison();
    }
    private void CancelComparison(long generation)
    {
        if (generation == _generation) ClearComparison();
    }
    private async Task AdoptComparison(DefinitionImportReport report)
    {
        if (_importPending) return;
        var cancellation = new CancellationTokenSource();
        _readCancellation = cancellation;
        _importPending = true;
        StateHasChanged();
        try { await AdoptComparisonAsync(report, cancellation.Token); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_readCancellation, cancellation))
            {
                _readCancellation = null;
                _importPending = false;
            }
            cancellation.Dispose();
            if (!_disposed) StateHasChanged();
        }
    }
    private async Task AdoptComparisonAsync(DefinitionImportReport report, CancellationToken cancellationToken)
    {
        if (_disposed || !_ready || !ReferenceEquals(_form!.ImportComparison, report)) return;
        DefinitionFormModel form = _form!;
        long generation = _generation;
        long revision = form.InputRevision;
        await YieldComparisonAsync(cancellationToken);
        DefinitionImportResult result = await form.AdoptImportComparisonAsync(YieldComparisonAsync, cancellationToken);
        if (_disposed || !_ready || !ReferenceEquals(form, _form)) return;
        // Adoption itself raises InputChanged, retiring this operation exactly once.
        if (result.Succeeded
            ? _generation != generation + 1 || form.InputRevision != revision + 1
            : _generation != generation || cancellationToken.IsCancellationRequested) return;
        _statusCode = result.Succeeded ? "imported" : result.Code;
        _status = result.Succeeded ? "Eligible file answers filled atomically. Review current workspace readiness before preparation."
            : "Autofill refused (" + result.Code + "). No answers changed. Review the comparison and repair a separate copy or deliberately clear conflicting workspace inputs, then re-import.";
        if (result.Succeeded) { ++_fileKey; _restoreFocus = true; }
    }
    private async ValueTask YieldComparisonAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _comparisonRendering ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./configuration-rendering.js").AsTask();
        IJSObjectReference module = await _comparisonRendering;
        cancellationToken.ThrowIfCancellationRequested();
        await module.InvokeVoidAsync("nextTask");
        cancellationToken.ThrowIfCancellationRequested();
    }
    private bool CanReadImport()
    {
        if (_disposed || !_ready) return false;
        if (_form!.SessionPrivacy != DefinitionSessionPrivacy.BrowserSecretsEnabled)
        {
            _importConsentRequired = true;
            _status = "File not read (import-secret-consent-required). Enable secret entry for this workspace before reading any configuration file, including unknown or malformed content.";
            _statusCode = "import-secret-consent-required";
            return false;
        }
        if (ImportCapability is not { InspectionSupported: true } ||
            _importPurpose == "fill" && !ImportCapability.StrictAutofillSupported)
        {
            _status = "File not read. The selected artifact does not support this import purpose.";
            _statusCode = ImportCapability?.Code ?? "inverse-unsupported";
            return false;
        }
        return true;
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
        DefinitionDraftBundleResult? draftBundle = null;
        DefinitionBundleResult? completeBundle = null;
        var staged = new List<PreparedArtifact>();
        try
        {
            IReadOnlyList<DefinitionArtifact> artifacts;
            if (draft)
            {
                draftBundle = DefinitionDraftArtifactComposer.ComposeBundle(Definition, Definition.Artifacts.Select(item => item.Id), answers);
                if (draftBundle.Artifacts.Count == 0 || draftBundle.Diagnostics.Any(item => item.Code is not ("draft-dependency-unresolved" or "artifact-omitted")))
                {
                    _diagnostics.AddRange(draftBundle.Diagnostics.Select(item => $"{item.FieldId}: {item.Code}"));
                    _status = "No configuration prepared. Correct required settings or reduce output size.";
                    _statusCode = "preparation-rejected";
                    return;
                }
                artifacts = draftBundle.Artifacts;
                _diagnostics.AddRange(draftBundle.Diagnostics.Select(item => item.Code));
            }
            else
            {
                completeBundle = DefinitionBundleComposer.ComposeActive(Definition, answers);
                if (!completeBundle.Succeeded)
                {
                    _diagnostics.AddRange(completeBundle.Diagnostics.Select(item => $"{item.FieldId}: {item.Code}"));
                    _status = "No configuration prepared. Correct required settings or reduce output size.";
                    _statusCode = "preparation-rejected";
                    return;
                }
                artifacts = completeBundle.Artifacts;
            }
            if (_disposed || !_ready || generation != _generation || !ReferenceEquals(form, _form)) return;
            foreach (DefinitionArtifact artifact in artifacts)
            {
                using DefinitionPreparedAdoption operation = DefinitionPresentationOperation.Adopt(artifact, draftBundle?.Completions);
                SetupWorkspaceId.TryCreate(Guid.NewGuid().ToString("N"), out var workspaceId);
                var workspace = _session.CreateWorkspace(workspaceId, operation);
                bool retained = false;
                try
                {
                    workspace.Activate();
                    await workspace.ExecuteAsync(Guid.NewGuid());
                    if (_disposed || !_ready || generation != _generation || !ReferenceEquals(form, _form)) return;
                    if (workspace.Result is not DefinitionPreparationMetadata { Succeeded: true } metadata)
                    {
                        if (workspace.Result is DefinitionPreparationMetadata failure) _diagnostics.AddRange(failure.DiagnosticCodes);
                        _status = "No configuration prepared. Prepare the current workspace again.";
                        _statusCode = "preparation-rejected";
                        return;
                    }
                    staged.Add(new PreparedArtifact(metadata, workspace, Javascript));
                    retained = true;
                }
                finally { if (!retained) workspace.Dispose(); }
            }
            _prepared.AddRange(staged);
            staged.Clear();
            bool incompleteDraft = draft && (_prepared.Any(item => item.Metadata.IsDraft) ||
                _diagnostics.Contains("draft-dependency-unresolved"));
            _status = incompleteDraft
                ? $"Prepared {_prepared.Count} draft artifact(s). Deferred secrets or withheld outputs still require completion."
                : $"Prepared {_prepared.Count} private artifact(s). Reveal, copy or download each explicitly.";
            _statusCode = "preparation-ready";
        }
        finally
        {
            foreach (PreparedArtifact artifact in staged) await artifact.DisposeAsync();
            draftBundle?.Dispose();
            completeBundle?.Dispose();
        }
    }
    private async Task CopyAsync(PreparedArtifact artifact)
    {
        if (!_prepared.Contains(artifact)) return;
        InvalidateImport();
        bool copied = await artifact.Export.CopyAsync();
        if (!_prepared.Contains(artifact)) return;
        _status = copied ? "Current artifact copied. Clipboard contents remain outside this workspace." : "Clipboard unavailable or denied. Use Download instead.";
        _statusCode = copied ? "copied" : "clipboard-unavailable";
    }
    private async Task DownloadAsync(PreparedArtifact artifact)
    {
        if (!_prepared.Contains(artifact)) return;
        InvalidateImport();
        bool downloaded = await artifact.Export.DownloadAsync(artifact.Metadata.FileName!, artifact.Metadata.MediaType!);
        if (!_prepared.Contains(artifact)) return;
        _status = downloaded ? $"Download requested as {artifact.Metadata.FileName}. If your browser changes the filename, rename it to this declared name. Protect the downloaded file on your device." : "Download could not start. Prepare current output again.";
    }
    private async Task RevealAsync(PreparedArtifact artifact)
    {
        if (!_prepared.Contains(artifact)) return;
        InvalidateImport();
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
    private Task ImportAsync(InputFileChangeEventArgs args) =>
        LoadAsync(async (buffer, token) =>
        {
            await using Stream stream = args.File.OpenReadStream(MaximumImportBytes, token);
            return await ReadBoundedAsync(stream, buffer, token);
        });

    public Task LoadAsync(Stream stream) =>
        LoadAsync((buffer, token) => ReadBoundedAsync(stream, buffer, token));

    private static async Task<int> ReadBoundedAsync(Stream stream, Memory<byte> buffer, CancellationToken token)
    {
        int length = 0;
        while (length < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[length..], token);
            if (read == 0) break;
            length += read;
        }
        return length;
    }
    private async Task LoadAsync(Func<Memory<byte>, CancellationToken, Task<int>> read)
    {
        if (_disposed || !_ready) return;
        InvalidateImport(releaseFileControl: false);
        if (!CanReadImport())
        {
            ++_fileKey;
            _restoreFocus = true;
            StateHasChanged();
            return;
        }
        DefinitionFormModel form = _form!;
        string artifactId = _importArtifact;
        string purpose = _importPurpose;
        long generation = _generation;
        var cancellation = new CancellationTokenSource();
        _readCancellation = cancellation;
        byte[] buffer = new byte[MaximumImportBytes + 1];
        _readBuffer = buffer;
        _importPending = true;
        bool tooLarge = false;
        StateHasChanged();
        try
        {
            await YieldComparisonAsync(cancellation.Token);
            var result = await form.CompareAsync(artifactId, async token =>
            {
                int length = await read(buffer, token);
                if (length > MaximumImportBytes)
                {
                    tooLarge = true;
                    throw new IOException();
                }
                return buffer.AsMemory(0, length);
            }, YieldComparisonAsync, cancellation.Token);
            // Paint the detached report before remounting the file control and restoring focus.
            if (result.Succeeded) await YieldComparisonAsync(cancellation.Token);
            if (_disposed || !_ready || !ReferenceEquals(form, _form) || generation != _generation ||
                cancellation.IsCancellationRequested) return;
            if (tooLarge)
            {
                _status = "File rejected: maximum size is 1 MiB. Answers and prepared output retained.";
                _statusCode = "import-too-large";
                return;
            }
            _statusCode = result.Code;
            _status = result.Succeeded
                ? "Imported file comparison completed. Answers, drafts and prepared output are unchanged; target acceptance is not assessed."
                : "Comparison refused (" + result.Code + "). No answers changed. Check the declared format and repair a separate copy before re-importing.";
            if (result.Succeeded && purpose == "fill")
            {
                await AdoptComparisonAsync(result.Report!, cancellation.Token);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or InvalidOperationException or SetupDefinitionException)
        {
            if (generation == _generation && ReferenceEquals(form, _form))
            {
                form.ClearImportComparison();
                _status = "Comparison could not finish. No answers changed; repair a separate copy or choose the file again.";
                _statusCode = "import-read-failed";
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            if (ReferenceEquals(_readBuffer, buffer)) _readBuffer = null;
            if (ReferenceEquals(_readCancellation, cancellation))
            {
                _readCancellation = null;
                _importPending = false;
                ++_fileKey;
                _restoreFocus = true;
            }
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
        InvalidateImport();
        PreparedArtifact[] previous = _prepared.ToArray();
        _prepared.Clear();
        _diagnostics.Clear();
        _validation = null;
        // Start every retirement before awaiting any JS cleanup. Each owner
        // revokes its workspace, bytes and generation synchronously.
        Task[] retirements = previous.Select(prepared => prepared.DisposeAsync().AsTask()).ToArray();
        return _outputRetirement = ObserveRetirementAsync(retirements);
    }
    private void InvalidateImport(bool releaseFileControl = true)
    {
        ++_generation;
        _readCancellation?.Cancel();
        _readCancellation = null;
        if (_readBuffer is not null) CryptographicOperations.ZeroMemory(_readBuffer);
        _readBuffer = null;
        _importPending = false;
        _importConsentRequired = false;
        _form?.ClearImportComparison();
        if (releaseFileControl) ++_fileKey;
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
        form.ImportComparisonChanged -= ImportComparisonChanged;
        form.Dispose();
        if (!_disposed && ReferenceEquals(form, _form))
        {
            _form = new DefinitionFormModel(Definition, DefinitionSessionPrivacy.BrowserNoSecrets);
            _compact = false;
            IndexSections();
            _form.InputChanged += InputChanged;
            _form.ViewChanged += ViewChanged;
            _form.ImportComparisonChanged += ImportComparisonChanged;
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
        Appearance.Changed -= AppearanceChanged;
        if (_disposed) return;
        long navigationLease = _navigationLease;
        Task retirement = _outputRetirement;
        _disposed = true;
        _mountCancellation.Cancel();
        _mountCancellation.Dispose();
        _search?.Release(this, _searchLease);
        _ready = false;
        long lease = _offlineLease;
        if (_form is not null) _form.ImportComparisonChanged -= ImportComparisonChanged;
        _form?.Dispose();
        _session.Dispose();
        await Task.WhenAll(retirement, InvalidateOutputAsync());
        await Offline.EndWorkspaceAsync(lease);
        _navigation?.Release(this, navigationLease);
        if (_comparisonRendering is not null)
        {
            IJSObjectReference module = await _comparisonRendering;
            await module.DisposeAsync();
        }
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
