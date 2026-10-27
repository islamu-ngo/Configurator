namespace Configurator.Terminal;

using System.ComponentModel;
using System.Drawing;
using System.Security.Cryptography;
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

internal sealed class SetupTerminalWindow : Window
{
    internal const int MinimumTerminalWidth = 80;
    internal const int MinimumTerminalHeight = 26;
    private readonly IApplication _application;
    private readonly SetupPresentationWorkspace _workspace;
    private readonly SetupTerminalArtifactOperation _operation;
    private readonly SetupTerminalSecretBuffer _secret;
    private readonly SetupTerminalFieldEditor _editor;
    private readonly SetupTerminalImportOperation _importOperation;
    private readonly Action<Action> _dispatch;
    private readonly TimeProvider _timeProvider;
    private readonly WeakReference<SetupTerminalWindow> _owner;
    private readonly Label _fieldLabel;
    private readonly Label _help;
    private readonly Label _artifactLabel;
    private readonly TextField _status;
    private readonly Label _smallNotice;
    private readonly TextField _value;
    private readonly SetupSecretTextField _masked;
    private readonly TextField _importPath;
    private readonly Button _import;
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
    private readonly Button[] _listButtons;
    private readonly View[] _normal;
    private SetupTerminalFieldEditor.Slot? _slot;
    private int _fieldIndex;
    private int _itemIndex;
    private int _outcomeIndex;
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

