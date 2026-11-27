namespace Configurator.Browser.Components.Configuration;

using Configurator.Browser.Resources;
using Configurator.Browser.Services;
using Configurator.Browser.Services.Docking;
using Configurator.Presentation;

public partial class ConfigurationEditor
{
    private const string UtilityFocusScope = "workspace-utility";
    private const int DesktopUtilityRailWidthPx = 44;

    private WorkspaceUtilityDockCoordinator? _utilityDock;
    private ConfigurationImportDockPanel? _importDockPanel;
    private ConfigurationTrustPrivacyPanel? _trustPrivacyPanel;
    private bool _utilityFocusSaved;
    private bool _onboardingDismissed;
    private string? _registeredUtilityLocale;
    private bool? _registeredImportAvailable;
    private bool _utilityDockRenderPending;

    private WorkspaceUtilityKind? ActiveUtility => _utilityDock?.ActiveUtility;
    private bool IsDocumentUtilityOpen => ActiveUtility == WorkspaceUtilityKind.DocumentJson;
    private bool IsInspectorUtilityOpen => ActiveUtility == WorkspaceUtilityKind.Inspector;
    private bool IsAnyUtilityOpen => _utilityDock?.IsAnyOpen == true;

    private bool HasDesktopUtilityRail =>
        !_disposed
        && _ready
        && _form is not null
        && _navigation?.IsSettingsOpen != true
        && (DockLayoutState.ViewportWidth == 0 || DockLayoutState.ViewportWidth > 960);

    private bool ShowOnboardingCue =>
        _ready
        && _form is not null
        && _activeWorkflowStage == WorkspaceStage.Configure
        && _form.InputRevision == 0
        && _form.BrowserProfile is null
        && _form.ImportComparison is null
        && !_importPending
        && !_onboardingDismissed;

    private string? CurrentSectionTitle
    {
        get
        {
            if (_form is null) return null;
            IReadOnlyList<DefinitionFormSection> sections = EditorSections();
            string? selectedId = SelectedSection(sections);
            return selectedId is not null && _editorSections.TryGetValue(selectedId, out DefinitionFormSection? section)
                ? section.Title
                : null;
        }
    }

    private string? CurrentSectionDescription
    {
        get
        {
            if (_form is null) return null;
            IReadOnlyList<DefinitionFormSection> sections = EditorSections();
            string? selectedId = SelectedSection(sections);
            return selectedId is not null && _editorSections.TryGetValue(selectedId, out DefinitionFormSection? section)
                ? section.Description
                : null;
        }
    }

    private void InitializeUtilityDockCoordinator()
    {
        DisposeUtilityDockCoordinator();
        _utilityDock = new WorkspaceUtilityDockCoordinator(
            DockLayoutState,
            ResolvePendingAsync,
            RetireDeveloper,
            _ => EnsureWorkspaceUtilityPanelsRegistered());
        _utilityDock.Changed += OnUtilityDockChanged;
    }

    private void DisposeUtilityDockCoordinator()
    {
        if (_utilityDock is null) return;
        _utilityDock.Changed -= OnUtilityDockChanged;
        _utilityDock.Dispose();
        _utilityDock = null;
    }

    private void OnUtilityDockChanged()
    {
        if (_disposed) return;
        if (!IsAnyUtilityOpen)
            _utilityFocusSaved = false;
        if (_utilityDockRenderPending) return;
        _utilityDockRenderPending = true;
        _ = InvokeAsync(() =>
        {
            _utilityDockRenderPending = false;
            if (!_disposed) StateHasChanged();
        });
    }

    private void EnsureWorkspaceUtilityPanelsRegistered()
    {
        if (_form is null) return;
        string locale = _form.Locale;
        bool importAvailable = ImportArtifacts.Length > 0;
        if (string.Equals(_registeredUtilityLocale, locale, StringComparison.Ordinal)
            && _registeredImportAvailable == importAvailable)
        {
            return;
        }

        _registeredUtilityLocale = locale;
        _registeredImportAvailable = importAvailable;

        DockLayoutState.RegisterOrUpdate(
            BrowserDockPanels.CreateWorkspaceUtility(WorkspaceUtilityKind.Inspector, locale),
            RenderInspectorDockPanel());
        DockLayoutState.RegisterOrUpdate(
            BrowserDockPanels.CreateWorkspaceUtility(WorkspaceUtilityKind.DocumentJson, locale),
            RenderDocumentJsonDockPanel());
        if (importAvailable)
        {
            DockLayoutState.RegisterOrUpdate(
                BrowserDockPanels.CreateWorkspaceUtility(WorkspaceUtilityKind.Import, locale),
                RenderImportDockPanel());
        }
        else
        {
            DockLayoutState.Unregister(BrowserDockPanels.ImportId);
        }

        DockLayoutState.RegisterOrUpdate(
            BrowserDockPanels.CreateWorkspaceUtility(WorkspaceUtilityKind.Profiles, locale),
            RenderProfilesDockPanel());
        DockLayoutState.RegisterOrUpdate(
            BrowserDockPanels.CreateWorkspaceUtility(WorkspaceUtilityKind.TrustPrivacy, locale),
            RenderTrustPrivacyDockPanel());
    }

    private void UnregisterWorkspaceUtilityPanels()
    {
        foreach (WorkspaceUtilityDescriptor descriptor in WorkspaceUtilities.All)
            DockLayoutState.Unregister(descriptor.PanelId);
        _registeredUtilityLocale = null;
        _registeredImportAvailable = null;
        _utilityFocusSaved = false;
        _importDockPanel = null;
        _trustPrivacyPanel = null;
    }

