namespace Configurator.Terminal;

using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

internal enum TerminalWorkbenchStage
{
    Configure,
    ReviewAndImport,
    FilesAndSave
}

internal sealed partial class TerminalWindow : Window
{
    internal const int MinimumTerminalWidth = 76;
    internal const int MinimumTerminalHeight = 20;
    internal const int SidebarBreakpointWidth = 88;

    private readonly IApplication _application;
    private readonly PresentationWorkspace _workspace;
    private readonly TerminalArtifactOperation _operation;
    private readonly TerminalSecretBuffer _secret;
    private readonly TerminalFieldEditor _editor;
    private readonly TerminalImportOperation _importOperation;
    private readonly Action<Action> _dispatch;
    private readonly TimeProvider _timeProvider;
    private readonly WeakReference<TerminalWindow> _owner;

    // Top chrome & utility bar
    private readonly Button _stageConfigure;
    private readonly Button _stageReview;
    private readonly Button _stageFiles;
    private readonly Button _profilesButton;
    private readonly Button _closeButton;
    private readonly Button _sectionsButton;
    private readonly Button _namingButton;
    private readonly Button _findButton;
    private readonly Button _documentButton;
    private readonly Button _undoButton;
    private readonly Button _outputTasks;
    private readonly Button _reuseValue;
    private readonly Label _nextActions;
    private readonly TerminalSectionNavigator _sectionNavigator;

    // Stage 1: Configure (Master-Detail)
    private readonly Button _experience;
    private readonly Button _optionalEditors;
    private readonly Button _locale;
    private readonly Button _correction;
    private readonly Label _fieldLabel;
    private readonly View _helpPane;
    private readonly Label _help;
    private readonly TextField _value;
    private readonly SecretTextField _masked;
    private readonly Button _previousField;
    private readonly Button _nextField;
    private readonly Button _applyField;
    private readonly Button _choice;
    private readonly Button _choiceList;
    private readonly Button _addItem;
    private readonly Button _supplyContainer;
    private readonly Button _clearContainer;
    private readonly Button _null;
    private readonly Button _reset;
    private readonly Button _suggest;
    private readonly Button _generateSecret;
    private readonly Button _secretOptions;
    private readonly Button _revealSecret;
    private readonly Button _copySecret;
    private readonly Button[] _itemActions;
    private readonly Button[] _listButtons;

    // Stage 2: Review & Import
    private readonly Label _reviewSummaryLabel;
    private readonly Button _reviewJumpButton;
    private readonly Button _reviewFindButton;
    private readonly Label _artifactLabel;
    private readonly Button _artifactButton;
    private readonly Button _selectOutputButton;
    private readonly Label _importFileLabel;
    private readonly TextField _importPath;
    private readonly Button _import;
    private readonly Button _fillImport;
    private readonly Button _nextFinding;
    private readonly Label _comparisonLabel;

    // Stage 3: Files & Save
    private readonly Label _filesSummaryLabel;
    private readonly Button _validateButton;
    private readonly Button _prepare;
    private readonly Button _save;
    private readonly Button _clearPrivateButton;
    private readonly Button _nextOutcome;
    private readonly Button _nextAttempt;
    private readonly Label _receiptLabel;

    // Footer & small-viewport gate
    private readonly TextField _status;
    private readonly StatusBar _statusBar;
    private readonly Shortcut _sectionsShortcut;
    private readonly Shortcut _findShortcut;
    private readonly Shortcut _documentShortcut;
    private readonly Shortcut _keysShortcut;
    private readonly Shortcut _closeShortcut;
    private readonly (Shortcut Shortcut, string Key)[] _statusBarShortcuts;
    private readonly Label _smallNotice;

    private TerminalWorkbenchStage _stage = TerminalWorkbenchStage.Configure;
    private bool _sidebarVisible;
    private Size _lastViewportSize = new(100, 30);
    private TerminalFieldEditor.Slot? _slot;
    private int _fieldIndex;
    private int _itemIndex;
    private int _outcomeIndex;
    private int _correctionIndex = -1;
    private int _comparisonIndex = -1;
    private ProtectedArtifactPublicationReceipt? _receipt;
    private int _publicationRefreshQueued;
    private bool _loading;
    private bool _draftEdited;
    private bool _draftValid = true;
    private bool _disposed;
    private bool _tooSmall;
    private bool _confirmRemove;
    private bool _layingOut;
    private int _closeRequested;
    private int _callbackGeneration;
    private bool _observersDetached;
    private bool _allowStop;
    private Task _shutdown = Task.CompletedTask;
    private Task _saveObservation = Task.CompletedTask;
    private Task _preparationCompletion = Task.CompletedTask;
    private readonly CancellationTokenSource _preparationCancellation = new();
    private bool _preparing;
    private readonly Dictionary<View, string> _localizedControls = [];
    private string? _renderedLocale;
    private string _statusKey = "Ready";
    private DefinitionDiagnostic[] _statusDiagnostics = [];

#if CONFIGURATOR_NATIVE_IMPORT_PROBE
    internal View ProbeStatusView => _status;
    internal int ProbeOwnerGeneration => _callbackGeneration;
    internal event Action<int, int, bool>? ProbeStatusMutation;
    private int _probeStatusMutation;

    private void ObserveProbeStatusMutation(bool progress) =>
        ProbeStatusMutation?.Invoke(++_probeStatusMutation, _callbackGeneration, progress);
#endif

