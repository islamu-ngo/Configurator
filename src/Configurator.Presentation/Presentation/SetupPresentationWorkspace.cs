namespace Configurator.Presentation;

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

public sealed partial class SetupPresentationWorkspace : ObservableRecipient,
    IRecipient<SetupOperationSettledMessage>, IDisposable
{
    private readonly object _gate = new();
    private readonly HashSet<SettlementIdentity> _received = [];
    private readonly HashSet<OperationLifetime> _pending = [];
    private readonly ISetupPresentationOperation _operation;
    private readonly SetupPresentationSession _session;
    private readonly IMessenger _sessionMessenger;
    private OperationLifetime? _active;
    private SetupPresentationOutcome? _ownedOutcome;
    private OperationLifetime? _ownedOperation;
    private SetupOperationGeneration _generation;
    private bool _disposed;
    private bool _isTerminated;
    private bool _isBusy;
    private ReadOnlyMemory<byte> _output;
    private object? _result;
    private string _publicInput = string.Empty;
    private int _receivedCompletionCount;

    internal SetupPresentationWorkspace(SetupPresentationSession session, IMessenger messenger,
        SetupWorkspaceId workspaceId, ISetupPresentationOperation operation) : base(messenger)
    {
        _session = session;
        _sessionMessenger = messenger;
        WorkspaceId = workspaceId;
        _operation = operation;
    }

    public event EventHandler<SetupCompletionDiscardedEventArgs>? CompletionDiscarded;
    public event EventHandler<SetupOperationInvalidatedEventArgs>? Invalidated;
    public int MaxPublicInputLength => 4096;
    public SetupWorkspaceId WorkspaceId { get; }
    public SetupAccessibilityStatus AccessibilityStatus => SetupAccessibilityStatus.NotEvaluated;
    public SetupOperationGeneration Generation { get { lock (_gate) return _generation; } }
    public bool IsBusy { get { lock (_gate) return _isBusy; } }
    public bool IsTerminated { get { lock (_gate) return _isTerminated; } }
    public ReadOnlyMemory<byte> Output { get { lock (_gate) return _output; } }
    public object? Result { get { lock (_gate) return _result; } }
    public string PublicInput { get { lock (_gate) return _publicInput; } }
    public int ReceivedCompletionCount { get { lock (_gate) return _receivedCompletionCount; } }

    public void Activate()
    {
        lock (_gate)
        {
            if (_disposed || _isTerminated || IsActive) return;
            IsActive = true;
        }
        NotifyState(Notifications.IsActive);
    }

    public void Deactivate() => Invalidate(SetupOperationInvalidationReason.Deactivated, preserveResult: false);

    public bool SetPublicInput(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > MaxPublicInputLength) return false;
        lock (_gate)
        {
            if (_disposed || _isTerminated) return false;
            if (_publicInput == value) return true;
            _publicInput = value;
        }
        OnPropertyChanged(nameof(PublicInput));
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteOperation))]
    public async Task ExecuteAsync(Guid operationId)
    {
        if (operationId == Guid.Empty || !_session.TryAllocate(out SetupOperationGeneration next)) return;
        OperationLifetime? previous;
        OperationLifetime current;
        Notifications notifications;
        lock (_gate)
        {
            if (_disposed || _isTerminated) return;
            State before = CaptureState();
            previous = _active;
            current = new OperationLifetime(operationId, next);
            _pending.Add(current);
            _active = current;
            _generation = next;
            _isBusy = true;
            notifications = ChangesSince(before);
        }
        Task<SetupPresentationOutcome>? pending = null;
        bool observeLate = false;
        Exception? failure = null;
        try
        {
            try
            {
                if (previous is not null)
                    RaiseInvalidated(previous.Generation, SetupOperationInvalidationReason.Replaced);
            }
            finally
            {
                if (previous is not null) Observe(previous.RequestCancellationAsync());
            }
            OnPropertyChanged(nameof(Generation));
            NotifyState(notifications);
            current.Token.ThrowIfCancellationRequested();
            pending = _operation.ExecuteAsync(current.Token);
            SetupPresentationOutcome outcome = await pending.WaitAsync(current.Token);
            Commit(current, outcome);
        }
        catch (OperationCanceledException) when (current.Token.IsCancellationRequested)
        {
            observeLate = pending is not null;
            try { Invalidate(SetupOperationInvalidationReason.Cancelled, preserveResult: true, current); }
            catch (Exception notificationFailure) { failure = notificationFailure; }
        }
        catch (Exception operationFailure)
        {
            failure = operationFailure;
            try { Invalidate(SetupOperationInvalidationReason.Faulted, preserveResult: false, current); }
            catch (Exception notificationFailure) { failure = new AggregateException(operationFailure, notificationFailure); }
        }
        finally
        {
            if (observeLate) Observe(ObserveDiscardedAsync(pending!, current));
            else
            {
                try { await RetireAsync(current); }
                catch (Exception retirementFailure)
                {
                    failure = failure is null ? retirementFailure : new AggregateException(failure, retirementFailure);
                }
            }
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel() => Invalidate(SetupOperationInvalidationReason.Cancelled, preserveResult: true);

    /// <summary>Requests cancellation without publishing model changes or running callbacks on the caller.</summary>
    public Task RequestCancellationAsync()
    {
        OperationLifetime? operation;
        lock (_gate) operation = _active;
        return operation?.RequestCancellationAsync() ?? Task.CompletedTask;
    }

    /// <summary>Waits for currently owned workers and cancellation callbacks, including discarded operations.</summary>
    public Task WaitForPendingOperationsAsync()
    {
        lock (_gate) return Task.WhenAll(_pending.Select(operation => operation.Completion));
    }

    public void Receive(SetupOperationSettledMessage message)
    {
        if (message.SessionId != _session.SessionId || message.WorkspaceId == WorkspaceId) return;
        lock (_gate)
        {
            if (_disposed || _isTerminated || !_received.Add(new(message.OperationId, message.Generation))) return;
            _receivedCompletionCount++;
        }
        OnPropertyChanged(nameof(ReceivedCompletionCount));
    }

    internal void TerminateFromSession() =>
        Invalidate(SetupOperationInvalidationReason.SessionTerminated, preserveResult: false, terminate: true);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        try { Invalidate(SetupOperationInvalidationReason.Disposed, preserveResult: false); }
        finally { _session.Release(this); }
    }

    public override string ToString() => nameof(SetupPresentationWorkspace);

    private bool CanExecuteOperation(Guid operationId)
    {
        lock (_gate) return operationId != Guid.Empty && !_disposed && !_isTerminated && !_isBusy;
    }

    private bool CanCancel()
    {
        lock (_gate) return !_disposed && !_isTerminated && _isBusy;
    }

    private void Commit(OperationLifetime operation, SetupPresentationOutcome outcome)
    {
        bool accepted;
        SetupPresentationOutcome? previous = null;
        Notifications notifications;
        lock (_gate)
        {
            State before = CaptureState();
            accepted = !_disposed && !_isTerminated && Matches(operation) && !operation.Token.IsCancellationRequested;
            if (accepted)
            {
                previous = _ownedOutcome;
                _ownedOutcome = outcome;
                _ownedOperation = operation;
                _active = null;
                _result = outcome.CoreResult;
                _output = outcome.Output;
                _isBusy = false;
            }
            notifications = ChangesSince(before);
        }
        if (!accepted)
        {
            outcome.Dispose();
            RaiseDiscarded(operation);
            return;
        }
        if (!ReferenceEquals(previous, outcome)) previous?.Dispose();
        NotifyState(notifications);
        _sessionMessenger.Send(new SetupOperationSettledMessage(_session.SessionId, WorkspaceId,
            operation.OperationId, operation.Generation, SetupOperationStatus.Succeeded));
    }

    private bool Matches(OperationLifetime operation) =>
        _active?.OperationId == operation.OperationId && _active.Generation == operation.Generation;

    private void Invalidate(SetupOperationInvalidationReason reason, bool preserveResult,
        OperationLifetime? matching = null, bool terminate = false)
    {
        OperationLifetime? operation;
        SetupPresentationOutcome? outcome = null;
        SetupOperationGeneration generation;
        Notifications notifications;
        lock (_gate)
        {
            bool matchesActive = matching is null || Matches(matching);
            if (!matchesActive && !ReferenceEquals(_ownedOperation, matching)) return;
            if (terminate && _isTerminated) return;
            State before = CaptureState();
            if (terminate) _isTerminated = true;
            if (reason is SetupOperationInvalidationReason.Deactivated or SetupOperationInvalidationReason.Disposed
                or SetupOperationInvalidationReason.SessionTerminated) IsActive = false;
            operation = matchesActive ? _active : null;
            generation = matching?.Generation ?? operation?.Generation ?? _generation;
            if (matchesActive)
            {
                _active = null;
                _isBusy = false;
            }
            if (!preserveResult)
            {
                outcome = _ownedOutcome;
                _ownedOutcome = null;
                _ownedOperation = null;
                _result = null;
                _output = ReadOnlyMemory<byte>.Empty;
            }
            notifications = ChangesSince(before);
        }
        outcome?.Dispose();
        try
        {
            NotifyAll([() => RaiseInvalidated(generation, reason), () => NotifyState(notifications)]);
        }
        finally
        {
            if (operation is not null) Observe(operation.RequestCancellationAsync());
        }
    }

    private async Task ObserveDiscardedAsync(Task<SetupPresentationOutcome> pending, OperationLifetime operation)
    {
        try
        {
            try { (await pending).Dispose(); }
            catch (Exception) when (pending.IsFaulted || pending.IsCanceled)
            {
                // The cancelled caller no longer owns a result; observing the worker still retires its resources.
            }
        }
        finally { await RetireAsync(operation); }
        RaiseDiscarded(operation);
    }

    private async Task RetireAsync(OperationLifetime operation)
    {
        try { await operation.DisposeAsync(); }
        finally { lock (_gate) _pending.Remove(operation); }
    }

    private State CaptureState() => new(_result, _output, _isBusy, _isTerminated, IsActive);

    private Notifications ChangesSince(State previous) =>
        (ReferenceEquals(previous.Result, _result) ? Notifications.None : Notifications.Result) |
        (previous.Output.Equals(_output) ? Notifications.None : Notifications.Output) |
        (previous.IsBusy == _isBusy ? Notifications.None : Notifications.IsBusy) |
        (previous.IsTerminated == _isTerminated ? Notifications.None : Notifications.IsTerminated) |
        (previous.IsActive == IsActive ? Notifications.None : Notifications.IsActive);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        // ObservableRecipient changes registration atomically; its notifications join the post-lock batch.
        if (!Monitor.IsEntered(_gate)) base.OnPropertyChanged(e);
    }

    protected override void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        if (!Monitor.IsEntered(_gate)) base.OnPropertyChanging(e);
    }

    private void NotifyState(Notifications notifications)
    {
        List<Action> callbacks = [];
        if (notifications.HasFlag(Notifications.Result)) callbacks.Add(() => OnPropertyChanged(nameof(Result)));
        if (notifications.HasFlag(Notifications.Output)) callbacks.Add(() => OnPropertyChanged(nameof(Output)));
        if (notifications.HasFlag(Notifications.IsBusy)) callbacks.Add(() => OnPropertyChanged(nameof(IsBusy)));
        if (notifications.HasFlag(Notifications.IsTerminated)) callbacks.Add(() => OnPropertyChanged(nameof(IsTerminated)));
        if (notifications.HasFlag(Notifications.IsActive))
        {
            callbacks.Add(() => OnPropertyChanging(nameof(IsActive)));
            callbacks.Add(() => OnPropertyChanged(nameof(IsActive)));
        }
        if ((notifications & (Notifications.IsBusy | Notifications.IsTerminated)) != 0)
        {
            callbacks.Add(ExecuteCommand.NotifyCanExecuteChanged);
            callbacks.Add(CancelCommand.NotifyCanExecuteChanged);
        }
        NotifyAll(callbacks);
    }

    private void RaiseInvalidated(SetupOperationGeneration generation, SetupOperationInvalidationReason reason) =>
        NotifyAll((Invalidated?.GetInvocationList() ?? []).Cast<EventHandler<SetupOperationInvalidatedEventArgs>>()
            .Select<EventHandler<SetupOperationInvalidatedEventArgs>, Action>(subscriber =>
                () => subscriber(this, new SetupOperationInvalidatedEventArgs(generation, reason))));

    private static void NotifyAll(IEnumerable<Action> notifications)
    {
        List<Exception>? failures = null;
        foreach (Action notification in notifications)
        {
            try { notification(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failures[0]);
        if (failures is not null) throw new AggregateException(failures);
    }

    private void RaiseDiscarded(OperationLifetime operation) => CompletionDiscarded?.Invoke(this,
        new SetupCompletionDiscardedEventArgs(operation.OperationId, operation.Generation));

    private static void Observe(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    private sealed class OperationLifetime : IAsyncDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _source = new();
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task _cancellation = Task.CompletedTask;
        private bool _retired;

        internal OperationLifetime(Guid operationId, SetupOperationGeneration generation)
        {
            OperationId = operationId;
            Generation = generation;
            Token = _source.Token;
            Observe(_completion.Task);
        }

        internal Guid OperationId { get; }
        internal SetupOperationGeneration Generation { get; }
        internal CancellationToken Token { get; }
        internal Task Completion => _completion.Task;

        internal Task RequestCancellationAsync()
        {
            lock (_gate)
            {
                if (!_retired && !_source.IsCancellationRequested)
                {
                    _cancellation = _source.CancelAsync();
                    Observe(_cancellation);
                }
                return _cancellation;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Task cancellation;
            lock (_gate)
            {
                if (_retired) return;
                _retired = true;
                cancellation = _cancellation;
            }
            try
            {
                await cancellation;
                _source.Dispose();
                _completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _source.Dispose();
                _completion.TrySetException(exception);
                throw;
            }
        }
    }

    private readonly record struct SettlementIdentity(Guid OperationId, SetupOperationGeneration Generation);
    private readonly record struct State(object? Result, ReadOnlyMemory<byte> Output, bool IsBusy, bool IsTerminated, bool IsActive);

    [Flags]
    private enum Notifications
    {
        None = 0,
        Result = 1,
        Output = 2,
        IsBusy = 4,
        IsTerminated = 8,
        IsActive = 16
    }
}
