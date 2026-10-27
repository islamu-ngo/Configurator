namespace Configurator.Browser.Tests;

using Configurator.Browser.Services;

public sealed class BrowserNavigationStateTests
{
    [Test]
    public async Task KeepingEditingPreservesOwnerAndDoesNotDiscard()
    {
        var navigation = new BrowserNavigationState();
        int discarded = 0;
        navigation.Register(new object(), () => discarded++, () => Task.CompletedTask);
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
        navigation.Register(new object(), () => invalidated = true, () => { entered.SetResult(); return cleanup.Task; });
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
        });
        Task<bool> leaving = navigation.TryLeaveAsync(() => Task.FromResult(true));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            await Assert.That(invalidated).IsTrue();
            if (replacement == "released-same-owner") navigation.Release(owner, lease);
            navigation.Register(replacement == "different-owner" ? new object() : owner,
                () => replacementInvalidated = true,
                () => { replacementDiscarded = true; return Task.CompletedTask; });
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
        long olderLease = navigation.Register(older, () => discarded++, () => Task.CompletedTask);
        Task<bool> leaving = navigation.TryLeaveAsync(() => confirmation.Task);
        object current = new();
        navigation.Register(current, () => discarded++, () => Task.CompletedTask);
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
        long previous = navigation.Register(owner, () => { }, () => Task.CompletedTask);
        if (releasePrevious) navigation.Release(owner, previous);
        navigation.Register(owner, () => { }, () =>
        {
            currentDiscarded = true;
            return Task.CompletedTask;
        });
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
        long previous = navigation.Register(owner, () => { }, () => Task.CompletedTask);
        navigation.Changed -= Replace;
        navigation.Release(owner, previous);
        await Assert.That(navigation.IsActive).IsTrue();
        navigation.Release(owner, current);
        await Assert.That(navigation.IsActive).IsFalse();

        void Replace(object? sender, EventArgs args)
        {
            if (replaced) return;
            replaced = true;
            current = navigation.Register(owner, () => { }, () => Task.CompletedTask);
        }
    }
}