    internal TerminalWindow(IApplication application, PresentationWorkspace workspace,
        TerminalArtifactOperation operation, TerminalSecretBuffer secret,
        Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>>? readImport = null,
        Action<Action>? dispatch = null, TimeProvider? timeProvider = null,
        Func<CancellationToken, ValueTask>? cooperativeYield = null)
    {
        _application = application;
        _workspace = workspace;
        _operation = operation;
        _secret = secret;
        _dispatch = dispatch ?? application.Invoke;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _owner = new(this);
        _importOperation = new(operation.Form, _dispatch, readImport,
            cooperativeYield: cooperativeYield ?? YieldToApplicationAsync);
        _importOperation.Settled += ImportSettled;
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        if (Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_MODE") == "preparation"
            || Environment.GetEnvironmentVariable("CONFIGURATOR_NATIVE_PROBE_INPUT") is { Length: > 0 })
            _application.Invoke(() => Configurator.Engineering.MeasureNativeImportComparison.Attach(this));
#endif
        _editor = new(operation.Form);
        Title = LocalizedText("WindowTitle");
        Width = Dim.Fill();
        Height = Dim.Fill();
        SetContentHeight(25);
        ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        FocusedChanged += (_, _) => RevealFocusedControl();

        // Top Stage & Utility Bars
        _stageConfigure = Button("StageConfigure", 0, 0, () => SetStage(TerminalWorkbenchStage.Configure));
        _stageReview = Button("StageReview", 16, 0, () => SetStage(TerminalWorkbenchStage.ReviewAndImport));
        _stageFiles = Button("StageFiles", 38, 0, () => SetStage(TerminalWorkbenchStage.FilesAndSave));
        _profilesButton = Button("ProfilesTask", 66, 0, OpenProfiles);
        _closeButton = Button("Close", 57, 0, CloseOrCancel);

        _sectionsButton = Button("Sections", 0, 1, OpenSectionsModal);
        _namingButton = Button("NamingTechnical", 13, 1, ToggleNamingMode);
        _findButton = Button("FindFields", 23, 1, OpenDiscoveryDialog);
        _documentButton = Button("DocumentEditor", 33, 1, OpenDocumentDialog);
        _undoButton = Button("UndoAction", 43, 1, () => UndoLastEdit());
        _undoButton.Enabled = _operation.Form.CanUndo;
        _outputTasks = Button("OutputTasks", 0, 1, OpenOutputTasks);

        _localizedControls.Remove(_namingButton);

        _sectionNavigator = new TerminalSectionNavigator(operation.Form, JumpToSection, HandleWorkbenchKey);

        // Stage 1: Configure (Master-Detail)
        View[] configurationChoices = CreateConfigurationChoices();
        _experience = Button("Choose", 0, 2, CycleExperience);
        _experience.HotKeySpecifier = (Rune)0xffff;
        _optionalEditors = Button("Choose", 0, 2, ToggleOptionalEditors);
        _locale = Button("Locale", 0, 2, CycleLocale);
        _correction = Button("NextCorrection", 0, 2, FocusNextCorrection);

        _fieldLabel = new Label { X = 0, Y = 4, Width = Dim.Fill(), Height = 1, HotKeySpecifier = (Rune)0xffff };
        _helpPane = new View
        {
            X = 0,
            Y = 5,
            Width = Dim.Fill(),
            Height = 3,
            CanFocus = true
        };
        _helpPane.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        _help = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Auto(DimAutoStyle.Text, 1), HotKeySpecifier = (Rune)0xffff };
        _helpPane.Add(_help);
        _helpPane.KeyDown += (_, key) =>
        {
            if (key == Key.CursorDown) { _helpPane.ScrollVertical(1); key.Handled = true; }
            else if (key == Key.CursorUp) { _helpPane.ScrollVertical(-1); key.Handled = true; }
            else if (key == Key.PageDown) { _helpPane.ScrollVertical(Math.Max(1, _helpPane.Viewport.Height - 1)); key.Handled = true; }
            else if (key == Key.PageUp) { _helpPane.ScrollVertical(-Math.Max(1, _helpPane.Viewport.Height - 1)); key.Handled = true; }
        };

        _value = new TextField { X = 0, Y = 8, Width = Dim.Fill(), Height = 1 };
        _masked = new SecretTextField(secret) { X = 0, Y = 8, Width = Dim.Fill(), Height = 1, Visible = false };

        _previousField = Button("Previous", 0, 9, () => MoveField(-1));
        _nextField = Button("Next", 0, 9, () => MoveField(1));
        _applyField = Button("ApplyField", 0, 9, () => { if (CommitField()) ShowField(); });
        _choice = Button("Choose", 0, 9, CycleChoice);
        _choiceList = Button("ChoiceList", 0, 9, OpenChoiceDialog);
        _supplyContainer = Button("SupplyContainer", 0, 10, SupplyContainer);
        _clearContainer = Button("ClearContainer", 0, 10, ClearContainer);
        _addItem = Button("AddItem", 0, 10, AddItem);
        var previousItem = Button("PreviousItem", 0, 11, () => MoveItem(-1));
        var nextItem = Button("NextItem", 0, 11, () => MoveItem(1));
        var removeItem = Button("RemoveItem", 0, 11, RemoveItem);
        _listButtons = [previousItem, nextItem, removeItem];
        _null = Button("SetNull", 0, 10, () => EditTyped("SetNull"));
        _reset = Button("ResetValue", 0, 10, () => EditTyped("ResetValue"));
        _suggest = Button("ApplySuggestion", 0, 10, () => EditTyped("ApplySuggestion"));
        _reuseValue = Button("ReuseValue", 0, 10, ReviewInlineTransfer);
        var itemNull = Button("ItemNull", 0, 12, () => EditTyped("SetNull", item: true));
        var itemSuggestion = Button("ItemSuggestion", 0, 12, () => EditTyped("ApplySuggestion", item: true));
        _itemActions = [itemNull, itemSuggestion];
        _generateSecret = Button("GenerateSecret", 0, 11, GenerateSecret);
        _secretOptions = Button("SecretOptions", 0, 11, OpenSecretGenerator);
        _revealSecret = Button("ShowSecret", 0, 11, ToggleSecretVisibility);
        _copySecret = Button("CopySecret", 0, 11, CopySecret);

