namespace Configurator.Terminal;

using System.ComponentModel;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Drawing;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

internal sealed class SetupTerminalWindow : Window
{
    internal const int MinimumTerminalWidth = 80;
    internal const int MinimumTerminalHeight = 26;
    private readonly IApplication _application;
    private readonly SetupPresentationWorkspace _workspace;
    private readonly SetupTerminalArtifactOperation _operation;
    private readonly SetupTerminalSecretBuffer _secret;
    private readonly SetupTerminalFieldEditor _editor;
    private readonly Label _fieldLabel;
    private readonly Label _help;
    private readonly Label _artifactLabel;
    private readonly Label _status;
    private readonly Label _smallNotice;
    private readonly TextField _value;
    private readonly SetupSecretTextField _masked;
    private readonly TextField _fileName;
    private readonly Button _save;
    private readonly Button _prepare;
    private readonly Button _mode;
    private readonly Button _choice;
    private readonly Button[] _listButtons;
    private readonly View[] _normal;
    private SetupTerminalFieldEditor.Slot? _slot;
    private int _fieldIndex;
    private int _itemIndex;
    private bool _loading;
    private bool _disposed;
    private bool _tooSmall;

    internal SetupTerminalWindow(IApplication application, SetupPresentationWorkspace workspace,
        SetupTerminalArtifactOperation operation, SetupTerminalSecretBuffer secret)
    {
        _application = application;
        _workspace = workspace;
        _operation = operation;
        _secret = secret;
        _editor = new(operation.Form);
        Title = SetupTerminalText.Get("WindowTitle");
        Width = Dim.Fill();
        Height = Dim.Fill();
        var definition = operation.Form.Definition;
        var heading = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Text = Safe($"{definition.Title} | {definition.ApplicationId} | r{definition.Revision}")
        };
        _mode = Button("DisplayMode", 1, 2, CycleMode);
        _fieldLabel = new Label { X = 1, Y = 4, Width = Dim.Fill(2) };
        _value = new TextField { X = 1, Y = 5, Width = Dim.Fill(2) };
        _masked = new SetupSecretTextField(secret) { X = 1, Y = 5, Width = Dim.Fill(2), Visible = false };
        _help = new Label { X = 1, Y = 6, Width = Dim.Fill(2), Height = 2 };
        _choice = Button("Choose", 1, 8, CycleChoice);
        var previous = Button("Previous", 1, 9, () => MoveField(-1));
        var next = Button("Next", 24, 9, () => MoveField(1));
        var apply = Button("ApplyField", 47, 9, () => { if (CommitField()) ShowField(); });
        var previousItem = Button("PreviousItem", 1, 10, () => MoveItem(-1));
        var nextItem = Button("NextItem", 25, 10, () => MoveItem(1));
        var removeItem = Button("RemoveItem", 49, 10, RemoveItem);
        _listButtons = [previousItem, nextItem, removeItem];
        var artifact = Button("Artifact", 1, 12, CycleArtifact);
        _artifactLabel = new Label { X = 1, Y = 13, Width = Dim.Fill(2) };
        var fileLabel = new Label { X = 1, Y = 14, Text = SetupTerminalText.Get("OutputFile") };
        _fileName = new TextField { X = 20, Y = 14, Width = Dim.Fill(2), Text = operation.FileName };
        var validate = Button("ValidateDraft", 1, 16, Validate);
        _prepare = Button("PreparePreview", 26, 16, Prepare);
        _save = Button("SaveProtected", 51, 16, Save);
        _status = new Label { X = 1, Y = 18, Width = Dim.Fill(2), Height = 3, Text = SetupTerminalText.Get("Ready") };
        var clear = Button("ClearPrivate", 1, 21, ClearPrivateState);
        var close = Button("Close", 40, 21, CloseOrCancel);
        var limitations = new Label { X = 1, Y = 22, Width = Dim.Fill(2), Height = 2, Text = SetupTerminalText.Get("Limitations") };
        _smallNotice = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = 3,
            Text = SetupTerminalText.Get("TerminalTooSmall"),
            Visible = false
        };
        Add(heading, _mode, _fieldLabel, _value, _masked, _help, _choice, previous, next, apply,
            _artifactLabel, artifact, fileLabel, _fileName, validate, _prepare, _save, _status,
            clear, close, limitations, previousItem, nextItem, removeItem);
        _normal = SubViews.ToArray();
        Add(_smallNotice);
        _value.ValueChanged += (_, _) => EditorChanged();
        _masked.ValueChanged += (_, _) => EditorChanged();
        _masked.InputRejected += (_, _) => Status("InvalidSecret");
        _masked.SensitiveCommandBlocked += (_, _) => Status("SecretCommandsDisabled");
        _fileName.ValueChanged += (_, _) =>
        {
            _workspace.Deactivate();
            _workspace.Activate();
            operation.FileName = _fileName.Text;
            RefreshCommands();
        };
        _operation.Invalidated += OperationInvalidated;
        _workspace.PropertyChanged += WorkspaceChanged;
        ViewportChanged += ViewportUpdated;
        ShowArtifact();
        ShowField();
    }

    internal int ExitCode { get; private set; } = 4;
    internal string StatusText => _status.Text;
    internal SetupSecretTextField MaskedInput => _masked;
    internal TextField PublicInput => _value;

    private Button Button(string key, int x, int y, Action action)
    {
        var button = new Button
        {
            X = x,
            Y = y,
            Height = 1,
            Text = SetupTerminalText.Get(key),
            BorderStyle = LineStyle.None,
            ShadowStyle = null
        };
        button.Accepting += (_, args) => { args.Handled = true; action(); };
        return button;
    }

    private static string Safe(string text) => new(text.Select(character =>
        char.IsControl(character) ? ' ' : character).ToArray());

    private void EditorChanged()
    {
        if (_loading) return;
        _operation.Invalidate();
        Status("InputChanged");
        RefreshCommands();
    }

    internal bool CommitField()
    {
        if (_slot is null) return true;
        byte[] bytes = [];
        try
        {
            string text = _value.Text;
            if (_slot.RequiresMasking)
            {
                bytes = _secret.Count == 0 ? [] : _secret.CopyUtf8Bytes();
                text = Encoding.UTF8.GetString(bytes);
            }
            if (!SetupTerminalFieldEditor.Write(_slot, text, _itemIndex))
            {
                Status("DraftInvalid");
                return false;
            }
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal void MoveField(int delta)
    {
        if (!CommitField()) return;
        var slots = _editor.Slots;
        if (slots.Count == 0) { ShowField(); return; }
        int current = _slot is null ? 0 : slots.ToList().FindIndex(slot =>
            slot.Input == _slot.Input && slot.Path.SequenceEqual(_slot.Path));
        _fieldIndex = (Math.Max(0, current) + delta + slots.Count) % slots.Count;
        _itemIndex = 0;
        ShowField();
        FocusInput();
    }

    private void MoveItem(int delta)
    {
        if (_slot is null || !_slot.IsList || !CommitField()) return;
        _itemIndex = Math.Clamp(_itemIndex + delta, 0, SetupTerminalFieldEditor.ItemCount(_slot));
        ShowField();
        FocusInput();
    }

    private void RemoveItem()
    {
        if (_slot is null || !_slot.IsList) return;
        SetupTerminalFieldEditor.Remove(_slot, _itemIndex);
        _itemIndex = Math.Min(_itemIndex, SetupTerminalFieldEditor.ItemCount(_slot));
        ShowField();
        FocusInput();
    }

    private void ShowField()
    {
        _loading = true;
        try
        {
            _masked.ClearSensitiveState();
            _value.Text = string.Empty;
            var slots = _editor.Slots;
            _fieldIndex = Math.Clamp(_fieldIndex, 0, Math.Max(0, slots.Count - 1));
            _slot = slots.Count == 0 ? null : slots[_fieldIndex];
            _fieldLabel.Text = _slot is null ? SetupTerminalText.Get("NoFields")
                : Safe($"{_fieldIndex + 1}/{slots.Count} {_slot.Input.Definition.Id} / {_slot.Title} ({_slot.ScalarType})"
                    + (_slot.IsList ? $" [{_itemIndex + 1}/{SetupTerminalFieldEditor.ItemCount(_slot) + 1}]" : ""));
            _help.Text = _slot is null ? string.Empty : Safe(_slot.Help);
            if (_slot is not null)
            {
                string text = SetupTerminalFieldEditor.Read(_slot, _itemIndex);
                if (_slot.RequiresMasking && text.Length > 0) _masked.TryReplaceSensitiveInput(text);
                else if (!_slot.RequiresMasking) _value.Text = text;
            }
            SetEditorVisibility();
            _mode.Text = SetupTerminalText.Get("DisplayMode") + ": " + SetupTerminalText.Get(operationModeKey());
            RefreshCommands();
        }
        finally { _loading = false; }
        string operationModeKey() => "Mode" + _operation.Form.DisplayMode;
    }

    private void SetEditorVisibility()
    {
        _value.Visible = !_tooSmall && _slot is { RequiresMasking: false };
        _masked.Visible = !_tooSmall && _slot is { RequiresMasking: true };
        _choice.Visible = !_tooSmall && _slot is not null && SetupTerminalFieldEditor.Choices(_slot).Count > 0;
        foreach (var button in _listButtons) button.Visible = !_tooSmall && _slot is { IsList: true };
    }

    private void FocusInput()
    {
        if (_slot?.RequiresMasking == true) _masked.SetFocus();
        else _value.SetFocus();
    }

    private void CycleMode()
    {
        if (!CommitField()) return;
        _operation.Form.DisplayMode = (SetupDisplayMode)(((int)_operation.Form.DisplayMode + 1) % 3);
        _fieldIndex = 0;
        _itemIndex = 0;
        ShowField();
    }

    private void CycleChoice()
    {
        if (_slot is null) return;
        var choices = SetupTerminalFieldEditor.Choices(_slot);
        if (choices.Count == 0) return;
        string current = SetupTerminalFieldEditor.Read(_slot, _itemIndex);
        string next = choices[(choices.ToList().IndexOf(current) + 1) % choices.Count];
        SetupTerminalFieldEditor.Write(_slot, next, _itemIndex);
        ShowField();
        FocusInput();
    }

    private void CycleArtifact()
    {
        if (!CommitField()) return;
        var artifacts = _operation.Form.Definition.Artifacts;
        int index = artifacts.ToList().FindIndex(item => item.Id == _operation.ArtifactId);
        _operation.ArtifactId = artifacts[(index + 1) % artifacts.Count].Id;
        _fileName.Text = _operation.FileName;
        ShowArtifact();
        RefreshCommands();
    }

    private void ShowArtifact()
    {
        var artifact = _operation.Form.Definition.Artifacts.Single(item => item.Id == _operation.ArtifactId);
        _artifactLabel.Text = Safe($"{artifact.Id} | {artifact.Format} | {artifact.ApplySemantics} | {artifact.Restart}");
    }

    private void Validate()
    {
        if (!CommitField()) return;
        try
        {
            var validation = _operation.Form.Validate();
            _status.Text = validation.IsReady && !_operation.Form.HasInputErrors
                ? SetupTerminalText.Get("PreviewReady")
                : SetupTerminalText.Get("DraftInvalid") + " " +
                    string.Join(", ", validation.Diagnostics.Select(item => Safe(item.Code + ":" + item.FieldId)));
        }
        catch (SetupDefinitionException) { Status("DraftInvalid"); }
    }

    internal void Prepare()
    {
        if (!CommitField()) return;
        try
        {
            bool ready = _operation.PrepareAsync().GetAwaiter().GetResult();
            _status.Text = ready ? SetupTerminalText.Get("PreviewReady") + " " + _operation.Preparation!.Digest
                : SetupTerminalText.Get("DraftInvalid");
        }
        catch (SetupDefinitionException) { Status("DraftInvalid"); }
        RefreshCommands();
    }

    internal void Save()
    {
        _operation.FileName = _fileName.Text;
        if (!_operation.IsPrepared || !SetupTerminalFileName.IsSafe(_fileName.Text)) { Status("InvalidOutput"); return; }
        Status("Writing");
        _workspace.ExecuteCommand.Execute(Guid.CreateVersion7());
    }

    internal void ClearPrivateState()
    {
        _loading = true;
        _masked.ClearSensitiveState();
        _operation.ClearPrivateState();
        _loading = false;
        ShowField();
        Status("PrivateCleared");
    }

    private void CloseOrCancel()
    {
        bool wasBusy = _workspace.IsBusy;
        ClearPrivateState();
        if (wasBusy) { Status("Cancelled"); return; }
        _application.RequestStop(this);
    }

    internal void RequestStopFromSignal()
    {
        ClearPrivateState();
        _application.RequestStop(this);
    }

    private void OperationInvalidated(object? sender, EventArgs args)
    {
        _workspace.Deactivate();
        _workspace.Activate();
    }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SetupPresentationWorkspace.IsBusy))
            _application.Invoke(_ => UpdateResult());
    }

    private void UpdateResult()
    {
        if (_workspace.Result is SetupTerminalArtifactResult result)
        {
            ExitCode = result.Status == ProtectedArtifactStatus.Written ? 0 : 4;
            _status.Text = SetupTerminalText.FormatResult(result);
            _loading = true;
            _masked.ClearSensitiveState();
            _loading = false;
            _operation.ClearPrivateState();
            ShowField();
        }
        RefreshCommands();
    }

    private void Status(string key) { _status.Text = SetupTerminalText.Get(key); _status.SetNeedsDraw(); }

    private void RefreshCommands()
    {
        _save.Enabled = !_tooSmall && !_workspace.IsBusy && _operation.ProtectedOutputAvailable
            && _operation.IsPrepared && SetupTerminalFileName.IsSafe(_fileName.Text);
        _prepare.Enabled = !_tooSmall && !_workspace.IsBusy;
    }

    private void ViewportUpdated(object? sender, DrawEventArgs args) => ApplyViewportPolicy(args.NewViewport.Size);
    internal void ApplyViewportPolicy(Size size)
    {
        _tooSmall = size.Width < MinimumTerminalWidth || size.Height < MinimumTerminalHeight;
        foreach (View view in _normal) view.Visible = !_tooSmall;
        _smallNotice.Visible = _tooSmall;
        SetEditorVisibility();
        RefreshCommands();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _workspace.PropertyChanged -= WorkspaceChanged;
            _operation.Invalidated -= OperationInvalidated;
            ViewportChanged -= ViewportUpdated;
            _masked.ClearSensitiveState();
            _workspace.Deactivate();
            _operation.ClearPrivateState();
        }
        base.Dispose(disposing);
    }
}
