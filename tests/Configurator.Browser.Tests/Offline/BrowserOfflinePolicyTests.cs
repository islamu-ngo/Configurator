namespace Configurator.Browser.Tests;

using Bunit;
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
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, true)).IsFalse();
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
        await Assert.That(await policy.PrepareOfflineAsync(BrowserFixture.Definition, false)).IsFalse();
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
