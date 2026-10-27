namespace Configurator.Presentation.Tests;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core;
using Configurator.Core.Definitions;

public sealed class SetupPresentationModelTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    [Test]
    [Arguments(true, false)]
    [Arguments(false, false)]
    [Arguments(false, true)]
    public async Task FaultSettlesPrivateOutputAndAllowsRetry(bool synchronous, bool unrequestedCancellation)
    {
        var pending = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception failure = unrequestedCancellation ? new OperationCanceledException() : new InvalidOperationException("synthetic-failure");
        int calls = 0;
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, token => ++calls switch
        {
            1 => Task.FromResult(new SetupPresentationOutcome(new object(), new byte[] { 17 })),
            2 => synchronous ? throw failure : pending.Task,
            _ => Task.FromResult(new SetupPresentationOutcome(new object(), ReadOnlyMemory<byte>.Empty))
        });
        await workspace.ExecuteAsync(Guid.NewGuid());
        ReadOnlyMemory<byte> oldOutput = workspace.Output;
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());
        if (!synchronous) pending.SetException(failure);
        Exception? observed = null;
        try { await execution.WaitAsync(SignalTimeout); }
        catch (Exception exception) { observed = exception; }
        await Assert.That(ReferenceEquals(observed, failure)).IsTrue();
        await Assert.That(workspace.IsBusy).IsFalse();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.Output.IsEmpty).IsTrue();
        await Assert.That(oldOutput.Span[0]).IsEqualTo((byte)0);
        await workspace.ExecuteAsync(Guid.NewGuid());
        await Assert.That(calls).IsEqualTo(3);
    }

    [Test]
    public async Task OldFaultCannotInvalidateNewGenerationAfterPrivateClear()
    {
        var first = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, _ => ++calls == 1 ? first.Task : second.Task);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        Task old = workspace.ExecuteAsync(Guid.NewGuid());
        workspace.Deactivate();
        Task current = workspace.ExecuteAsync(Guid.NewGuid());
        await old.WaitAsync(SignalTimeout);
        first.SetException(new InvalidOperationException("synthetic-private-canary"));
        await discarded.Task.WaitAsync(SignalTimeout);
        await Assert.That(workspace.IsBusy).IsTrue();
        await Assert.That(workspace.Output.IsEmpty).IsTrue();
        var result = new object();
        using var outcome = new SetupPresentationOutcome(result, ReadOnlyMemory<byte>.Empty);
        second.SetResult(outcome);
        await current.WaitAsync(SignalTimeout);
        await Assert.That(ReferenceEquals(workspace.Result, result)).IsTrue();
    }

    [Test]
    public async Task PropertyNotificationsAllowCrossThreadReentryAndObserveCoherentState()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, _ => Task.FromResult(new SetupPresentationOutcome(new object(), ReadOnlyMemory<byte>.Empty)));
        bool observed = false;
        workspace.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(workspace.Generation)) return;
            if (!workspace.IsBusy) throw new InvalidOperationException("incoherent-operation-state");
            Task.Run(() => workspace.Activate()).WaitAsync(SignalTimeout).GetAwaiter().GetResult();
            observed = true;
        };
        await workspace.ExecuteAsync(Guid.NewGuid());
        await Assert.That(observed).IsTrue();
        await Assert.That(workspace.IsActive).IsTrue();
    }

    [Test]
    public async Task CancellationRetainsSourceUntilLateWorkerCompletes()
    {
        var pending = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken retained = default;
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, token => { retained = token; return pending.Task; });
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());
        workspace.Cancel();
        await execution.WaitAsync(SignalTimeout);
        Task retirement = workspace.WaitForPendingOperationsAsync();
        await Assert.That(retirement.IsCompleted).IsFalse();
        Exception? sourceFailure = null;
        try { _ = retained.WaitHandle; }
        catch (ObjectDisposedException exception) { sourceFailure = exception; }
        using var late = new SetupPresentationOutcome(new object(), new byte[] { 83 });
        ReadOnlyMemory<byte> output = late.Output;
        pending.SetResult(late);
        await discarded.Task.WaitAsync(SignalTimeout);
        await retirement.WaitAsync(SignalTimeout);
        await Assert.That(sourceFailure).IsNull();
        await Assert.That(output.Span[0]).IsEqualTo((byte)0);
    }

    private static SetupPresentationWorkspace CreateWorkspace(SetupPresentationSession session,
        Func<CancellationToken, Task<SetupPresentationOutcome>> execute)
    {
        if (!SetupWorkspaceId.TryCreate("lifetime", out SetupWorkspaceId id)) throw new InvalidOperationException();
        return session.CreateWorkspace(id, new DelegateOperation(execute));
    }

    [Test]
    public async Task RequestOnlyCancellationRetiresAfterBlockedCallbackAndWorker()
    {
        var pending = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCallback = new ManualResetEventSlim();
        CancellationToken retained = default;
        CancellationTokenRegistration registration = default;
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, token =>
        {
            retained = token;
            registration = token.Register(() =>
            {
                callbackStarted.TrySetResult();
                if (!releaseCallback.Wait(SignalTimeout)) throw new TimeoutException("callback-release");
            });
            return pending.Task;
        });
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());
        Task cancellation = workspace.RequestCancellationAsync();
        try
        {
            await callbackStarted.Task.WaitAsync(SignalTimeout);
            await Assert.That(cancellation.IsCompleted).IsFalse();
            using var late = new SetupPresentationOutcome(new object(), ReadOnlyMemory<byte>.Empty);
            pending.SetResult(late);
            _ = retained.WaitHandle;
        }
        finally { releaseCallback.Set(); }
        await cancellation.WaitAsync(SignalTimeout);
        await execution.WaitAsync(SignalTimeout);
        await discarded.Task.WaitAsync(SignalTimeout);
        registration.Dispose();
        Exception? retired = null;
        try { _ = retained.WaitHandle; }
        catch (ObjectDisposedException exception) { retired = exception; }
        await Assert.That(retired).IsNotNull();
    }

    [Test]
    public async Task ThrowingInvalidationSubscriberCannotPreventCancellationOrLateOutputCleanup()
    {
        var pending = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken retained = default;
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, token => { retained = token; return pending.Task; });
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        EventHandler<SetupOperationInvalidatedEventArgs> subscriber = (_, _) => throw new InvalidOperationException("synthetic-subscriber");
        workspace.Invalidated += subscriber;
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());
        Exception? observed = null;
        try { workspace.Cancel(); }
        catch (InvalidOperationException exception) { observed = exception; }
        finally { workspace.Invalidated -= subscriber; }
        await Assert.That(observed).IsNotNull();
        await Assert.That(retained.IsCancellationRequested).IsTrue();
        await execution.WaitAsync(SignalTimeout);
        using var late = new SetupPresentationOutcome(new object(), new byte[] { 91 });
        ReadOnlyMemory<byte> bytes = late.Output;
        pending.SetResult(late);
        await discarded.Task.WaitAsync(SignalTimeout);
        await Assert.That(bytes.Span[0]).IsEqualTo((byte)0);
    }

    private sealed class DelegateOperation(Func<CancellationToken, Task<SetupPresentationOutcome>> execute) : ISetupPresentationOperation
    {
        public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken) => execute(cancellationToken);
    }

    [Test]
    public async Task WorkerFaultRemainsOwnedWhenItsInvalidationSubscriberAlsoFails()
    {
        var workerFailure = new InvalidOperationException("synthetic-worker");
        var subscriberFailure = new InvalidOperationException("synthetic-subscriber");
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, _ => Task.FromException<SetupPresentationOutcome>(workerFailure));
        EventHandler<SetupOperationInvalidatedEventArgs> subscriber = (_, _) => throw subscriberFailure;
        workspace.Invalidated += subscriber;
        AggregateException? observed = null;
        try { await workspace.ExecuteAsync(Guid.NewGuid()); }
        catch (AggregateException exception) { observed = exception; }
        finally { workspace.Invalidated -= subscriber; }
        await Assert.That(observed).IsNotNull();
        await Assert.That(observed!.Flatten().InnerExceptions.Contains(workerFailure)).IsTrue();
        await Assert.That(observed.Flatten().InnerExceptions.Contains(subscriberFailure)).IsTrue();
        await Assert.That(workspace.IsBusy).IsFalse();
        await workspace.WaitForPendingOperationsAsync().WaitAsync(SignalTimeout);
    }

    [Test]
    public async Task RequestOnlyCancellationQueuesObservableSettlementToCapturedOwner()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var pending = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        using var workspace = CreateWorkspace(session, token =>
        {
            registration = token.Register(() => pending.TrySetCanceled(token));
            return pending.Task;
        });
        var owner = new QueuedOwnerContext();
        SynchronizationContext? original = SynchronizationContext.Current;
        Task execution;
        try
        {
            SynchronizationContext.SetSynchronizationContext(owner);
            execution = workspace.ExecuteAsync(Guid.NewGuid());
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
        int notifications = 0;
        workspace.PropertyChanged += (_, _) => notifications++;
        await Task.Run(workspace.RequestCancellationAsync).WaitAsync(SignalTimeout);
        await owner.Posted.Task.WaitAsync(SignalTimeout);
        await Assert.That(notifications).IsEqualTo(0);
        await Assert.That(workspace.IsBusy).IsTrue();
        owner.Deliver();
        await execution.WaitAsync(SignalTimeout);
        await workspace.WaitForPendingOperationsAsync().WaitAsync(SignalTimeout);
        registration.Dispose();
        await Assert.That(workspace.IsBusy).IsFalse();
        await Assert.That(notifications > 0).IsTrue();
    }

    private sealed class QueuedOwnerContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        internal TaskCompletionSource Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            Posted.TrySetResult();
        }

        internal void Deliver()
        {
            while (_callbacks.TryDequeue(out var item)) item.Callback(item.State);
        }
    }

    [Test]
    public async Task ReentrantActivationAfterDeactivationKeepsNewRecipientAndGeneration()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, _ => Task.FromResult(new SetupPresentationOutcome(new object(), ReadOnlyMemory<byte>.Empty)));
        workspace.Activate();
        Task? replacement = null;
        EventHandler<SetupOperationInvalidatedEventArgs> subscriber = (_, _) =>
        {
            workspace.Activate();
            replacement = workspace.ExecuteAsync(Guid.NewGuid());
        };
        workspace.Invalidated += subscriber;
        try { workspace.Deactivate(); }
        finally { workspace.Invalidated -= subscriber; }
        await replacement!.WaitAsync(SignalTimeout);
        await Assert.That(workspace.IsActive).IsTrue();
        await Assert.That(workspace.Result).IsNotNull();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task AcceptedOutcomeNotificationFaultClearsOnlyItsOwnPrivateResult(bool replaceDuringNotification, bool failMessenger)
    {
        var firstResult = new object();
        var replacementResult = new object();
        using var firstOutcome = new SetupPresentationOutcome(firstResult, new byte[] { 73 });
        using var replacementOutcome = new SetupPresentationOutcome(replacementResult, new byte[] { 19 });
        ReadOnlyMemory<byte> firstBytes = firstOutcome.Output;
        int calls = 0;
        var messenger = new WeakReferenceMessenger();
        using var session = new SetupPresentationSession(messenger);
        using var workspace = CreateWorkspace(session, _ => Task.FromResult(++calls == 1 ? firstOutcome : replacementOutcome));
        var notificationFailure = new InvalidOperationException("synthetic-notification");
        if (failMessenger)
            messenger.Register<SetupOperationSettledMessage>(this, (_, _) => throw notificationFailure);
        Task? replacement = null;
        workspace.PropertyChanged += (_, args) =>
        {
            if (failMessenger || args.PropertyName != nameof(workspace.Result) || !ReferenceEquals(workspace.Result, firstResult)) return;
            if (replaceDuringNotification) replacement = workspace.ExecuteAsync(Guid.NewGuid());
            throw notificationFailure;
        };
        Exception? observed = null;
        try { await workspace.ExecuteAsync(Guid.NewGuid()); }
        catch (Exception exception) { observed = exception; }
        if (replacement is not null) await replacement;
        await Assert.That(ReferenceEquals(observed, notificationFailure)).IsTrue();
        await Assert.That(workspace.IsBusy).IsFalse();
        await Assert.That(firstBytes.Span[0]).IsEqualTo((byte)0);
        if (replaceDuringNotification)
        {
            await Assert.That(ReferenceEquals(workspace.Result, replacementResult)).IsTrue();
            await Assert.That(workspace.Output.Span[0]).IsEqualTo((byte)19);
        }
        else
        {
            await Assert.That(workspace.Result).IsNull();
            await Assert.That(workspace.Output.IsEmpty).IsTrue();
        }
    }

    [Test]
    public async Task ThrowingInvalidationSubscriberCannotSkipStateAndCommandNotifications()
    {
        var pending = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, _ => pending.Task);
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());
        bool invalidationObserved = false;
        bool busyObserved = false;
        bool eligibilityObserved = false;
        EventHandler<SetupOperationInvalidatedEventArgs> failing = (_, _) => throw new InvalidOperationException("synthetic-subscriber");
        workspace.Invalidated += failing;
        workspace.Invalidated += (_, _) => invalidationObserved = true;
        workspace.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(workspace.IsBusy)) busyObserved = !workspace.IsBusy;
        };
        workspace.ExecuteCommand.CanExecuteChanged += (_, _) => eligibilityObserved = workspace.ExecuteCommand.CanExecute(Guid.NewGuid());
        Exception? observed = null;
        try { workspace.Cancel(); }
        catch (Exception exception) { observed = exception; }
        finally { workspace.Invalidated -= failing; }
        await execution.WaitAsync(SignalTimeout);
        Task retirement = workspace.WaitForPendingOperationsAsync();
        using var late = new SetupPresentationOutcome(new object(), ReadOnlyMemory<byte>.Empty);
        pending.SetResult(late);
        await retirement.WaitAsync(SignalTimeout);
        await Assert.That(observed).IsNotNull();
        await Assert.That(invalidationObserved).IsTrue();
        await Assert.That(busyObserved).IsTrue();
        await Assert.That(eligibilityObserved).IsTrue();
    }

    [Test]
    public async Task AcceptedOutcomeFaultCannotDetachReentrantPendingReplacement()
    {
        using var firstOutcome = new SetupPresentationOutcome(new object(), new byte[] { 29 });
        ReadOnlyMemory<byte> firstBytes = firstOutcome.Output;
        var pending = new TaskCompletionSource<SetupPresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        using var workspace = CreateWorkspace(session, _ => ++calls == 1 ? Task.FromResult(firstOutcome) : pending.Task);
        Task? replacement = null;
        workspace.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(workspace.Result) || !ReferenceEquals(workspace.Result, firstOutcome.CoreResult)) return;
            replacement = workspace.ExecuteAsync(Guid.NewGuid());
            throw new InvalidOperationException("synthetic-notification");
        };
        Exception? observed = null;
        try { await workspace.ExecuteAsync(Guid.NewGuid()); }
        catch (InvalidOperationException exception) { observed = exception; }
        await Assert.That(observed).IsNotNull();
        await Assert.That(workspace.IsBusy).IsTrue();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(firstBytes.Span[0]).IsEqualTo((byte)0);
        using var replacementOutcome = new SetupPresentationOutcome(new object(), new byte[] { 31 });
        pending.SetResult(replacementOutcome);
        await replacement!.WaitAsync(SignalTimeout);
        await Assert.That(ReferenceEquals(workspace.Result, replacementOutcome.CoreResult)).IsTrue();
        await Assert.That(workspace.Output.Span[0]).IsEqualTo((byte)31);
    }

    [Test]
    public async Task WorkspaceOwnsAndClearsAcceptedReplacedAndDiscardedOutput()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation(3);
        using var workspace = CreateWorkspace(session, "private-output", operation);
        byte[] caller = [11, 22, 33];
        Task first = workspace.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        ReadOnlyMemory<byte> accepted = operation.Complete(0, CoreResult(), caller);
        await first.WaitAsync(SignalTimeout);
        caller[0] = 99;
        await Assert.That(accepted.Span.SequenceEqual(new byte[] { 11, 22, 33 })).IsTrue();

        Task second = workspace.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        ReadOnlyMemory<byte> replacement = operation.Complete(1, CoreResult(), new byte[] { 44, 55 });
        await second.WaitAsync(SignalTimeout);
        await Assert.That(accepted.Span.SequenceEqual(new byte[] { 0, 0, 0 })).IsTrue();
        await Assert.That(replacement.Span.SequenceEqual(new byte[] { 44, 55 })).IsTrue();

        Task third = workspace.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[2].Started.Task.WaitAsync(SignalTimeout);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        workspace.Cancel();
        ReadOnlyMemory<byte> late = operation.Complete(2, CoreResult(), new byte[] { 66, 77 });
        await third.WaitAsync(SignalTimeout);
        await discarded.Task.WaitAsync(SignalTimeout);
        await Assert.That(late.Span.SequenceEqual(new byte[] { 0, 0 })).IsTrue();
        await Assert.That(replacement.Span.SequenceEqual(new byte[] { 44, 55 })).IsTrue();

        workspace.Deactivate();
        await Assert.That(replacement.Span.SequenceEqual(new byte[] { 0, 0 })).IsTrue();
    }

    [Test]
    public async Task SessionsOwnDistinctMessengersAndNeverCrossTalk()
    {
        var firstMessenger = new WeakReferenceMessenger();
        var secondMessenger = new WeakReferenceMessenger();
        using var first = new SetupPresentationSession(firstMessenger);
        using var second = new SetupPresentationSession(secondMessenger);
        var firstMessages = new List<SetupOperationSettledMessage>();
        var secondMessages = new List<SetupOperationSettledMessage>();
        firstMessenger.Register<SetupOperationSettledMessage>(firstMessages, (_, message) => firstMessages.Add(message));
        secondMessenger.Register<SetupOperationSettledMessage>(secondMessages, (_, message) => secondMessages.Add(message));
        var operation = new ControlledPresentationOperation();
        using var firstSender = CreateWorkspace(first, "first-sender", operation);

        Task execution = firstSender.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), ReadOnlyMemory<byte>.Empty);
        await execution.WaitAsync(SignalTimeout);

        await Assert.That(ReferenceEquals(firstMessenger, secondMessenger)).IsFalse();
        await Assert.That(first.SessionId == second.SessionId).IsFalse();
        await Assert.That(firstMessages.Count).IsEqualTo(1);
        await Assert.That(firstMessages[0].SessionId).IsEqualTo(first.SessionId);
        await Assert.That(secondMessages).IsEmpty();
    }

    [Test]
    public async Task OnlyAcceptedCompletionPublishesTypedDelivery()
    {
        var messenger = new WeakReferenceMessenger();
        using var session = new SetupPresentationSession(messenger);
        var messages = new List<SetupOperationSettledMessage>();
        messenger.Register<SetupOperationSettledMessage>(messages, (_, message) => messages.Add(message));
        var operation = new ControlledPresentationOperation(2);
        using var workspace = CreateWorkspace(session, "sender", operation);
        Guid operationId = Guid.CreateVersion7();
        Task stale = workspace.ExecuteAsync(operationId);
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        Task current = workspace.ExecuteAsync(operationId);
        await operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        operation.Complete(0, CoreResult(), new byte[] { 1 });
        await stale.WaitAsync(SignalTimeout);
        await discarded.Task.WaitAsync(SignalTimeout);
        await Assert.That(messages).IsEmpty();
        operation.Complete(1, CoreResult(), new byte[] { 2 });
        await current.WaitAsync(SignalTimeout);
        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0].OperationId).IsEqualTo(operationId);
        await Assert.That(messages[0].Generation).IsEqualTo(new SetupOperationGeneration(2));
        await Assert.That(messages[0].WorkspaceId).IsEqualTo(workspace.WorkspaceId);
        await Assert.That(messages[0].Status).IsEqualTo(SetupOperationStatus.Succeeded);
    }

    [Test]
    public async Task CancellationWinsOnlyAfterValueFreeInvalidationAndLateCompletionIsDiscarded()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation();
        using var workspace = CreateWorkspace(session, "cancel-wins", operation);
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = SubscribeInvalidated(workspace, args =>
        {
            if (invocation.Cancelled.Task.IsCompleted)
                invalidated.TrySetException(new InvalidOperationException(
                    "invalidation-after-cancellation"));
            else
                invalidated.TrySetResult(args);
        });

        workspace.Cancel();
        EventArgs signal = await invalidated.Task.WaitAsync(SignalTimeout);
        await invocation.Cancelled.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), new byte[] { 3 });
        await execution.WaitAsync(SignalTimeout);

        await AssertValueFreeSignal(signal);
        await Assert.That(workspace.IsBusy).IsFalse();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.Output).IsEmpty();
    }

    [Test]
    public async Task SettlementWinsBeforeLaterCancellationAndRetainsOwnedOutcome()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation();
        using var workspace = CreateWorkspace(session, "settlement-wins", operation);
        object result = CoreResult();
        byte[] buffer = [5, 8, 13, 21];
        ReadOnlyMemory<byte> region = new(buffer, 1, 2);
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, result, region);
        await execution.WaitAsync(SignalTimeout);
        var invalidated = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = SubscribeInvalidated(workspace,
            args => invalidated.TrySetResult(args));

        workspace.Cancel();
        await invalidated.Task.WaitAsync(SignalTimeout);

        await Assert.That(invocation.Cancelled.Task.IsCompleted).IsFalse();
        await Assert.That(ReferenceEquals(workspace.Result, result)).IsTrue();
        await AssertOwnedMemory(workspace.Output, region);
    }

    [Test]
    public async Task ReplacementInvalidatesBeforeCancellingRetainedOperation()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation(2);
        using var workspace = CreateWorkspace(session, "replacement", operation);
        Task stale = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation first = operation.Invocations[0];
        await first.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = OrderedInvalidationSignal(workspace, first);

        Task current = workspace.ExecuteAsync(Guid.CreateVersion7());
        await invalidated.Signal.Task.WaitAsync(SignalTimeout);
        await first.Cancelled.Task.WaitAsync(SignalTimeout);
        await operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), new byte[] { 1 });
        await stale.WaitAsync(SignalTimeout);
        operation.Complete(1, CoreResult(), new byte[] { 2 });
        await current.WaitAsync(SignalTimeout);

        await Assert.That(workspace.Generation.Value).IsEqualTo(2);
        invalidated.Subscription.Dispose();
    }

    [Test]
    public async Task DeactivationInvalidatesBeforeCancellationAndDeniesLateOutput()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation();
        using var workspace = CreateWorkspace(session, "deactivation", operation);
        workspace.Activate();
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = OrderedInvalidationSignal(workspace, invocation);

        workspace.Deactivate();
        await invalidated.Signal.Task.WaitAsync(SignalTimeout);
        await invocation.Cancelled.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), ReadOnlyMemory<byte>.Empty);
        await execution.WaitAsync(SignalTimeout);
        await workspace.WaitForPendingOperationsAsync().WaitAsync(SignalTimeout);
        await Assert.That(workspace.IsActive).IsFalse();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.Output).IsEmpty();
        invalidated.Subscription.Dispose();
    }

    [Test]
    public async Task DisposalInvalidatesBeforeCancellationAndIsIdempotent()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation();
        using var workspace = CreateWorkspace(session, "disposal", operation);
        workspace.Activate();
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = OrderedInvalidationSignal(workspace, invocation);

        workspace.Dispose();
        await invalidated.Signal.Task.WaitAsync(SignalTimeout);
        await invocation.Cancelled.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), new byte[] { 5 });
        await execution.WaitAsync(SignalTimeout);
        workspace.Dispose();

        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.IsActive).IsFalse();
        invalidated.Subscription.Dispose();
    }

    [Test]
    public async Task DuplicateSettlementsRaceAtBarrierAndCommitPublishRefreshDiscardExactlyOnce()
    {
        var messenger = new WeakReferenceMessenger();
        using var session = new SetupPresentationSession(messenger);
        var operation = new ControlledPresentationOperation(2);
        using var workspace = CreateWorkspace(session, "settlement-race", operation);
        var messages = new List<SetupOperationSettledMessage>();
        messenger.Register<SetupOperationSettledMessage>(messages, (_, message) => messages.Add(message));
        Guid operationId = Guid.CreateVersion7();
        Task staleExecution = workspace.ExecuteAsync(operationId);
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        Task currentExecution = workspace.ExecuteAsync(operationId);
        await operation.Invocations[0].Cancelled.Task.WaitAsync(SignalTimeout);
        await operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        object staleResult = CoreResult();
        object currentResult = CoreResult();
        int resultRefreshes = 0;
        int commandRefreshes = 0;
        int discards = 0;
        var discarded = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler propertyHandler = (_, args) =>
        {
            if (args.PropertyName == "Result")
                Interlocked.Increment(ref resultRefreshes);
        };
        workspace.PropertyChanged += propertyHandler;
        EventHandler commandHandler = (_, _) => Interlocked.Increment(ref commandRefreshes);
        workspace.ExecuteCommand.CanExecuteChanged += commandHandler;
        workspace.CompletionDiscarded += (_, args) =>
        {
            Interlocked.Increment(ref discards);
            discarded.TrySetResult(args);
        };
        using var barrier = new Barrier(2);

        Task[] contenders =
        [
            Task.Run(() =>
            {
                if (!barrier.SignalAndWait(SignalTimeout))
                    throw new TimeoutException("settlement-barrier-timeout");
                operation.Complete(0, staleResult, new byte[] { 3 });
            }),
            Task.Run(() =>
            {
                if (!barrier.SignalAndWait(SignalTimeout))
                    throw new TimeoutException("settlement-barrier-timeout");
                operation.Complete(1, currentResult, new byte[] { 7 });
            })
        ];
        await Task.WhenAll(contenders).WaitAsync(SignalTimeout);
        await Task.WhenAll(staleExecution, currentExecution).WaitAsync(SignalTimeout);
        EventArgs discard = await discarded.Task.WaitAsync(SignalTimeout);

        await Assert.That(ReferenceEquals(workspace.Result, currentResult)).IsTrue();
        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(resultRefreshes).IsEqualTo(1);
        await Assert.That(commandRefreshes).IsEqualTo(1);
        await Assert.That(discards).IsEqualTo(1);
        await AssertValueFreeSignal(discard);
        workspace.ExecuteCommand.CanExecuteChanged -= commandHandler;
        workspace.PropertyChanged -= propertyHandler;
    }

    [Test]
    public async Task BoundedAllocatorReachesFinalGenerationThenTerminatesRetainedCompletion()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger(),
            new ControlledGenerationAllocator(long.MaxValue - 1, long.MaxValue, null));
        var operation = new ControlledPresentationOperation(3);
        using var workspace = CreateWorkspace(session, "exhaustion", operation);
        Task penultimate = workspace.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        Task retainedFinal = workspace.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[0].Cancelled.Task.WaitAsync(SignalTimeout);
        await operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), new byte[] { 1 });
        await penultimate.WaitAsync(SignalTimeout);
        Guid sessionId = session.SessionId;

        Task terminated = workspace.ExecuteAsync(Guid.CreateVersion7());
        await terminated.WaitAsync(SignalTimeout);
        await operation.Invocations[1].Cancelled.Task.WaitAsync(SignalTimeout);
        operation.Complete(1, CoreResult(), new byte[] { 9 });
        await retainedFinal.WaitAsync(SignalTimeout);

        await Assert.That(workspace.Generation.Value).IsEqualTo(long.MaxValue);
        await Assert.That(workspace.IsTerminated).IsTrue();
        await Assert.That(session.IsTerminated).IsTrue();
        await Assert.That(session.SessionId).IsEqualTo(sessionId);
        await Assert.That(operation.Invocations[2].Started.Task.IsCompleted).IsFalse();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.Output).IsEmpty();
    }

    [Test]
    public async Task AllocatorDuplicateOrDecreaseTerminatesInsteadOfWrappingOrReseeding()
    {
        await AssertInvalidAllocationTerminates(41);
        await AssertInvalidAllocationTerminates(40);
    }

    [Test]
    public async Task CompletionMessagesAreImmutableTypedAndValueFree()
    {
        Type message = typeof(SetupOperationSettledMessage);
        Type generation = typeof(SetupOperationGeneration);
        Type workspaceId = typeof(SetupWorkspaceId);
        PropertyInfo[] properties = message.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(message.IsSealed).IsTrue();
        await Assert.That(properties.All(property => property.SetMethod?.IsPublic != true)).IsTrue();
        await Assert.That(message.GetFields(BindingFlags.Public | BindingFlags.Instance)).IsEmpty();
        await Assert.That(properties.Select(property => property.Name).Order(StringComparer.Ordinal))
            .IsEquivalentTo(["Generation", "OperationId", "SessionId", "Status", "WorkspaceId"]);
        await Assert.That(properties.Single(property => property.Name == "Generation").PropertyType)
            .IsEqualTo(generation);
        await Assert.That(properties.Single(property => property.Name == "WorkspaceId").PropertyType)
            .IsEqualTo(workspaceId);
        await Assert.That(properties.Any(property =>
            ForbiddenSensitiveName(property.Name) || MutableSecretCarrier(property.PropertyType))).IsFalse();
    }

    [Test]
    public async Task FormInputNotificationObservesAcceptedRevision()
    {
        using var form = new DefinitionFormModel(PublicDefinition());
        var changed = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        form.InputChanged += (_, _) => changed.TrySetResult(form.Snapshot().InputRevision);

        bool accepted = form.EditValue(["title"], "public-title");
        long revision = await changed.Task.WaitAsync(SignalTimeout);

        await Assert.That(accepted).IsTrue();
        await Assert.That(revision).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values["title"].GetString()).IsEqualTo("public-title");
    }

    [Test]
    public async Task AsyncCommandsRefreshEligibilityExecuteAndCancelThroughPublicBehavior()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation();
        using var workspace = CreateWorkspace(session, "commands", operation);
        Guid operationId = Guid.CreateVersion7();
        IAsyncRelayCommand execute = workspace.ExecuteCommand;

        await Assert.That(workspace.ExecuteCommand.CanExecute(operationId)).IsTrue();
        await Assert.That(workspace.CancelCommand.CanExecute(null)).IsFalse();

        workspace.ExecuteCommand.Execute(operationId);
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        await Assert.That(workspace.IsBusy).IsTrue();
        await Assert.That(workspace.ExecuteCommand.CanExecute(operationId)).IsFalse();
        await Assert.That(workspace.CancelCommand.CanExecute(null)).IsTrue();

        workspace.CancelCommand.Execute(null);
        await operation.Invocations[0].Cancelled.Task.WaitAsync(SignalTimeout);
        Task executionTask = execute.ExecutionTask!;
        await executionTask.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), ReadOnlyMemory<byte>.Empty);
        await workspace.WaitForPendingOperationsAsync().WaitAsync(SignalTimeout);
        await Assert.That(workspace.IsBusy).IsFalse();
        await Assert.That(workspace.ExecuteCommand.CanExecute(operationId)).IsTrue();
        await Assert.That(workspace.CancelCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task CoreResultAndReadOnlyMemoryProjectWithOwnedBytes()
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger());
        var operation = new ControlledPresentationOperation();
        using var workspace = CreateWorkspace(session, "core-projection", operation);
        object coreResult = CoreResult();
        byte[] backing = [2, 3, 5, 7, 11];
        ReadOnlyMemory<byte> bytes = new(backing, 1, 3);

        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, coreResult, bytes);
        await execution.WaitAsync(SignalTimeout);

        await Assert.That(ReferenceEquals(workspace.Result, coreResult)).IsTrue();
        await AssertOwnedMemory(workspace.Output, bytes);
    }

    [Test]
    public async Task GenerationTypeFlowsEndToEndWithoutPrimitiveIdentity()
    {
        Type generation = typeof(SetupOperationGeneration);
        Type workspace = typeof(SetupPresentationWorkspace);
        Type message = typeof(SetupOperationSettledMessage);
        Type allocator = typeof(ISetupOperationGenerationAllocator);

        await Assert.That(workspace.GetProperty("Generation")?.PropertyType).IsEqualTo(generation);
        await Assert.That(message.GetProperty("Generation")?.PropertyType).IsEqualTo(generation);
        MethodInfo allocate = allocator.GetMethod("TryAllocate")
            ?? throw new InvalidOperationException(
                "missing-approved-owner:ISetupOperationGenerationAllocator.TryAllocate");
        await Assert.That(allocate.GetParameters().Any(parameter =>
            parameter.ParameterType == generation
            || parameter.ParameterType == generation.MakeByRefType())).IsTrue();
        await Assert.That(workspace.GetProperties().Any(property =>
            property.Name == "Generation" && property.PropertyType == typeof(long))).IsFalse();
    }

    [Test]
    public async Task WorkspaceIdentityIsNamedBoundedAndValidated()
    {
        int maximum = SetupWorkspaceId.MaxLength;

        await Assert.That(maximum).IsGreaterThan(0);
        await Assert.That(maximum).IsLessThanOrEqualTo(256);
        await Assert.That(SetupWorkspaceId.TryCreate("bounded-workspace", out SetupWorkspaceId accepted)).IsTrue();
        await Assert.That(accepted.Value).IsEqualTo("bounded-workspace");
        await Assert.That(SetupWorkspaceId.TryCreate(string.Empty, out _)).IsFalse();
        await Assert.That(SetupWorkspaceId.TryCreate("   ", out _)).IsFalse();
        await Assert.That(SetupWorkspaceId.TryCreate(new string('w', maximum + 1), out _)).IsFalse();
    }

    [Test]
    public async Task DynamicCanaryAppearsOnlyAsExplicitlyAcceptedPublicEditValue()
    {
        using var form = new DefinitionFormModel(PublicDefinition());
        string canary = $"{Guid.CreateVersion7():N}-{Guid.NewGuid():N}";
        await Assert.That(form.EditValue(["title"], canary)).IsTrue();
        await Assert.That(form.Snapshot().Values["title"].GetString()).IsEqualTo(canary);
        form.SetDiscovery(canary);
        await Assert.That(form.Projection.Matches).IsEmpty();
        await Assert.That(form.EditValue(["title"], new string('x', 65_537))).IsFalse();
        await Assert.That(form.InputDiagnostics.Count).IsEqualTo(1);
        await Assert.That(form.InputDiagnostics[0].Code).IsEqualTo("answer-budget-exceeded");
        await Assert.That(form.InputDiagnostics[0].ToString()).DoesNotContain(canary);
        await Assert.That(form.ToString()).DoesNotContain(canary);
        await Assert.That(typeof(SetupPresentationWorkspace).Assembly.GetExportedTypes().SelectMany(type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly)).Any(property => ForbiddenSensitiveName(property.Name))).IsFalse();
    }

    private static async Task AssertInvalidAllocationTerminates(
        long invalid)
    {
        using var session = new SetupPresentationSession(new WeakReferenceMessenger(),
            new ControlledGenerationAllocator(41, invalid));
        var operation = new ControlledPresentationOperation(2);
        using var workspace = CreateWorkspace(session, $"invalid-{invalid}", operation);
        Task first = workspace.ExecuteAsync(Guid.CreateVersion7());
        await operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        operation.Complete(0, CoreResult(), ReadOnlyMemory<byte>.Empty);
        await first.WaitAsync(SignalTimeout);

        await workspace.ExecuteAsync(Guid.CreateVersion7()).WaitAsync(SignalTimeout);

        await Assert.That(workspace.Generation.Value).IsEqualTo(41);
        await Assert.That(workspace.IsTerminated).IsTrue();
        await Assert.That(operation.Invocations[1].Started.Task.IsCompleted).IsFalse();
    }

    private static (TaskCompletionSource<EventArgs> Signal, IDisposable Subscription)
        OrderedInvalidationSignal(
            SetupPresentationWorkspace workspace,
            ControlledPresentationInvocation invocation)
    {
        var signal = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable subscription = SubscribeInvalidated(workspace, args =>
        {
            if (invocation.Cancelled.Task.IsCompleted)
                signal.TrySetException(new InvalidOperationException(
                    "invalidation-after-cancellation"));
            else
                signal.TrySetResult(args);
        });
        return (signal, subscription);
    }

    private static SetupPresentationWorkspace CreateWorkspace(SetupPresentationSession session,
        string id, ISetupPresentationOperation operation)
    {
        if (!SetupWorkspaceId.TryCreate(id, out SetupWorkspaceId workspaceId))
            throw new InvalidOperationException("invalid-test-workspace");
        return session.CreateWorkspace(workspaceId, operation);
    }

    private static CallbackSubscription SubscribeInvalidated(SetupPresentationWorkspace workspace,
        Action<SetupOperationInvalidatedEventArgs> observer)
    {
        EventHandler<SetupOperationInvalidatedEventArgs> handler = (_, args) => observer(args);
        workspace.Invalidated += handler;
        return new CallbackSubscription(() => workspace.Invalidated -= handler);
    }

    private static SetupDefinition PublicDefinition() => SetupDefinitionCodec.ParseJson("""
        {"schemaVersion":1,"familyId":"eba722b7-f132-444b-8c44-21cece008ca7","revision":1,
         "applicationId":"example","applicationVersions":["1"],"publisher":"example","title":"Example",
         "entryPoints":["setup"],"locales":["en"],
         "fields":[{"id":"title","type":"string","title":"Title","sensitivity":"public","disclosure":"guided"}],
         "artifacts":[{"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"process",
           "projection":{"op":"object","members":{"title":{"op":"copy","field":"title"}}}}]}
        """u8.ToArray());

    private static async Task AssertValueFreeSignal(EventArgs signal)
    {
        await Assert.That(signal.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.PropertyType == typeof(string)
                || property.PropertyType == typeof(byte[])
                || property.PropertyType == typeof(ReadOnlyMemory<byte>))).IsFalse();
        await Assert.That(signal.ToString()).DoesNotContain("=");
    }

    private static async Task AssertOwnedMemory(
        ReadOnlyMemory<byte> actual,
        ReadOnlyMemory<byte> expected)
    {
        bool actualArray = MemoryMarshal.TryGetArray(actual, out ArraySegment<byte> actualSegment);
        bool expectedArray = MemoryMarshal.TryGetArray(expected, out ArraySegment<byte> expectedSegment);
        await Assert.That(actualArray).IsTrue();
        await Assert.That(expectedArray).IsTrue();
        await Assert.That(ReferenceEquals(actualSegment.Array, expectedSegment.Array)).IsFalse();
        await Assert.That(actual.Span.SequenceEqual(expected.Span)).IsTrue();
    }

    private static SetupReadinessResult CoreResult() => SetupReadiness.Evaluate(
        new SetupReadinessInput([], [], []));

    private static bool ForbiddenSensitiveName(string name) =>
        name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Credential", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
        || name.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase);

    private static bool MutableSecretCarrier(Type type)
    {
        if (type == typeof(char[]) || type == typeof(byte[])
            || type == typeof(Memory<char>) || type == typeof(Memory<byte>))
            return true;
        if (!type.IsGenericType)
            return false;
        Type definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>)
            || definition == typeof(IList<>)
            || definition == typeof(ICollection<>);
    }
}
