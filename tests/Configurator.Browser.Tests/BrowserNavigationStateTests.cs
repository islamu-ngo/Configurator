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
    public async Task LateConfirmationCannotDiscardReplacementEditor()
    {
        var navigation = new BrowserNavigationState();
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        object older = new();
        int discarded = 0;
        navigation.Register(older, () => discarded++, () => Task.CompletedTask);
        Task<bool> leaving = navigation.TryLeaveAsync(() => confirmation.Task);
        object current = new();
        navigation.Register(current, () => discarded++, () => Task.CompletedTask);
        navigation.Release(older);
        confirmation.SetResult(true);
        await Assert.That(await leaving).IsFalse();
        await Assert.That(discarded).IsEqualTo(0);
        await Assert.That(navigation.IsActive).IsTrue();
    }
}
