namespace Configurator.Browser.Tests;

using Configurator.Browser.Services;

public sealed class BrowserNavigationStateTests
{
    [Test]
    public async Task PendingResolutionCannotLeaveAReplacementRegistration()
    {
        var navigation = new BrowserNavigationState();
        var resolution = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new PendingOwner(_ =>
        {
            entered.SetResult();
            return resolution.Task;
        });
        navigation.Register(owner, () => { }, () => Task.CompletedTask, () => ("en", "ltr"), () => true);
        int confirmations = 0, discarded = 0;
        Task<bool> leaving = navigation.TryLeaveAsync(() =>
        {
            confirmations++;
            return Task.FromResult(true);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            navigation.Register(owner, () => { }, () => { discarded++; return Task.CompletedTask; },
                () => ("en", "ltr"), () => true);
        }
        finally { resolution.TrySetResult(true); }

        await Assert.That(await leaving.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(confirmations).IsEqualTo(0);
        await Assert.That(discarded).IsEqualTo(0);
        await Assert.That(navigation.IsActive).IsTrue();
    }

    [Test]
    public async Task StayingWithPendingDocumentPreventsWorkspaceLossConfirmation()
    {
        var navigation = new BrowserNavigationState();
        var owner = new PendingOwner(_ => Task.FromResult(false));
        int confirmations = 0, discarded = 0;
        navigation.Register(owner, () => { }, () => { discarded++; return Task.CompletedTask; },
            () => ("en", "ltr"), () => true);

        bool left = await navigation.TryLeaveAsync(() =>
        {
            confirmations++;
            return Task.FromResult(true);
        });

        await Assert.That(left).IsFalse();
        await Assert.That(confirmations).IsEqualTo(0);
        await Assert.That(discarded).IsEqualTo(0);
        await Assert.That(navigation.IsActive).IsTrue();
    }

    [Test]
    public async Task DiscardingOnlyPendingTextDoesNotPromptForAnUntouchedForm()
    {
        var navigation = new BrowserNavigationState();
        bool work = true;
        long lease = 0;
        var owner = new PendingOwner(current =>
        {
            work = false;
            navigation.RefreshWork(current, lease);
            return Task.FromResult(true);
        });
        lease = navigation.Register(owner, () => { }, () => Task.CompletedTask,
            () => ("en", "ltr"), () => work);
        int confirmations = 0;
        bool left = await navigation.TryLeaveAsync(() =>
        {
            confirmations++;
            return Task.FromResult(true);
        });

        await Assert.That(left).IsTrue();
        await Assert.That(confirmations).IsEqualTo(0);
        await Assert.That(navigation.HasWork).IsFalse();
    }

    [Test]
    public async Task KeepingEditingPreservesOwnerAndDoesNotDiscard()
    {
        var navigation = new BrowserNavigationState();
        int discarded = 0;
        navigation.Register(new object(), () => discarded++, () => Task.CompletedTask, () => ("en", "ltr"), () => true);
        bool left = await navigation.TryLeaveAsync(() => Task.FromResult(false));
        await Assert.That(left).IsFalse();
        await Assert.That(navigation.IsActive).IsTrue();
        await Assert.That(discarded).IsEqualTo(0);
    }

    [Test]
    public async Task ConfirmedDiscardInvalidatesBeforeAwaitingCleanup()
    {
        var navigation = new BrowserNavigationState();
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool invalidated = false;
        navigation.Register(new object(), () => invalidated = true, () => { entered.SetResult(); return cleanup.Task; }, () => ("en", "ltr"), () => true);
        Task<bool> leaving = navigation.TryLeaveAsync(() => Task.FromResult(true));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(invalidated).IsTrue();
        await Assert.That(leaving.IsCompleted).IsFalse();
        cleanup.SetResult();
        await Assert.That(await leaving).IsTrue();
        await Assert.That(navigation.IsActive).IsFalse();
    }

    [Test]
    [Arguments("same-owner")]
    [Arguments("released-same-owner")]
    [Arguments("different-owner")]
    public async Task CleanupCannotLeaveOrReleaseReplacementRegistration(string replacement)
    {
        var navigation = new BrowserNavigationState();
        object owner = new();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool invalidated = false;
        bool replacementInvalidated = false;
        bool replacementDiscarded = false;
        long lease = navigation.Register(owner, () => invalidated = true, () =>
        {
            entered.TrySetResult();
            return cleanup.Task;
        }, () => ("en", "ltr"), () => true);
        Task<bool> leaving = navigation.TryLeaveAsync(() => Task.FromResult(true));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            await Assert.That(invalidated).IsTrue();
            if (replacement == "released-same-owner") navigation.Release(owner, lease);
            navigation.Register(replacement == "different-owner" ? new object() : owner,
                () => replacementInvalidated = true,
                () => { replacementDiscarded = true; return Task.CompletedTask; }, () => ("en", "ltr"), () => true);
        }
        finally { cleanup.TrySetResult(); }

        await Assert.That(await leaving.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None)).IsFalse();
        await Assert.That(navigation.IsActive).IsTrue();
        await Assert.That(replacementInvalidated).IsFalse();
        await Assert.That(replacementDiscarded).IsFalse();
        await Assert.That(await navigation.TryLeaveAsync(() => Task.FromResult(true))).IsTrue();
        await Assert.That(replacementInvalidated).IsTrue();
        await Assert.That(replacementDiscarded).IsTrue();
        await Assert.That(navigation.IsActive).IsFalse();
    }

