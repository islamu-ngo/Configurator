namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Browser.Resources;
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
    [Parameter] public string? InitialEntry { get; set; }
    private SetupDefinition? _pinned;
    private DefinitionFormModel? _form;
    private readonly SetupPresentationSession _session = new(new WeakReferenceMessenger());
    private readonly List<PreparedArtifact> _prepared = [];
    private readonly List<string> _diagnostics = [];
    private string _activeWorkflowStage = "configure";
    private long _viewGeneration;
    private (DefinitionFormModel Form, long Generation, FocusTarget Target)? _pendingFocus;
    private enum FocusTarget { Configure, Review, Files, Consent, Import, Start, Output }
    private int _requiredCount;
    private ElementReference _configureHeading;
    private ElementReference _outputControl;
    private ConfigurationConfigurePanel? _configurePanel;
    private ConfigurationReviewPanel? _reviewPanel;
    private ConfigurationFilesPanel? _filesPanel;
    private string _statusKey = "WorkspaceReadyStatus";
    private object?[] _statusArguments = [];
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
    private bool _returnToReview;
    private ElementReference _consentControl;
    private long _generation;
    private CancellationTokenSource _mountCancellation = new();
    private Task _outputRetirement = Task.CompletedTask;
    private long _fileKey;
    private CancellationTokenSource? _readCancellation;
    private byte[]? _readBuffer;
    private InputFile? _fileInput;
    private ElementReference _startButton;
    private bool _disposed;
    private bool _ready;
    private bool _beginPending;
    private bool HasWork(DefinitionFormModel form) => !_disposed && _ready && ReferenceEquals(form, _form) &&
        (form.HasWorkspaceChanges || _prepared.Count > 0 || _importPending || form.ImportComparison is not null);
    private string PreparationReadiness
    {
        get
        {
            DefinitionFormProgress progress = _form!.Projection.Progress;
            return _form.HasInputErrors || _form.HasAnswerBudgetError || progress.Invalid > 0 || progress.Missing > progress.Deferred
                ? "blocked" : progress.Deferred > 0 ? "deferred" : "complete";
        }
    }
    private bool _compact;
    private long _offlineLease;
    private DefinitionValidationResult? _validation;
    private BrowserNavigationState? _navigation;
    private long _navigationLease;
    private WorkspaceSearchState? _search;
    private long _searchLease;
    private Dictionary<string, DefinitionFormSection> _editorSections = new(StringComparer.Ordinal);
    private DefinitionDiscoverySelection? _observedDiscoverySelection;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_pinned, Definition)) return;
        RetireView();
        _configurePanel = null;
        _reviewPanel = null;
        _filesPanel = null;
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
        if (InitialEntry is not null) _form.SelectOutput(InitialEntry);
        IndexSections();
        _requiredCount = _form.Projection.Progress.Required;
        _form.InputChanged += InputChanged;
        _form.ViewChanged += ViewChanged;
        _form.ImportComparisonChanged += ImportComparisonChanged;
        _importArtifact = ImportArtifacts.FirstOrDefault()?.Id ?? string.Empty;
        _navigation ??= Services.GetService(typeof(BrowserNavigationState)) as BrowserNavigationState;
        DefinitionFormModel form = _form;
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync, () => (form.Locale, form.Direction), () => HasWork(form)) ?? 0;
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
    private DefinitionOutputSelector? OutputSelector => _form?.Definition.Presentation?.OutputSelector;
    private string UiText(string key) => BrowserStrings.Get(key, _form!.Locale);
    private string UiFormat(string key, params object?[] arguments) =>
        string.Format(CultureInfo.GetCultureInfo(_form!.Locale), UiText(key), arguments);
    private string LanguageName(string locale) => CultureInfo.GetCultureInfo(locale).TwoLetterISOLanguageName switch
    {
        "en" => UiText("EnglishLanguage"),
        "ar" => UiText("ArabicLanguage"),
        _ => CultureInfo.GetCultureInfo(locale).NativeName
    };
    private void SetStatus(string key, params object?[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
    }
    private string OutputSelection
    {
        get
        {
            if (OutputSelector is not { } selector || !_form!.Validate().TryGetEffectiveValue(selector.Location, out JsonElement value)) return "-1";
            string? selected = value.GetString();
            for (int index = 0; index < selector.Options.Count; ++index)
                if (selector.Options[index].Value == selected) return index.ToString(CultureInfo.InvariantCulture);
            return "-1";
        }
    }
    private string OutputTitle(int index) => _form!.LocalizedText(DefinitionPath.FromSegments(
        ["presentation", "outputSelector", "options", index.ToString(CultureInfo.InvariantCulture), "title"]), OutputSelector!.Options[index].Title)!;
    private void ChangeOutput(ChangeEventArgs args)
    {
        if (OutputSelector is not { } selector || args.Value is not string choice
            || !int.TryParse(choice, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            || index < 0 || index >= selector.Options.Count) return;
        if (!_form!.SelectOutput(selector.Options[index].Value)) return;
        _selectedSection = null;
        if (_activeWorkflowStage != "configure")
        {
            SelectWorkflowStage("configure");
            RequestFocus(FocusTarget.Output);
        }
    }

    private SetupArtifactDefinition[] ImportArtifacts { get; set; } = [];
    private DefinitionImportCapability? ImportCapability => _importCapabilities.GetValueOrDefault(_importArtifact);
    private void IndexSections()
    {
        _observedDiscoverySelection = _form!.DiscoverySelection;
        _sectionLocale = _form.Locale;
        _editorSections = _form.DisplaySections.ToDictionary(section => section.Id, StringComparer.Ordinal);
    }
    private DefinitionFormSection[] EditorSections()
    {
        return _form!.Projection.Sections
            .Select(section => _editorSections[section.Id])
            .ToArray();
    }
    private string? SelectedSection(IReadOnlyList<DefinitionFormSection> sections) =>
        sections.Any(item => item.Id == _selectedSection) ? _selectedSection : sections.Count > 0 ? sections[0].Id : null;
    private void SelectSection(string section)
    {
        _selectedSection = section;
        _focusField = null;
        SelectWorkflowStage("configure");
    }

    private void SelectWorkflowStage(string stage)
    {
        bool leavingFiles = _activeWorkflowStage == "prepare" && stage != "prepare";
        RetireView();
        InvalidateMounts();
        // Retire in-flight work without discarding a completed comparison owned by the form.
        if (_importPending)
        {
            InvalidateImport();
            SetStatus("ImportCancelledStatus");
            _statusCode = "import-cancelled";
        }
        else ++_generation;
        _activeWorkflowStage = stage;
        if (stage != "configure") _configurePanel = null;
        if (stage != "review") _reviewPanel = null;
        if (stage != "prepare") _filesPanel = null;
        if (stage == "review") _validation = _form!.Validate();
        RequestFocus(stage switch { "review" => FocusTarget.Review, "prepare" => FocusTarget.Files, _ => FocusTarget.Configure });
        if (leavingFiles)
        {
            Task[] retirements = _prepared.Select(artifact =>
            {
                artifact.Revealed = false;
                artifact.View = null;
                return artifact.Export.InvalidateAsync().AsTask();
            }).ToArray();
            _outputRetirement = Task.WhenAll(_outputRetirement, ObserveRetirementAsync(retirements));
        }
    }
    private void CorrectField(DefinitionDiagnostic diagnostic)
    {
        string fieldId = diagnostic.FieldId;
        SetupFieldDefinition? field = Definition.Fields.FirstOrDefault(item => item.Id == fieldId);
        if (field is null) return;
        IReadOnlyList<string> path = new[] { fieldId }.Concat(diagnostic.RelativePath).ToArray();
        if (diagnostic.Location is { } location && !_form!.TryResolveCorrection(fieldId, location, out path)) return;
        if (OutputSelector is { } selector && field.Name == selector.Location.Segments[0] && path.Count == 1)
        {
            SelectWorkflowStage("configure");
            RequestFocus(FocusTarget.Output);
            return;
        }
        bool focused = _form!.FocusEditor(path, preserveDiscovery: true);
        bool denied = !_form.CanEdit(path);
        if (!focused && !denied) return;
        SelectWorkflowStage("configure");
        _selectedSection = EditorSections().Single(section => section.Fields.Contains(fieldId)).Id;
        _focusField = denied ? null : field.Id;
        _focusPath = denied ? [] : path;
        _pendingFocus = null;
        ++_focusGeneration;
        if (denied) RequestFocus(FocusTarget.Consent);
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
    private void ReviewImportConsent()
    {
        SelectWorkflowStage("configure");
        RequestFocus(FocusTarget.Consent);
    }
    private void ImportComparisonChanged(object? sender, EventArgs args)
    {
        if (_disposed || !_ready || !ReferenceEquals(sender, _form)) return;
        if (_form!.ImportComparison is null)
        {
            _navigation?.RefreshWork(this, _navigationLease);
            return;
        }
        // Publishing private comparison state renews custody, never answer-editor state.
        DefinitionFormModel form = _form;
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync, () => (form.Locale, form.Direction), () => HasWork(form)) ?? 0;
    }
    private async Task EnableSecretsAsync()
    {
        DefinitionFormModel form = _form!;
        await ConsentAsync(new ChangeEventArgs { Value = true });
        if (!_disposed && ReferenceEquals(form, _form) && form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled)
        {
            SelectWorkflowStage("configure");
            RequestFocus(FocusTarget.Consent);
        }
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _form)) return;
        RetireView();
        InvalidateMounts();
        _focusField = null;
        Invalidate();
        int required = _form!.Projection.Progress.Required;
        if (required == _requiredCount) SetStatus("AnswersChangedStatus");
        else SetStatus("RequiredWorkChangedStatus", _requiredCount, required);
        _requiredCount = required;
        _statusCode = "answers-changed";
        _search?.Refresh(this, _searchLease);
        StateHasChanged();
    }
    private void ViewChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _form)) return;
        if (_sectionLocale != _form!.Locale)
        {
            IndexSections();
            _navigation?.RefreshCulture(this, _navigationLease);
        }
        _search?.Refresh(this, _searchLease);
        // Discovery owns its input events and rerenders its own query results.
        if (!ReferenceEquals(_observedDiscoverySelection, _form!.DiscoverySelection))
        {
            _observedDiscoverySelection = _form.DiscoverySelection;
            return;
        }
        RetireView();
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
    private bool IsCurrentView(DefinitionFormModel form, long generation) =>
        !_disposed && ReferenceEquals(form, _form) && generation == _viewGeneration;
    private void RetireView()
    {
        ++_viewGeneration;
        ++_focusGeneration;
        _pendingFocus = null;
        _focusField = null;
        _focusPath = [];
    }
    private void RequestFocus(FocusTarget target) => _pendingFocus = (_form!, _viewGeneration, target);
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
        SelectWorkflowStage("configure");
    }
    private void ChangeImportFormat(ChangeEventArgs args)
    {
        string? artifactId = args.Value?.ToString();
        if (!Definition.Artifacts.Any(item => item.Id == artifactId)) return;
        InvalidateImport();
        _importArtifact = artifactId!;
        SetStatus("ImportFormatChangedStatus");
        _statusCode = "import-format-changed";
    }
    private void ChangeImportPurpose(ChangeEventArgs args)
    {
        if (args.Value is not ("compare" or "fill")) return;
        InvalidateImport();
        _importPurpose = (string)args.Value;
        SetStatus("ImportPurposeChangedStatus");
        _statusCode = "import-purpose-changed";
    }
    private void ClearComparison()
    {
        InvalidateImport();
        SetStatus("ImportClearedStatus");
        _statusCode = "import-cleared";
        RequestFocus(FocusTarget.Import);
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
        _navigation?.RefreshWork(this, _navigationLease);
        StateHasChanged();
        try { await AdoptComparisonAsync(report, cancellation.Token); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_readCancellation, cancellation))
            {
                _readCancellation = null;
                _importPending = false;
                _navigation?.RefreshWork(this, _navigationLease);
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
        SetStatus(result.Succeeded ? "ImportedStatus" : "AutofillRefusedStatus", result.Code);
        if (result.Succeeded) { ++_fileKey; RequestFocus(FocusTarget.Import); }
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
            SetStatus("ImportConsentRequiredStatus");
            _statusCode = "import-secret-consent-required";
            return false;
        }
        if (ImportCapability is not { InspectionSupported: true } ||
            _importPurpose == "fill" && !ImportCapability.StrictAutofillSupported)
        {
            SetStatus("ImportUnsupportedStatus");
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
        SetStatus(enabled ? "SecretEntryEnabledStatus" : "SecretEntryRevokedStatus");
        _statusCode = enabled ? "secret-entry-enabled" : "secret-entry-revoked";
        await retirement;
    }

    private async Task PrepareAsync(bool draft)
    {
        if (_disposed || !_ready) return;
        if (_activeWorkflowStage != "prepare") SelectWorkflowStage("prepare");
        DefinitionFormModel form = _form!;
        long generation = _generation + 1;
        await InvalidateOutputAsync();
        if (_disposed || !_ready || generation != _generation || !ReferenceEquals(form, _form)) return;
        _activeWorkflowStage = "prepare";
        SetupAnswerSnapshot answers;
        try { answers = form.Snapshot(); }
        catch (SetupDefinitionException)
        {
            SetStatus("AnswersBudgetExceededStatus");
            _statusCode = "answer-budget-exceeded";
            _diagnostics.Add("answer-budget-exceeded");
            return;
        }
        if (form.HasAnswerBudgetError)
        {
            SetStatus("InputBudgetExceededStatus");
            _statusCode = "answer-budget-exceeded";
            _diagnostics.Add("answer-budget-exceeded");
            return;
        }
        _validation = draft ? DefinitionValidator.ValidateDraft(Definition, answers) : form.Validate();
        if (form.HasInputErrors)
        {
            _diagnostics.Add("input-type-invalid");
            SetStatus("InputTypeInvalidStatus");
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
                    SetStatus("PreparationRejectedStatus");
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
                    SetStatus("PreparationRejectedStatus");
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
                        SetStatus("PreparationRetryStatus");
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
            _navigation?.RefreshWork(this, _navigationLease);
            bool incompleteDraft = draft && (_prepared.Any(item => item.Metadata.IsDraft) ||
                _diagnostics.Contains("draft-dependency-unresolved"));
            SetStatus(incompleteDraft
                ? (_prepared.Count == 1 ? "PreparedDraftStatus" : "PreparedDraftsStatus")
                : (_prepared.Count == 1 ? "PreparedArtifactStatus" : "PreparedArtifactsStatus"), _prepared.Count);
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
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!IsCurrentExport(artifact, form, view)) return;
        InvalidateImport();
        if (!artifact.Export.HasOutput) artifact.Export.SetOutput(artifact.Workspace.Output);
        bool copied = await artifact.Export.CopyAsync();
        if (!IsCurrentExport(artifact, form, view)) return;
        artifact.StatusKey = copied ? "ArtifactCopiedStatus" : "ClipboardUnavailableStatus";
        artifact.StatusCode = copied ? "copied" : "clipboard-unavailable";
    }
    private async Task DownloadAsync(PreparedArtifact artifact)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!IsCurrentExport(artifact, form, view)) return;
        InvalidateImport();
        if (!artifact.Export.HasOutput) artifact.Export.SetOutput(artifact.Workspace.Output);
        bool downloaded = await artifact.Export.DownloadAsync(artifact.Metadata.FileName!, artifact.Metadata.MediaType!);
        if (!IsCurrentExport(artifact, form, view)) return;
        artifact.StatusKey = downloaded ? "DownloadRequestedStatus" : "DownloadUnavailableStatus";
        artifact.StatusCode = downloaded ? "download-requested" : "download-unavailable";
    }
    private async Task RevealAsync(PreparedArtifact artifact)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!IsCurrentExport(artifact, form, view)) return;
        InvalidateImport();
        if (artifact.Revealed)
        {
            artifact.Revealed = false;
            await artifact.Export.InvalidateAsync();
            if (!IsCurrentExport(artifact, form, view)) return;
            artifact.StatusKey = "PreviewHiddenStatus";
            artifact.StatusCode = "preview-hidden";
            return;
        }
        if (artifact.View?.PreviewElement is not { } preview) return;
        if (!artifact.Export.HasOutput) artifact.Export.SetOutput(artifact.Workspace.Output);
        artifact.Revealed = true;
        StateHasChanged();
        bool revealed = await artifact.Export.RevealAsync(preview);
        if (!IsCurrentExport(artifact, form, view)) return;
        artifact.Revealed = revealed;
        artifact.StatusKey = revealed ? "PreviewRevealedStatus" : "PreviewUnavailableStatus";
        artifact.StatusCode = revealed ? "preview-revealed" : "preview-unavailable";
    }
    private bool IsCurrentExport(PreparedArtifact artifact, DefinitionFormModel form, long view) =>
        IsCurrentView(form, view) && _ready && _activeWorkflowStage == "prepare" && _prepared.Contains(artifact);
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
            RequestFocus(FocusTarget.Import);
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
        _navigation?.RefreshWork(this, _navigationLease);
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
                SetStatus("ImportTooLargeStatus");
                _statusCode = "import-too-large";
                return;
            }
            _statusCode = result.Code;
            SetStatus(result.Succeeded ? "ImportComparedStatus" : "ImportRefusedStatus", result.Code);
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
                SetStatus("ImportReadFailedStatus");
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
                _navigation?.RefreshWork(this, _navigationLease);
                ++_fileKey;
                RequestFocus(FocusTarget.Import);
            }
            cancellation.Dispose();
            if (!_disposed) await InvokeAsync(StateHasChanged);
        }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        DefinitionFormModel form = _form!;
        long generation = _viewGeneration;
        if (firstRender || _beginPending) { _beginPending = false; await BeginAsync(); }
        if (!IsCurrentView(form, generation) || _pendingFocus is not { } request) return;
        _pendingFocus = null;
        if (!IsCurrentView(request.Form, request.Generation)) return;
        ElementReference? target = request.Target switch
        {
            FocusTarget.Configure when _ready && _activeWorkflowStage == "configure" => _configurePanel?.SectionHeading ?? _configureHeading,
            FocusTarget.Review when _ready && _activeWorkflowStage == "review" => _reviewPanel?.Heading,
            FocusTarget.Files when _ready && _activeWorkflowStage == "prepare" => _filesPanel?.Heading,
            FocusTarget.Consent when _ready && _activeWorkflowStage == "configure" => _consentControl,
            FocusTarget.Import when _ready && _activeWorkflowStage == "configure" => _fileInput?.Element,
            FocusTarget.Output when _ready && OutputSelector is not null => _outputControl,
            FocusTarget.Start when !_ready => _startButton,
            _ => null
        };
        if (target is not { } element || string.IsNullOrEmpty(element.Id)) return;
        await element.FocusAsync(preventScroll: request.Target is FocusTarget.Import or FocusTarget.Start);
        if (!IsCurrentView(request.Form, request.Generation)) return;
    }
    public async Task BeginAsync()
    {
        if (_disposed) return;
        _ready = false;
        Invalidate();
        DefinitionFormModel form = _form!;
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync, () => (form.Locale, form.Direction), () => HasWork(form)) ?? 0;
        long generation = _generation;
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
            SetStatus(form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled
                ? "SecretEntryEnabledStatus" : "WorkspaceReadyStatus");
            _statusCode = "workspace-ready";
        }
        else { SetStatus("AppPreparationIncompleteStatus"); _statusCode = "app-preparation-incomplete"; }
        _navigation?.RefreshWork(this, _navigationLease);
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
        _navigation?.RefreshWork(this, _navigationLease);
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
        _navigation?.RefreshWork(this, _navigationLease);
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
                if (_disposed) return;
                // Value-free cleanup warnings belong to the editor lifetime, not the retired form.
                _diagnostics.Add("output-cleanup-failed");
                StateHasChanged();
            });
        }
    }
    private async Task ClearAsync()
    {
        if (_disposed || !_ready) return;
        DefinitionFormModel current = _form!;
        long view = _viewGeneration;
        long operation = _generation;
        if (HasWork(current) && !await Javascript.InvokeAsync<bool>("confirm", UiText("DiscardConfirm"))) return;
        if (!IsCurrentView(current, view) || operation != _generation) return;
        Task discarding = DiscardAsync();
        long generation = _generation;
        DefinitionFormModel form = _form!;
        await discarding;
        if (_disposed || generation != _generation || !ReferenceEquals(form, _form) || _ready) return;
        ++_fileKey;
        SetStatus("WorkspaceClearedStatus");
        _statusCode = "workspace-cleared";
        RequestFocus(FocusTarget.Start);
    }
    public async Task DiscardAsync()
    {
        RetireView();
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
        RetireView();
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
        public ConfigurationFiles? View;
        public bool Revealed { get; set; }
        public string? StatusKey { get; set; }
        public string? StatusCode { get; set; }
        public async ValueTask DisposeAsync()
        {
            try { Workspace.Dispose(); }
            finally { await Export.DisposeAsync(); }
        }
    }
}
