namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Models;
using Microsoft.JSInterop;

public sealed class BrowserOfflinePolicyTests
{
    [Test]
    public async Task ReconnectingAfterOfflineInitializationEnablesCleanEntryWithoutReload()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        var connectivity = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        connectivity.Notify(true);
        await Assert.That(policy.CanFetchDefinition).IsTrue();
        await Assert.That(policy.OfflineReady).IsTrue();
        await Assert.That(policy.IsWorkspaceActive).IsFalse();
    }

    [Test]
    public async Task DisconnectingAfterOnlineInitializationDeniesCleanEntry()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        var connectivity = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.CanFetchDefinition).IsTrue();
        connectivity.Notify(false);
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(policy.IsOffline).IsTrue();
    }

    [Test]
    public async Task ReconnectingDuringLiveCustodyCannotGrantFetchEligibility()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        var connectivity = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        module.Setup<bool>("beginWorkspace", 1L).SetResult(true);
        module.SetupVoid("endWorkspace", 2L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        connectivity.Notify(false);
        await Assert.That(policy.IsOffline).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        connectivity.Notify(true);
        await Assert.That(policy.IsOffline).IsFalse();
        await Assert.That(policy.IsWorkspaceActive).IsTrue();
        await Assert.That(policy.WorkspaceGeneration).IsEqualTo(1L);
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await policy.EndWorkspaceAsync();
        await Assert.That(policy.CanFetchDefinition).IsTrue();
    }

    [Test]
    public async Task ConnectionNotificationDuringSubscriptionCannotBeOverwrittenByInitialStatus()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        var subscribed = new TaskCompletionSource<BrowserOfflinePolicy>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = module.Setup<long>("subscribeConnectivity", invocation =>
        {
            subscribed.SetResult(((DotNetObjectReference<BrowserOfflinePolicy>)invocation.Arguments[0]!).Value);
            return true;
        });
        module.SetupVoid("unsubscribeConnectivity", 1L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        Task initialization = policy.InitializeAsync().AsTask();
        BrowserOfflinePolicy receiver = await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receiver.ConnectivityChanged(true);
        subscription.SetResult(1L);
        await initialization.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(policy.CanFetchDefinition).IsTrue();
        await Assert.That(policy.OfflineReady).IsTrue();
    }

    [Test]
    public async Task DisposalWhileSubscribingRejectsQueuedNotificationsAndReleasesLateSubscription()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        var subscribed = new TaskCompletionSource<BrowserOfflinePolicy>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = module.Setup<long>("subscribeConnectivity", invocation =>
        {
            subscribed.SetResult(((DotNetObjectReference<BrowserOfflinePolicy>)invocation.Arguments[0]!).Value);
            return true;
        });
        bool unsubscribed = false;
        module.SetupVoid("unsubscribeConnectivity", invocation =>
        {
            unsubscribed = invocation.Arguments.Count == 1 && invocation.Arguments[0] is 7L;
            return unsubscribed;
        }).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        Task initialization = policy.InitializeAsync().AsTask();
        BrowserOfflinePolicy receiver = await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task disposal = policy.DisposeAsync().AsTask();
        receiver.ConnectivityChanged(true);
        await Assert.That(policy.IsOffline).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(disposal.IsCompleted).IsFalse();
        subscription.SetResult(7L);
        await Task.WhenAll(initialization, disposal).WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(unsubscribed).IsTrue();
        receiver.ConnectivityChanged(true);
        await Assert.That(policy.IsOffline).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
    }

    [Test]
    public async Task DisposalDuringInitializationCannotInstallALateConnectionSubscription()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        var connectivity = new ConnectivityNotifications(module);
        var state = module.Setup<BrowserOfflineState>("initialize");
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        Task initialization = policy.InitializeAsync().AsTask();
        Task disposal = policy.DisposeAsync().AsTask();
        state.SetResult(new(true, true, true));
        await Task.WhenAll(initialization, disposal).WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(connectivity.IsSubscribed).IsFalse();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsFalse();
    }

    [Test]
    public async Task DisposalUnsubscribesAndQueuedConnectionCallbacksCannotMutateThePolicy()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        var connectivity = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        connectivity.Notify(false);
        await Assert.That(policy.IsOffline).IsTrue();
        await Assert.That(connectivity.IsSubscribed).IsTrue();
        await policy.DisposeAsync();
        await Assert.That(connectivity.IsSubscribed).IsFalse();
        connectivity.Notify(true);
        await Assert.That(policy.IsOffline).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
    }

    [Test]
    public async Task IncompleteInstallationCannotStartAnswerCustodyOrClaimOfflineReadiness()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(false, false, true));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.OfflineReady).IsFalse();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsFalse();
        await Assert.That(policy.IsWorkspaceActive).IsFalse();
    }

    [Test]
    public async Task LiveAnswersRejectRemoteSourcesAndDefinitionRetentionUntilCleared()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        module.Setup<bool>("beginWorkspace", 1L).SetResult(true);
        module.SetupVoid("endWorkspace", 2L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, true))
            .IsEqualTo(BrowserOfflineRetentionStatus.BusyInWorkspace);
        await policy.EndWorkspaceAsync();
        await Assert.That(policy.CanFetchDefinition).IsTrue();
    }

    [Test]
    public async Task DisconnectedEntryRejectsRemoteSourcesAndRetentionRequiresExplicitPublicConfirmation()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, false))
            .IsEqualTo(BrowserOfflineRetentionStatus.InvalidDefinition);
    }

    [Test]
    public async Task RetainedDefinitionIsReadmittedAndDigestBoundBeforeReopening()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        module.Setup<byte[]?>("readDefinition", BrowserFixture.Definition.Digest.Value)
            .SetResult(BrowserFixture.Revision(2).CanonicalBytes.ToArray());
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(await policy.ReadRetainedDefinitionAsync(BrowserFixture.Definition.Digest.Value)).IsNull();
    }

    [Test]
    public async Task ClearWhileBeginningCannotResurrectLiveCustody()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        var beginning = module.Setup<bool>("beginWorkspace", 1L);
        module.SetupVoid("endWorkspace", 2L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        ValueTask<bool> start = policy.BeginWorkspaceAsync();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await policy.EndWorkspaceAsync();
        beginning.SetResult(true);
        await Assert.That(await start).IsFalse();
        await Assert.That(policy.IsWorkspaceActive).IsFalse();
    }

    [Test]
    public async Task DisposingAReplacedEditorCannotReleaseTheCurrentEditorsCustody()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        module.Setup<bool>("beginWorkspace", 1L).SetResult(true);
        module.Setup<bool>("beginWorkspace", 2L).SetResult(true);
        module.SetupVoid("endWorkspace", 3L).SetVoidResult();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        long replacedEditor = policy.WorkspaceGeneration;
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        await policy.EndWorkspaceAsync(replacedEditor);
        await Assert.That(policy.IsWorkspaceActive).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
    }

    [Test]
    public async Task FailedInitializationRecoversViaRetryWithoutDuplicatingConnectivitySubscription()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        int subscriptionCount = 0;
        Action<bool>? notify = null;
        module.Setup<long>("subscribeConnectivity", invocation =>
        {
            subscriptionCount++;
            notify = ((DotNetObjectReference<BrowserOfflinePolicy>)invocation.Arguments[0]!).Value.ConnectivityChanged;
            return true;
        }).SetResult(1L);
        module.SetupVoid("unsubscribeConnectivity", 1L).SetVoidResult();
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(false, false, true));
        module.Setup<BrowserOfflineState>("retryInitialize").SetResult(new(true, true, true));

        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.OfflineReady).IsFalse();
        await Assert.That(subscriptionCount).IsEqualTo(1);

        // Retrying after a failed init must recover readiness without adding a second subscription.
        await Assert.That(await policy.RetryInitializeAsync()).IsTrue();
        await Assert.That(policy.OfflineReady).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsTrue();
        await Assert.That(subscriptionCount).IsEqualTo(1);

        notify?.Invoke(false);
        await Assert.That(policy.IsOffline).IsTrue();
        notify?.Invoke(true);
        await Assert.That(policy.CanFetchDefinition).IsTrue();
    }

    [Test]
    public async Task WorkspaceActivationOrDisposalMidRetryCannotOverrideCustodyOrGrantFetchEligibility()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, false, true));
        var retryCall = module.Setup<BrowserOfflineState>("retryInitialize");
        module.Setup<bool>("beginWorkspace", 1L).SetResult(true);
        module.SetupVoid("endWorkspace", 2L).SetVoidResult();

        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await Assert.That(policy.OfflineReady).IsFalse();

        Task<bool> retryTask = policy.RetryInitializeAsync().AsTask();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        retryCall.SetResult(new(true, true, true));

        await Assert.That(await retryTask.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(policy.IsWorkspaceActive).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(policy.OfflineReady).IsFalse();

        await policy.EndWorkspaceAsync();

        // Disposal mid-retry must also discard the late completion.
        using var disposeContext = new BunitContext();
        var disposeModule = disposeContext.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(disposeModule);
        disposeModule.Setup<BrowserOfflineState>("initialize").SetResult(new(false, false, true));
        var disposeRetry = disposeModule.Setup<BrowserOfflineState>("retryInitialize");
        var disposablePolicy = new BrowserOfflinePolicy(disposeContext.JSInterop.JSRuntime);
        await disposablePolicy.InitializeAsync();

        Task<bool> pendingRetry = disposablePolicy.RetryInitializeAsync().AsTask();
        Task disposal = disposablePolicy.DisposeAsync().AsTask();
        disposeRetry.SetResult(new(true, true, true));

        await Task.WhenAll(pendingRetry, disposal).WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(await pendingRetry).IsFalse();
        await Assert.That(disposablePolicy.OfflineReady).IsFalse();
        await Assert.That(disposablePolicy.CanFetchDefinition).IsFalse();
    }

    [Test]
    public async Task PrepareOfflineDistinguishesRetainedCapacityReachedAndStorageUnavailable()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));

        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();

        module.Setup<string>("retainDefinition", _ => true).SetResult("retained");
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, true))
            .IsEqualTo(BrowserOfflineRetentionStatus.Retained);

        module.Setup<string>("retainDefinition", _ => true).SetResult("capacity-reached");
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, true))
            .IsEqualTo(BrowserOfflineRetentionStatus.CapacityReached);

        module.Setup<string>("retainDefinition", _ => true).SetResult("storage-unavailable");
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, true))
            .IsEqualTo(BrowserOfflineRetentionStatus.StorageUnavailable);

        module.Setup<string>("retainDefinition", _ => true).SetResult("workspace-active");
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, true))
            .IsEqualTo(BrowserOfflineRetentionStatus.BusyInWorkspace);
    }

    [Test]
    public async Task RemoveRetainedDefinitionDeletesOutsideCustodyAndFailsClosedDuringWorkspaceRace()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        _ = new ConnectivityNotifications(module);
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        module.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        module.SetupVoid("endWorkspace", _ => true).SetVoidResult();
        string digest = BrowserFixture.Definition.Digest.Value;

        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();

        module.Setup<bool>("deleteDefinition", digest).SetResult(true);
        await Assert.That(await policy.RemoveRetainedDefinitionAsync(digest)).IsTrue();

        // Workspace activation racing an in-flight delete must reject the late completion.
        var inflightDelete = module.Setup<bool>("deleteDefinition", digest);
        Task<bool> pendingDelete = policy.RemoveRetainedDefinitionAsync(digest).AsTask();
        await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue();
        inflightDelete.SetResult(true);
        await Assert.That(await pendingDelete.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();

        // Direct delete during active custody must refuse before invoking JS.
        int invocationsBefore = module.Invocations.Count(call => call.Identifier == "deleteDefinition");
        await Assert.That(await policy.RemoveRetainedDefinitionAsync(digest)).IsFalse();
        await Assert.That(module.Invocations.Count(call => call.Identifier == "deleteDefinition")).IsEqualTo(invocationsBefore);
        await policy.EndWorkspaceAsync();
    }

    private sealed class ConnectivityNotifications
    {
        private Action<bool>? _notify;

        internal bool IsSubscribed { get; private set; }

        internal ConnectivityNotifications(BunitJSModuleInterop module)
        {
            module.Setup<long>("subscribeConnectivity", invocation =>
            {
                _notify = ((DotNetObjectReference<BrowserOfflinePolicy>)invocation.Arguments[0]!).Value.ConnectivityChanged;
                IsSubscribed = true;
                return true;
            }).SetResult(1L);
            module.SetupVoid("unsubscribeConnectivity", invocation =>
            {
                if (invocation.Arguments.Count != 1 || invocation.Arguments[0] is not 1L) return false;
                IsSubscribed = false;
                return true;
            }).SetVoidResult();
        }

        internal void Notify(bool online)
        {
            // Dispatch the JS callback only when the managed owner actually subscribed.
            _notify?.Invoke(online);
        }
    }
}
