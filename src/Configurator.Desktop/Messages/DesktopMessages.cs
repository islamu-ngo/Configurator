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

/// <summary>A root-owned operation lease. Completion never releases a different operation.</summary>
public sealed class DesktopOperation(long generation, Func<bool> isCurrent, Action complete,
    CancellationToken token) : IDisposable
{
    private bool _completed;
    public long Generation { get; } = generation;
    public CancellationToken Token { get; } = token;
    public bool IsCurrent => !_completed && isCurrent();

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
        complete();
    }
}
