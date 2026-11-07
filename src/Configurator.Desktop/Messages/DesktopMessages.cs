namespace Configurator.Desktop.Messages;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Definitions;
using Configurator.Presentation;

public sealed record DefinitionLoadedMessage(Definition Definition);
public sealed record DefinitionClearedMessage;
public sealed record FormWorkspaceChangedMessage(DefinitionFormModel? Form);
public sealed record FormInputsChangedMessage;
public sealed record InputsInvalidatedMessage;
public sealed record WorkspaceInvalidatedMessage(long Generation);
public sealed record OperationStateMessage(bool IsBusy, string? Kind);
public sealed record StatusChangedMessage(string Status, long? Generation = null,
    DesktopStatusGuidance Guidance = DesktopStatusGuidance.General);
public sealed record FocusRequestedMessage(string Target);
public sealed record FormFocusRequestedMessage;
public sealed record FormValidationRequestedMessage(Action<bool> Reply);
public sealed record OperationRequestedMessage(string Kind, Action<DesktopOperation?> Reply,
    CancellationToken CancellationToken);

/// <summary>Owns physical settlement independently of owner-context adoption and UI release.</summary>
public sealed class DesktopOperation : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<bool> _isCurrent;
    private readonly Action _complete;
    private readonly SynchronizationContext? _owner;
    private readonly CancellationTokenSource _cancellation;
    private readonly CancellationToken _requestedCancellation;
    private bool _completed;
    private bool _explicitCancellation;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DesktopOperation(long generation, Func<bool> isCurrent, Action complete, CancellationToken token)
    {
        Generation = generation;
        _isCurrent = isCurrent;
        _complete = complete;
        _owner = SynchronizationContext.Current;
        _requestedCancellation = token;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Token = _cancellation.Token;
    }

    public long Generation { get; }
    public CancellationToken Token { get; }
    public bool IsCurrent => !_completed && !Token.IsCancellationRequested && _isCurrent();
    public Task Completion => _completion.Task;
    internal bool WasCancelled => _requestedCancellation.IsCancellationRequested || _explicitCancellation;

    public static DesktopOperation? Begin(IMessenger messenger, string kind,
        CancellationToken cancellationToken = default)
    {
        DesktopOperation? operation = null;
        messenger.Send(new OperationRequestedMessage(kind, value => operation = value, cancellationToken));
        return operation;
    }

    internal void Cancel(bool explicitlyRequested = false)
    {
        lock (_gate)
        {
            if (_completed) return;
            _explicitCancellation |= explicitlyRequested;
            _cancellation.Cancel();
        }
    }

    /// <summary>Cancellation retires queued adoption, but never releases storage underneath an executing adoption.</summary>
    internal async Task<bool> TryApplyAsync(Action apply)
    {
        var applied = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool executing = false;
        using CancellationTokenRegistration registration = Token.Register(() =>
        {
            lock (_gate)
                if (!executing) applied.TrySetResult(false);
        });
        void Apply()
        {
            lock (_gate)
            {
                if (applied.Task.IsCompleted) return;
                executing = true;
                try
                {
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                    Configurator.Engineering.MeasureNativePreparation.Mark("adoption-start");
#endif
                    if (IsCurrent) apply();
                    applied.TrySetResult(IsCurrent);
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
                    Configurator.Engineering.MeasureNativePreparation.Mark("adoption-end");
#endif
                }
                catch (Exception exception) { applied.TrySetException(exception); }
                finally { executing = false; }
            }
        }
        if (_owner is null || ReferenceEquals(SynchronizationContext.Current, _owner)) Apply();
        else _owner.Post(_ => Apply(), null);
        return await applied.Task.ConfigureAwait(false);
    }

    internal void Finish(Action? apply = null)
    {
        lock (_gate)
        {
            if (_completed) return;
            _completed = true;
        }
        _cancellation.Dispose();
        void Complete()
        {
            try
            {
                if (!Token.IsCancellationRequested && _isCurrent()) apply?.Invoke();
            }
            finally { _complete(); }
        }
        try
        {
            if (_owner is null || ReferenceEquals(SynchronizationContext.Current, _owner)) Complete();
            else _owner.Post(_ => Complete(), null);
        }
        finally
        {
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
            Configurator.Engineering.MeasureNativePreparation.Mark("physical-settled");
#endif
            _completion.TrySetResult();
        }
    }

    public void Dispose() => Finish();
}