        // Stage 2: Review & Import
        _reviewSummaryLabel = new Label { X = 0, Y = 2, Width = Dim.Fill(), Height = 1, HotKeySpecifier = (Rune)0xffff };
        _reviewJumpButton = Button("NextCorrection", 0, 3, FocusNextCorrection);
        _reviewFindButton = Button("FindFields", 22, 3, OpenDiscoveryDialog);
        _artifactLabel = new Label { X = 0, Y = 5, Width = Dim.Fill(), Height = 2, HotKeySpecifier = (Rune)0xffff };
        _artifactButton = Button("Artifact", 0, 7, CycleArtifact);
        _selectOutputButton = Button("SelectOutput", 22, 7, ToggleOutput);
        _comparisonLabel = new Label { X = 0, Y = 9, Width = Dim.Fill(), Height = 3, HotKeySpecifier = (Rune)0xffff };
        _importFileLabel = new Label { X = 0, Y = 12, Text = LocalizedText("ImportFile"), HotKeySpecifier = (Rune)0xffff };
        _importPath = new TextField { X = 18, Y = 12, Width = Dim.Fill(), Height = 1 };
        _import = Button("ImportSelected", 0, 13, ImportSelected);
        _fillImport = Button("FillFields", 20, 13, FillFields);
        _nextFinding = Button("NextFinding", 40, 13, NextComparisonFinding);
        _localizedControls.Add(_importFileLabel, "ImportFile");

        // Stage 3: Files & Save
        _filesSummaryLabel = new Label { X = 0, Y = 2, Width = Dim.Fill(), Height = 2, Text = LocalizedText("FilesStageSummary"), HotKeySpecifier = (Rune)0xffff };
        _localizedControls.Add(_filesSummaryLabel, "FilesStageSummary");
        _validateButton = Button("ValidateDraft", 0, 5, Validate);
        _prepare = Button("PreparePreview", 20, 5, Prepare);
        _save = Button("SaveProtected", 40, 5, Save);
        _clearPrivateButton = Button("ClearPrivate", 0, 7, ClearPrivateState);
        _nextOutcome = Button("NextOutcome", 0, 9, NextOutcome);
        _nextOutcome.Enabled = false;
        _nextAttempt = Button("NextAttempt", 24, 9, NextAttempt);
        _nextAttempt.Enabled = false;
        _receiptLabel = new Label { X = 0, Y = 11, Width = Dim.Fill(), Height = 4, HotKeySpecifier = (Rune)0xffff };
        _nextActions = new Label { Width = Dim.Fill(), Height = 2, HotKeySpecifier = (Rune)0xffff };

        // Persistent Footer & 5-Shortcut StatusBar
        _status = new TextField
        {
            X = 0,
            Y = 20,
            Width = Dim.Fill(),
            Height = 1,
            ReadOnly = true,
            Text = LocalizedText("Ready")
        };
        _sectionsShortcut = BarShortcut(Key.F2, "StatusBarSections", ActivateSectionsShortcut);
        _findShortcut = BarShortcut(Key.F3, "StatusBarFind", OpenDiscoveryDialog);
        _documentShortcut = BarShortcut(Key.E.WithCtrl, "StatusBarDoc", OpenDocumentDialog);
        _keysShortcut = BarShortcut(Key.F1, "StatusBarKeys", OpenShortcutsDialog);
        _closeShortcut = BarShortcut(Key.F10, "StatusBarClose", CloseOrCancel);
        _statusBarShortcuts =
        [
            (_sectionsShortcut, "StatusBarSections"),
            (_findShortcut, "StatusBarFind"),
            (_documentShortcut, "StatusBarDoc"),
            (_keysShortcut, "StatusBarKeys"),
            (_closeShortcut, "StatusBarClose")
        ];
        _statusBar = new StatusBar([_sectionsShortcut, _findShortcut, _documentShortcut, _keysShortcut, _closeShortcut])
        {
            X = 0,
            Y = 21,
            Width = Dim.Fill(),
            Height = 1
        };

