namespace Configurator.Browser.Components.Configuration;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Browser.Components.Common;
using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Browser.Services.Docking;
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
    [Inject] private DockLayoutState DockLayoutState { get; set; } = null!;
    [Parameter, EditorRequired] public Definition Definition { get; set; } = null!;
    [Parameter] public string? InitialLocale { get; set; }
    [Parameter] public string? InitialEntry { get; set; }
    [Parameter] public string? InitialMode { get; set; }
    [Parameter] public RenderFragment? SourceDetails { get; set; }
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
    private WorkspaceStage _activeWorkflowStage = WorkspaceStage.Configure;
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
    private bool _compactShowMatchesOnly;
    private bool _hadPreparedOutput;
    private ConfigureLayoutMode _observedLayoutMode = ConfigureLayoutMode.SectionBySection;
    private bool _contextOpen;
    private bool _advancedOptionsOpen;
    private bool _consentOpen;
    private bool _taskToolsOpen;
    private WorkspaceHeader? _workspaceHeader;
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
    private ConfigureLayoutMode LayoutMode => _form?.ConfigureLayoutMode ?? ConfigureLayoutMode.SectionBySection;
    private WorkspaceDensity Density => _form?.WorkspaceDensity ?? Appearance.Current.ToWorkspaceDensity();
    private WorkspaceNamingMode NamingMode => _form?.WorkspaceNamingMode ?? Appearance.Current.ToWorkspaceNamingMode();
    private bool IsFilesSurfaceActive => _activeWorkflowStage == WorkspaceStage.Files;
    private WorkspaceStage ActiveHeaderStage => _activeWorkflowStage;
    private string? ScopeSummary => _form?.Projection.ContextSummary?.Summary
        ?? (_form?.ConfigurationContext is not { } context ? null : $"{context.RecipeId} · {context.ScopeId}");
    private bool HasSideFormNav
    {
        get
        {
            if (_disposed || !_ready || _form is null || _activeWorkflowStage != WorkspaceStage.Configure)
                return false;

            if (_navigation?.IsSettingsOpen == true)
                return false;

            if (DockLayoutState.ViewportWidth is > 0 and <= 960)
                return false;

            if (!DefinitionEditingSupport.HasPropertyRoot(_form.Definition))
                return false;

            return ConfigurationConfigurePanel.ResolveVisibleSections(_form, EditorSections()).Length > 1;
        }
    }

    private int WorkspaceMinimumContentWidth => !HasSideFormNav
        ? DockLayoutState.DefaultMinimumContentWidth
        : Density switch
        {
            WorkspaceDensity.Spacious => 806,
            WorkspaceDensity.Compact => 774,
            WorkspaceDensity.Dense => 766,
            _ => 790
        };

    private int ActiveAdvancedOptionsCount => _form is null
        ? 0
        : (_developerMode ? 1 : 0)
            + (IsAnyEndDockOpen || _inspectorOpen ? 1 : 0)
            + (_form.DisclosureOverride != (_form.Definition.Presentation is not null ? FieldDisclosure.Essential : (FieldDisclosure?)null) ? 1 : 0)
            + (_form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled ? 1 : 0);

    private void ToggleAdvancedOptions()
    {
        _advancedOptionsOpen = !_advancedOptionsOpen;
        if (!_advancedOptionsOpen)
            _contextOpen = false;
    }

    protected override void OnInitialized()
    {
        Appearance.Changed += AppearanceChanged;
        DockLayoutState.Changed += OnDockChanged;
        _navigation = Services.GetService(typeof(BrowserNavigationState)) as BrowserNavigationState;
        if (_navigation is not null)
            _navigation.Changed += OnNavigationStateChanged;
        _suggestionReviewCallback = EventCallback.Factory.Create<DefinitionPath>(this, HandleSuggestionReviewRequestedAsync);
        _fieldFocusCallback = OnFieldFocused;
        _keyDownCallback = HandleWorkspaceKeyAsync;
        RefreshConsentAction();
    }

    private void OnNavigationStateChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            StateHasChanged();
        });
    }

    private void OnDockChanged()
    {
        if (_disposed) return;
        SyncDockFlags();
        _ = InvokeAsync(StateHasChanged);
    }

    private void SyncDockFlags()
    {
        DockPanelEntry? inspector = DockLayoutState.GetPanel(BrowserDockPanels.InspectorId);
        DockPanelEntry? docJson = DockLayoutState.GetPanel(BrowserDockPanels.DocumentJsonId);
        bool anyOpen = inspector is { State.IsOpen: true } || docJson is { State.IsOpen: true };
        if (!anyOpen && _inspectorOpen)
        {
            _inspectorOpen = false;
            _inspectorMobileSheet = false;
        }
    }

    private bool IsEndDockPanelOpen(DockPanelId id) =>
        DockLayoutState.GetPanel(id) is { State.IsOpen: true };

    private bool IsAnyEndDockOpen => IsEndDockPanelOpen(BrowserDockPanels.InspectorId)
        || IsEndDockPanelOpen(BrowserDockPanels.DocumentJsonId);

    private void EnsureWorkspaceDockPanelsRegistered()
    {
        if (_form is null) return;
        string locale = _form.Locale;
        DockLayoutState.RegisterOrUpdate(
            BrowserDockPanels.CreateInspector(BrowserStrings.Get("DockTabInspector", locale), BrowserStrings.Get("InspectorPaneLabel", locale)),
            RenderInspectorDockPanel());
        DockLayoutState.RegisterOrUpdate(
            BrowserDockPanels.CreateDocumentJson(BrowserStrings.Get("DockTabDocumentJson", locale), BrowserStrings.Get("DocumentJsonDockLabel", locale)),
            RenderDocumentJsonDockPanel());
    }

    private void UnregisterWorkspaceDockPanels()
    {
        DockLayoutState.Unregister(BrowserDockPanels.InspectorId);
        DockLayoutState.Unregister(BrowserDockPanels.DocumentJsonId);
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
    private bool _inspectorMobileSheet;
    private bool _focusInspectorToggleOnClose;
    private ElementReference _inspectorToggleControl;
    private bool _errorSummaryRequested;
    private DefinitionPath? _inspectedPath;
    private bool _suppressUndoToast;
    private long _lastToastRevision = -1;
    private Action<DefinitionPath> _fieldFocusCallback = null!;
    private Func<KeyboardEventArgs, Task> _keyDownCallback = null!;
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
            WorkspaceProgressSummary summary = _form!.Projection.ProgressSummary;
            return _form.ConfigurationContext is { IsReady: false } || _form.HasInputErrors || _form.HasAnswerBudgetError ||
                   summary.TotalInvalidCount > 0 || summary.MissingRequired > summary.DeferredSecretCount
                ? "blocked" : summary.DeferredSecretCount > 0 ? "deferred" : "complete";
        }
    }
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
        if (_disposed || ReferenceEquals(_pinned, Definition)) return;
        RetireDeveloper();
        _developerMode = false;
        _inspectorOpen = false;
        _inspectorMobileSheet = false;
        UnregisterWorkspaceDockPanels();
        DockLayoutState.ClearMinimumContentWidth(DockScope.Workspace);
        _developerArtifactId = null;
        RetireView();
        _configurePanel = null;
        _reviewPanel = null;
        _filesPanel = null;
        _compactShowMatchesOnly = false;
        _observedLayoutMode = ConfigureLayoutMode.SectionBySection;
        InvalidateMounts();
        Invalidate();
        _search?.Release(this, _searchLease);
        _navigation?.Release(this, _navigationLease);
        if (_form is not null) _form.ImportComparisonChanged -= ImportComparisonChanged;
        _form?.Dispose();
        _activeWorkflowStage = WorkspaceStage.Configure;
        _returnToReview = false;
        _selectedSection = null;
        _focusField = null;
        _pinned = Definition;
        _contextOpen = false;
        _advancedOptionsOpen = false;
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
            () => (startup.Locale, startup.Direction), () => false,
            locale => _form?.SelectLocale(locale), () => Definition.Locales) ?? 0;
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
        _errorSummaryRequested = false;
        _inspectorMobileSheet = false;
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

    private enum StatusRecoveryTarget { Correction, Consent, Import, Profile, RetryAggregateExport, Files, Context, Review }

    private sealed record StatusRecoveryAction(StatusRecoveryTarget Target, string LabelKey)
    {
        public string TargetToken => Target switch
        {
            StatusRecoveryTarget.Correction => "correction",
            StatusRecoveryTarget.Consent => "consent",
            StatusRecoveryTarget.Import => "import",
            StatusRecoveryTarget.Profile => "profile",
            StatusRecoveryTarget.RetryAggregateExport => "retry-aggregate-export",
            StatusRecoveryTarget.Files => "files",
            StatusRecoveryTarget.Context => "context",
            StatusRecoveryTarget.Review => "review",
            _ => throw new ArgumentOutOfRangeException(nameof(Target))
        };
    }

    private StatusRecoveryAction? StatusRecovery => _statusCode switch
    {
        "input-type-invalid" or "preparation-rejected" or "answer-budget-exceeded"
            => new(StatusRecoveryTarget.Correction, "RecoverCorrection"),
        "import-secret-consent-required"
            => new(StatusRecoveryTarget.Consent, "ReviewSecretConsent"),
        "import-too-large" or "import-read-failed" or "import-cancelled" or "inverse-unsupported"
            => new(StatusRecoveryTarget.Import, "RecoverImport"),
        _ when _statusKey is "ImportRefusedStatus" or "AutofillRefusedStatus" or "ImportUnsupportedStatus"
            => new(StatusRecoveryTarget.Import, "RecoverImport"),
        "profile-private-output-required"
            => new(StatusRecoveryTarget.Profile, "RecoverProfile"),
        "aggregate-export-unavailable"
            => new(StatusRecoveryTarget.RetryAggregateExport, "RetryFileExport"),
        "aggregate-export-rejected"
            => new(StatusRecoveryTarget.Files, "RecoverExportFiles"),
        "deployment-context-invalid"
            => new(StatusRecoveryTarget.Context, "ConfigurationContext"),
        "transfer-rejected"
            => new(StatusRecoveryTarget.Review, "WorkflowReview"),
        _ => null
    };

    private async Task ExecuteStatusRecoveryAsync()
    {
        if (_disposed || !_ready || _form is null || StatusRecovery is not { } recovery) return;
        switch (recovery.Target)
        {
            case StatusRecoveryTarget.Correction:
                _errorSummaryRequested = true;
                if (_form.ResolveNextActionableTarget(_selectedSection) is { } target)
                {
                    await JumpToActionableTargetAsync(target);
                    return;
                }
                SelectWorkflowStage(WorkspaceStage.Configure);
                RequestFocus(FocusTarget.Configure);
                break;
            case StatusRecoveryTarget.Consent:
                _consentOpen = true;
                RequestFocus(FocusTarget.Consent);
                break;
            case StatusRecoveryTarget.Import:
                if (_activeWorkflowStage != WorkspaceStage.Configure)
                    SelectWorkflowStage(WorkspaceStage.Configure);
                _importOpen = true;
                _profileOpen = false;
                RequestFocus(FocusTarget.Import);
                break;
            case StatusRecoveryTarget.Profile:
                _profileOpen = true;
                _importOpen = false;
                RequestFocus(FocusTarget.Profile);
                break;
            case StatusRecoveryTarget.RetryAggregateExport:
                await ExportAllAsync(_lastAggregateExportWasArchive);
                break;
            case StatusRecoveryTarget.Files:
                if (_activeWorkflowStage != WorkspaceStage.Files)
                    SelectWorkflowStage(WorkspaceStage.Files);
                RequestFocus(FocusTarget.Files);
                break;
            case StatusRecoveryTarget.Context:
                if (_activeWorkflowStage != WorkspaceStage.Configure)
                    SelectWorkflowStage(WorkspaceStage.Configure);
                _contextOpen = true;
                RequestFocus(FocusTarget.Context);
                break;
            case StatusRecoveryTarget.Review:
                SelectWorkflowStage(WorkspaceStage.Review);
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
        if (developer)
        {
            EnsureWorkspaceDockPanelsRegistered();
            DockLayoutState.Open(BrowserDockPanels.InspectorId, DockLayoutChangeReason.UserAction);
            DockLayoutState.Open(BrowserDockPanels.DocumentJsonId, DockLayoutChangeReason.UserAction);
            if (_developerMode) return;
            if (!await ResolvePendingAsync()) return;
            RetireDeveloper();
            _developerMode = true;
            _inspectorOpen = true;
            SelectWorkflowStage(WorkspaceStage.Configure);
            return;
        }

        if (!_developerMode && !IsEndDockPanelOpen(BrowserDockPanels.DocumentJsonId))
            return;

        if (!await ResolvePendingAsync()) return;
        RetireDeveloper();
        _developerMode = false;
        if (DockLayoutState.GetPanel(BrowserDockPanels.DocumentJsonId) is not null)
            DockLayoutState.Close(BrowserDockPanels.DocumentJsonId);
        SelectWorkflowStage(WorkspaceStage.Configure);
    }

    private async Task HandleDockTabRequestedAsync(DockPanelId panelId)
    {
        if (_disposed || _form is null) return;
        if (panelId == BrowserDockPanels.DocumentJsonId)
        {
            await RequestEditorModeAsync(true);
            return;
        }

        if (panelId == BrowserDockPanels.InspectorId)
        {
            if (_developerMode)
            {
                if (!await ResolvePendingAsync()) return;
                RetireDeveloper();
                _developerMode = false;
            }

            DockLayoutState.Activate(BrowserDockPanels.InspectorId);
        }
    }

    private async Task CloseEndDockAsync()
    {
        if (_disposed) return;
        if (_developerMode && !await ResolvePendingAsync()) return;
        if (_developerMode)
        {
            RetireDeveloper();
            _developerMode = false;
        }

        if (DockLayoutState.GetPanel(BrowserDockPanels.InspectorId) is not null)
            DockLayoutState.Close(BrowserDockPanels.InspectorId);
        if (DockLayoutState.GetPanel(BrowserDockPanels.DocumentJsonId) is not null)
            DockLayoutState.Close(BrowserDockPanels.DocumentJsonId);
        _inspectorOpen = false;
        _inspectorMobileSheet = false;
        _focusInspectorToggleOnClose = true;
    }

    private async Task RequestWorkflowStageAsync(WorkspaceStage stage)
    {
        if (!await ResolvePendingAsync()) return;
        if (_developerMode && stage == WorkspaceStage.Configure)
        {
            RetireDeveloper();
            _developerMode = false;
            if (DockLayoutState.GetPanel(BrowserDockPanels.DocumentJsonId) is not null)
                DockLayoutState.Close(BrowserDockPanels.DocumentJsonId);
            _configurePanel = null;
        }
        if (_activeWorkflowStage == stage) return;
        SelectWorkflowStage(stage);
    }

    private async Task FocusNextRequiredWorkAsync()
    {
        if (_disposed || _form is null) return;
        DefinitionFormModel form = _form;
        if (!await ResolvePendingAsync() || _disposed || !ReferenceEquals(form, _form)) return;
        _errorSummaryRequested = true;
        if (form.ResolveNextActionableTarget(_selectedSection) is { } target)
        {
            await JumpToActionableTargetAsync(target);
            return;
        }
        SelectWorkflowStage(WorkspaceStage.Review);
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

    private Task SelectSectionAsync(string section)
    {
        _selectedSection = section;
        _focusField = null;
        _form?.RecordSectionOpened(section);
        if (_activeWorkflowStage == WorkspaceStage.Configure && LayoutMode == ConfigureLayoutMode.AllSections)
            return _configurePanel?.JumpToCompactSectionAsync(section) ?? Task.CompletedTask;
        SelectWorkflowStage(WorkspaceStage.Configure);
        return Task.CompletedTask;
    }

    private Task HandleConfigureSectionSelectedAsync(string section)
    {
        if (_renderedConfigureForm is not null && IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
            return SelectSectionAsync(section);
        return Task.CompletedTask;
    }

    private void HandleCompactShowMatchesOnlyChanged(bool value)
    {
        if (_renderedConfigureForm is not null && IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
            _compactShowMatchesOnly = value;
    }

    private void HandleConfigureReviewRequested()
    {
        if (_renderedConfigureForm is not null && IsCurrentView(_renderedConfigureForm, _renderedConfigureView))
            SelectWorkflowStage(WorkspaceStage.Review);
    }

    private void SelectWorkflowStage(WorkspaceStage stage)
    {
        if (_activeWorkflowStage == WorkspaceStage.Configure && _developerMode && stage != WorkspaceStage.Configure)
        {
            RetireDeveloper();
            _developerMode = false;
            if (DockLayoutState.GetPanel(BrowserDockPanels.DocumentJsonId) is not null)
                DockLayoutState.Close(BrowserDockPanels.DocumentJsonId);
        }
        bool leavingFiles = _activeWorkflowStage == WorkspaceStage.Files && stage != WorkspaceStage.Files;
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
        if (stage != WorkspaceStage.Configure)
        {
            _configurePanel = null;
            if (DockLayoutState.ViewportWidth > 0)
                DockLayoutState.ClearMinimumContentWidth(DockScope.Workspace);
        }
        if (stage != WorkspaceStage.Review) _reviewPanel = null;
        if (stage != WorkspaceStage.Files) _filesPanel = null;
        if (stage == WorkspaceStage.Review) _validation = _form!.Validate();
        RequestFocus(stage switch { WorkspaceStage.Review => FocusTarget.Review, WorkspaceStage.Files => FocusTarget.Files, _ => FocusTarget.Configure });
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
            SelectWorkflowStage(WorkspaceStage.Configure);
            RequestFocus(FocusTarget.Output);
            return;
        }
        if (_form!.ConfigurationContext is not null && Definition.Deployment is { } deployment
            && DefinitionPath.FromSegments(new[] { field.Name }.Concat(path.Skip(1)).ToArray()).ToPointer() is var selectorPointer
            && (selectorPointer == deployment.RecipePointer || selectorPointer == deployment.ScopePointer))
        {
            SelectWorkflowStage(WorkspaceStage.Configure);
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
        if (_form.ConfigureLayoutMode == ConfigureLayoutMode.AllSections)
        {
            _compactShowMatchesOnly = false;
            if (_activeWorkflowStage != WorkspaceStage.Configure)
                SelectWorkflowStage(WorkspaceStage.Configure);
            _selectedSection = EditorSections().FirstOrDefault(section => section.Fields.Contains(fieldId))?.Id ?? _selectedSection;
        }
        else
        {
            SelectWorkflowStage(WorkspaceStage.Configure);
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
        if (_activeWorkflowStage != WorkspaceStage.Configure)
            SelectWorkflowStage(WorkspaceStage.Configure);
        _compactShowMatchesOnly = false;
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
        SelectWorkflowStage(WorkspaceStage.Review);
    }
    private void ReviewImportConsent()
    {
        SelectWorkflowStage(WorkspaceStage.Configure);
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
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync, () => (form.Locale, form.Direction), () => HasWork(form),
            locale => form.SelectLocale(locale), () => form.Definition.Locales) ?? 0;
    }
    private async Task EnableSecretsAsync()
    {
        DefinitionFormModel form = _form!;
        await ConsentAsync(new ChangeEventArgs { Value = true });
        if (!_disposed && ReferenceEquals(form, _form) && form.SessionPrivacy == DefinitionSessionPrivacy.BrowserSecretsEnabled)
        {
            SelectWorkflowStage(WorkspaceStage.Configure);
            RequestFocus(FocusTarget.Consent);
        }
    }

    private void InputChanged(object? sender, EventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _form)) return;
        RetireView();
        bool keepSettledCompactMounts = _activeWorkflowStage == WorkspaceStage.Configure &&
            LayoutMode == ConfigureLayoutMode.AllSections && !_developerMode && _configurePanel?.IsCompactSettled == true;
        if (!keepSettledCompactMounts)
            InvalidateMounts();
        _focusField = null;
        Invalidate();
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
            if (_form.ConfigureLayoutMode == ConfigureLayoutMode.AllSections && _compactShowMatchesOnly)
                StateHasChanged();
            return;
        }
        bool layoutChanged = _observedLayoutMode != _form.ConfigureLayoutMode;
        _observedLayoutMode = _form.ConfigureLayoutMode;
        if (layoutChanged && _form.ConfigureLayoutMode != ConfigureLayoutMode.AllSections)
            _compactShowMatchesOnly = false;
        RetireView();
        if (layoutChanged || _form.ConfigureLayoutMode != ConfigureLayoutMode.AllSections)
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
        if (target is FocusTarget.Context or FocusTarget.Output)
            _advancedOptionsOpen = true;
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
        if (_activeWorkflowStage != WorkspaceStage.Configure)
        {
            SelectWorkflowStage(WorkspaceStage.Configure);
            _contextOpen = true;
            RequestFocus(FocusTarget.Context);
            return;
        }
        _contextOpen = !_contextOpen;
        if (_contextOpen) RequestFocus(FocusTarget.Context);
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
    private async Task SelectLayoutModeAsync(ConfigureLayoutMode layout)
    {
        if (_disposed || _form is null) return;
        DefinitionFormModel form = _form;
        if (!await ResolvePendingAsync() || _disposed || !ReferenceEquals(form, _form)) return;
        if (_developerMode)
        {
            RetireDeveloper();
            _developerMode = false;
            _configurePanel = null;
        }
        if (layout == ConfigureLayoutMode.AllSections)
        {
            _form?.SetConfigureLayoutMode(ConfigureLayoutMode.AllSections, DefinitionEditorVisibilityPolicy.Continuous);
        }
        else
        {
            string? activeSection = LayoutMode == ConfigureLayoutMode.AllSections
                ? _configurePanel?.ActiveScrollspySectionId
                : null;
            if (!string.IsNullOrWhiteSpace(activeSection))
            {
                _selectedSection = activeSection;
                _form?.RecordSectionOpened(activeSection);
            }

            _compactShowMatchesOnly = false;
            _form?.SetConfigureLayoutMode(ConfigureLayoutMode.SectionBySection);
        }
        if (_activeWorkflowStage != WorkspaceStage.Configure)
            SelectWorkflowStage(WorkspaceStage.Configure);
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
                return ToggleInspectorAsync();
            if (args.Key == "F7")
                return JumpToNextActionableAsync(errorsOnly: true);
            if (args.Key == "F8")
                return JumpToNextActionableAsync(errorsOnly: false);
            if (args.Key == "F9")
                return JumpToNextUnvisitedAsync();
            if (args.Key == "/")
            {
                _search?.RequestOpen(this, _searchLease);
                return Task.CompletedTask;
            }
            if (args.Key == "?")
                return OpenShortcutsHelpAsync();
        }
        return Task.CompletedTask;
    }

    private Task JumpToNextActionableAsync(bool errorsOnly)
    {
        if (_disposed || _form is null) return Task.CompletedTask;
        WorkspaceActionableTarget? target = null;
        if (errorsOnly)
        {
            target = _form.ResolveNextActionableTarget(_selectedSection, errorsOnly: true)
                ?? _form.ResolveNextActionableTarget(_selectedSection, errorsOnly: false);
        }
        else
        {
            DefinitionValidationResult validation = _validation ?? _form.Validate();
            foreach (DefinitionDiagnostic diagnostic in validation.Diagnostics)
            {
                if (diagnostic.Code == "value-required" &&
                    _form.ResolveActionableTarget(diagnostic) is { CanEdit: true } candidate)
                {
                    target = candidate;
                    break;
                }
            }
            target ??= _form.ResolveNextActionableTarget(_selectedSection, errorsOnly: false);
        }
        if (target is not null)
            return JumpToActionableTargetAsync(target);
        return Task.CompletedTask;
    }

    private Task JumpToNextUnvisitedAsync()
    {
        if (_disposed || _form is null) return Task.CompletedTask;
        if (_form.Projection.Navigation.NextUnvisitedSectionId is { } sectionId)
        {
            _compactShowMatchesOnly = false;
            return SelectSectionAsync(sectionId);
        }
        return Task.CompletedTask;
    }

    private Task JumpToActionableTargetAsync(WorkspaceActionableTarget target)
    {
        if (_disposed || _form is null) return Task.CompletedTask;
        CorrectField(target.Diagnostic);
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

    private async Task ToggleInspectorAsync()
    {
        if (_disposed) return;
        if (IsAnyEndDockOpen || _inspectorOpen)
        {
            await CloseEndDockAsync();
            return;
        }
        bool isNarrow = false;
        try
        {
            _comparisonRendering ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js").AsTask();
            IJSObjectReference module = await _comparisonRendering;
            if (_disposed) return;
            isNarrow = await module.InvokeAsync<bool>("isNarrowViewport");
        }
        catch (JSException)
        {
            isNarrow = false;
        }
        if (_disposed) return;
        _inspectorMobileSheet = isNarrow;
        if (isNarrow && DockLayoutState.ViewportWidth == 0)
            DockLayoutState.UpdateViewport(600, isMobile: true, DockScope.Workspace, DockLayoutState.DefaultMinimumContentWidth);
        EnsureWorkspaceDockPanelsRegistered();
        DockLayoutState.Open(BrowserDockPanels.DocumentJsonId, DockLayoutChangeReason.UserAction);
        DockLayoutState.Open(BrowserDockPanels.InspectorId, DockLayoutChangeReason.UserAction);
        _inspectorOpen = true;
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
            case WorkspaceCommandId.SwitchToSectionBySection:
                await SelectLayoutModeAsync(ConfigureLayoutMode.SectionBySection);
                break;
            case WorkspaceCommandId.SwitchToAllSections:
                await SelectLayoutModeAsync(ConfigureLayoutMode.AllSections);
                break;
            case WorkspaceCommandId.SwitchToReview:
                await RequestWorkflowStageAsync(WorkspaceStage.Review);
                break;
            case WorkspaceCommandId.SwitchToFiles:
                await RequestWorkflowStageAsync(WorkspaceStage.Files);
                break;
            case WorkspaceCommandId.ToggleNamingMode:
                await ChangeNamingModeAsync(NamingMode == WorkspaceNamingMode.Technical
                    ? WorkspaceNamingMode.Humanized : WorkspaceNamingMode.Technical);
                break;
            case WorkspaceCommandId.ToggleInspector:
                await ToggleInspectorAsync();
                break;
            case WorkspaceCommandId.OpenShortcutsHelp:
                await OpenShortcutsHelpAsync();
                break;
            case WorkspaceCommandId.JumpToNextError:
                await JumpToNextActionableAsync(errorsOnly: true);
                break;
            case WorkspaceCommandId.JumpToNextRequired:
                await JumpToNextActionableAsync(errorsOnly: false);
                break;
            case WorkspaceCommandId.JumpToNextUnvisited:
                await JumpToNextUnvisitedAsync();
                break;
        }
    }

    private void ChangeExperience(ChangeEventArgs args)
    {
        if (args.Value is not string id || !_form!.SelectExperience(id)) return;
        _selectedSection = null;
        SelectWorkflowStage(WorkspaceStage.Configure);
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
            try
            {
                _comparisonRendering ??= Javascript.InvokeAsync<IJSObjectReference>("import", "./js/configuration-rendering.js").AsTask();
                IJSObjectReference module = await _comparisonRendering;
                if (!IsCurrentStartup(startup)) return;
                await module.InvokeVoidAsync("paintWorkspace", startup.Cancellation, _startupControl,
                    startup.View.ToString(CultureInfo.InvariantCulture));
            }
            catch (OperationCanceledException) when (!IsCurrentStartup(startup))
            {
                if (_comparisonRendering?.IsCanceled == true) _comparisonRendering = null;
                return;
            }
            catch (JSException) when (!IsCurrentStartup(startup))
            {
                if (_comparisonRendering?.IsFaulted == true) _comparisonRendering = null;
                return;
            }
            catch (JSException)
            {
                if (_comparisonRendering?.IsFaulted == true) _comparisonRendering = null;
                InitializeForm(startup);
                _preparing = false;
                _preparationFailed = true;
                SetStatus("AppPreparationIncompleteStatus");
                _statusCode = "app-preparation-incomplete";
                StateHasChanged();
                return;
            }
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
        if (_focusInspectorToggleOnClose && _ready && !_inspectorOpen)
        {
            _focusInspectorToggleOnClose = false;
            if (_advancedOptionsOpen && !string.IsNullOrEmpty(_inspectorToggleControl.Id))
                await _inspectorToggleControl.FocusAsync(preventScroll: true);
            else if (_workspaceHeader is not null)
                await _workspaceHeader.FocusAdvancedToggleAsync();
        }
        if (!IsCurrentView(form, generation) || _pendingFocus is not { } request) return;
        _pendingFocus = null;
        if (!IsCurrentView(request.Form, request.Generation)) return;
        ElementReference? target = request.Target switch
        {
            FocusTarget.Configure when _ready && _activeWorkflowStage == WorkspaceStage.Configure => _developerMode
                ? (_developerPanel?.Heading ?? _configurePanel?.SectionHeading)
                : _configurePanel?.SectionHeading,
            FocusTarget.Review when _ready && _activeWorkflowStage == WorkspaceStage.Review => _reviewPanel?.Heading,
            FocusTarget.Files when _ready && IsFilesSurfaceActive => _filesPanel?.Heading,
            FocusTarget.TransferReview when _ready && _transferReview is not null => _nextActionsPanel?.ReviewHeading,
            FocusTarget.Consent when _ready => _consentControl,
            FocusTarget.Import when _ready && _activeWorkflowStage == WorkspaceStage.Configure => _fileInput?.Element,
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
        _navigationLease = _navigation?.Register(this, Invalidate, DiscardAsync, () => (form.Locale, form.Direction), () => HasWork(form),
            locale => form.SelectLocale(locale), () => form.Definition.Locales) ?? 0;
        long generation = _generation;
        long view = _viewGeneration;
        CancellationToken cancellation = _mountCancellation.Token;
        await Offline.InitializeAsync();
        if (!IsCurrentView(form, view) || generation != _generation || cancellation.IsCancellationRequested) return;
        EnsureWorkspaceDockPanelsRegistered();
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
        UnregisterWorkspaceDockPanels();
        DockLayoutState.ClearMinimumContentWidth(DockScope.Workspace);
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
        _advancedOptionsOpen = false;
        _consentOpen = false;
        _taskToolsOpen = false;
        _importOpen = false;
        _profileOpen = false;
        _activeWorkflowStage = WorkspaceStage.Configure;
        _compactShowMatchesOnly = false;
        _hadPreparedOutput = false;
        _observedLayoutMode = ConfigureLayoutMode.SectionBySection;
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
        DockLayoutState.Changed -= OnDockChanged;
        if (_navigation is not null)
            _navigation.Changed -= OnNavigationStateChanged;
        UnregisterWorkspaceDockPanels();
        DockLayoutState.ClearMinimumContentWidth(DockScope.Workspace);
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