    private async Task<bool> OpenWorkspaceUtilityAsync(
        WorkspaceUtilityKind kind,
        FocusTarget? focusTarget = null,
        bool saveFocus = true)
    {
        if (_disposed || !_ready || _form is null || _utilityDock is null) return false;
        if (kind == WorkspaceUtilityKind.Import && ImportArtifacts.Length == 0) return false;

        DefinitionFormModel form = _form;
        if (saveFocus && !_utilityFocusSaved && !IsAnyUtilityOpen)
        {
            _utilityFocusSaved = true;
            await AccessibilityFocus.SaveFocusAsync(UtilityFocusScope);
            if (_disposed || !_ready || !ReferenceEquals(form, _form))
            {
                _utilityFocusSaved = false;
                return false;
            }
        }

        bool opened = await _utilityDock.OpenAsync(kind, DockLayoutChangeReason.UserAction);
        if (!opened || _disposed || !_ready || !ReferenceEquals(form, _form))
        {
            if (!IsAnyUtilityOpen)
                _utilityFocusSaved = false;
            return false;
        }

        FocusTarget? target = focusTarget ?? kind switch
        {
            WorkspaceUtilityKind.TrustPrivacy => FocusTarget.Consent,
            WorkspaceUtilityKind.Import => FocusTarget.Import,
            WorkspaceUtilityKind.Profiles => FocusTarget.Profile,
            WorkspaceUtilityKind.DocumentJson => FocusTarget.Document,
            _ => null
        };
        if (target is { } resolvedTarget)
            RequestFocus(resolvedTarget);

        StateHasChanged();
        return true;
    }

    private async Task ToggleWorkspaceUtilityAsync(WorkspaceUtilityKind kind)
    {
        if (_disposed || !_ready || _form is null || _utilityDock is null) return;
        if (_utilityDock.IsOpen(kind))
        {
            await CloseWorkspaceUtilityAsync(restoreFocus: true);
            return;
        }

        await OpenWorkspaceUtilityAsync(kind);
    }

    private async Task<bool> CloseWorkspaceUtilityAsync(bool restoreFocus = true)
    {
        if (_disposed || _utilityDock is null) return false;

        WorkspaceUtilityKind? closing = ActiveUtility;
        bool closed = await _utilityDock.CloseAsync(DockLayoutChangeReason.UserAction);
        if (!closed || _disposed) return false;

        if (restoreFocus)
        {
            _utilityFocusSaved = false;
            string fallbackSelector = closing is { } kind
                ? $"[data-utility-rail-action='{WorkspaceUtilities.Get(kind).ActionToken}']"
                : "header.workspace-header [data-action='toggle-advanced-options']";
            await AccessibilityFocus.RestoreFocusAsync(fallbackSelector, preventScroll: true, scope: UtilityFocusScope);
        }

        return true;
    }

    private Task HandleDockTabRequestedAsync(DockPanelId panelId)
    {
        if (_disposed || _form is null) return Task.CompletedTask;
        return WorkspaceUtilities.TryGetByPanelId(panelId, out WorkspaceUtilityDescriptor? descriptor) && descriptor is not null
            ? OpenWorkspaceUtilityAsync(descriptor.Kind)
            : Task.CompletedTask;
    }

    private void RetireAndCloseDocumentUtility()
    {
        RetireDeveloper();
        if (DockLayoutState.GetPanel(BrowserDockPanels.DocumentJsonId) is { State.IsOpen: true })
            DockLayoutState.Close(BrowserDockPanels.DocumentJsonId, DockLayoutChangeReason.UserAction);
    }

    private void DismissOnboardingCue() => _onboardingDismissed = true;

    private Task<bool> OpenOnboardingImportAsync()
    {
        _onboardingDismissed = true;
        return OpenWorkspaceUtilityAsync(WorkspaceUtilityKind.Import);
    }

    private Task<bool> OpenOnboardingProfileAsync()
    {
        _onboardingDismissed = true;
        return OpenWorkspaceUtilityAsync(WorkspaceUtilityKind.Profiles);
    }

    private List<BrowserWorkspaceCommand> BuildBrowserWorkspaceCommands(DefinitionFormModel form, long lease)
    {
        var commands = new List<BrowserWorkspaceCommand>(3);
        string locale = form.Locale;

        if (ImportArtifacts.Length > 0)
        {
            commands.Add(new BrowserWorkspaceCommand(
                "browser.open-import",
                BrowserStrings.Get("CommandOpenImportTitle", locale),
                null,
                IsEnabled: true,
                () => ExecuteBrowserWorkspaceCommandAsync(form, lease, WorkspaceUtilityKind.Import)));
        }

        commands.Add(new BrowserWorkspaceCommand(
            "browser.open-profiles",
            BrowserStrings.Get("CommandOpenProfilesTitle", locale),
            null,
            IsEnabled: true,
            () => ExecuteBrowserWorkspaceCommandAsync(form, lease, WorkspaceUtilityKind.Profiles)));

        commands.Add(new BrowserWorkspaceCommand(
            "browser.open-trust-privacy",
            BrowserStrings.Get("CommandOpenTrustPrivacyTitle", locale),
            null,
            IsEnabled: true,
            () => ExecuteBrowserWorkspaceCommandAsync(form, lease, WorkspaceUtilityKind.TrustPrivacy)));

        return commands;
    }

    private async Task ExecuteBrowserWorkspaceCommandAsync(DefinitionFormModel form, long lease, WorkspaceUtilityKind kind)
    {
        if (_disposed || !_ready || !ReferenceEquals(form, _form) || _search is null || lease != _search.Generation)
            return;
        _search.CompleteNavigation(this, lease);
        await OpenWorkspaceUtilityAsync(kind);
    }
}