        _smallNotice = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = 3,
            Text = LocalizedText("TerminalTooSmall"),
            Visible = false
        };

        Add(_stageConfigure, _stageReview, _stageFiles, _profilesButton, _closeButton,
            _sectionsButton, _namingButton, _findButton, _documentButton, _undoButton, _outputTasks,
            _sectionNavigator);
        Add(configurationChoices);
        Add(_experience, _optionalEditors, _locale, _correction,
            _fieldLabel, _helpPane, _value, _masked,
            _previousField, _nextField, _applyField, _choice, _choiceList,
            _supplyContainer, _clearContainer, _addItem,
            previousItem, nextItem, removeItem, _null, _reset, _suggest, _reuseValue,
            itemNull, itemSuggestion, _generateSecret, _secretOptions, _revealSecret, _copySecret,
            _reviewSummaryLabel, _reviewJumpButton, _reviewFindButton,
            _artifactLabel, _artifactButton, _selectOutputButton, _comparisonLabel,
            _importFileLabel, _importPath, _import, _fillImport, _nextFinding,
            _filesSummaryLabel, _validateButton, _prepare, _save, _clearPrivateButton,
            _nextOutcome, _nextAttempt, _receiptLabel, _nextActions,
            _status, _statusBar, _smallNotice);

        _value.ValueChanged += (_, _) => EditorChanged();
        _value.KeyDown += (_, key) =>
        {
            if (!_tooSmall && key == Key.Z.WithCtrl && UndoLastEdit())
                key.Handled = true;
        };
        _masked.SensitiveInputChanged += (_, _) => EditorChanged();
        _masked.InputRejected += (_, _) => Status("InvalidSecret");
        _masked.SensitiveCommandBlocked += (_, _) => Status("SecretCommandsDisabled");
        _importPath.ValueChanged += (_, _) =>
        {
            if (!_loading) CancelImport();
            RefreshCommands();
        };
        _operation.Invalidated += OperationInvalidated;
        _operation.PublicationChanged += PublicationChanged;
        _operation.Form.ViewChanged += FormViewChanged;
        _operation.Form.ImportComparisonChanged += ImportComparisonChanged;
        _workspace.PropertyChanged += WorkspaceChanged;
        ViewportChanged += ViewportUpdated;

        RefreshModeAndNamingLabels();
        ShowArtifact();
        ShowField();
    }

    internal int ExitCode { get; private set; } = 4;
    internal string StatusText => _status.Text;
    internal string? StatusCode { get; private set; }
    internal string HelpText => _help.Text;
    internal string FieldText => _fieldLabel.Text;
    internal SecretTextField MaskedInput => _masked;
    internal TextField PublicInput => _value;
    internal TextField ImportPathInput => _importPath;
    internal View HelpPane => _helpPane;
    internal TerminalSectionNavigator SectionNavigator => _sectionNavigator;
    internal TerminalWorkbenchStage ActiveStage => _stage;
    internal Task ImportCompletion => _importOperation.Completion;
    internal bool IsImportBusy => _importOperation.IsBusy;
    internal Task ShutdownCompletion => _shutdown;
    internal Task SaveCompletion => _saveObservation;
    internal Task PreparationCompletion => _preparationCompletion;

    private TerminalFieldEditor.Slot[] VisibleSlots =>
        _editor.Slots.Where(slot => _operation.Form.IsEditorVisible(slot.Location)).ToArray();

    private string LocalizedText(string key) => TerminalStrings.Get(key, _operation.Form.Locale);
    private string LocalizedNumber(int value) => value.ToString(TerminalStrings.Culture(_operation.Form.Locale));

    internal void SetStage(TerminalWorkbenchStage stage)
    {
        if (_stage == stage) return;
        CommitField();
        _stage = stage;
        SetEditorVisibility();
        RefreshCommands();
        if (_stage == TerminalWorkbenchStage.Configure) FocusInput();
    }

    private void ToggleNamingMode()
    {
        WorkspaceNamingMode next = _operation.Form.WorkspaceNamingMode == WorkspaceNamingMode.Humanized
            ? WorkspaceNamingMode.Technical
            : WorkspaceNamingMode.Humanized;
        _operation.Form.SetWorkspaceNamingMode(next);
        RefreshModeAndNamingLabels();
        ShowField(preserveLocation: true);
    }

    internal bool UndoLastEdit()
    {
        if (!_operation.Form.CanUndo) return false;
        _masked.ClearSensitiveState();
        bool undone = _operation.Form.Undo();
        if (undone)
        {
            _draftEdited = false;
            ShowField(preserveLocation: true);
            Status("UndoAction");
            RefreshCommands();
        }
        return undone;
    }

    internal void JumpToSection(string sectionId)
    {
        var section = _operation.Form.Projection.Sections
            .FirstOrDefault(item => string.Equals(item.Id, sectionId, StringComparison.Ordinal));
        if (section is null) return;

        _operation.Form.RecordSectionOpened(sectionId, acknowledgeNewlyRelevant: true);
        _sectionNavigator.Refresh();

        if (section.Pointers.Count > 0)
        {
            foreach (string pointer in section.Pointers)
            {
                if (!pointer.StartsWith('/')) continue;
                DefinitionPath parsed = DefinitionPath.Parse(pointer);
                if (_operation.Form.FocusEditor(parsed, preserveDiscovery: true))
                {
                    FocusSlotPath(parsed);
                    return;
                }
            }
        }
        foreach (string fieldId in section.Fields)
        {
            DefinitionPath candidate = DefinitionPath.FromSegments([fieldId]);
            if (_operation.Form.FocusEditor(candidate, preserveDiscovery: true))
            {
                FocusSlotPath(candidate);
                return;
            }
        }
    }

    internal void FocusSlotPath(DefinitionPath path)
    {
        SetStage(TerminalWorkbenchStage.Configure);
        var slots = VisibleSlots;
        int index = slots.ToList().FindIndex(slot => slot.Location.Equals(path));
        if (index < 0 && path.Segments.Count > 0)
        {
            index = slots.ToList().FindIndex(slot => slot.IsList &&
                slot.Location.Segments.SequenceEqual(path.Segments.SkipLast(1)));
            if (index >= 0 && int.TryParse(path.Segments[^1], out int row)) _itemIndex = row;
        }
        if (index >= 0) _fieldIndex = index;
        ShowField(preserveLocation: false);
        FocusInput();
    }

    private void ActivateSectionsShortcut()
    {
        if (_tooSmall) return;
        if (_sidebarVisible) _sectionNavigator.SetFocus();
        else OpenSectionsModal();
    }

    private void OpenSectionsModal()
    {
        if (_tooSmall) return;
        TerminalSectionNavigator.ShowModal(_application, _operation.Form, JumpToSection);
    }

    private void OpenShortcutsDialog()
    {
        if (_tooSmall) return;
        CommitField();
        using var dialog = new TerminalShortcutsDialog(_operation.Form.Locale, _application);
        _application.Run(dialog);
    }

    private void OpenChoiceDialog()
    {
        if (_tooSmall || _slot is null) return;
        IReadOnlyList<DefinitionChoicePresentation> choices = TerminalFieldEditor.ChoicePresentations(_slot);
        if (choices.Count == 0) return;
        string? currentWire = TerminalFieldEditor.ChoiceIndex(_slot, _itemIndex) is int idx && idx >= 0 && idx < choices.Count
            ? choices[idx].WireToken
            : null;
        using var dialog = new TerminalChoiceDialog(choices, _operation.Form.Locale, currentWire, _application);
        _application.Run(dialog);
        if (dialog.SelectedChoice is { } picked &&
            TerminalFieldEditor.SelectChoicePresentation(_slot, picked, _itemIndex))
        {
            ShowField();
            if (!_slot.RequiresMasking) _status.Text = Safe(picked.Label);
            FocusInput();
        }
    }

    private void OpenDiscoveryDialog()
    {
        if (_tooSmall) return;
        CommitField();
        using var dialog = new TerminalDiscoveryDialog(_operation.Form, _application);
        _application.Run(dialog);
        if (dialog.SelectedPath is { } selected)
            FocusSlotPath(selected);
    }

    private void OpenDocumentDialog()
    {
        if (_tooSmall || _operation.Form.SessionPrivacy == DefinitionSessionPrivacy.BrowserNoSecrets) return;
        CommitField();
        using var dialog = new TerminalDocumentDialog(_operation.Form, DefinitionDocumentRepresentation.Canonical, application: _application);
        _application.Run(dialog);
        ShowField(preserveLocation: true);
    }

    private bool FocusedIsTextInput()
    {
        View? focused = Focused ?? MostFocused;
        if (focused is null) return false;
        if (ReferenceEquals(focused, _value) || ReferenceEquals(focused, _masked) || ReferenceEquals(focused, _importPath))
            return true;
        return focused is TextField field && !field.ReadOnly;
    }

    protected override bool OnKeyDown(Key key) =>
        HandleWorkbenchKey(key) || base.OnKeyDown(key);

    private bool HandleWorkbenchKey(Key key)
    {
        if (_tooSmall) return false;
        if (key == Key.Z.WithCtrl && UndoLastEdit()) return true;
        if (key == Key.F1) { OpenShortcutsDialog(); return true; }
        if (key == Key.F2) { ActivateSectionsShortcut(); return true; }
        if (key == Key.F3) { OpenDiscoveryDialog(); return true; }
        if (key == Key.F7) { JumpToNextActionable(errorsOnly: true); return true; }
        if (key == Key.F8) { JumpToNextRequired(); return true; }
        if (key == Key.F9) { JumpToNextUnvisited(); return true; }
        if (key == Key.F10) { CloseOrCancel(); return true; }
        if (key == Key.E.WithCtrl) { OpenDocumentDialog(); return true; }
        if (key == Key.P.WithCtrl) { OpenProfiles(); return true; }

        if (!FocusedIsTextInput())
        {
            if (key == new Key('?')) { OpenShortcutsDialog(); return true; }
            if (key == new Key('/')) { OpenDiscoveryDialog(); return true; }
            if (key == new Key('m') && ToggleCurrentSectionReviewed()) return true;
            if (_stage == TerminalWorkbenchStage.Configure)
            {
                if (key == new Key('j')) { MoveField(1, focusInput: false); return true; }
                if (key == new Key('k')) { MoveField(-1, focusInput: false); return true; }
                if (key == new Key('g')) { MoveToSlotBoundary(last: false); return true; }
                if (key == new Key('G') || key == Key.G.WithShift) { MoveToSlotBoundary(last: true); return true; }
            }
        }
        return false;
    }

    private void RefreshLocale()
    {
        if (_renderedLocale == _operation.Form.Locale) return;
        bool refreshStatus = _status.Text == RenderStatus(_renderedLocale ?? _operation.Form.Locale);
        _renderedLocale = _operation.Form.Locale;
        Title = LocalizedText("WindowTitle");
        foreach ((View control, string key) in _localizedControls) control.Text = LocalizedText(key);
        foreach ((Shortcut shortcut, string key) in _statusBarShortcuts) shortcut.Title = LocalizedText(key);
        RefreshModeAndNamingLabels();
        _sectionNavigator.Refresh();
        _smallNotice.Text = LocalizedText("TerminalTooSmall");
        if (_receipt is not null) UpdatePublication();
        else if (refreshStatus) Status(_statusKey, _statusDiagnostics);
        ShowArtifact();
        LayoutCurrentStage();
    }

    private void RefreshModeAndNamingLabels()
    {
        _namingButton.Text = LocalizedText(_operation.Form.WorkspaceNamingMode == WorkspaceNamingMode.Humanized
            ? "NamingTechnical"
            : "NamingHumanized");
    }

    private Shortcut BarShortcut(Key key, string localizationKey, Action action) => new()
    {
        Key = key,
        Title = LocalizedText(localizationKey),
        Action = action,
        BindKeyToApplication = true
    };

    private Button Button(string key, int x, int y, Action action)
    {
        var button = new Button
        {
            X = x,
            Y = y,
            Width = MeasureButtonWidth(LocalizedText(key), 30),
            Height = 1,
            Text = LocalizedText(key),
            BorderStyle = LineStyle.None,
            ShadowStyle = null
        };
        button.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (Volatile.Read(ref _closeRequested) != 0) return;
            if (key != "RemoveItem") _confirmRemove = false;
            action();
        };
        if (_renderedLocale is null) _localizedControls.Add(button, key);
        return button;
    }

    private static string Safe(string text) => new(text.Select(character =>
        char.IsControl(character) ? ' ' : character).ToArray());

    private static int MeasureButtonWidth(string text, int maxWidth)
    {
        int columns = 0;
        foreach (Rune rune in text.Replace("_", string.Empty, StringComparison.Ordinal).EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) != UnicodeCategory.NonSpacingMark)
                columns += 1;
        }
        return Math.Min(maxWidth, Math.Max(6, columns + 4));
    }

    private int PlaceButtonRows(int startX, int startY, int maxWidth, params Button[] buttons)
    {
        int x = startX;
        int y = startY;
        bool anyVisible = false;
        foreach (Button button in buttons)
        {
            if (!button.Visible) continue;
            anyVisible = true;
            int width = MeasureButtonWidth(button.Text, maxWidth);
            if (x > startX && x + width > startX + maxWidth)
            {
                x = startX;
                y++;
            }
            button.X = x;
            button.Y = y;
            button.Width = width;
            button.Height = 1;
            x += width + 1;
        }
        return anyVisible ? y + 1 : startY;
    }

    private void LayoutCurrentStage()
    {
        if (_layingOut) return;
        _layingOut = true;
        try
        {
            LayoutCurrentStageCore();
        }
        finally
        {
            _layingOut = false;
        }
    }

    private void LayoutCurrentStageCore()
    {
        int totalWidth = Math.Max(1, Viewport.Width > 0 ? Viewport.Width : _lastViewportSize.Width);
        int totalHeight = Math.Max(1, Viewport.Height > 0 ? Viewport.Height : _lastViewportSize.Height);
        int footerY = Math.Max(2, totalHeight - 2);

        if (_tooSmall)
        {
            foreach (View view in SubViews)
                view.Visible = ReferenceEquals(view, _smallNotice);
            return;
        }

        // Row 0: stage switcher + Close (always visible)
        _stageConfigure.Visible = _stageReview.Visible = _stageFiles.Visible = _profilesButton.Visible = _closeButton.Visible = true;
        int utilityY = PlaceButtonRows(0, 0, totalWidth, _stageConfigure, _stageReview, _stageFiles, _profilesButton, _closeButton);

        // Row 1: utility bar
        _namingButton.Visible = _findButton.Visible = _undoButton.Visible = true;
        _documentButton.Visible = _operation.Form.SessionPrivacy != DefinitionSessionPrivacy.BrowserNoSecrets;
        _outputTasks.Visible = _operation.Form.Definition.Presentation?.OutputSelector is { Options.Count: > 1 };
        int bodyTopY = PlaceButtonRows(0, utilityY, totalWidth,
            _sectionsButton, _namingButton, _findButton, _documentButton, _undoButton, _outputTasks);

        // Docked left Master navigator when width >= 88
        int mainX = 0;
        int mainWidth = totalWidth;
        if (_sidebarVisible && totalWidth > 40)
        {
            _sectionNavigator.X = 0;
            _sectionNavigator.Y = bodyTopY;
            _sectionNavigator.Width = 32;
            _sectionNavigator.Height = Math.Max(4, footerY - bodyTopY);
            mainX = 33;
            mainWidth = Math.Max(20, totalWidth - mainX);
        }

        // Persistent Footer & 5-Shortcut StatusBar
        _status.Visible = true;
        _status.X = 0;
        _status.Y = footerY;
        _status.Width = totalWidth;
        _status.Height = 1;

        _statusBar.Visible = true;
        _statusBar.X = 0;
        _statusBar.Y = footerY + 1;
        _statusBar.Width = totalWidth;
        _statusBar.Height = 1;

        if (_stage == TerminalWorkbenchStage.Configure)
        {
            int y = bodyTopY;
            if (_configurationChoices.Count > 0 && _operation.Form.ConfigurationContext is not null)
            {
                foreach (var choice in _configurationChoices)
                {
                    if (!choice.Button.Visible) continue;
                    choice.Button.X = mainX;
                    choice.Button.Y = y++;
                    choice.Button.Width = mainWidth;
                    choice.Button.Height = 1;
                }
            }
            y = PlaceButtonRows(mainX, y, mainWidth, _experience, _optionalEditors, _locale, _correction);
            _fieldLabel.X = mainX;
            _fieldLabel.Y = y++;
            _fieldLabel.Width = mainWidth;
            _fieldLabel.Height = 1;

            int helpHeight = totalHeight < 22 ? 3 : 4;
            _helpPane.X = mainX;
            _helpPane.Y = y;
            _helpPane.Width = mainWidth;
            _helpPane.Height = helpHeight;
            _help.X = 0;
            _help.Y = 0;
            _help.Width = Dim.Fill();
            y += helpHeight;

            _value.X = mainX;
            _value.Y = y;
            _value.Width = mainWidth;
            _value.Height = 1;
            _masked.X = mainX;
            _masked.Y = y;
            _masked.Width = mainWidth;
            _masked.Height = 1;
            y++;

            y = PlaceButtonRows(mainX, y, mainWidth,
                _previousField, _nextField, _applyField, _choice, _choiceList,
                _addItem, _listButtons[0], _listButtons[1], _listButtons[2],
                _supplyContainer, _clearContainer,
                _null, _reset, _suggest, _reuseValue, _itemActions[0], _itemActions[1],
                _generateSecret, _secretOptions, _revealSecret, _copySecret);
            _ = y;
        }
        else if (_stage == TerminalWorkbenchStage.ReviewAndImport)
        {
            WorkspaceProgressSummary summary = _operation.Form.Projection.ProgressSummary;
            _reviewSummaryLabel.Text = Safe(string.Format(TerminalStrings.Culture(_operation.Form.Locale),
                LocalizedText("ReviewProgressFormat"), summary.ReadyRequired, summary.TotalRequired,
                summary.MissingRequired, summary.InvalidCount, summary.DeferredSecretCount));
            int y = bodyTopY;
            _reviewSummaryLabel.X = mainX;
            _reviewSummaryLabel.Y = y++;
            _reviewSummaryLabel.Width = mainWidth;
            _reviewSummaryLabel.Height = 1;

            y = PlaceButtonRows(mainX, y, mainWidth, _reviewJumpButton, _reviewFindButton);
            _artifactLabel.X = mainX;
            _artifactLabel.Y = y;
            _artifactLabel.Width = mainWidth;
            _artifactLabel.Height = 2;
            y += 2;

            y = PlaceButtonRows(mainX, y, mainWidth, _artifactButton, _selectOutputButton);
            int remainingBeforeFooter = Math.Max(4, footerY - y);
            int comparisonHeight = Math.Clamp(remainingBeforeFooter - 3, 1, 3);
            _comparisonLabel.X = mainX;
            _comparisonLabel.Y = y;
            _comparisonLabel.Width = mainWidth;
            _comparisonLabel.Height = comparisonHeight;
            y += comparisonHeight;

            int fileLabelWidth = Math.Min(18, Math.Max(8, _importFileLabel.Text.Length + 1));
            _importFileLabel.X = mainX;
            _importFileLabel.Y = y;
            if (mainWidth - fileLabelWidth >= 18)
            {
                _importFileLabel.Width = fileLabelWidth;
                _importFileLabel.Height = 1;
                _importPath.X = mainX + fileLabelWidth;
                _importPath.Y = y;
                _importPath.Width = Math.Max(12, mainWidth - fileLabelWidth);
                _importPath.Height = 1;
                y++;
            }
            else
            {
                _importFileLabel.Width = mainWidth;
                _importFileLabel.Height = 1;
                y++;
                _importPath.X = mainX;
                _importPath.Y = y++;
                _importPath.Width = mainWidth;
                _importPath.Height = 1;
            }
            PlaceButtonRows(mainX, y, mainWidth, _import, _fillImport, _nextFinding);
        }
        else
        {
            _filesSummaryLabel.X = mainX;
            _filesSummaryLabel.Y = bodyTopY;
            _filesSummaryLabel.Width = mainWidth;
            _filesSummaryLabel.Height = 2;

            int afterSave = PlaceButtonRows(mainX, bodyTopY + 3, mainWidth,
                _validateButton, _prepare, _save, _clearPrivateButton);
            int afterReceipts = PlaceButtonRows(mainX, afterSave + 1, mainWidth, _nextOutcome, _nextAttempt);

            _receiptLabel.X = mainX;
            _nextActions.X = mainX;
            _nextActions.Y = afterReceipts;
            _nextActions.Width = mainWidth;
            _nextActions.Height = 2;
            int guidanceHeight = _nextActions.Visible ? 2 : 0;
            _receiptLabel.Y = afterReceipts + guidanceHeight;
            _receiptLabel.Width = mainWidth;
            _receiptLabel.Height = Math.Max(2, footerY - afterReceipts - guidanceHeight);
        }

        int desiredContentHeight = Math.Max(25, totalHeight);
        if (GetContentSize().Height != desiredContentHeight) SetContentHeight(desiredContentHeight);
    }

    private void CycleArtifact()
    {
        if (!CommitField()) return;
        CancelImport();
        var artifacts = _operation.EligibleArtifacts;
        if (artifacts.Count == 0) return;
        int index = artifacts.ToList().FindIndex(item => item.Id == _operation.ArtifactId);
        _operation.ArtifactId = artifacts[(index + 1) % artifacts.Count].Id;
        ShowArtifact();
        RefreshCommands();
    }

    private void ToggleOutput()
    {
        if (!CommitField()) return;
        CancelImport();
        _operation.ToggleCurrentArtifact();
        ShowArtifact();
        Status("InputChanged");
        RefreshCommands();
    }

    private void ShowArtifact()
    {
        var artifact = _operation.EligibleArtifacts.SingleOrDefault(item => item.Id == _operation.ArtifactId);
        if (artifact is null) { _artifactLabel.Text = LocalizedText("InvalidOutput"); return; }
        int artifactIndex = _operation.Form.Definition.Artifacts.ToList().IndexOf(artifact);
        string? title = _operation.Form.LocalizedText(DefinitionPath.FromSegments(["artifacts",
            artifactIndex.ToString(CultureInfo.InvariantCulture), "title"]), artifact.Title);
        _artifactLabel.Text = Safe($"{title} | "
            + $"{(_operation.CurrentArtifactSelected ? LocalizedText("Selected") : LocalizedText("NotSelected"))} "
            + $"{LocalizedNumber(_operation.SelectedArtifactIds.Count)}/{LocalizedNumber(_operation.EligibleArtifacts.Count)} | "
            + $"{artifact.FileName} | {artifact.Format.ToToken()} | {artifact.ApplySemantics.ToToken()} | {artifact.Restart.ToToken()}");
    }

    internal void Validate()
    {
        if (!CommitField()) return;
        try
        {
            var validation = _operation.Form.Validate();
            Status(validation.IsReady && !_operation.Form.HasInputErrors ? "PreviewReady" : "DraftInvalid",
                validation.Diagnostics);
        }
        catch (DefinitionException) { Status("DraftInvalid"); }
    }

    private string RenderStatus(string locale) => TerminalStrings.Get(_statusKey, locale)
        + (_statusDiagnostics.Length == 0 ? string.Empty : " " + string.Join(", ",
            _statusDiagnostics.Select(diagnostic => PresentationStrings.Diagnostic(diagnostic.Code, locale)
                + " " + JsonSerializer.Serialize(new[] { diagnostic.FieldId }.Concat(diagnostic.RelativePath)))));

    private void Status(string key, IEnumerable<DefinitionDiagnostic>? diagnostics = null)
    {
        _statusDiagnostics = diagnostics?.Distinct().ToArray() ?? [];
        StatusCode = _statusDiagnostics.FirstOrDefault()?.Code;
        _statusKey = key;
        _status.Text = RenderStatus(_operation.Form.Locale);
        _status.ClearHistoryChanges();
        _status.SetNeedsDraw();
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
        ObserveProbeStatusMutation(key is "Preparing" or "Writing");
#endif
    }

    private void RefreshCommands()
    {
        RefreshConfigurationChoices();
        bool available = !_tooSmall && !_workspace.IsBusy && !_importOperation.IsBusy
            && !_operation.IsPublishing && !_preparing && Volatile.Read(ref _closeRequested) == 0;
        _undoButton.Enabled = available && _operation.Form.CanUndo;
        _outputTasks.Enabled = _reuseValue.Enabled = available;
        var proposals = _operation.Form.OutputWorkflow.ProposedNextActions;
        _nextActions.Text = proposals.Count == 0 ? string.Empty : Safe(
            PresentationStrings.Get("NextActionsTitle", _operation.Form.Locale) + ": "
            + string.Join("; ", proposals.Select(action => action.Title)) + " | " + LocalizedText("OutputTasks"));
        _nextActions.Visible = !_tooSmall && _stage == TerminalWorkbenchStage.FilesAndSave && proposals.Count > 0;
        _save.Enabled = available && _operation.ProtectedOutputAvailable && _operation.IsPrepared;
        _prepare.Enabled = available;
        _import.Enabled = available && _operation.EligibleArtifacts.Count > 0 && !string.IsNullOrWhiteSpace(_importPath.Text);
        _fillImport.Enabled = available && _operation.EligibleArtifacts.Count > 0 && !_draftEdited && (_operation.Form.ImportComparison is null
            ? !string.IsNullOrWhiteSpace(_importPath.Text) : _operation.Form.CanAdoptImportComparison);
        _nextFinding.Enabled = available && _operation.Form.ImportComparison is { Findings.Count: > 0 };
        _generateSecret.Enabled = available && _slot is not null && _operation.Form.CanGenerateSecret(
            TerminalFieldEditor.EditLocation(_slot, _itemIndex));
        _generateSecret.Text = LocalizedText(_secret.Count > 0 ? "RegenerateSecret" : "GenerateSecret");
        _secretOptions.Enabled = _generateSecret.Enabled && _slot is not null && HasConfigurableSecretOptions(_slot);
        _revealSecret.Enabled = available && _secret.Count > 0;
        _copySecret.Enabled = available && _secret.Count > 0;
    }

    private void ViewportUpdated(object? sender, DrawEventArgs args)
    {
        if (_layingOut) return;
        if (args.NewViewport.Size == Size.Empty && _lastViewportSize.Width >= MinimumTerminalWidth && Frame.Width >= MinimumTerminalWidth)
            return;
        ApplyViewportPolicy(args.NewViewport.Size);
    }

    private void RevealFocusedControl()
    {
        if (_tooSmall || Focused is not { } focused || focused.Frame.Height == 0) return;
        int top = Viewport.Y;
        if (focused.Frame.Top < top) top = focused.Frame.Top;
        else if (focused.Frame.Bottom > Viewport.Bottom) top = focused.Frame.Bottom - Viewport.Height;
        if (top != Viewport.Y) ScrollVertical(top - Viewport.Y);
    }

    internal void ApplyViewportPolicy(Size size)
    {
        _lastViewportSize = size;
        _tooSmall = size.Width < MinimumTerminalWidth || size.Height < MinimumTerminalHeight;
        _sidebarVisible = !_tooSmall && size.Width >= SidebarBreakpointWidth;
        _smallNotice.Visible = _tooSmall;
        _nextOutcome.Enabled = _receipt is { Outcomes.Count: > 1 };
        _nextAttempt.Enabled = _receipt is not null;
        if (_operation.Form.ImportComparison is null) ShowHelp();
        else ShowComparison();
        SetEditorVisibility();
        RefreshCommands();
        RevealFocusedControl();
    }
}