    [Test]
    public async Task LateConfirmationCannotDiscardReplacementEditor()
    {
        var navigation = new BrowserNavigationState();
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        object older = new();
        int discarded = 0;
        long olderLease = navigation.Register(older, () => discarded++, () => Task.CompletedTask, () => ("en", "ltr"), () => true);
        Task<bool> leaving = navigation.TryLeaveAsync(() => confirmation.Task);
        object current = new();
        navigation.Register(current, () => discarded++, () => Task.CompletedTask, () => ("en", "ltr"), () => true);
        navigation.Release(older, olderLease);
        confirmation.SetResult(true);
        await Assert.That(await leaving).IsFalse();
        await Assert.That(discarded).IsEqualTo(0);
        await Assert.That(navigation.IsActive).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StaleReleaseCannotDiscardReacquiredOwner(bool releasePrevious)
    {
        var navigation = new BrowserNavigationState();
        object owner = new();
        bool currentDiscarded = false;
        long previous = navigation.Register(owner, () => { }, () => Task.CompletedTask, () => ("en", "ltr"), () => true);
        if (releasePrevious) navigation.Release(owner, previous);
        navigation.Register(owner, () => { }, () =>
        {
            currentDiscarded = true;
            return Task.CompletedTask;
        }, () => ("en", "ltr"), () => true);
        navigation.Release(owner, previous);
        await Assert.That(navigation.IsActive).IsTrue();
        await Assert.That(currentDiscarded).IsFalse();
        await Assert.That(await navigation.TryLeaveAsync(() => Task.FromResult(true))).IsTrue();
        await Assert.That(currentDiscarded).IsTrue();
        await Assert.That(navigation.IsActive).IsFalse();
    }

    [Test]
    public async Task ReentrantRegistrationCannotChangeReturnedLease()
    {
        var navigation = new BrowserNavigationState();
        object owner = new();
        bool replaced = false;
        long current = 0;
        navigation.Changed += Replace;
        long previous = navigation.Register(owner, () => { }, () => Task.CompletedTask, () => ("en", "ltr"), () => true);
        navigation.Changed -= Replace;
        navigation.Release(owner, previous);
        await Assert.That(navigation.IsActive).IsTrue();
        navigation.Release(owner, current);
        await Assert.That(navigation.IsActive).IsFalse();

        void Replace(object? sender, EventArgs args)
        {
            if (replaced) return;
            replaced = true;
            current = navigation.Register(owner, () => { }, () => Task.CompletedTask, () => ("en", "ltr"), () => true);
        }
    }

    [Test]
    public async Task ChangedWorkRetiresConfirmationWithoutRerenderingChrome()
    {
        var navigation = new BrowserNavigationState();
        object owner = new();
        bool discarded = false;
        long lease = navigation.Register(owner, () => { }, () => { discarded = true; return Task.CompletedTask; },
            () => ("en", "ltr"), () => true);
        int chromeChanges = 0, workChanges = 0;
        navigation.Changed += (_, _) => chromeChanges++;
        navigation.WorkChanged += (_, _) => workChanges++;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> leaving = navigation.TryLeaveAsync(() =>
        {
            entered.TrySetResult();
            return confirmation.Task;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            navigation.RefreshWork(owner, lease);
        }
        finally { confirmation.TrySetResult(true); }

        await Assert.That(await leaving.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(discarded).IsFalse();
        await Assert.That(chromeChanges).IsEqualTo(0);
        await Assert.That(workChanges).IsEqualTo(1);
        await Assert.That(navigation.IsActive).IsTrue();
    }

    private sealed class PendingOwner(Func<PendingOwner, Task<bool>> resolve) : IBrowserPendingChanges
    {
        public Task<bool> ResolvePendingAsync() => resolve(this);
    }
}
