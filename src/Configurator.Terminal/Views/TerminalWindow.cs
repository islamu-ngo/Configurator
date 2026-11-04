namespace Configurator.Terminal;

using System.Drawing;
using System.Text;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

internal sealed partial class TerminalWindow : Window
{
    internal const int MinimumTerminalWidth = 76;
    internal const int MinimumTerminalHeight = 20;
    private readonly IApplication _application;
    private readonly PresentationWorkspace _workspace;
    private readonly TerminalArtifactOperation _operation;
    private readonly TerminalSecretBuffer _secret;
    private readonly TerminalFieldEditor _editor;
    private readonly TerminalImportOperation _importOperation;
    private readonly Action<Action> _dispatch;
    private readonly TimeProvider _timeProvider;
    private readonly WeakReference<TerminalWindow> _owner;
    private readonly Label _fieldLabel;
    private readonly Label _help;
    private readonly Label _artifactLabel;
    private readonly TextField _status;
    private readonly Label _smallNotice;
    private readonly TextField _value;
    private readonly SecretTextField _masked;
    private readonly TextField _importPath;
    private readonly Button _import;
    private readonly Button _fillImport;
    private readonly Button _nextFinding;
    private readonly Button _nextOutcome;
    private readonly Button _nextAttempt;
    private readonly Button _save;
    private readonly Button _prepare;
    private readonly Button _experience;
    private readonly Button _optionalEditors;
    private readonly Button _choice;
    private readonly Button _addItem;
    private readonly Button _supplyContainer;
    private readonly Button _clearContainer;
    private readonly Button _null;
    private readonly Button _reset;
    private readonly Button _suggest;
    private readonly Button _locale;
    private readonly Button _helpNext;
    private readonly Button _correction;
    private readonly Button _generateSecret;
    private readonly Button _revealSecret;
    private readonly Button _copySecret;
    private readonly Button[] _itemActions;
    private readonly Button[] _listButtons;
    private readonly View[] _normal;
    private TerminalFieldEditor.Slot? _slot;
    private int _fieldIndex;
    private int _itemIndex;
    private int _outcomeIndex;
    private int _helpPage;
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

    private string LocalizedText(string key) => TerminalStrings.Get(key, _operation.Form.Locale);
    private string LocalizedNumber(int value) => value.ToString(TerminalStrings.Culture(_operation.Form.Locale));

