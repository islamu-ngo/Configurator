namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Browser.Components.Common;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

public partial class ConfigurationEditor : IBrowserPendingChanges
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Inject] private MudBlazor.IDialogService Dialogs { get; set; } = null!;
    [Inject] private MudBlazor.ISnackbar Snackbar { get; set; } = null!;
    private Task<IJSObjectReference>? _comparisonRendering;
    [Inject] private BrowserOfflinePolicy Offline { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Inject] private AppearanceThemeService Appearance { get; set; } = null!;
    [Parameter, EditorRequired] public Definition Definition { get; set; } = null!;
    [Parameter] public string? InitialLocale { get; set; }
    [Parameter] public string? InitialEntry { get; set; }
    [Parameter] public string? InitialMode { get; set; }
    private Definition? _pinned;
    private DefinitionFormModel? _form;
    private StartupRequest? _startup;
    private bool _startupScheduled;
    private ElementReference _startupControl;
    private sealed record StartupRequest(Definition Definition, string Locale, string Direction, string? Entry,
        long View, long Generation, bool Begin, CancellationToken Cancellation);
    private readonly PresentationSession _session = new(new WeakReferenceMessenger());
    private readonly List<PreparedArtifact> _prepared = [];
    private BrowserArtifactBundleExport? _bundleExport;
    private readonly List<string> _diagnostics = [];
    private WorkflowStage _activeWorkflowStage = WorkflowStage.Configure;
    private enum WorkflowStage { Configure, Review, Prepare }
    private long _viewGeneration;
    private (DefinitionFormModel Form, long Generation, FocusTarget Target)? _pendingFocus;
    private enum FocusTarget { Configure, Review, Files, Consent, Import, Profile, Start, Output, Context, DeveloperDestination, TransferReview }
    private int _requiredCount;
    private ElementReference _outputControl;
    private ConfigurationConfigurePanel? _configurePanel;
    private ConfigurationReviewPanel? _reviewPanel;
    private ConfigurationFilesPanel? _filesPanel;
    private ConfigurationDeveloper? _developerPanel;
    private bool _developerMode;
    private bool _compactReviewOpen;
    private bool _compactFilesOpen;
    private bool _compactShowMatchesOnly;
    private bool _hadPreparedOutput;
    private WorkspaceLayoutMode _observedLayoutMode = WorkspaceLayoutMode.Guided;
    private bool _contextOpen;
    private bool _consentOpen;
    private bool _taskToolsOpen;
    private EventCallback _consentAction;
    private EventCallback<DefinitionPath> _suggestionReviewCallback;
    private long _developerEpoch;
    private long _outputChoiceKey;
    private string _statusKey = "WorkspaceReadyStatus";
    private object?[] _statusArguments = [];
    private string _statusCode = "workspace-ready";
    private bool _lastAggregateExportWasArchive;
    private string _importArtifact = string.Empty;
    private string _importOutputSelection = "-1";
    private string _importPurpose = "fill";
    private bool _importPending;
    private bool _importConsentRequired;
    private const int MaximumImportBytes = 1024 * 1024;
    private IReadOnlyDictionary<string, DefinitionImportCapability> _importCapabilities =
        new Dictionary<string, DefinitionImportCapability>();
    private string? _selectedSection;
    private string? _sectionLocale;
    private string DisplayDirection => Appearance.Current.Direction == "auto"
        ? _form?.Direction ?? _startup!.Direction : Appearance.Current.Direction;
    private WorkspaceLayoutMode LayoutMode => _form?.WorkspaceLayoutMode ?? WorkspaceLayoutMode.Guided;
    private WorkspaceDensity Density => _form?.WorkspaceDensity ?? Appearance.Current.ToWorkspaceDensity();
    private WorkspaceNamingMode NamingMode => _form?.WorkspaceNamingMode ?? Appearance.Current.ToWorkspaceNamingMode();
    private bool IsFilesSurfaceActive => _activeWorkflowStage == WorkflowStage.Prepare ||
        (LayoutMode == WorkspaceLayoutMode.Compact && _compactFilesOpen);
    private string WorkflowStageToken => LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode
        ? (_compactFilesOpen ? "prepare" : _compactReviewOpen ? "review" : "configure")
        : _activeWorkflowStage switch
        {
            WorkflowStage.Review => "review",
            WorkflowStage.Prepare => "prepare",
            _ => "configure"
        };
    private string? ScopeSummary => _form?.Projection.ContextSummary?.Summary
        ?? (_form?.ConfigurationContext is not { } context ? null : $"{context.RecipeId} · {context.ScopeId}");
    protected override void OnInitialized()
    {
        Appearance.Changed += AppearanceChanged;
        _suggestionReviewCallback = EventCallback.Factory.Create<DefinitionPath>(this, HandleSuggestionReviewRequestedAsync);
        _fieldFocusCallback = OnFieldFocused;
        RefreshConsentAction();
    }
    private void AppearanceChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            if (_form is not null)
            {
                _form.SetWorkspaceDensity(Appearance.Current.ToWorkspaceDensity());
                _form.SetWorkspaceNamingMode(Appearance.Current.ToWorkspaceNamingMode());
            }
            StateHasChanged();
        });
    }
    private string? _focusField;
    private IReadOnlyList<string> _focusPath = [];
    private long _focusGeneration;
    private bool _returnToReview;
    private ElementReference _consentControl;
    private string _workspaceLifetime = Guid.NewGuid().ToString("N");
    private long _generation;
    private CancellationTokenSource _mountCancellation = new();
    private Task _outputRetirement = Task.CompletedTask;
    private long _fileKey;
    private CancellationTokenSource? _readCancellation;
    private byte[]? _readBuffer;
    private InputFile? _fileInput;
    private WorkspaceStartupGate? _startupGate;
    private bool _disposed;
    private bool _ready;
    private bool _inspectorOpen;
    private DefinitionPath? _inspectedPath;
    private bool _suppressUndoToast;
    private long _lastToastRevision = -1;
    private Action<DefinitionPath> _fieldFocusCallback = null!;
    private bool _preparing;
    private bool _preparationFailed;
    private WorkspaceStartupStage StartupStage => _ready && _form is not null ? WorkspaceStartupStage.Ready
        : _form is null || _preparing ? WorkspaceStartupStage.Preparing
        : _preparationFailed ? WorkspaceStartupStage.PreparationFailed
        : WorkspaceStartupStage.BeforeEntry;
    private bool HasWork(DefinitionFormModel form) => !_disposed && _ready && ReferenceEquals(form, _form) &&
        (form.HasWorkspaceChanges || form.RequiresBrowserProfileComposition || _prepared.Count > 0 || _importPending || form.ImportComparison is not null
            || _developerPanel is { } developer && ReferenceEquals(developer.Form, form) && developer.HasWork);
    private string PreparationReadiness
    {
        get
        {
            if (_form!.RequiresBrowserProfileComposition) return "blocked";
            DefinitionFormProgress progress = _form!.Projection.Progress;
            return _form.ConfigurationContext is { IsReady: false } || _form.HasInputErrors || _form.HasAnswerBudgetError || progress.Invalid > 0 || progress.Missing > progress.Deferred
                ? "blocked" : progress.Deferred > 0 ? "deferred" : "complete";
        }
    }
    private long _offlineLease;
    private DefinitionValidationResult? _validation;
    private DefinitionValidationResult? _reviewValidation;
    private BrowserNavigationState? _navigation;
    private long _navigationLease;
    private WorkspaceSearchState? _search;
    private long _searchLease;
    private Dictionary<string, DefinitionFormSection> _editorSections = new(StringComparer.Ordinal);
    private DefinitionDiscoverySelection? _observedDiscoverySelection;

    protected override void OnParametersSet()
    {
        if (_disposed || ReferenceEquals(_pinned, Definition)) return;
        RetireDeveloper();
        _developerMode = false;
        _developerArtifactId = null;
        RetireView();
        _configurePanel = null;
        _reviewPanel = null;
        _filesPanel = null;
        _compactReviewOpen = false;
        _reviewValidation = null;
        _compactFilesOpen = false;
        _compactShowMatchesOnly = false;
        _observedLayoutMode = WorkspaceLayoutMode.Guided;
        InvalidateMounts();
        Invalidate();
        _search?.Release(this, _searchLease);
        _navigation?.Release(this, _navigationLease);
        if (_form is not null) _form.ImportComparisonChanged -= ImportComparisonChanged;
        _form?.Dispose();
        _activeWorkflowStage = WorkflowStage.Configure;
        _returnToReview = false;
        _selectedSection = null;
        _focusField = null;
        _pinned = Definition;
        _contextOpen = false;
        _consentOpen = false;
        _taskToolsOpen = false;
        _importOpen = false;
        _profileOpen = false;
        _initialMode = InitialMode is "import" or "profile" ? InitialMode : "fresh";
        _importPurpose = "compare";
        _ready = false;
        _preparing = false;
        _preparationFailed = false;
        QueueStartup(InitialLocale, InitialEntry, begin: true);
        _navigation ??= Services.GetService(typeof(BrowserNavigationState)) as BrowserNavigationState;
        StartupRequest startup = _startup!;
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync,
            () => (startup.Locale, startup.Direction), () => false) ?? 0;
    }

    private void QueueStartup(string? initialLocale, string? initialEntry, bool begin)
    {
        _form = null;
        _observedDiscoverySelection = null;
        RefreshConsentAction();
        SetStatus("Preparing");
        _statusCode = "workspace-starting";
        var localization = new DefinitionLocalization(Definition);
        string locale = initialLocale ?? localization.DefaultLocale;
        if (!localization.Locales.Contains(locale, StringComparer.Ordinal))
            throw new InvalidOperationException("locale-unsupported");
        _startup = new(Definition, locale, localization.Direction(locale), initialEntry,
            _viewGeneration, _generation, begin, _mountCancellation.Token);
        _startupScheduled = false;
    }

    private bool IsCurrentStartup(StartupRequest startup) => !_disposed
        && ReferenceEquals(startup, _startup) && ReferenceEquals(startup.Definition, _pinned)
        && ReferenceEquals(startup.Definition, Definition) && startup.View == _viewGeneration
        && startup.Generation == _generation && !startup.Cancellation.IsCancellationRequested;

    private void InitializeForm(StartupRequest startup)
    {
        _form = new DefinitionFormModel(startup.Definition, DefinitionSessionPrivacy.BrowserNoSecrets,
            BrowserLaunchSelection.EditingCapabilities, startup.Definition.Presentation is null
                ? DefinitionEditorVisibilityPolicy.Direct : DefinitionEditorVisibilityPolicy.Authored);
        _workspaceLifetime = Guid.NewGuid().ToString("N");
        _hadPreparedOutput = false;
        ++_profileConsentEpoch;
        if (startup.Definition.Presentation is not null) _form.SetDisclosureOverride(FieldDisclosure.Essential);
        _form.SelectLocale(startup.Locale);
        if (startup.Entry is not null) _form.SelectOutput(startup.Entry);
        _form.SetWorkspaceDensity(Appearance.Current.ToWorkspaceDensity());
        _form.SetWorkspaceNamingMode(Appearance.Current.ToWorkspaceNamingMode());
        IndexSections();
        _requiredCount = _form.Projection.Progress.Required;
        _form.InputChanged += InputChanged;
        _form.ViewChanged += ViewChanged;
        _form.ImportComparisonChanged += ImportComparisonChanged;
        _importPurpose = "fill";
        RefreshImportArtifacts();
        SelectCurrentImportArtifact();
        RegisterSearch();
        RefreshConsentAction();
        _startup = null;
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
                long inputRevision = form.InputRevision;
                builder.OpenComponent<ConfigurationDiscovery>(0);
                builder.AddAttribute(1, nameof(ConfigurationDiscovery.Form), form);
                builder.AddAttribute(2, nameof(ConfigurationDiscovery.Navigate),
                    EventCallback.Factory.Create<DefinitionDiagnostic>(this, async diagnostic =>
                    {
                        if (_disposed || !ReferenceEquals(form, _form) || lease != _search.Generation || inputRevision != form.InputRevision) return;
                        if (!await ResolvePendingAsync() || _disposed || !ReferenceEquals(form, _form)
                            || lease != _search.Generation || inputRevision != form.InputRevision) return;
                        _search.CompleteNavigation(this, lease);
                        CorrectField(diagnostic);
                    }));
                builder.AddAttribute(3, nameof(ConfigurationDiscovery.CommandSelected),
                    EventCallback.Factory.Create<WorkspaceCommandDescriptor>(this, async command =>
                    {
                        if (_disposed || !ReferenceEquals(form, _form) || lease != _search.Generation) return;
                        _search.CompleteNavigation(this, lease);
                        await ExecuteCommandAsync(command);
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

    private sealed record StatusRecoveryAction(string Target, string LabelKey);

    private StatusRecoveryAction? StatusRecovery => _statusCode switch
    {
        "input-type-invalid" or "preparation-rejected" or "answer-budget-exceeded"
            => new("correction", "RecoverCorrection"),
        "import-secret-consent-required"
            => new("consent", "ReviewSecretConsent"),
        "import-too-large" or "import-read-failed" or "import-cancelled" or "inverse-unsupported"
            => new("import", "RecoverImport"),
        _ when _statusKey is "ImportRefusedStatus" or "AutofillRefusedStatus" or "ImportUnsupportedStatus"
            => new("import", "RecoverImport"),
        "profile-private-output-required"
            => new("profile", "RecoverProfile"),
        "aggregate-export-unavailable"
            => new("retry-aggregate-export", "RetryFileExport"),
        "aggregate-export-rejected"
            => new("files", "RecoverExportFiles"),
        "deployment-context-invalid"
            => new("context", "ConfigurationContext"),
        "transfer-rejected"
            => new("review", "WorkflowReview"),
        _ => null
    };

    private async Task ExecuteStatusRecoveryAsync()
    {
        if (_disposed || !_ready || _form is null || StatusRecovery is not { } recovery) return;
        switch (recovery.Target)
        {
            case "correction":
                if (_form.GetNextActionableDiagnostic(_selectedSection) is { } diagnostic)
                {
                    CorrectField(diagnostic);
                    return;
                }
                if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
                    CloseCompactInlinePanel();
                else
                    SelectWorkflowStage(WorkflowStage.Configure);
                RequestFocus(FocusTarget.Configure);
                break;
            case "consent":
                _consentOpen = true;
                RequestFocus(FocusTarget.Consent);
                break;
            case "import":
                if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
                    CloseCompactInlinePanel();
                else if (_activeWorkflowStage != WorkflowStage.Configure)
                    SelectWorkflowStage(WorkflowStage.Configure);
                _importOpen = true;
                _profileOpen = false;
                RequestFocus(FocusTarget.Import);
                break;
            case "profile":
                _profileOpen = true;
                _importOpen = false;
                RequestFocus(FocusTarget.Profile);
                break;
            case "retry-aggregate-export":
                await ExportAllAsync(_lastAggregateExportWasArchive);
                break;
            case "files":
                if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
                    OpenCompactFilesPanel();
                else if (_activeWorkflowStage != WorkflowStage.Prepare)
                    SelectWorkflowStage(WorkflowStage.Prepare);
                RequestFocus(FocusTarget.Files);
                break;
            case "context":
                if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
                    CloseCompactInlinePanel();
                else if (_activeWorkflowStage != WorkflowStage.Configure)
                    SelectWorkflowStage(WorkflowStage.Configure);
                _contextOpen = true;
                RequestFocus(FocusTarget.Context);
                break;
            case "review":
                if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
                    OpenCompactReviewPanel();
                else
                    SelectWorkflowStage(WorkflowStage.Review);
                break;
        }
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
    public async Task<bool> ResolvePendingAsync()
    {
        if (_disposed) return false;
        ConfigurationDeveloper? panel = _developerPanel;
        DefinitionFormModel? form = _form;
        if (panel is null || !ReferenceEquals(panel.Form, form) || !panel.IsDirty) return true;
        bool resolved = await panel.ResolvePendingAsync();
        return resolved && !_disposed && _ready && ReferenceEquals(form, _form)
            && ReferenceEquals(panel, _developerPanel);
    }

    private void RetireDeveloper()
    {
        ConfigurationDeveloper? panel = _developerPanel;
        _developerPanel = null;
        ++_developerEpoch;
        if (panel is not null)
            _outputRetirement = Task.WhenAll(_outputRetirement, ObserveRetirementAsync([panel.RetireAsync()]));
        _navigation?.RefreshWork(this, _navigationLease);
    }

    private async Task RequestEditorModeAsync(bool developer)
    {
        if (_developerMode == developer || !await ResolvePendingAsync()) return;
        RetireDeveloper();
        _developerMode = developer;
        _configurePanel = null;
        SelectWorkflowStage(WorkflowStage.Configure);
    }

    private async Task RequestWorkflowStageAsync(WorkflowStage stage)
    {
        if (!await ResolvePendingAsync()) return;
        if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
        {
            if (stage == WorkflowStage.Review) OpenCompactReviewPanel();
            else if (stage == WorkflowStage.Prepare) OpenCompactFilesPanel();
            else CloseCompactInlinePanel();
            return;
        }
        if (_activeWorkflowStage == stage) return;
        SelectWorkflowStage(stage);
    }

    private void OpenCompactReviewPanel()
    {
        if (_compactFilesOpen) RetireCompactFilesPanel();
        else RetireView();
        _compactReviewOpen = true;
        _reviewValidation = _form!.Validate();
        RequestFocus(FocusTarget.Review);
    }

    private void OpenCompactFilesPanel()
    {
        if (_compactFilesOpen) return;
        _compactReviewOpen = false;
        _reviewValidation = null;
        _reviewPanel = null;
        RetireView();
        _compactFilesOpen = true;
        RequestFocus(FocusTarget.Files);
    }

    private void RetireCompactFilesPanel()
    {
        if (!_compactFilesOpen) return;
        _compactFilesOpen = false;
        _filesPanel = null;
        RetireView();
        Task[] retirements = _prepared.Select(artifact =>
        {
            artifact.Revealed = false;
            artifact.View = null;
            return artifact.Export.InvalidateAsync().AsTask();
        }).Concat(_bundleExport is null ? [] : new[] { _bundleExport.InvalidateAsync().AsTask() }).ToArray();
        _outputRetirement = Task.WhenAll(_outputRetirement, ObserveRetirementAsync(retirements));
    }

    private void CloseCompactInlinePanel()
    {
        if (_compactFilesOpen)
            RetireCompactFilesPanel();
        else if (_compactReviewOpen)
        {
            _compactReviewOpen = false;
            _reviewValidation = null;
            _reviewPanel = null;
            RetireView();
        }
    }

    private async Task FocusNextRequiredWorkAsync()
    {
        if (_disposed || _form is null) return;
        DefinitionFormModel form = _form;
        if (!await ResolvePendingAsync() || _disposed || !ReferenceEquals(form, _form)) return;
        if (form.GetNextActionableDiagnostic(_selectedSection) is { } diagnostic)
        {
            CorrectField(diagnostic);
            return;
        }
        if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
            OpenCompactReviewPanel();
        else
            SelectWorkflowStage(WorkflowStage.Review);
    }

    private async Task RequestOutputChangeAsync(ChangeEventArgs args)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (OutputSelector is not { } selector || args.Value is not string choice
            || !int.TryParse(choice, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            || index < 0 || index >= selector.Options.Count) return;
        if (!await ReviewTransitionAsync(selector.Options[index].Value))
        {
            if (ReferenceEquals(form, _form))
            {
                ++_outputChoiceKey;
                if (view == _viewGeneration) RequestFocus(FocusTarget.Output);
            }
            return;
        }
    }
    private ArtifactDefinition[] ImportArtifacts { get; set; } = [];
    private DefinitionImportCapability? ImportCapability => _importCapabilities.GetValueOrDefault(_importArtifact);
    private void IndexSections()
    {
        _observedDiscoverySelection = _form!.DiscoverySelection;
        _sectionLocale = _form.Locale;
        _editorSections = _form.DisplaySections.ToDictionary(section => section.Id, StringComparer.Ordinal);
    }
    private IReadOnlyList<DefinitionFormSection> EditorSections()
    {
        return _form!.Projection.Sections;
    }
    private string? SelectedSection(IReadOnlyList<DefinitionFormSection> sections) =>
        sections.Any(item => item.Id == _selectedSection) ? _selectedSection : sections.Count > 0 ? sections[0].Id : null;
    private DefinitionFormModel? _renderedConfigureForm;
    private long _renderedConfigureView;

    private void SelectSection(string section)
    {
        _selectedSection = section;
        _focusField = null;
        _form?.RecordSectionOpened(section);
        if (_activeWorkflowStage == WorkflowStage.Configure && LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
        {
            CloseCompactInlinePanel();
            return;
        }
        SelectWorkflowStage(WorkflowStage.Configure);
    }

    private void HandleConfigureSectionSelected(string section)
    {
        if (_renderedConfigureForm is not null && IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
            SelectSection(section);
    }

    private void HandleCompactShowMatchesOnlyChanged(bool value)
    {
        if (_renderedConfigureForm is not null && IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
            _compactShowMatchesOnly = value;
    }

    private void HandleConfigureReviewRequested()
    {
        if (_renderedConfigureForm is not null && IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
            SelectWorkflowStage(WorkflowStage.Review);
    }

    private void SelectWorkflowStage(WorkflowStage stage)
    {
        if (_activeWorkflowStage == WorkflowStage.Configure && _developerMode && stage != WorkflowStage.Configure)
            RetireDeveloper();
        bool leavingFiles = _activeWorkflowStage == WorkflowStage.Prepare && stage != WorkflowStage.Prepare;
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
        if (stage != WorkflowStage.Configure) _configurePanel = null;
        if (stage != WorkflowStage.Review) _reviewPanel = null;
        if (stage != WorkflowStage.Prepare) _filesPanel = null;
        if (stage == WorkflowStage.Review) _validation = _form!.Validate();
        RequestFocus(stage switch { WorkflowStage.Review => FocusTarget.Review, WorkflowStage.Prepare => FocusTarget.Files, _ => FocusTarget.Configure });
        if (leavingFiles)
        {
            Task[] retirements = _prepared.Select(artifact =>
            {
                artifact.Revealed = false;
                artifact.View = null;
                return artifact.Export.InvalidateAsync().AsTask();
            }).Concat(_bundleExport is null ? [] : new[] { _bundleExport.InvalidateAsync().AsTask() }).ToArray();
            _outputRetirement = Task.WhenAll(_outputRetirement, ObserveRetirementAsync(retirements));
        }
    }
    private void CorrectField(DefinitionDiagnostic diagnostic)
    {
        string fieldId = diagnostic.FieldId;
        if (fieldId.Length == 0)
        {
            DefinitionPath literal;
            if (diagnostic.Location is { } rootLocation)
            {
                if (!_form!.TryResolveCorrectionPath("", rootLocation, out literal)) return;
            }
            else if (diagnostic.RelativePath.Count == 0) literal = DefinitionPath.FromSegments([]);
            else return;
            FocusRootCorrection(literal);
            return;
        }
        FieldDefinition? field = _form!.Validate().Fields.FirstOrDefault(item => item.Id == fieldId)?.Definition;
        if (field is null) return;
        IReadOnlyList<string> path = new[] { fieldId }.Concat(diagnostic.RelativePath).ToArray();
        if (diagnostic.Location is { } location && !_form!.TryResolveCorrection(fieldId, location, out path)) return;
        RetireDeveloper();
        _developerMode = false;
        if (OutputSelector is { } selector && field.Name == selector.Location.Segments[0] && path.Count == 1)
        {
            SelectWorkflowStage(WorkflowStage.Configure);
            RequestFocus(FocusTarget.Output);
            return;
        }
        if (_form!.ConfigurationContext is not null && Definition.Deployment is { } deployment
            && DefinitionPath.FromSegments(new[] { field.Name }.Concat(path.Skip(1)).ToArray()).ToPointer() is var selectorPointer
            && (selectorPointer == deployment.RecipePointer || selectorPointer == deployment.ScopePointer))
        {
            SelectWorkflowStage(WorkflowStage.Configure);
            RequestFocus(FocusTarget.Context);
            return;
        }
        if (!DefinitionEditingSupport.HasPropertyRoot(Definition))
        {
            DefinitionPath literal;
            if (diagnostic.Location is { } rootLocation)
            {
                if (!_form!.TryResolveCorrectionPath(fieldId, rootLocation, out literal)) return;
            }
            else literal = DefinitionPath.FromSegments(new[] { field.Name }.Concat(diagnostic.RelativePath));
            FocusRootCorrection(literal);
            return;
        }
        bool focused = _form!.FocusEditor(path, preserveDiscovery: true);
        bool denied = !_form.CanEdit(path);
        if (!focused && !denied) return;
        if (_form.WorkspaceLayoutMode == WorkspaceLayoutMode.Compact)
        {
            _compactShowMatchesOnly = false;
            CloseCompactInlinePanel();
            if (_activeWorkflowStage != WorkflowStage.Configure)
                SelectWorkflowStage(WorkflowStage.Configure);
            _selectedSection = EditorSections().FirstOrDefault(section => section.Fields.Contains(fieldId))?.Id ?? _selectedSection;
        }
        else
        {
            SelectWorkflowStage(WorkflowStage.Configure);
            _selectedSection = EditorSections().Single(section => section.Fields.Contains(fieldId)).Id;
        }
        _focusField = denied ? null : field.Id;
        _focusPath = denied ? [] : path;
        _pendingFocus = null;
        ++_focusGeneration;
        if (denied) RequestFocus(FocusTarget.Consent);
    }
    private void FocusRootCorrection(DefinitionPath location)
    {
        bool canEdit = _form!.GetFieldValidation(location).CanEdit;
        RetireDeveloper();
        _developerMode = false;
        if (canEdit) _form.FocusEditor(location, preserveDiscovery: true);
        if (_form.WorkspaceLayoutMode == WorkspaceLayoutMode.Compact)
        {
            _compactShowMatchesOnly = false;
            CloseCompactInlinePanel();
            if (_activeWorkflowStage != WorkflowStage.Configure)
                SelectWorkflowStage(WorkflowStage.Configure);
        }
        else
            SelectWorkflowStage(WorkflowStage.Configure);
        _focusField = canEdit ? "" : null;
        _focusPath = canEdit ? location.Segments : [];
        _pendingFocus = null;
        ++_focusGeneration;
        if (!canEdit) RequestFocus(FocusTarget.Consent);
    }
    private void ChangeReviewField(DefinitionDiagnostic diagnostic)
    {
        _returnToReview = true;
        CorrectField(diagnostic);
    }
    private void ReturnToReview()
    {
        _returnToReview = false;
        if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
            OpenCompactReviewPanel();
        else
            SelectWorkflowStage(WorkflowStage.Review);
    }
    private void ReviewImportConsent()
    {
        SelectWorkflowStage(WorkflowStage.Configure);
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
            SelectWorkflowStage(WorkflowStage.Configure);
            RequestFocus(FocusTarget.Consent);
        }
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _form)) return;
        RetireView();
        bool keepSettledCompactMounts = _activeWorkflowStage == WorkflowStage.Configure &&
            LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode && _configurePanel?.IsCompactSettled == true;
        if (!keepSettledCompactMounts)
            InvalidateMounts();
        _focusField = null;
        Invalidate();
        if (_compactReviewOpen) _reviewValidation = _form!.Validate();
        RefreshImportArtifacts();
        if (_importOutputSelection != OutputSelection || !ImportArtifacts.Any(artifact => artifact.Id == _importArtifact)) SelectCurrentImportArtifact();
        int required = _form!.Projection.Progress.Required;
        if (required == _requiredCount) SetStatus("AnswersChangedStatus");
        else SetStatus("RequiredWorkChangedStatus", _requiredCount, required);
        _requiredCount = required;
        _statusCode = "answers-changed";
        _search?.Refresh(this, _searchLease);
        if (!_suppressUndoToast && _form!.CanUndo && _lastToastRevision != _form.InputRevision &&
            IsUndoToastAction(_form.LastUndoLabel, _form.Locale))
        {
            _lastToastRevision = _form.InputRevision;
            ShowUndoSnackbar(_form, _form.LastUndoLabel!);
        }
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
            if (_form.WorkspaceLayoutMode == WorkspaceLayoutMode.Compact && _compactShowMatchesOnly)
                StateHasChanged();
            return;
        }
        bool layoutChanged = _observedLayoutMode != _form.WorkspaceLayoutMode;
        _observedLayoutMode = _form.WorkspaceLayoutMode;
        if (layoutChanged && _form.WorkspaceLayoutMode != WorkspaceLayoutMode.Compact)
            _compactShowMatchesOnly = false;
        RetireView();
        if (layoutChanged || _form.WorkspaceLayoutMode != WorkspaceLayoutMode.Compact)
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
        _transferReview = null;
        ++_viewGeneration;
        ++_focusGeneration;
        _pendingFocus = null;
        _focusField = null;
        _focusPath = [];
    }

    private void RefreshConsentAction()
    {
        DefinitionFormModel? form = _form;
        string lifetime = _workspaceLifetime;
        _consentAction = EventCallback.Factory.Create(this, () =>
        {
            if (_disposed || form is null || !ReferenceEquals(form, _form) ||
                !string.Equals(lifetime, _workspaceLifetime, StringComparison.Ordinal) ||
                _renderedConfigureForm is null || !IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
                return;
            RequestFocus(FocusTarget.Consent);
        });
    }

    private Task HandleSuggestionReviewRequestedAsync(DefinitionPath path)
    {
        if (_disposed || _form is null || _renderedConfigureForm is null || !IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
            return Task.CompletedTask;
        return ReviewInlineSuggestionAsync(path);
    }
    private void RequestFocus(FocusTarget target)
    {
        if (target == FocusTarget.Context) _contextOpen = true;
        if (target == FocusTarget.Consent) _consentOpen = true;
        _pendingFocus = (_form!, _viewGeneration, target);
    }
    private void ChangeDetail(ChangeEventArgs args)
    {
        switch (args.Value)
        {
            case "authored": _form!.SetDisclosureOverride(null); break;
            case "essential": _form!.SetDisclosureOverride(FieldDisclosure.Essential); break;
            case "guided": _form!.SetDisclosureOverride(FieldDisclosure.Guided); break;
            case "advanced": _form!.SetDisclosureOverride(FieldDisclosure.Advanced); break;
        }
    }
    private void ChangeLocale(ChangeEventArgs args)
    {
        if (args.Value is string locale) _form!.SelectLocale(locale);
    }
    private void OpenScopeSelector()
    {
        _contextOpen = true;
        RequestFocus(FocusTarget.Context);
    }
    private Task ChangeNamingModeAsync(WorkspaceNamingMode mode)
    {
        _form?.SetWorkspaceNamingMode(mode);
        return Appearance.SetNamingModeAsync(AppearanceState.FromWorkspaceNamingMode(mode)).AsTask();
    }
    private Task ChangeDensityValueAsync(WorkspaceDensity density)
    {
        _form?.SetWorkspaceDensity(density);
        return Appearance.SetDensityAsync(AppearanceState.FromWorkspaceDensity(density)).AsTask();
    }
    private Task ChangeDensitySelectAsync(ChangeEventArgs args)
    {
        WorkspaceDensity target = args.Value?.ToString() switch
        {
            "spacious" => WorkspaceDensity.Spacious,
            "compact" => WorkspaceDensity.Compact,
            "dense" => WorkspaceDensity.Dense,
            _ => WorkspaceDensity.Comfortable
        };
        return ChangeDensityValueAsync(target);
    }
    private async Task SelectStageAndLayoutAsync((string Stage, WorkspaceLayoutMode Layout) selection)
    {
        if (_disposed || _form is null) return;
        DefinitionFormModel form = _form;
        if (!await ResolvePendingAsync() || _disposed || !ReferenceEquals(form, _form)) return;
        if (_developerMode && selection.Stage == "configure")
        {
            RetireDeveloper();
            _developerMode = false;
            _configurePanel = null;
        }
        if (selection.Stage == "configure")
        {
            CloseCompactInlinePanel();
            if (selection.Layout == WorkspaceLayoutMode.Compact)
                _form?.SetWorkspaceLayoutMode(WorkspaceLayoutMode.Compact, DefinitionEditorVisibilityPolicy.Continuous);
            else
            {
                _compactShowMatchesOnly = false;
                _form?.SetWorkspaceLayoutMode(WorkspaceLayoutMode.Guided);
            }
            if (_activeWorkflowStage != WorkflowStage.Configure)
                SelectWorkflowStage(WorkflowStage.Configure);
            return;
        }
        if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
        {
            if (selection.Stage == "review") OpenCompactReviewPanel();
            else if (selection.Stage == "prepare") OpenCompactFilesPanel();
            return;
        }
        WorkflowStage target = selection.Stage switch
        {
            "review" => WorkflowStage.Review,
            "prepare" => WorkflowStage.Prepare,
            _ => WorkflowStage.Configure
        };
        if (_activeWorkflowStage != target)
            SelectWorkflowStage(target);
    }
    private Task HandleWorkspaceKeyAsync(KeyboardEventArgs args)
    {
        if ((args.CtrlKey || args.MetaKey) && !args.AltKey)
        {
            if (args.ShiftKey && string.Equals(args.Key, "K", StringComparison.OrdinalIgnoreCase))
            {
                WorkspaceNamingMode next = NamingMode == WorkspaceNamingMode.Technical
                    ? WorkspaceNamingMode.Humanized
                    : WorkspaceNamingMode.Technical;
                return ChangeNamingModeAsync(next);
            }
            if (!args.ShiftKey && string.Equals(args.Key, "k", StringComparison.OrdinalIgnoreCase))
            {
                _search?.RequestOpen(this, _searchLease);
                return Task.CompletedTask;
            }
            if ((args.ShiftKey && string.Equals(args.Key, "z", StringComparison.OrdinalIgnoreCase))
                || (!args.ShiftKey && string.Equals(args.Key, "y", StringComparison.OrdinalIgnoreCase)))
            {
                PerformRedo();
                return Task.CompletedTask;
            }
            if (!args.ShiftKey && string.Equals(args.Key, "z", StringComparison.OrdinalIgnoreCase))
            {
                PerformUndo();
                return Task.CompletedTask;
            }
        }
        if (!args.CtrlKey && !args.MetaKey && !args.AltKey)
        {
            if (args.Key == "F2")
            {
                ToggleInspector();
                return Task.CompletedTask;
            }
            if (args.Key == "?")
                return OpenShortcutsHelpAsync();
        }
        return Task.CompletedTask;
    }

    private void OnFieldFocused(DefinitionPath path)
    {
        if (_disposed) return;
        bool changed = !Equals(_inspectedPath, path);
        _inspectedPath = path;
        if (_inspectorOpen && changed)
            _ = InvokeAsync(StateHasChanged);
    }

    private void ToggleInspector()
    {
        if (_disposed) return;
        _inspectorOpen = !_inspectorOpen;
    }

    private void PerformUndo()
    {
        if (_disposed || _form is null || !_form.CanUndo) return;
        _suppressUndoToast = true;
        try
        {
            if (_form.Undo())
                Snackbar.RemoveByKey("workspace-undo");
        }
        finally { _suppressUndoToast = false; }
    }

    private void PerformRedo()
    {
        if (_disposed || _form is null || !_form.CanRedo) return;
        _suppressUndoToast = true;
        try
        {
            if (_form.Redo())
                Snackbar.RemoveByKey("workspace-undo");
        }
        finally { _suppressUndoToast = false; }
    }

    private static bool IsUndoToastAction(string? label, string locale) => !string.IsNullOrEmpty(label) && (
        string.Equals(label, PresentationStrings.Get("UndoActionOmit", locale), StringComparison.Ordinal) ||
        string.Equals(label, PresentationStrings.Get("UndoActionReset", locale), StringComparison.Ordinal) ||
        string.Equals(label, PresentationStrings.Get("UndoActionRemoveItem", locale), StringComparison.Ordinal) ||
        string.Equals(label, PresentationStrings.Get("UndoActionChangeVariant", locale), StringComparison.Ordinal) ||
        string.Equals(label, PresentationStrings.Get("UndoActionDocument", locale), StringComparison.Ordinal) ||
        string.Equals(label, PresentationStrings.Get("UndoActionImport", locale), StringComparison.Ordinal));

    private void ShowUndoSnackbar(DefinitionFormModel form, string label)
    {
        Snackbar.RemoveByKey("workspace-undo");
        string undoText = UiText("UndoAction");
        Snackbar.Add(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "workspace-undo-toast");
            builder.OpenElement(2, "span");
            builder.AddContent(3, label);
            builder.CloseElement();
            builder.OpenElement(4, "button");
            builder.AddAttribute(5, "type", "button");
            builder.AddAttribute(6, "class", "button button--secondary");
            builder.AddAttribute(7, "data-snackbar-undo", "");
            builder.AddAttribute(8, "onclick", EventCallback.Factory.Create(this, () =>
            {
                if (!_disposed && ReferenceEquals(form, _form))
                    PerformUndo();
            }));
            builder.AddContent(9, undoText);
            builder.CloseElement();
            builder.CloseElement();
        }, MudBlazor.Severity.Info, options => { options.RequireInteraction = false; }, "workspace-undo");
    }

    private Task OpenShortcutsHelpAsync()
    {
        if (_disposed || _form is null) return Task.CompletedTask;
        var parameters = new MudBlazor.DialogParameters
        {
            [nameof(KeyboardShortcutsDialog.Locale)] = _form.Locale
        };
        return Dialogs.ShowAsync<KeyboardShortcutsDialog>(
            UiText("ShortcutsTitle"), parameters, DialogOptionsFactory.Standard());
    }

    private async Task ExecuteCommandAsync(WorkspaceCommandDescriptor command)
    {
        if (_disposed || _form is null) return;
        switch (command.Id)
        {
            case WorkspaceCommandId.Undo: PerformUndo(); break;
            case WorkspaceCommandId.Redo: PerformRedo(); break;
            case WorkspaceCommandId.SwitchToGuided:
                await SelectStageAndLayoutAsync(("configure", WorkspaceLayoutMode.Guided));
                break;
            case WorkspaceCommandId.SwitchToCompact:
                await SelectStageAndLayoutAsync(("configure", WorkspaceLayoutMode.Compact));
                break;
            case WorkspaceCommandId.SwitchToReview:
                await RequestWorkflowStageAsync(WorkflowStage.Review);
                break;
            case WorkspaceCommandId.SwitchToFiles:
                await RequestWorkflowStageAsync(WorkflowStage.Prepare);
                break;
            case WorkspaceCommandId.ToggleNamingMode:
                await ChangeNamingModeAsync(NamingMode == WorkspaceNamingMode.Technical
                    ? WorkspaceNamingMode.Humanized : WorkspaceNamingMode.Technical);
                break;
            case WorkspaceCommandId.ToggleInspector:
                ToggleInspector();
                break;
            case WorkspaceCommandId.OpenShortcutsHelp:
                await OpenShortcutsHelpAsync();
                break;
            case WorkspaceCommandId.JumpToNextError:
            case WorkspaceCommandId.JumpToNextRequired:
            case WorkspaceCommandId.JumpToNextUnvisited:
                if (command.TargetSectionId is { } sectionId)
                {
                    if (LayoutMode == WorkspaceLayoutMode.Compact && !_developerMode)
                    {
                        _compactShowMatchesOnly = false;
                        CloseCompactInlinePanel();
                        if (_configurePanel is not null)
                            await _configurePanel.JumpToCompactSectionAsync(sectionId);
                    }
                    else
                    {
                        await SelectStageAndLayoutAsync(("configure", WorkspaceLayoutMode.Guided));
                        SelectSection(sectionId);
                    }
                }
                break;
        }
    }

    private void ChangeExperience(ChangeEventArgs args)
    {
        if (args.Value is not string id || !_form!.SelectExperience(id)) return;
        _selectedSection = null;
        SelectWorkflowStage(WorkflowStage.Configure);
    }
    private async Task ConsentAsync(ChangeEventArgs args)
    {
        bool enabled = args.Value is true;
        if (enabled != (_form!.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled))
            ++_profileConsentEpoch;
        if (!enabled)
        {
            Snackbar.RemoveByKey("workspace-undo");
            Snackbar.Clear();
        }
        DefinitionFormModel form = _form!;
        long generation = _generation;
        form.SetSecretEntryEnabled(enabled);
        if (generation == _generation) Invalidate();
        Task retirement = _outputRetirement;
        SetStatus(enabled ? "SecretEntryEnabledStatus" : "SecretEntryRevokedStatus");
        _statusCode = enabled ? "secret-entry-enabled" : "secret-entry-revoked";
        await retirement;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (_form is null)
        {
            if (_startup is not { } startup || _startupScheduled || !IsCurrentStartup(startup)) return;
            _startupScheduled = true;
            _comparisonRendering ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js").AsTask();
            IJSObjectReference module = await _comparisonRendering;
            if (!IsCurrentStartup(startup)) return;
            try
            {
                await module.InvokeVoidAsync("paintWorkspace", startup.Cancellation, _startupControl,
                    startup.View.ToString(CultureInfo.InvariantCulture));
            }
            catch (OperationCanceledException) when (!IsCurrentStartup(startup)) { return; }
            catch (JSException) when (!IsCurrentStartup(startup)) { return; }
            if (!IsCurrentStartup(startup)) return;
            InitializeForm(startup);
            if (startup.Begin)
            {
                DefinitionFormModel initialized = _form!;
                Task beginning = BeginAsync();
                if (!beginning.IsCompleted && IsCurrentView(initialized, startup.View)
                    && !startup.Cancellation.IsCancellationRequested) StateHasChanged();
                await beginning;
                return;
            }
            RequestFocus(FocusTarget.Start);
            StateHasChanged();
            return;
        }
        DefinitionFormModel form = _form;
        long generation = _viewGeneration;
        if (!IsCurrentView(form, generation) || _pendingFocus is not { } request) return;
        _pendingFocus = null;
        if (!IsCurrentView(request.Form, request.Generation)) return;
        ElementReference? target = request.Target switch
        {
            FocusTarget.Configure when _ready && _activeWorkflowStage == WorkflowStage.Configure => _developerMode
                ? _developerPanel?.Heading : _configurePanel?.SectionHeading,
            FocusTarget.Review when _ready && (_activeWorkflowStage == WorkflowStage.Review || (LayoutMode == WorkspaceLayoutMode.Compact && _compactReviewOpen)) => _reviewPanel?.Heading,
            FocusTarget.Files when _ready && IsFilesSurfaceActive => _filesPanel?.Heading,
            FocusTarget.TransferReview when _ready && _transferReview is not null => _nextActionsPanel?.ReviewHeading,
            FocusTarget.Consent when _ready => _consentControl,
            FocusTarget.Import when _ready && _activeWorkflowStage == WorkflowStage.Configure => _fileInput?.Element,
            FocusTarget.Profile when _ready => _vaultPanel?.Heading,
            FocusTarget.Output when _ready && OutputSelector is not null => _outputControl,
            FocusTarget.Context when _ready && _form!.ConfigurationContext is not null => _contextControl,
            FocusTarget.DeveloperDestination when _ready && _developerMode && DeveloperArtifacts.Length > 1 => _developerDestinationControl,
            FocusTarget.Start when !_ready => _startupGate?.StartButton,
            _ => null
        };
        if (target is not { } element || string.IsNullOrEmpty(element.Id)) return;
        await element.FocusAsync(preventScroll: request.Target is FocusTarget.Import or FocusTarget.Start);
        if (!IsCurrentView(request.Form, request.Generation)) return;
    }
    public async Task BeginAsync()
    {
        if (_disposed || _form is null) return;
        _ready = false;
        _preparing = true;
        _preparationFailed = false;
        Invalidate();
        DefinitionFormModel form = _form;
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync, () => (form.Locale, form.Direction), () => HasWork(form)) ?? 0;
        long generation = _generation;
        long view = _viewGeneration;
        CancellationToken cancellation = _mountCancellation.Token;
        await Offline.InitializeAsync();
        if (!IsCurrentView(form, view) || generation != _generation || cancellation.IsCancellationRequested) return;
        ValueTask<bool> beginning = Offline.BeginWorkspaceAsync();
        long lease = Offline.WorkspaceGeneration;
        bool ready = await beginning;
        if (!IsCurrentView(form, view) || generation != _generation || cancellation.IsCancellationRequested
            || lease != Offline.WorkspaceGeneration)
        {
            await Offline.EndWorkspaceAsync(lease);
            return;
        }
        _preparing = false;
        _ready = ready;
        _preparationFailed = !ready;
        if (_ready)
        {
            _offlineLease = lease;
            if (_initialMode == "import" && ImportArtifacts.Length > 0)
            {
                _importOpen = true;
                _profileOpen = false;
                RequestFocus(FocusTarget.Import);
            }
            else if (_initialMode == "profile")
            {
                _profileOpen = true;
                _importOpen = false;
                RequestFocus(FocusTarget.Profile);
            }
            SetStatus(form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled
                ? "SecretEntryEnabledStatus" : "WorkspaceReadyStatus");
            _statusCode = "workspace-ready";
        }
        else
        {
            bool offlineIncomplete = Offline.IsOffline && !Offline.OfflineReady;
            SetStatus(offlineIncomplete ? "AppPreparationOfflineStatus" : "AppPreparationIncompleteStatus");
            _statusCode = offlineIncomplete ? "app-preparation-offline" : "app-preparation-incomplete";
        }
        _navigation?.RefreshWork(this, _navigationLease);
        StateHasChanged();
    }

    private async Task CancelStartupAsync()
    {
        if (_disposed || _ready) return;
        ++_generation;
        long lease = _offlineLease;
        _offlineLease = 0;
        _preparing = false;
        _preparationFailed = false;
        InvalidateMounts();
        if (_form is null && _startup is { } pending)
            InitializeForm(pending);
        if (_form is not null)
        {
            SetStatus("StartupBeforeEntryBody");
            _statusCode = "startup-cancelled";
            RequestFocus(FocusTarget.Start);
        }
        await Offline.EndWorkspaceAsync();
        if (lease != 0) await Offline.EndWorkspaceAsync(lease);
        StateHasChanged();
    }
    public void Invalidate() => _ = InvalidateOutputAsync();
    private Task InvalidateOutputAsync()
    {
        _outputExportContext = null;
        if (!_disposed) _form?.InvalidateOutputExports();
        InvalidateImport();
        PreparedArtifact[] previous = _prepared.ToArray();
        _prepared.Clear();
        _diagnostics.Clear();
        _validation = null;
        _reviewValidation = null;
        _navigation?.RefreshWork(this, _navigationLease);
        // Start every retirement before awaiting any JS cleanup. Each owner
        // revokes its workspace, bytes and generation synchronously.
        Task[] retirements = previous.Select(prepared => prepared.DisposeAsync().AsTask())
            .Concat(_bundleExport is null ? [] : new[] { _bundleExport.InvalidateAsync().AsTask() }).ToArray();
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
#if CONFIGURATOR_IMPORT_OWNERSHIP_PROBE
        Configurator.Engineering.ImportComparisonOwnershipProbe.ObserveRetirement(this);
#endif
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
        if (HasWork(current) && !await AppDiscardDialog.ConfirmAsync(Dialogs, current.Locale, _mountCancellation.Token)) return;
        if (!IsCurrentView(current, view) || operation != _generation) return;
        Task discarding = DiscardAsync();
        long clearedView = _viewGeneration;
        await discarding;
        if (_disposed || clearedView != _viewGeneration || _ready) return;
        ++_fileKey;
        SetStatus("WorkspaceClearedStatus");
        _statusCode = "workspace-cleared";
        if (_form is not null) RequestFocus(FocusTarget.Start);
        StateHasChanged();
    }
    public async Task DiscardAsync()
    {
        if (_disposed) return;
        RetireDeveloper();
        _developerMode = false;
        RetireView();
        InvalidateMounts();
        DefinitionFormModel? form = _form;
        long lease = _offlineLease;
        long navigationLease = _navigationLease;
        Task retirement = _outputRetirement;
        _ready = false;
        _preparing = false;
        _preparationFailed = false;
        _search?.Release(this, _searchLease);
        if (form is not null) form.ImportComparisonChanged -= ImportComparisonChanged;
        form?.Dispose();
        _form = null;
        Task outputs = InvalidateOutputAsync();
        _contextOpen = false;
        _consentOpen = false;
        _taskToolsOpen = false;
        _importOpen = false;
        _profileOpen = false;
        _activeWorkflowStage = WorkflowStage.Configure;
        _compactReviewOpen = false;
        _reviewValidation = null;
        _compactFilesOpen = false;
        _compactShowMatchesOnly = false;
        _hadPreparedOutput = false;
        _observedLayoutMode = WorkspaceLayoutMode.Guided;
        _configurePanel = null;
        _reviewPanel = null;
        _filesPanel = null;
        QueueStartup(initialLocale: null, initialEntry: null, begin: false);
        SetStatus("WorkspaceClearedStatus");
        _statusCode = "workspace-cleared";
        StateHasChanged();
        await Task.WhenAll(retirement, outputs);
        await Offline.EndWorkspaceAsync(lease);
        _navigation?.Release(this, navigationLease);
    }
    public async ValueTask DisposeAsync()
    {
        Appearance.Changed -= AppearanceChanged;
        if (_disposed) return;
        RetireDeveloper();
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
        if (_bundleExport is not null) await _bundleExport.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    private sealed class PreparedArtifact : IAsyncDisposable
    {
        public PreparedArtifact(DefinitionPreparationMetadata metadata, PresentationWorkspace workspace, IJSRuntime javascript)
        {
            Metadata = metadata;
            Workspace = workspace;
            Export = new BrowserArtifactExport(javascript);
            Export.SetOutput(workspace.Output);
        }
        public DefinitionPreparationMetadata Metadata { get; }
        public PresentationWorkspace Workspace { get; }
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