    internal SetupTerminalWindow(IApplication application, SetupPresentationWorkspace workspace,
        SetupTerminalArtifactOperation operation, SetupTerminalSecretBuffer secret,
        Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>>? readImport = null,
        Action<Action>? dispatch = null, TimeProvider? timeProvider = null)
    {
        _application = application;
        _workspace = workspace;
        _operation = operation;
        _secret = secret;
        _dispatch = dispatch ?? application.Invoke;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _owner = new(this);
        _importOperation = new(operation.Form, _dispatch, readImport);
        _importOperation.Settled += ImportSettled;
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
        _experience = Button("Choose", 1, 2, CycleExperience);
        _experience.Width = Dim.Fill(2);
        _optionalEditors = Button("Choose", 1, 3, ToggleOptionalEditors);
        _optionalEditors.Width = Dim.Fill(2);
        _fieldLabel = new Label { X = 1, Y = 4, Width = Dim.Fill(2) };
        _value = new TextField { X = 1, Y = 5, Width = Dim.Fill(2) };
        _masked = new SetupSecretTextField(secret) { X = 1, Y = 5, Width = Dim.Fill(2), Visible = false };
        _help = new Label { X = 1, Y = 6, Width = Dim.Fill(2), Height = 2 };
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
        var artifact = Button("Artifact", 1, 12, CycleArtifact);
        var selectOutput = Button("SelectOutput", 27, 12, ToggleOutput);
        _artifactLabel = new Label { X = 1, Y = 13, Width = Dim.Fill(2) };
        var fileLabel = new Label { X = 1, Y = 14, Text = SetupTerminalText.Get("ImportFile") };
        _importPath = new TextField { X = 20, Y = 14, Width = Dim.Fill(2) };
        _import = Button("ImportSelected", 1, 15, ImportSelected);
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
            Text = SetupTerminalText.Get("Ready")
        };
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
        Add(heading, _experience, _optionalEditors, _fieldLabel, _value, _masked, _help, _choice, _supplyContainer, _clearContainer, previous, next, apply,
            _artifactLabel, artifact, selectOutput, fileLabel, _importPath, _import,
            validate, _prepare, _save, _nextOutcome, _nextAttempt, _status,
            clear, close, limitations, previousItem, nextItem, removeItem, _addItem);
        _normal = SubViews.ToArray();
        Add(_smallNotice);
        _value.ValueChanged += (_, _) => EditorChanged();
        _masked.ValueChanged += (_, _) => EditorChanged();
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
        _workspace.PropertyChanged += WorkspaceChanged;
        ViewportChanged += ViewportUpdated;
        ShowArtifact();
        ShowField();
    }

    internal int ExitCode { get; private set; } = 4;
    internal string StatusText => _status.Text;
    internal SetupSecretTextField MaskedInput => _masked;
    internal TextField PublicInput => _value;
    internal Task ImportCompletion => _importOperation.Completion;
    internal bool IsImportBusy => _importOperation.IsBusy;
    internal Task ShutdownCompletion => _shutdown;
    internal Task SaveCompletion => _saveObservation;
    private IReadOnlyList<SetupTerminalFieldEditor.Slot> VisibleSlots =>
        _editor.Slots.Where(slot => _operation.Form.IsEditorVisible(slot.FullPath)).ToArray();

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
        button.Accepting += (_, args) =>
        {
            args.Handled = true;
            if (Volatile.Read(ref _closeRequested) != 0) return;
            if (key != "RemoveItem") _confirmRemove = false;
            action();
        };
        return button;
    }

    private static string Safe(string text) => new(text.Select(character =>
        char.IsControl(character) ? ' ' : character).ToArray());

    private void EditorChanged()
    {
        if (_loading) return;
        _draftEdited = true;
        _confirmRemove = false;
        _importOperation.RequestCancellationAsync();
        _operation.Invalidate();
        Status(CommitField() ? "InputChanged" : "DraftInvalid");
        RefreshCommands();
    }

    internal bool CommitField()
    {
        if (Volatile.Read(ref _closeRequested) != 0) return false;
        if (_slot is null) return true;
        if (!_draftEdited) return _draftValid;
        _draftEdited = false;
        byte[] bytes = [];
        try
        {
            string text = _value.Text;
            if (_slot.RequiresMasking)
            {
                bytes = _secret.Count == 0 ? [] : _secret.CopyUtf8Bytes();
                text = Encoding.UTF8.GetString(bytes);
            }
            _draftValid = SetupTerminalFieldEditor.Write(_slot, text, _itemIndex);
            if (!_draftValid)
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
        var slots = VisibleSlots;
        if (slots.Count == 0) { ShowField(); return; }
        int current = _slot is null ? 0 : slots.ToList().FindIndex(slot =>
            slot.Input == _slot.Input && slot.Path.SequenceEqual(_slot.Path));
        _fieldIndex = (Math.Max(0, current) + delta + slots.Count) % slots.Count;
        if (_slot is { IsObjectList: true } collection && delta > 0)
        {
            string[] rowPath = [.. collection.Path, _itemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            int firstRowField = slots.ToList().FindIndex(slot => slot.Input == collection.Input &&
                slot.Path.Length > rowPath.Length && slot.Path.Take(rowPath.Length).SequenceEqual(rowPath));
            if (firstRowField >= 0) _fieldIndex = firstRowField;
        }
        _itemIndex = 0;
        ShowField(preserveLocation: false);
        FocusInput();
    }

    private void MoveItem(int delta)
    {
        if (_slot is null || !_slot.IsList || !CommitField()) return;
        int requested = _itemIndex + delta;
        _itemIndex = Math.Clamp(requested, 0, SetupTerminalFieldEditor.LastItemPosition(_slot));
        ShowField();
        if (requested > _itemIndex) Status("ListBound");
        FocusInput();
    }

    private void RemoveItem()
    {
        if (_slot is null || !_slot.IsList) return;
        if (!_confirmRemove && SetupTerminalFieldEditor.HasItem(_slot, _itemIndex))
        {
            _confirmRemove = true;
            Status("ConfirmRemove");
            return;
        }
        SetupTerminalFieldEditor.Remove(_slot, _itemIndex);
        _itemIndex = Math.Min(_itemIndex, Math.Max(0, SetupTerminalFieldEditor.ItemCount(_slot) - 1));
        ShowField();
        FocusInput();
    }

    private void AddItem()
    {
        if (_slot is not { IsList: true } collection || !CommitField()) return;
        if (SetupTerminalFieldEditor.ItemCount(collection) >= SetupTerminalFieldEditor.MaximumItems(collection))
        {
            Status("ListBound");
            return;
        }
        if (!collection.IsObjectList)
        {
            if (!SetupTerminalFieldEditor.AddItem(collection)) { Status("DraftInvalid"); return; }
            _itemIndex = SetupTerminalFieldEditor.ItemCount(collection) - 1;
            ShowField();
            FocusInput();
            return;
        }
        if (!SetupTerminalFieldEditor.AddItem(collection)) { Status("DraftInvalid"); return; }
        _itemIndex = SetupTerminalFieldEditor.ItemCount(collection) - 1;
        MoveField(1);
    }

    private void SupplyContainer()
    {
        if (!CommitField()) return;
        if (_slot is null || !SetupTerminalFieldEditor.Supply(_slot)) { Status("DraftInvalid"); return; }
        ShowField();
        FocusInput();
    }

    private void ClearContainer()
    {
        if (_slot is null || !SetupTerminalFieldEditor.Clear(_slot)) { Status("DraftInvalid"); return; }
        _itemIndex = 0;
        ShowField();
        FocusInput();
    }

    private void ShowField(bool preserveLocation = true)
    {
        _loading = true;
        try
        {
            _confirmRemove = false;
            _draftEdited = false;
            _draftValid = true;
            _masked.ClearSensitiveState();
            _value.Text = string.Empty;
            var slots = VisibleSlots;
            if (preserveLocation && _slot is not null)
            {
                int current = slots.ToList().FindIndex(slot => slot.Input == _slot.Input && slot.Path.SequenceEqual(_slot.Path));
                if (current >= 0) _fieldIndex = current;
            }
            _fieldIndex = Math.Clamp(_fieldIndex, 0, Math.Max(0, slots.Count - 1));
            _slot = slots.Count == 0 ? null : slots[_fieldIndex];
            if (_slot?.IsList == true) _itemIndex = Math.Clamp(_itemIndex, 0, SetupTerminalFieldEditor.LastItemPosition(_slot));
            _fieldLabel.Text = _slot is null ? SetupTerminalText.Get("NoFields")
                : Safe($"{_fieldIndex + 1}/{slots.Count} {string.Join('/', _slot.FullPath)} / {_slot.Title} ({_slot.ScalarType})"
                    + (_slot.IsList ? $" [{_itemIndex + 1}/{SetupTerminalFieldEditor.ItemCount(_slot) + 1}]" : ""));
            _help.Text = _slot is null ? string.Empty : Safe(_slot.Help);
            if (_slot is not null)
            {
                string text = SetupTerminalFieldEditor.Read(_slot, _itemIndex);
                if (_slot.RequiresMasking && text.Length > 0) _masked.TryReplaceSensitiveInput(text);
                else if (!_slot.RequiresMasking) _value.Text = text;
            }
            SetEditorVisibility();
            var experience = _operation.Form.Experiences.Single(item => item.Id == _operation.Form.SelectedExperienceId);
            _experience.Text = Safe(experience.Title);
            _experience.Enabled = _operation.Form.Experiences.Count > 1;
            _optionalEditors.Text = _operation.Form.OptionalEditorsExpanded
                ? "Restore default disclosure" : "Show optional editors";
            RefreshCommands();
        }
        finally { _loading = false; }
    }

    private void SetEditorVisibility()
    {
        bool scalar = !_tooSmall && _slot is { IsObject: false, IsObjectList: false }
            && (!_slot.IsList || SetupTerminalFieldEditor.MaximumItems(_slot) > 0);
        _value.Visible = scalar && _slot is { RequiresMasking: false };
        _masked.Visible = scalar && _slot is { RequiresMasking: true };
        _addItem.Visible = !_tooSmall && _slot is { IsList: true };
        _supplyContainer.Visible = !_tooSmall && _slot is { IsContainer: true };
        _clearContainer.Visible = !_tooSmall && _slot is not null;
        _clearContainer.Text = _slot is { IsContainer: true } ? SetupTerminalText.Get("ClearContainer") : "Omit value";
        _choice.Visible = !_tooSmall && _slot is not null && SetupTerminalFieldEditor.Choices(_slot).Count > 0;
        foreach (var button in _listButtons) button.Visible = !_tooSmall && _slot is { IsList: true };
    }

    private void FocusInput()
    {
        if (_slot?.IsObject == true) _supplyContainer.SetFocus();
        else if (_slot?.IsObjectList == true) _addItem.SetFocus();
        else if (_slot is { IsList: true } && SetupTerminalFieldEditor.MaximumItems(_slot) == 0) _supplyContainer.SetFocus();
        else if (_slot?.RequiresMasking == true) _masked.SetFocus();
        else _value.SetFocus();
    }

    internal void CycleExperience()
    {
        var form = _operation.Form;
        var experiences = form.Experiences;
        int current = experiences.ToList().FindIndex(item => item.Id == form.SelectedExperienceId);
        form.SelectExperience(experiences[(current + 1) % experiences.Count].Id);
    }

    internal void ToggleOptionalEditors() =>
        _operation.Form.SetAllSectionsExpanded(!_operation.Form.OptionalEditorsExpanded);

    private void FormViewChanged(object? sender, EventArgs args) =>
        DispatchToOwner(static window =>
        {
            if (Volatile.Read(ref window._closeRequested) == 0) window.ShowField();
        });

    private void CycleChoice()
    {
        if (_slot is null) return;
        var choices = SetupTerminalFieldEditor.Choices(_slot);
        if (choices.Count == 0) return;
        if (!CommitField()) return;
        string current = SetupTerminalFieldEditor.Read(_slot, _itemIndex);
        string next = choices[(choices.ToList().IndexOf(current) + 1) % choices.Count];
        if (!SetupTerminalFieldEditor.Write(_slot, next, _itemIndex)) { Status("DraftInvalid"); return; }
        ShowField();
        FocusInput();
    }

    private void CycleArtifact()
    {
        if (!CommitField()) return;
        CancelImport();
        var artifacts = _operation.Form.Definition.Artifacts;
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
        var artifact = _operation.Form.Definition.Artifacts.Single(item => item.Id == _operation.ArtifactId);
        _artifactLabel.Text = Safe($"{artifact.Id} | "
            + $"{(_operation.CurrentArtifactSelected ? SetupTerminalText.Get("Selected") : SetupTerminalText.Get("NotSelected"))} "
            + $"{_operation.SelectedArtifactIds.Count}/{_operation.Form.Definition.Artifacts.Count} | "
            + $"{artifact.FileName} | {artifact.Format} | {artifact.ApplySemantics} | {artifact.Restart}");
    }

    private void ImportSelected()
    {
        if (_importOperation.IsBusy || _workspace.IsBusy || _operation.IsPublishing || !CommitField()) return;
        string path = _importPath.Text;
        _loading = true;
        _importPath.Text = string.Empty;
        _loading = false;
        if (!_importOperation.Start(path, _operation.ArtifactId)) return;
        Status("ImportReading");
        RefreshCommands();
    }

    private void CancelImport()
    {
        bool pending = _importOperation.IsBusy;
        _importOperation.RequestCancellationAsync();
        if (pending) Status("ImportCancelled");
    }

    private void ImportSettled(object? sender, SetupTerminalImportResult result)
    {
        if (_disposed || Volatile.Read(ref _closeRequested) != 0) return;
        if (result.IsCurrent)
        {
            if (result.Succeeded)
            {
                ShowField();
                FocusInput();
                Status("ImportReady");
            }
            else _status.Text = SetupTerminalText.Get("ImportFailed") + " " + result.Code;
        }
        RefreshCommands();
    }

    internal void Validate()
    {
        if (!CommitField()) return;
        try
        {
            var validation = _operation.Form.Validate();
            _status.Text = validation.IsReady && !_operation.Form.HasInputErrors
                ? SetupTerminalText.Get("PreviewReady")
                : SetupTerminalText.Get("DraftInvalid") + " " +
                    string.Join(", ", validation.Diagnostics.Select(item => Safe(item.Code + ":" +
                        JsonSerializer.Serialize(new[] { item.FieldId }.Concat(item.RelativePath)))));
        }
        catch (SetupDefinitionException) { Status("DraftInvalid"); }
    }

    internal void Prepare()
    {
        if (_importOperation.IsBusy || _operation.IsPublishing || Volatile.Read(ref _closeRequested) != 0) return;
        if (!CommitField()) return;
        try
        {
            bool ready = _operation.PrepareAsync().GetAwaiter().GetResult();
            _status.Text = ready ? SetupTerminalText.Get("PreviewReady") + " "
                + _operation.Preparation!.ArtifactCount + " " + SetupTerminalText.Get("Selected")
                : SetupTerminalText.Get("DraftInvalid");
        }
        catch (SetupDefinitionException) { Status("DraftInvalid"); }
        RefreshCommands();
    }

    internal void Save()
    {
        if (_importOperation.IsBusy || _operation.IsPublishing || Volatile.Read(ref _closeRequested) != 0) return;
        if (!_operation.IsPrepared) { Status("InvalidOutput"); return; }
        ExitCode = 4;
        _receipt = null;
        Status("Writing");
        Task execution = _workspace.ExecuteAsync(Guid.CreateVersion7());
        _saveObservation = ObserveSaveAsync(execution, _dispatch, _owner, _callbackGeneration);
        Observe(_saveObservation);
        UpdatePublication();
        RefreshCommands();
    }

    private static async Task ObserveSaveAsync(Task execution, Action<Action> dispatch,
        WeakReference<SetupTerminalWindow> owner, int generation)
    {
        try { await execution.ConfigureAwait(false); }
        catch (Exception) when (execution.IsFaulted || execution.IsCanceled)
        {
            TryDispatch(dispatch, () =>
            {
                if (!owner.TryGetTarget(out SetupTerminalWindow? window) || window._disposed
                    || window._callbackGeneration != generation || Volatile.Read(ref window._closeRequested) != 0) return;
                window.UpdatePublication();
                if (window._receipt is null)
                    window._status.Text = SetupTerminalText.Get("InvalidOutput") + " terminal-operation-failed";
                window.RefreshCommands();
            });
        }
    }

    internal void ClearPrivateState()
    {
        _importOperation.RequestCancellationAsync();
        _loading = true;
        _masked.ClearSensitiveState();
        _importPath.Text = string.Empty;
        _operation.ClearPrivateState();
        _loading = false;
        ShowField();
        Status("PrivateCleared");
    }

    private void CloseOrCancel()
    {
        RequestStopFromSignal();
    }

    internal void RequestStopFromSignal()
    {
        _secret.Clear();
        if (Volatile.Read(ref _disposed) || Interlocked.Exchange(ref _closeRequested, 1) != 0) return;
        Observe(_importOperation.RequestCancellationAsync());
        Observe(_workspace.RequestCancellationAsync());
        DispatchToOwner(static window => window.BeginClose());
    }

    private void BeginClose()
    {
        ClearPrivateState();
        foreach (View view in _normal) view.Enabled = false;
        Status("ShutdownWaiting");
        Task pending = Task.WhenAll(_importOperation.Completion, _workspace.WaitForPendingOperationsAsync());
        Observe(pending);
        _shutdown = FinishCloseAsync(pending, _timeProvider, _dispatch, _owner, _callbackGeneration);
        Observe(_shutdown);
    }

    private static async Task FinishCloseAsync(Task pending, TimeProvider timeProvider, Action<Action> dispatch,
        WeakReference<SetupTerminalWindow> owner, int generation)
    {
        bool unknown = false;
        try { await pending.WaitAsync(TimeSpan.FromSeconds(2), timeProvider).ConfigureAwait(false); }
        catch (TimeoutException) { unknown = true; }
        catch (Exception) when (pending.IsFaulted || pending.IsCanceled) { unknown = true; }
        TryDispatch(dispatch, () =>
        {
            if (!owner.TryGetTarget(out SetupTerminalWindow? window) || window._disposed
                || window._callbackGeneration != generation) return;
            if (unknown)
            {
                window._operation.MarkPublicationUnknown();
                window.Status("ShutdownUnknown");
            }
            ProtectedArtifactPublicationReceipt? receipt = window._operation.LatestPublication;
            if (receipt is not null)
                window._status.Text = (unknown ? SetupTerminalText.Get("ShutdownUnknown") + " " : string.Empty)
                    + SetupTerminalText.FormatClosingReceipt(receipt);
            window.DetachObservers();
            window._allowStop = true;
            window._application.RequestStop(window);
        });
    }

    private void DispatchToOwner(Action<SetupTerminalWindow> callback)
    {
        WeakReference<SetupTerminalWindow> owner = _owner;
        int generation = Volatile.Read(ref _callbackGeneration);
        TryDispatch(_dispatch, () =>
        {
            if (owner.TryGetTarget(out SetupTerminalWindow? window) && !window._disposed
                && window._callbackGeneration == generation) callback(window);
        });
    }

    protected override bool OnIsRunningChanging(bool oldIsRunning, bool newIsRunning)
    {
        if (!newIsRunning && !_allowStop && !_disposed)
        {
            RequestStopFromSignal();
            return !_allowStop;
        }
        return base.OnIsRunningChanging(oldIsRunning, newIsRunning);
    }

    private static void TryDispatch(Action<Action> dispatch, Action callback)
    {
        try { dispatch(callback); }
        catch (Exception exception) when (exception is NotInitializedException or ObjectDisposedException) { }
    }

    private static void Observe(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    private void DetachObservers()
    {
        if (_observersDetached) return;
        _observersDetached = true;
        Interlocked.Increment(ref _callbackGeneration);
        _importOperation.Settled -= ImportSettled;
        _importOperation.Dispose();
        _workspace.PropertyChanged -= WorkspaceChanged;
        _operation.Invalidated -= OperationInvalidated;
        _operation.PublicationChanged -= PublicationChanged;
        _operation.Form.ViewChanged -= FormViewChanged;
        ViewportChanged -= ViewportUpdated;
    }

    private void OperationInvalidated(object? sender, EventArgs args)
    {
        _workspace.Deactivate();
        _workspace.Activate();
    }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SetupPresentationWorkspace.IsBusy))
            DispatchToOwner(static window =>
            {
                if (Volatile.Read(ref window._closeRequested) == 0) window.UpdateResult();
            });
    }

    private void PublicationChanged(object? sender, EventArgs args)
    {
        if (Interlocked.Exchange(ref _publicationRefreshQueued, 1) != 0) return;
        DispatchToOwner(static window =>
        {
            Interlocked.Exchange(ref window._publicationRefreshQueued, 0);
            if (Volatile.Read(ref window._closeRequested) != 0) return;
            window.UpdatePublication();
            window.RefreshCommands();
        });
    }

    private IReadOnlyList<ProtectedArtifactPublicationReceipt> PublicationReceipts()
    {
        IReadOnlyList<ProtectedArtifactPublicationReceipt> history = _operation.PublicationHistory;
        return _operation.CurrentPublication is { } current ? [.. history, current] : history;
    }

    private void UpdatePublication()
    {
        IReadOnlyList<ProtectedArtifactPublicationReceipt> receipts = PublicationReceipts();
        ProtectedArtifactPublicationReceipt? receipt = receipts.FirstOrDefault(item => item.AttemptId == _receipt?.AttemptId)
            ?? (receipts.Count == 0 ? null : receipts[^1]);
        if (receipt is null) return;
        if (_receipt?.AttemptId != receipt.AttemptId) _outcomeIndex = 0;
        _receipt = receipt;
        ShowOutcome();
    }

    private void UpdateResult()
    {
        if (_workspace.Result is SetupTerminalArtifactResult result)
        {
            ExitCode = result.Succeeded ? 0 : 4;
            UpdatePublication();
            _loading = true;
            _masked.ClearSensitiveState();
            _importPath.Text = string.Empty;
            _loading = false;
            _operation.ClearPrivateState();
            ShowField();
        }
        RefreshCommands();
    }

    internal void PresentReceipt(ProtectedArtifactPublicationReceipt receipt)
    {
        _receipt = receipt;
        _outcomeIndex = 0;
        ShowOutcome();
    }

    private void NextAttempt()
    {
        IReadOnlyList<ProtectedArtifactPublicationReceipt> receipts = PublicationReceipts();
        if (receipts.Count == 0) return;
        int index = receipts.ToList().FindIndex(item => item.AttemptId == _receipt?.AttemptId);
        PresentReceipt(receipts[(index + receipts.Count - 1) % receipts.Count]);
    }

    private void NextOutcome()
    {
        if (_receipt is null || _receipt.Outcomes.Count == 0) return;
        _outcomeIndex = (_outcomeIndex + 1) % _receipt.Outcomes.Count;
        ShowOutcome();
    }

    private void ShowOutcome()
    {
        if (_receipt is null) return;
        _status.Text = SetupTerminalText.FormatReceipt(_receipt, _outcomeIndex);
        _nextOutcome.Visible = !_tooSmall && _receipt.Outcomes.Count > 1;
        _nextAttempt.Visible = !_tooSmall;
        _status.SetNeedsDraw();
    }

    private void Status(string key) { _status.Text = SetupTerminalText.Get(key); _status.SetNeedsDraw(); }

    private void RefreshCommands()
    {
        bool available = !_tooSmall && !_workspace.IsBusy && !_importOperation.IsBusy
            && !_operation.IsPublishing && Volatile.Read(ref _closeRequested) == 0;
        _save.Enabled = available && _operation.ProtectedOutputAvailable
            && _operation.IsPrepared;
        _prepare.Enabled = available;
        _import.Enabled = available && !string.IsNullOrWhiteSpace(_importPath.Text);
    }

    private void ViewportUpdated(object? sender, DrawEventArgs args) => ApplyViewportPolicy(args.NewViewport.Size);
    internal void ApplyViewportPolicy(Size size)
    {
        _tooSmall = size.Width < MinimumTerminalWidth || size.Height < MinimumTerminalHeight;
        foreach (View view in _normal) view.Visible = !_tooSmall;
        _nextOutcome.Visible = !_tooSmall && _receipt is { Outcomes.Count: > 1 };
        _nextAttempt.Visible = !_tooSmall && _receipt is not null;
        _smallNotice.Visible = _tooSmall;
        SetEditorVisibility();
        RefreshCommands();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            DetachObservers();
            _masked.ClearSensitiveState();
            _importPath.Text = string.Empty;
            _workspace.Deactivate();
            _operation.ClearPrivateState();
        }
        base.Dispose(disposing);
    }
}
