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
using Microsoft.JSInterop;

public partial class ConfigurationEditor : IBrowserPendingChanges
{
    [Inject] private IJSRuntime Javascript { get; set; } = null!;
    [Inject] private MudBlazor.IDialogService Dialogs { get; set; } = null!;
    private Task<IJSObjectReference>? _comparisonRendering;
    [Inject] private BrowserOfflinePolicy Offline { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Inject] private AppearanceThemeService Appearance { get; set; } = null!;
    [Parameter, EditorRequired] public Definition Definition { get; set; } = null!;
    [Parameter] public string? InitialLocale { get; set; }
    [Parameter] public string? InitialEntry { get; set; }
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
    private enum FocusTarget { Configure, Review, Files, Consent, Import, Start, Output, Context, DeveloperDestination }
    private int _requiredCount;
    private ElementReference _outputControl;
    private ConfigurationConfigurePanel? _configurePanel;
    private ConfigurationReviewPanel? _reviewPanel;
    private ConfigurationFilesPanel? _filesPanel;
    private ConfigurationDeveloper? _developerPanel;
    private bool _developerMode;
    private bool _consentOpen;
    private bool _taskToolsOpen;
    private EventCallback _consentAction;
    private long _developerEpoch;
    private long _outputChoiceKey;
    private string _statusKey = "WorkspaceReadyStatus";
    private object?[] _statusArguments = [];
    private string _statusCode = "workspace-ready";
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
    private string _workspaceLifetime = Guid.NewGuid().ToString("N");
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
    private bool HasWork(DefinitionFormModel form) => !_disposed && _ready && ReferenceEquals(form, _form) &&
        (form.HasWorkspaceChanges || _prepared.Count > 0 || _importPending || form.ImportComparison is not null
            || _developerPanel is { } developer && ReferenceEquals(developer.Form, form) && developer.HasWork);
    private string PreparationReadiness
    {
        get
        {
            DefinitionFormProgress progress = _form!.Projection.Progress;
            return _form.ConfigurationContext is { IsReady: false } || _form.HasInputErrors || _form.HasAnswerBudgetError || progress.Invalid > 0 || progress.Missing > progress.Deferred
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
        if (_disposed || ReferenceEquals(_pinned, Definition)) return;
        RetireDeveloper();
        _developerMode = false;
        _developerArtifactId = null;
        RetireView();
        _configurePanel = null;
        _reviewPanel = null;
        _filesPanel = null;
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
        _compact = false;
        _consentOpen = false;
        _taskToolsOpen = false;
        _importPurpose = "compare";
        _ready = false;
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
        if (startup.Definition.Presentation is not null) _form.SetDisclosureOverride(FieldDisclosure.Essential);
        _form.SelectLocale(startup.Locale);
        if (startup.Entry is not null) _form.SelectOutput(startup.Entry);
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
        if (_activeWorkflowStage != WorkflowStage.Configure)
        {
            SelectWorkflowStage(WorkflowStage.Configure);
            RequestFocus(FocusTarget.Output);
        }
    }

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
        if (_activeWorkflowStage == stage || !await ResolvePendingAsync()) return;
        SelectWorkflowStage(stage);
    }

    private async Task RequestOutputChangeAsync(ChangeEventArgs args)
    {
        DefinitionFormModel form = _form!;
        long view = _viewGeneration;
        if (!await ResolvePendingAsync())
        {
            if (ReferenceEquals(form, _form))
            {
                ++_outputChoiceKey;
                if (view == _viewGeneration) RequestFocus(FocusTarget.Output);
            }
            return;
        }
        RetireDeveloper();
        ChangeOutput(args);
    }
    private ArtifactDefinition[] ImportArtifacts { get; set; } = [];
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
        SelectWorkflowStage(WorkflowStage.Configure);
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
        if (_form!.ConfigurationContext is not null && Definition.Deployment is { } deployment && path.Count == 1
            && (DefinitionPath.FromSegments([field.Name]).ToPointer() == deployment.RecipePointer
                || DefinitionPath.FromSegments([field.Name]).ToPointer() == deployment.ScopePointer))
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
        SelectWorkflowStage(WorkflowStage.Configure);
        _selectedSection = EditorSections().Single(section => section.Fields.Contains(fieldId)).Id;
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
        RefreshConsentAction();
    }
    private void RefreshConsentAction()
    {
        DefinitionFormModel? form = _form;
        long view = _viewGeneration;
        _consentAction = EventCallback.Factory.Create(this, () =>
        {
            if (form is not null && IsCurrentView(form, view)) RequestFocus(FocusTarget.Consent);
        });
    }
    private void RequestFocus(FocusTarget target)
    {
        if (target == FocusTarget.Consent) _consentOpen = true;
        if (target == FocusTarget.Output) _taskToolsOpen = true;
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
    private void ChangeDensity(ChangeEventArgs args)
    {
        if (args.Value is not ("compact" or "comfortable")) return;
        _compact = args.Value is "compact";
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
            await module.InvokeVoidAsync("paintWorkspace", _startupControl, startup.View.ToString(CultureInfo.InvariantCulture));
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
            FocusTarget.Review when _ready && _activeWorkflowStage == WorkflowStage.Review => _reviewPanel?.Heading,
            FocusTarget.Files when _ready && _activeWorkflowStage == WorkflowStage.Prepare => _filesPanel?.Heading,
            FocusTarget.Consent when _ready && _activeWorkflowStage == WorkflowStage.Configure => _consentControl,
            FocusTarget.Import when _ready && _activeWorkflowStage == WorkflowStage.Configure => _fileInput?.Element,
            FocusTarget.Output when _ready && OutputSelector is not null => _outputControl,
            FocusTarget.Context when _ready && _form!.ConfigurationContext is not null => _contextControl,
            FocusTarget.DeveloperDestination when _ready && _developerMode && DeveloperArtifacts.Length > 1 => _developerDestinationControl,
            FocusTarget.Start when !_ready => _startButton,
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
        StateHasChanged();
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
        _search?.Release(this, _searchLease);
        if (form is not null) form.ImportComparisonChanged -= ImportComparisonChanged;
        form?.Dispose();
        _form = null;
        Task outputs = InvalidateOutputAsync();
        _compact = false;
        _consentOpen = false;
        _taskToolsOpen = false;
        _activeWorkflowStage = WorkflowStage.Configure;
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
