namespace Configurator.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Desktop.Messages;

/// <summary>Coordinates operation lifetime and status without owning field or artifact behavior.</summary>
public sealed partial class DesktopShellViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private CancellationTokenSource? _operationCancellation;
    private DesktopOperation? _activeOperation;
    private long _generation;
    private bool _disposed;

    [ObservableProperty]
    private bool _isBusy;
    [ObservableProperty]
    private bool _isSaving;
    [ObservableProperty]
    private bool _isSelecting;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusHelp))]
    private string _status = "definition-required";
    [ObservableProperty]
    private string _focusTarget = "DefinitionPath";
    [ObservableProperty]
    private int _focusRequestGeneration = 1;

    public DesktopShellViewModel(IMessenger messenger, DefinitionLoaderViewModel loader,
        FormWorkspaceViewModel workspace, ArtifactPublisherViewModel publisher)
    {
        _messenger = messenger;
        Loader = loader;
        Workspace = workspace;
        Publisher = publisher;
        messenger.Register<DesktopShellViewModel, OperationRequestedMessage>(this,
            static (root, message) => root.Begin(message));
        messenger.Register<DesktopShellViewModel, InputsInvalidatedMessage>(this,
            static (root, _) => root.Invalidate());
        messenger.Register<DesktopShellViewModel, FormInputsChangedMessage>(this,
            static (root, _) => root.Invalidate());
        messenger.Register<DesktopShellViewModel, DefinitionClearedMessage>(this,
            static (root, _) => root.Invalidate());
        messenger.Register<DesktopShellViewModel, StatusChangedMessage>(this, static (root, message) =>
        {
            if (!root._disposed && (message.Generation is null || message.Generation == root._generation))
                root.Status = message.Status;
        });
        messenger.Register<DesktopShellViewModel, FocusRequestedMessage>(this, static (root, message) =>
        {
            if (root._disposed) return;
            root.FocusTarget = message.Target;
            root.FocusRequestGeneration = checked(root.FocusRequestGeneration + 1);
        });
    }

    public DefinitionLoaderViewModel Loader { get; }
    public FormWorkspaceViewModel Workspace { get; }
    public ArtifactPublisherViewModel Publisher { get; }
    public string StatusHelp => Status.Split(':')[0] switch
    {
        "definition-required" => "Choose a local JSON or YAML definition, then load it.",
        "definition-invalid" or "definition-unreadable" => "Check the local definition and its supported format, size and permissions, then load it again.",
        "validation-invalid" => "Correct the marked fields, then validate or prepare again.",
        "bundle-invalid" or "preparation-invalid" => "Correct the answers or select active declared outputs, then prepare again.",
        "selection-unavailable" => "Native selection is unavailable. Enter a local path and use the adjacent action.",
        "selection-nonlocal" => "Choose a file or folder on this computer; remote locations are not accepted.",
        "import-unreadable" => "Check the existing file and its permissions, then retry the import.",
        "import-unsupported" or "inverse-unsupported" or "inverse-ambiguous" => "Select an artifact with a supported unambiguous inverse mapping, then retry the import.",
        "answer-conflict" => "Resolve conflicting current answers before retrying the whole import.",
        "input-invalid" or "input-ambiguous" or "import-field-invalid" => "Correct the existing file's format and declared field values, then retry the whole import.",
        "save-failed" or "bundle-save-blocked" => "Check host protection, the existing destination and file conflicts; prepare again before retrying.",
        "save-installed-unverified" => "Installation occurred, but verification did not succeed. Inspect the destination manually; its current contents are not guaranteed. Keep existing files and use a fresh destination before retrying.",
        "save-installed-verified" => "Installation was verified, but cleanup failed. Keep the file and review the cleanup problem; retrying cannot overwrite it.",
        "bundle-save-partial" => "Keep installed files and inspect unverified destinations manually. Select only failed or unattempted outputs, resolve the problem and prepare again using fresh destinations.",
        "save-cancelled" or "bundle-save-cancelled" or "operation-cancelled" => "Preparation was discarded. Prepare current answers again when ready.",
        "inputs-changed" => "Validate and prepare the current answers before saving.",
        _ => "Local preparation does not apply configuration to the target application."
    };

    private void Begin(OperationRequestedMessage message)
    {
        if (_disposed || IsBusy)
        {
            message.Reply(null);
            return;
        }
        long generation = _generation;
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        CancellationTokenSource cancellation = _operationCancellation;
        SetOperationState(true, message.Kind);
        _activeOperation = new DesktopOperation(generation,
            () => !_disposed && generation == _generation && !cancellation.IsCancellationRequested,
            () =>
            {
                if (ReferenceEquals(_operationCancellation, cancellation))
                {
                    _operationCancellation = null;
                    _activeOperation = null;
                    SetOperationState(false, null);
                }
                cancellation.Dispose();
            }, cancellation.Token);
        message.Reply(_activeOperation);
    }

    private void SetOperationState(bool busy, string? kind)
    {
        IsBusy = busy;
        IsSaving = busy && kind == "save";
        IsSelecting = busy && kind == "selection";
        _messenger.Send(new OperationStateMessage(busy, kind));
    }

    private void Invalidate()
    {
        checked { _generation++; }
        _operationCancellation?.Cancel();
        _messenger.Send(new WorkspaceInvalidatedMessage(_generation));
        if (!_disposed) Status = Workspace.Form is null ? "definition-required" : "inputs-changed";
    }

    [RelayCommand]
    public void Cancel()
    {
        if (_disposed) return;
        Invalidate();
        Status = "operation-cancelled";
    }

    [RelayCommand]
    private void FocusDefinitionPath() => _messenger.Send(new FocusRequestedMessage("DefinitionPath"));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Invalidate();
        _activeOperation?.Dispose();
        Loader.Dispose();
        Publisher.Dispose();
        Workspace.Dispose();
        _messenger.UnregisterAll(this);
    }

    public override string ToString() => nameof(DesktopShellViewModel);
}
