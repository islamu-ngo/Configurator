namespace Configurator.Presentation.Tests;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core;

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
            Task.Run(() => workspace.SetPublicInput("reentrant")).WaitAsync(SignalTimeout).GetAwaiter().GetResult();
            observed = true;
        };
        await workspace.ExecuteAsync(Guid.NewGuid());
        await Assert.That(observed).IsTrue();
        await Assert.That(workspace.PublicInput).IsEqualTo("reentrant");
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
        var contract = new SetupPresentationModelContract();
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("private-output", invocationCount: 3);
        byte[] caller = [11, 22, 33];
        Task first = workspace.ExecuteAsync(Guid.CreateVersion7());
        await workspace.Operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        ReadOnlyMemory<byte> accepted = workspace.Complete(0, CoreResult(), caller);
        await first.WaitAsync(SignalTimeout);
        caller[0] = 99;
        await Assert.That(accepted.Span.SequenceEqual(new byte[] { 11, 22, 33 })).IsTrue();

        Task second = workspace.ExecuteAsync(Guid.CreateVersion7());
        await workspace.Operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        ReadOnlyMemory<byte> replacement = workspace.Complete(1, CoreResult(), new byte[] { 44, 55 });
        await second.WaitAsync(SignalTimeout);
        await Assert.That(accepted.Span.SequenceEqual(new byte[] { 0, 0, 0 })).IsTrue();
        await Assert.That(replacement.Span.SequenceEqual(new byte[] { 44, 55 })).IsTrue();

        Task third = workspace.ExecuteAsync(Guid.CreateVersion7());
        await workspace.Operation.Invocations[2].Started.Task.WaitAsync(SignalTimeout);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = workspace.SubscribeDiscarded(_ => discarded.TrySetResult());
        workspace.Cancel();
        ReadOnlyMemory<byte> late = workspace.Complete(2, CoreResult(), new byte[] { 66, 77 });
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
        var contract = new SetupPresentationModelContract();
        using SetupPresentationSessionHandle first = contract.CreateSession();
        using SetupPresentationSessionHandle second = contract.CreateSession();
        using PresentationWorkspaceHandle firstRecipient = first.CreateWorkspace("first-recipient");
        using PresentationWorkspaceHandle secondRecipient = second.CreateWorkspace("second-recipient");
        using PresentationWorkspaceHandle firstSender = first.CreateWorkspace("first-sender");
        firstRecipient.Activate();
        secondRecipient.Activate();

        Task execution = firstSender.ExecuteAsync(Guid.CreateVersion7());
        await firstSender.Operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        firstSender.Complete(0, CoreResult(), ReadOnlyMemory<byte>.Empty);
        await execution.WaitAsync(SignalTimeout);

        await Assert.That(ReferenceEquals(first.Messenger, second.Messenger)).IsFalse();
        await Assert.That(first.SessionId == second.SessionId).IsFalse();
        await Assert.That(firstRecipient.ReceivedCompletionCount).IsEqualTo(1);
        await Assert.That(secondRecipient.ReceivedCompletionCount).IsEqualTo(0);
    }

    [Test]
    public async Task RecipientsObserveOnlyActiveLifetimeAndDeduplicateTypedDelivery()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.SettledMessageType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle recipient = session.CreateWorkspace("recipient");
        object firstGeneration = contract.CreateGeneration(1);
        Guid operationId = Guid.CreateVersion7();

        session.PublishSettlement("sender", operationId, firstGeneration, "Succeeded");
        await Assert.That(recipient.ReceivedCompletionCount).IsEqualTo(0);

        recipient.Activate();
        session.PublishSettlement("sender", operationId, firstGeneration, "Succeeded");
        session.PublishSettlement("sender", operationId, firstGeneration, "Succeeded");
        await Assert.That(recipient.ReceivedCompletionCount).IsEqualTo(1);

        recipient.Deactivate();
        session.PublishSettlement(
            "sender",
            Guid.CreateVersion7(),
            contract.CreateGeneration(2),
            "Succeeded");
        await Assert.That(recipient.ReceivedCompletionCount).IsEqualTo(1);
    }

    [Test]
    public async Task CancellationWinsOnlyAfterValueFreeInvalidationAndLateCompletionIsDiscarded()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.InvalidationEventArgsType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("cancel-wins");
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = workspace.Operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = workspace.SubscribeInvalidated(args =>
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
        workspace.Complete(0, CoreResult(), new byte[] { 3 });
        await execution.WaitAsync(SignalTimeout);

        await AssertValueFreeSignal(signal);
        await Assert.That(workspace.IsBusy).IsFalse();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.Output).IsEmpty();
    }

    [Test]
    public async Task SettlementWinsBeforeLaterCancellationAndRetainsOwnedOutcome()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.WorkspaceType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("settlement-wins");
        object result = CoreResult();
        byte[] buffer = [5, 8, 13, 21];
        ReadOnlyMemory<byte> region = new(buffer, 1, 2);
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = workspace.Operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        workspace.Complete(0, result, region);
        await execution.WaitAsync(SignalTimeout);
        var invalidated = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = workspace.SubscribeInvalidated(
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
        var contract = new SetupPresentationModelContract();
        _ = contract.InvalidationEventArgsType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("replacement", 2);
        Task stale = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation first = workspace.Operation.Invocations[0];
        await first.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = OrderedInvalidationSignal(workspace, first);

        Task current = workspace.ExecuteAsync(Guid.CreateVersion7());
        await invalidated.Signal.Task.WaitAsync(SignalTimeout);
        await first.Cancelled.Task.WaitAsync(SignalTimeout);
        await workspace.Operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        workspace.Complete(0, CoreResult(), new byte[] { 1 });
        await stale.WaitAsync(SignalTimeout);
        workspace.Complete(1, CoreResult(), new byte[] { 2 });
        await current.WaitAsync(SignalTimeout);

        await Assert.That(workspace.GenerationValue).IsEqualTo(2);
        invalidated.Subscription.Dispose();
    }

    [Test]
    public async Task DeactivationInvalidatesBeforeCancellationAndUnregistersRecipient()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.InvalidationEventArgsType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("deactivation");
        workspace.Activate();
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = workspace.Operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = OrderedInvalidationSignal(workspace, invocation);

        workspace.Deactivate();
        await invalidated.Signal.Task.WaitAsync(SignalTimeout);
        await invocation.Cancelled.Task.WaitAsync(SignalTimeout);
        workspace.Complete(0, CoreResult(), ReadOnlyMemory<byte>.Empty);
        await execution.WaitAsync(SignalTimeout);
        session.PublishSettlement(
            "sender",
            Guid.CreateVersion7(),
            contract.CreateGeneration(9),
            "Succeeded");

        await Assert.That(workspace.ReceivedCompletionCount).IsEqualTo(0);
        invalidated.Subscription.Dispose();
    }

    [Test]
    public async Task DisposalInvalidatesBeforeCancellationAndIsIdempotent()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.InvalidationEventArgsType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("disposal");
        workspace.Activate();
        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        ControlledPresentationInvocation invocation = workspace.Operation.Invocations[0];
        await invocation.Started.Task.WaitAsync(SignalTimeout);
        var invalidated = OrderedInvalidationSignal(workspace, invocation);

        workspace.Dispose();
        await invalidated.Signal.Task.WaitAsync(SignalTimeout);
        await invocation.Cancelled.Task.WaitAsync(SignalTimeout);
        workspace.Complete(0, CoreResult(), new byte[] { 5 });
        await execution.WaitAsync(SignalTimeout);
        workspace.Dispose();

        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.ReceivedCompletionCount).IsEqualTo(0);
        invalidated.Subscription.Dispose();
    }

    [Test]
    public async Task DuplicateSettlementsRaceAtBarrierAndCommitPublishRefreshDiscardExactlyOnce()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.WorkspaceType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("settlement-race", 2);
        using PresentationWorkspaceHandle recipient = session.CreateWorkspace("observer");
        recipient.Activate();
        Guid operationId = Guid.CreateVersion7();
        Task staleExecution = workspace.ExecuteAsync(operationId);
        await workspace.Operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        Task currentExecution = workspace.ExecuteAsync(operationId);
        await workspace.Operation.Invocations[0].Cancelled.Task.WaitAsync(SignalTimeout);
        await workspace.Operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        object staleResult = CoreResult();
        object currentResult = CoreResult();
        int resultRefreshes = 0;
        int commandRefreshes = 0;
        int discards = 0;
        var discarded = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable propertySubscription = workspace.SubscribePropertyChanged((_, args) =>
        {
            if (args.PropertyName == "Result")
                Interlocked.Increment(ref resultRefreshes);
        });
        EventHandler commandHandler = (_, _) => Interlocked.Increment(ref commandRefreshes);
        workspace.ExecuteCommand.CanExecuteChanged += commandHandler;
        using IDisposable discardSubscription = workspace.SubscribeDiscarded(args =>
        {
            Interlocked.Increment(ref discards);
            discarded.TrySetResult(args);
        });
        using var barrier = new Barrier(2);

        Task[] contenders =
        [
            Task.Run(() =>
            {
                if (!barrier.SignalAndWait(SignalTimeout))
                    throw new TimeoutException("settlement-barrier-timeout");
                workspace.Complete(0, staleResult, new byte[] { 3 });
            }),
            Task.Run(() =>
            {
                if (!barrier.SignalAndWait(SignalTimeout))
                    throw new TimeoutException("settlement-barrier-timeout");
                workspace.Complete(1, currentResult, new byte[] { 7 });
            })
        ];
        await Task.WhenAll(contenders).WaitAsync(SignalTimeout);
        await Task.WhenAll(staleExecution, currentExecution).WaitAsync(SignalTimeout);
        EventArgs discard = await discarded.Task.WaitAsync(SignalTimeout);

        await Assert.That(ReferenceEquals(workspace.Result, currentResult)).IsTrue();
        await Assert.That(recipient.ReceivedCompletionCount).IsEqualTo(1);
        await Assert.That(resultRefreshes).IsEqualTo(1);
        await Assert.That(commandRefreshes).IsEqualTo(1);
        await Assert.That(discards).IsEqualTo(1);
        await AssertValueFreeSignal(discard);
        workspace.ExecuteCommand.CanExecuteChanged -= commandHandler;
    }

    [Test]
    public async Task BoundedAllocatorReachesFinalGenerationThenTerminatesRetainedCompletion()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.RequireProductType("ISetupOperationGenerationAllocator");
        using SetupPresentationSessionHandle session = contract.CreateSession(
            long.MaxValue - 1,
            long.MaxValue,
            null);
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("exhaustion", 3);
        Task penultimate = workspace.ExecuteAsync(Guid.CreateVersion7());
        await workspace.Operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        Task retainedFinal = workspace.ExecuteAsync(Guid.CreateVersion7());
        await workspace.Operation.Invocations[0].Cancelled.Task.WaitAsync(SignalTimeout);
        await workspace.Operation.Invocations[1].Started.Task.WaitAsync(SignalTimeout);
        workspace.Complete(0, CoreResult(), new byte[] { 1 });
        await penultimate.WaitAsync(SignalTimeout);
        Guid sessionId = session.SessionId;

        Task terminated = workspace.ExecuteAsync(Guid.CreateVersion7());
        await terminated.WaitAsync(SignalTimeout);
        await workspace.Operation.Invocations[1].Cancelled.Task.WaitAsync(SignalTimeout);
        workspace.Complete(1, CoreResult(), new byte[] { 9 });
        await retainedFinal.WaitAsync(SignalTimeout);

        await Assert.That(workspace.GenerationValue).IsEqualTo(long.MaxValue);
        await Assert.That(workspace.IsTerminated).IsTrue();
        await Assert.That(session.IsTerminated).IsTrue();
        await Assert.That(session.SessionId).IsEqualTo(sessionId);
        await Assert.That(workspace.Operation.Invocations[2].Started.Task.IsCompleted).IsFalse();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(workspace.Output).IsEmpty();
    }

    [Test]
    public async Task AllocatorDuplicateOrDecreaseTerminatesInsteadOfWrappingOrReseeding()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.RequireProductType("ISetupOperationGenerationAllocator");
        await AssertInvalidAllocationTerminates(contract, 41);
        await AssertInvalidAllocationTerminates(contract, 40);
    }

    [Test]
    public async Task CompletionMessagesAreImmutableTypedAndValueFree()
    {
        var contract = new SetupPresentationModelContract();
        Type message = contract.SettledMessageType;
        Type generation = contract.RequireProductType("SetupOperationGeneration");
        Type workspaceId = contract.RequireProductType("SetupWorkspaceId");
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
    public async Task ToolkitGeneratedPropertyRaisesBehavioralNotification()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.WorkspaceType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("notification");
        var changed = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = workspace.SubscribePropertyChanged((_, args) =>
            changed.TrySetResult(args.PropertyName));

        bool accepted = workspace.SetPublicInput("public-title");
        string? property = await changed.Task.WaitAsync(SignalTimeout);

        await Assert.That(accepted).IsTrue();
        await Assert.That(property).IsEqualTo("PublicInput");
        await Assert.That(workspace.PublicInput).IsEqualTo("public-title");
    }

    [Test]
    public async Task AsyncCommandsRefreshEligibilityExecuteAndCancelThroughPublicBehavior()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.WorkspaceType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("commands");
        Guid operationId = Guid.CreateVersion7();
        object execute = workspace.ExecuteCommand;

        await Assert.That(execute.GetType().GetInterfaces().Any(type =>
            type.FullName == "CommunityToolkit.Mvvm.Input.IAsyncRelayCommand")).IsTrue();
        await Assert.That(workspace.ExecuteCommand.CanExecute(operationId)).IsTrue();
        await Assert.That(workspace.CancelCommand.CanExecute(null)).IsFalse();

        workspace.ExecuteCommand.Execute(operationId);
        await workspace.Operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        await Assert.That(workspace.IsBusy).IsTrue();
        await Assert.That(workspace.ExecuteCommand.CanExecute(operationId)).IsFalse();
        await Assert.That(workspace.CancelCommand.CanExecute(null)).IsTrue();

        workspace.CancelCommand.Execute(null);
        await workspace.Operation.Invocations[0].Cancelled.Task.WaitAsync(SignalTimeout);
        Task executionTask = (Task)SetupPresentationModelContract.Property(execute, "ExecutionTask");
        await executionTask.WaitAsync(SignalTimeout);
        await Assert.That(workspace.IsBusy).IsFalse();
        await Assert.That(workspace.ExecuteCommand.CanExecute(operationId)).IsTrue();
        await Assert.That(workspace.CancelCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task CoreResultAndReadOnlyMemoryProjectWithOwnedBytes()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.RequireProductType("SetupPresentationOutcome");
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("core-projection");
        object coreResult = CoreResult();
        byte[] backing = [2, 3, 5, 7, 11];
        ReadOnlyMemory<byte> bytes = new(backing, 1, 3);

        Task execution = workspace.ExecuteAsync(Guid.CreateVersion7());
        await workspace.Operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        workspace.Complete(0, coreResult, bytes);
        await execution.WaitAsync(SignalTimeout);

        await Assert.That(ReferenceEquals(workspace.Result, coreResult)).IsTrue();
        await AssertOwnedMemory(workspace.Output, bytes);
    }

    [Test]
    public async Task GenerationTypeFlowsEndToEndWithoutPrimitiveIdentity()
    {
        var contract = new SetupPresentationModelContract();
        Type generation = contract.RequireProductType("SetupOperationGeneration");
        Type workspace = contract.WorkspaceType;
        Type message = contract.SettledMessageType;
        Type allocator = contract.RequireProductType("ISetupOperationGenerationAllocator");

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
        var contract = new SetupPresentationModelContract();
        _ = contract.RequireProductType("SetupWorkspaceId");
        using SetupPresentationSessionHandle session = contract.CreateSession();
        int maximum = session.WorkspaceIdMaxLength();

        await Assert.That(maximum).IsGreaterThan(0);
        await Assert.That(maximum).IsLessThanOrEqualTo(256);
        await Assert.That(session.TryCreateWorkspaceId("bounded-workspace", out object? accepted)).IsTrue();
        await Assert.That(accepted).IsNotNull();
        await Assert.That(session.TryCreateWorkspaceId(string.Empty, out _)).IsFalse();
        await Assert.That(session.TryCreateWorkspaceId("   ", out _)).IsFalse();
        await Assert.That(session.TryCreateWorkspaceId(new string('w', maximum + 1), out _)).IsFalse();
    }

    [Test]
    public async Task DynamicCanaryAppearsOnlyAsExplicitlyAcceptedPublicEditValue()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.WorkspaceType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("canary-surface");
        string canary = $"{Guid.CreateVersion7():N}-{Guid.NewGuid():N}";
        await Assert.That(workspace.SetPublicInput(canary)).IsTrue();
        await Assert.That(workspace.PublicInput).IsEqualTo(canary);

        var observed = new List<string>();
        foreach (object owner in new[] { session.Session, workspace.Workspace })
        {
            observed.Add(owner.ToString() ?? string.Empty);
            foreach (PropertyInfo property in owner.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (owner == workspace.Workspace && property.Name == "PublicInput")
                    continue;
                if (property.GetIndexParameters().Length != 0 || property.GetMethod is null)
                    continue;
                try
                {
                    object? value = property.GetValue(owner);
                    observed.Add(value?.ToString() ?? string.Empty);
                }
                catch (TargetInvocationException exception)
                {
                    observed.Add(exception.InnerException?.Message ?? exception.Message);
                }
            }
        }

        string oversized = string.Concat(
            Enumerable.Repeat(canary, workspace.MaxPublicInputLength + 1));
        Exception? error = null;
        try
        {
            _ = workspace.SetPublicInput(oversized);
        }
        catch (Exception exception)
        {
            error = exception;
        }
        observed.Add(error?.Message ?? string.Empty);
        observed.Add(error?.ToString() ?? string.Empty);

        await Assert.That(observed.Any(value => value.Contains(canary, StringComparison.Ordinal))).IsFalse();
        await Assert.That(contract.PublicPresentationTypes().SelectMany(type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly)).Any(property => ForbiddenSensitiveName(property.Name))).IsFalse();
    }

    [Test]
    public async Task SharedPresentationReportsOnlyTargetAgnosticAccessibilityStatus()
    {
        var contract = new SetupPresentationModelContract();
        _ = contract.WorkspaceType;
        using SetupPresentationSessionHandle session = contract.CreateSession();
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace("accessibility");

        await Assert.That(workspace.AccessibilityStatus).IsEqualTo("NotEvaluated");
    }

    private static async Task AssertInvalidAllocationTerminates(
        SetupPresentationModelContract contract,
        long invalid)
    {
        using SetupPresentationSessionHandle session = contract.CreateSession(41, invalid);
        using PresentationWorkspaceHandle workspace = session.CreateWorkspace($"invalid-{invalid}", 2);
        Task first = workspace.ExecuteAsync(Guid.CreateVersion7());
        await workspace.Operation.Invocations[0].Started.Task.WaitAsync(SignalTimeout);
        workspace.Complete(0, CoreResult(), ReadOnlyMemory<byte>.Empty);
        await first.WaitAsync(SignalTimeout);

        await workspace.ExecuteAsync(Guid.CreateVersion7()).WaitAsync(SignalTimeout);

        await Assert.That(workspace.GenerationValue).IsEqualTo(41);
        await Assert.That(workspace.IsTerminated).IsTrue();
        await Assert.That(workspace.Operation.Invocations[1].Started.Task.IsCompleted).IsFalse();
    }

    private static (TaskCompletionSource<EventArgs> Signal, IDisposable Subscription)
        OrderedInvalidationSignal(
            PresentationWorkspaceHandle workspace,
            ControlledPresentationInvocation invocation)
    {
        var signal = new TaskCompletionSource<EventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable subscription = workspace.SubscribeInvalidated(args =>
        {
            if (invocation.Cancelled.Task.IsCompleted)
                signal.TrySetException(new InvalidOperationException(
                    "invalidation-after-cancellation"));
            else
                signal.TrySetResult(args);
        });
        return (signal, subscription);
    }

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
