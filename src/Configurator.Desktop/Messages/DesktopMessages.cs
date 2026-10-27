namespace Configurator.Desktop.Messages;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Presentation;

public sealed record DefinitionLoadedMessage(SetupDefinition Definition);
public sealed record DefinitionClearedMessage;
public sealed record FormWorkspaceChangedMessage(DefinitionFormModel? Form);
public sealed record FormInputsChangedMessage;
public sealed record InputsInvalidatedMessage;
public sealed record WorkspaceInvalidatedMessage(long Generation);
public sealed record OperationStateMessage(bool IsBusy, string? Kind);
public sealed record StatusChangedMessage(string Status, long? Generation = null);
public sealed record FocusRequestedMessage(string Target);
public sealed record FormFocusRequestedMessage;
public sealed record FormValidationRequestedMessage(Action<bool> Reply);
public sealed record OperationRequestedMessage(string Kind, Action<DesktopOperation?> Reply,
    CancellationToken CancellationToken);

/// <summary>A consumer-owned lease, released only after its awaited work and cleanup finish.</summary>
public sealed class DesktopOperation(long generation, Func<bool> isCurrent, Action complete,
    CancellationToken token) : IDisposable
{
    private bool _completed;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public long Generation { get; } = generation;
    public CancellationToken Token { get; } = token;
    public bool IsCurrent => !_completed && isCurrent();
    public Task Completion => _completion.Task;

    public static DesktopOperation? Begin(IMessenger messenger, string kind,
        CancellationToken cancellationToken = default)
    {
        DesktopOperation? operation = null;
        messenger.Send(new OperationRequestedMessage(kind, value => operation = value, cancellationToken));
        return operation;
    }

    public void Dispose()
    {
        if (_completed) return;
        _completed = true;
        try { complete(); }
        finally { _completion.TrySetResult(); }
    }
}