    private void RefreshLocale()
    {
        if (_renderedLocale == _operation.Form.Locale) return;
        bool refreshStatus = _status.Text == RenderStatus(_renderedLocale ?? _operation.Form.Locale);
        _renderedLocale = _operation.Form.Locale;
        Title = LocalizedText("WindowTitle");
        foreach ((View control, string key) in _localizedControls) control.Text = LocalizedText(key);
        _smallNotice.Text = LocalizedText("TerminalTooSmall");
        if (_receipt is not null) UpdatePublication();
        else if (refreshStatus) Status(_statusKey, _statusDiagnostics);
        ShowArtifact();
    }

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
        _application.Invoke(() => Configurator.Engineering.MeasureNativeImportComparison.Attach(this));
#endif
        _editor = new(operation.Form);
        Title = LocalizedText("WindowTitle");
        Width = Dim.Fill();
        Height = Dim.Fill();
        SetContentHeight(25);
        ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        FocusedChanged += (_, _) => RevealFocusedControl();
        var definition = operation.Form.Definition;
        var heading = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Text = Safe(FormattableString.Invariant($"{definition.Title} | {definition.ApplicationId} | r{definition.Revision}"))
        };
        _locale = Button("Locale", 1, 1, CycleLocale);
        _correction = Button("NextCorrection", 25, 1, FocusNextCorrection);
        _experience = Button("Choose", 1, 2, CycleExperience);
        _experience.Width = Dim.Fill(2);
        _optionalEditors = Button("Choose", 1, 3, ToggleOptionalEditors);
        _optionalEditors.Width = Dim.Fill(2);
        _fieldLabel = new Label { X = 1, Y = 4, Width = Dim.Fill(2), HotKeySpecifier = (Rune)0xffff };
        _value = new TextField { X = 1, Y = 5, Width = Dim.Fill(2) };
        _masked = new SecretTextField(secret) { X = 1, Y = 5, Width = Dim.Fill(2), Visible = false };
        _help = new Label { X = 1, Y = 6, Width = Dim.Fill(2), Height = 2, HotKeySpecifier = (Rune)0xffff };
        _experience.HotKeySpecifier = (Rune)0xffff;
        _choice = Button("Choose", 1, 8, CycleChoice);
        _supplyContainer = Button("SupplyContainer", 25, 8, SupplyContainer);
        _clearContainer = Button("ClearContainer", 49, 8, ClearContainer);
        var previous = Button("Previous", 1, 9, () => MoveField(-1));
        var next = Button("Next", 24, 9, () => MoveField(1));
        var apply = Button("ApplyField", 47, 9, () => { if (CommitField()) ShowField(); });
        var previousItem = Button("PreviousItem", 1, 10, () => MoveItem(-1));
        var nextItem = Button("NextItem", 25, 10, () => MoveItem(1));
        var removeItem = Button("RemoveItem", 49, 10, RemoveItem);
        _listButtons = [previousItem, nextItem, removeItem];
        _addItem = Button("AddItem", 1, 11, AddItem);
        _helpNext = Button("Help", 49, 11, () => { _helpPage++; ShowHelp(); });
        var artifact = Button("Artifact", 1, 12, CycleArtifact);
        var selectOutput = Button("SelectOutput", 27, 12, ToggleOutput);
        _artifactLabel = new Label { X = 1, Y = 13, Width = Dim.Fill(2) };
        var fileLabel = new Label { X = 1, Y = 14, Text = LocalizedText("ImportFile") };
        _importPath = new TextField { X = 20, Y = 14, Width = Dim.Fill(2) };
        _import = Button("ImportSelected", 1, 15, ImportSelected);
        _fillImport = Button("FillFields", 25, 15, FillFields);
        _nextFinding = Button("NextFinding", 49, 15, NextComparisonFinding);
        var validate = Button("ValidateDraft", 1, 16, Validate);
        _prepare = Button("PreparePreview", 26, 16, Prepare);
        _save = Button("SaveProtected", 51, 16, Save);
        _nextOutcome = Button("NextOutcome", 1, 17, NextOutcome);
        _nextOutcome.Visible = false;
        _nextAttempt = Button("NextAttempt", 34, 17, NextAttempt);
        _nextAttempt.Visible = false;
        _status = new TextField
        {
            X = 1,
            Y = 18,
            Width = Dim.Fill(2),
            ReadOnly = true,
            Text = LocalizedText("Ready")
        };
        _null = Button("SetNull", 1, 19, () => EditTyped("SetNull"));
        _reset = Button("ResetValue", 25, 19, () => EditTyped("ResetValue"));
        _suggest = Button("ApplySuggestion", 49, 19, () => EditTyped("ApplySuggestion"));
        var itemNull = Button("ItemNull", 1, 20, () => EditTyped("SetNull", item: true));
        var itemSuggestion = Button("ItemSuggestion", 25, 20, () => EditTyped("ApplySuggestion", item: true));
        _itemActions = [itemNull, itemSuggestion];
        _generateSecret = Button("GenerateSecret", 1, 22, GenerateSecret);
        _revealSecret = Button("ShowSecret", 25, 22, ToggleSecretVisibility);
        _copySecret = Button("CopySecret", 49, 22, CopySecret);
        var clear = Button("ClearPrivate", 1, 21, ClearPrivateState);
        var close = Button("Close", 40, 21, CloseOrCancel);
        var limitations = new Label { X = 1, Y = 23, Width = Dim.Fill(2), Height = 2, Text = LocalizedText("Limitations") };
        _localizedControls.Add(fileLabel, "ImportFile");
        _localizedControls.Add(limitations, "Limitations");
        _smallNotice = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = 3,
            Text = LocalizedText("TerminalTooSmall"),
            Visible = false
        };
        View[] configurationChoices = CreateConfigurationChoices();
        Add(heading, _locale, _correction);
        Add(configurationChoices);
        Add(_experience, _optionalEditors, _fieldLabel, _value, _masked, _help, _choice, _supplyContainer, _clearContainer, previous, next, apply,
            _artifactLabel, artifact, selectOutput, fileLabel, _importPath, _import, _fillImport, _nextFinding,
            validate, _prepare, _save, _nextOutcome, _nextAttempt, _status,
            clear, close, limitations, previousItem, nextItem, removeItem, _addItem, _helpNext, _null, _reset, _suggest, itemNull, itemSuggestion,
            _generateSecret, _revealSecret, _copySecret);
        _configurationContent = SubViews.Where(view => view != heading && view != _locale && view != _correction
            && !configurationChoices.Contains(view)).ToArray();
        _normal = SubViews.ToArray();
        Add(_smallNotice);
        _value.ValueChanged += (_, _) => EditorChanged();
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
        ShowArtifact();
        ShowField();
    }

    internal int ExitCode { get; private set; } = 4;
    internal string StatusText => _status.Text;
    internal string? StatusCode { get; private set; }
    internal SecretTextField MaskedInput => _masked;
    internal TextField PublicInput => _value;
    internal Task ImportCompletion => _importOperation.Completion;
    internal bool IsImportBusy => _importOperation.IsBusy;
    internal Task ShutdownCompletion => _shutdown;
    internal Task SaveCompletion => _saveObservation;
    internal Task PreparationCompletion => _preparationCompletion;
    private TerminalFieldEditor.Slot[] VisibleSlots =>
        _editor.Slots.Where(slot => _operation.Form.IsEditorVisible(slot.FullPath)).ToArray();

    private Button Button(string key, int x, int y, Action action)
    {
        var button = new Button
        {
            X = x < 20 ? 1 : x < 40 ? Pos.Percent(33) : Pos.Percent(66),
            Y = y,
            Width = Dim.Percent(33) - 1,
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
            artifactIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), "title"]), artifact.Title);
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
    }

    private void RefreshCommands()
    {
        RefreshConfigurationChoices();
        bool available = !_tooSmall && !_workspace.IsBusy && !_importOperation.IsBusy
            && !_operation.IsPublishing && !_preparing && Volatile.Read(ref _closeRequested) == 0;
        _save.Enabled = available && _operation.ProtectedOutputAvailable
            && _operation.IsPrepared;
        _prepare.Enabled = available;
        _import.Enabled = available && _operation.EligibleArtifacts.Count > 0 && !string.IsNullOrWhiteSpace(_importPath.Text);
        _fillImport.Enabled = available && _operation.EligibleArtifacts.Count > 0 && !_draftEdited && (_operation.Form.ImportComparison is null
            ? !string.IsNullOrWhiteSpace(_importPath.Text) : _operation.Form.CanAdoptImportComparison);
        _nextFinding.Enabled = available && _operation.Form.ImportComparison is { Findings.Count: > 0 };
        _generateSecret.Enabled = available && _slot is not null && _operation.Form.CanGenerateSecret(
            TerminalFieldEditor.EditPath(_slot, _itemIndex));
        _generateSecret.Text = LocalizedText(_secret.Count > 0 ? "RegenerateSecret" : "GenerateSecret");
        _revealSecret.Enabled = available && _secret.Count > 0;
        _copySecret.Enabled = available && _secret.Count > 0;
    }

    private void ViewportUpdated(object? sender, DrawEventArgs args) => ApplyViewportPolicy(args.NewViewport.Size);

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
        _tooSmall = size.Width < MinimumTerminalWidth || size.Height < MinimumTerminalHeight;
        foreach (View view in _normal) view.Visible = !_tooSmall;
        _nextOutcome.Visible = !_tooSmall && _receipt is { Outcomes.Count: > 1 };
        _nextAttempt.Visible = !_tooSmall && _receipt is not null;
        _smallNotice.Visible = _tooSmall;
        if (_operation.Form.ImportComparison is null) ShowHelp();
        else ShowComparison();
        SetEditorVisibility();
        RefreshCommands();
        RevealFocusedControl();
    }

}
