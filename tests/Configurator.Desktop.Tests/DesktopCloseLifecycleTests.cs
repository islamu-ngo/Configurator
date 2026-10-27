namespace Configurator.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Configurator.Artifacts;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

public sealed class DesktopCloseLifecycleTests
{
    [Test]
    public async Task DeadlineKeepsUnknownWorkOwnedAndStayOpenWithdrawsOnlyCloseIntent()
    {
        var clock = new CloseClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root(save: async (_, _, _) =>
        {
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return ProtectedArtifactCommitResult.Installed(true);
        }, protectedOutputAvailable: true, timeProvider: clock);
        await PrepareAsync(shell);
        Task saving = shell.Publisher.SaveAsync();
        var decision = DecisionSignal(shell);
        Task<bool> closing = shell.RequestCloseAsync();
        try
        {
            await Assert.That(ReferenceEquals(closing, shell.RequestCloseAsync())).IsTrue();
            await Assert.That(clock.RequestedDueTime).IsEqualTo(TimeSpan.FromSeconds(2));
            clock.Advance(TimeSpan.FromMilliseconds(1999));
            await Assert.That(shell.CloseDecisionRequired).IsFalse();
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await decision.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(closing.IsCompleted).IsFalse();
            await Assert.That(shell.CloseDecisionRequired && shell.IsClosing && shell.IsBusy).IsTrue();
            await Assert.That(shell.Publisher.CurrentPublication!.IsUnknown).IsTrue();
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            shell.StayOpen();
            await Assert.That(await closing.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
            await Assert.That(shell.IsClosing || shell.CloseDecisionRequired || shell.IsCloseAuthorized).IsFalse();
            await Assert.That(shell.IsBusy).IsTrue();
        }
        finally { release.TrySetResult(); }
        await saving.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(shell.IsCloseAuthorized).IsFalse();
        await Assert.That(shell.Workspace.Form).IsNotNull();
        await Assert.That(shell.Publisher.PublicationHistory.Single().Outcomes.Single().Result!.Succeeded).IsTrue();
        await Assert.That(await shell.RequestCloseAsync()).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeliberateForceCloseRetainsPhysicalOwnerAndFencesLateCallbacks(bool fail)
    {
        var clock = new CloseClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadOnlyMemory<byte> borrowed = default;
        using var shell = DesktopTestContext.Root(save: async (_, bytes, _) =>
        {
            borrowed = bytes;
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            if (fail) throw new IOException("synthetic-private-exception");
            return ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true);
        }, protectedOutputAvailable: true, timeProvider: clock);
        await PrepareAsync(shell);
        Task saving = shell.Publisher.SaveAsync();
        Task physical = shell.ActiveCompletion;
        Task decision = DecisionSignal(shell);
        Task<bool> closing = shell.RequestCloseAsync();
        try
        {
            shell.ForceClose();
            await Assert.That(closing.IsCompleted).IsFalse();
            clock.Advance(TimeSpan.FromSeconds(2));
            await decision.WaitAsync(TimeSpan.FromSeconds(10));
            shell.ForceClose();
            await Assert.That(await closing.WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
            shell.Dispose();
            shell.Dispose();
            await Assert.That(physical.IsCompleted).IsFalse();
            await Assert.That(borrowed.Span.SequenceEqual("NAME=\"Example service\"\n"u8)).IsTrue();
        }
        finally { release.TrySetResult(); }
        string status = shell.Status;
        await saving.WaitAsync(TimeSpan.FromSeconds(10));
        await physical.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(shell.Status).IsEqualTo(status);
        await Assert.That(shell.Workspace.Form).IsNull();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(borrowed.ToArray().All(value => value == 0)).IsTrue();
        ProtectedArtifactPublicationReceipt receipt = shell.Publisher.PublicationHistory.Single();
        await Assert.That(receipt.IsSettled).IsTrue();
        await Assert.That(receipt.Failed).IsEqualTo(fail);
        await Assert.That(shell.Publisher.PublicationSummary).DoesNotContain("synthetic-private");
        if (!fail)
            await Assert.That(receipt.Outcomes.Single().Result!.InstallationEstablished).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SettlementAndDeadlineOrderingsAuthorizeOnlyOneActiveClose(bool deadlineFirst)
    {
        var clock = new CloseClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root(save: async (_, _, _) =>
        {
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return ProtectedArtifactCommitResult.Installed(true);
        }, protectedOutputAvailable: true, timeProvider: clock);
        await PrepareAsync(shell);
        Task saving = shell.Publisher.SaveAsync();
        Task decision = DecisionSignal(shell);
        Task<bool> closing = shell.RequestCloseAsync();
        try
        {
            if (deadlineFirst)
            {
                clock.Advance(TimeSpan.FromSeconds(2));
                await decision.WaitAsync(TimeSpan.FromSeconds(10));
            }
            release.TrySetResult();
            await saving.WaitAsync(TimeSpan.FromSeconds(10));
            if (!deadlineFirst) clock.Advance(TimeSpan.FromSeconds(2));
            await Assert.That(await closing.WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
            await Assert.That(shell.IsCloseAuthorized).IsTrue();
            await Assert.That(shell.CloseDecisionRequired).IsFalse();
            await Assert.That(shell.IsBusy).IsFalse();
        }
        finally { release.TrySetResult(); }
    }

    [Test]
    public async Task AWithdrawnCloseCannotAuthorizeItsReplacementIntent()
    {
        var clock = new CloseClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root(save: async (_, _, _) =>
        {
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return ProtectedArtifactCommitResult.Installed(true);
        }, protectedOutputAvailable: true, timeProvider: clock);
        await PrepareAsync(shell);
        Task saving = shell.Publisher.SaveAsync();
        Task<bool> first = shell.RequestCloseAsync();
        shell.StayOpen();
        Task<bool> second = shell.RequestCloseAsync();
        try
        {
            await Assert.That(ReferenceEquals(first, second)).IsFalse();
            await Assert.That(await first.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
            await Assert.That(shell.IsClosing).IsTrue();
            release.TrySetResult();
            await saving.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(await second.WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        }
        finally { release.TrySetResult(); }
    }

    private static async Task PrepareAsync(DesktopShellViewModel shell)
    {
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ArtifactId = "environment";
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
    }

    internal static Task DecisionSignal(DesktopShellViewModel shell)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (!shell.CloseDecisionRequired) return;
            shell.PropertyChanged -= Changed;
            signal.TrySetResult();
        }
        shell.PropertyChanged += Changed;
        return signal.Task;
    }

    internal sealed class CloseClock : TimeProvider
    {
        private readonly List<CloseTimer> _timers = [];
        internal TimeSpan RequestedDueTime { get; private set; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            RequestedDueTime = dueTime;
            var timer = new CloseTimer(callback, state, dueTime);
            _timers.Add(timer);
            return timer;
        }
        internal void Advance(TimeSpan elapsed)
        {
            foreach (CloseTimer timer in _timers.ToArray()) timer.Advance(elapsed);
        }

        private sealed class CloseTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
        {
            private readonly object _gate = new();
            private TimeSpan _remaining = dueTime;
            private bool _disposed;
            public bool Change(TimeSpan due, TimeSpan period)
            {
                lock (_gate)
                {
                    if (_disposed) return false;
                    _remaining = due;
                    return true;
                }
            }
            internal void Advance(TimeSpan elapsed)
            {
                lock (_gate)
                {
                    if (_disposed || _remaining == Timeout.InfiniteTimeSpan) return;
                    _remaining -= elapsed;
                    if (_remaining > TimeSpan.Zero) return;
                    _remaining = Timeout.InfiniteTimeSpan;
                }
                callback(state);
            }
            public void Dispose() { lock (_gate) _disposed = true; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    [Test]
    [NotInParallel]
    public async Task OrdinaryWindowCloseWaitsForPhysicalSettlement()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            bool finished = await session.Dispatch(async () =>
            {
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                ReadOnlyMemory<byte> borrowed = default;
                using var shell = DesktopTestContext.Root(save: async (_, bytes, _) =>
                {
                    borrowed = bytes;
                    entered.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                    return ProtectedArtifactCommitResult.Installed(true);
                }, protectedOutputAvailable: true);
                var window = new MainWindow { DataContext = shell };
                int closeCount = 0;
                window.Closed += (_, _) => { closeCount++; closed.TrySetResult(); };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                    shell.Publisher.ArtifactId = "environment";
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
                    Task saving = shell.Publisher.SaveAsync();
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    window.Close();
                    await Assert.That(window.IsVisible).IsTrue();
                    await Assert.That(closed.Task.IsCompleted).IsFalse();
                    await Assert.That(shell.IsBusy).IsTrue();
                    await Assert.That(borrowed.Span.SequenceEqual("NAME=\"Example service\"\n"u8)).IsTrue();
                    window.Close();
                    release.TrySetResult();
                    await saving.WaitAsync(TimeSpan.FromSeconds(10));
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await Assert.That(closeCount).IsEqualTo(1);
                    await Assert.That(shell.Workspace.Form).IsNull();
                    await Assert.That(borrowed.ToArray().All(value => value == 0)).IsTrue();
                    return true;
                }
                finally
                {
                    release.TrySetResult();
                    await shell.ActiveCompletion.WaitAsync(TimeSpan.FromSeconds(10));
                    window.Close();
                }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(finished).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }
}
