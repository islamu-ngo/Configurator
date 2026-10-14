namespace Configurator.Terminal.Tests;

using System.Drawing;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Presentation;
using Configurator.Terminal;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.Views;

[NotInParallel]
public sealed class SetupTerminalImportOperationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ImportCommandReturnsAndAnotherOwnerActionRunsWhileReadIsSuspended(bool blockedOpen)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Harness? harness = null;
        Task? command = null;
        try
        {
            await owner.InvokeAsync(() => harness = new Harness(owner, async (_, _) =>
            {
                reached.TrySetResult();
                if (blockedOpen) release.Task.GetAwaiter().GetResult();
                else await release.Task.ConfigureAwait(false);
                return Encoding.UTF8.GetBytes("""{"name":"imported","token":"synthetic-private"}""");
            }));
            command = owner.InvokeAsync(() => harness!.Import());
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await command.WaitAsync(TimeSpan.FromSeconds(1));
            await owner.InvokeAsync(() => harness!.Window.PublicInput.Text = "new-visible-draft")
                .WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            if (command is { IsCompleted: false }) owner.RecoverBlockedCallbacks();
            release.TrySetResult();
            if (command is not null) await command.WaitAsync(TimeSpan.FromSeconds(5));
            owner.ResumeOwnerDispatch();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose()).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    [Arguments("edit")]
    [Arguments("target")]
    [Arguments("path")]
    [Arguments("clear")]
    [Arguments("dispose")]
    public async Task LateReadCannotReplaceNewerEditorTargetOrLifetime(string invalidation)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = Encoding.UTF8.GetBytes("""{"name":"late-answer","token":"synthetic-late-private"}""");
        Harness? harness = null;
        int reads = 0;
        string status = string.Empty;
        try
        {
            await owner.InvokeAsync(() => harness = new Harness(owner, async (_, _) =>
            {
                Interlocked.Increment(ref reads);
                reached.TrySetResult();
                await release.Task.ConfigureAwait(false);
                return bytes;
            }));
            await owner.InvokeAsync(() => harness!.Import());
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                switch (invalidation)
                {
                    case "edit": harness!.Window.PublicInput.Text = "uncommitted-new-answer"; break;
                    case "target": harness!.Button("Artifact").InvokeCommand(Command.Accept); break;
                    case "path": harness!.ImportPath.Text = "replacement.json"; break;
                    case "clear": harness!.Window.ClearPrivateState(); break;
                    case "dispose": harness!.Window.Dispose(); break;
                }
                if (invalidation != "dispose") harness!.Import();
                status = harness!.Window.StatusText;
            });
            await Assert.That(reads).IsEqualTo(1);
            release.TrySetResult();
            await harness!.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                if (invalidation == "dispose") return;
                if (harness.Form.Snapshot().Values.ContainsKey("token")) throw new InvalidOperationException("stale-import-applied");
                if (harness.Window.StatusText != status) throw new InvalidOperationException("stale-import-status");
            });
            await Assert.That(bytes.All(value => value == 0)).IsTrue();
        }
        finally
        {
            release.TrySetResult();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    [Test]
    public async Task SuccessfulImportAppliesOnOwnerAndClearsReadStorage()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        byte[] bytes = Encoding.UTF8.GetBytes("""{"name":"imported","token":"synthetic-private"}""");
        Harness? harness = null;
        int changedOn = 0;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(bytes));
                harness.Form.InputChanged += (_, _) => changedOn = Environment.CurrentManagedThreadId;
                harness.Import();
            });
            await harness!.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                if (harness.Form.Snapshot().Values["name"].GetString() != "imported")
                    throw new InvalidOperationException("import-not-applied");
                if (harness.Window.StatusText != SetupTerminalText.Get("ImportReady"))
                    throw new InvalidOperationException("import-status-missing");
            });
            await Assert.That(changedOn).IsEqualTo(owner.OwnerThreadId);
            await Assert.That(bytes.All(value => value == 0)).IsTrue();
        }
        finally { await owner.InvokeAsync(() => harness?.Dispose()); }
    }

    [Test]
    [Arguments("io")]
    [Arguments("permission")]
    [Arguments("oversize")]
    [Arguments("invalid")]
    public async Task RejectedReadLeavesAnswersAndDiagnosticsValueFree(string failure)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        byte[] bytes = failure == "oversize" ? new byte[4 * 1024 * 1024 + 1]
            : Encoding.UTF8.GetBytes("synthetic-private-invalid-content");
        Harness? harness = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => failure switch
                {
                    "io" => throw new IOException("synthetic-private-path"),
                    "permission" => throw new UnauthorizedAccessException("synthetic-private-path"),
                    _ => Task.FromResult<ReadOnlyMemory<byte>>(bytes)
                });
                harness.Import();
            });
            await harness!.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            string status = string.Empty;
            await owner.InvokeAsync(() =>
            {
                status = harness.Window.StatusText;
                if (harness.Form.Snapshot().Values.ContainsKey("token")) throw new InvalidOperationException("rejected-import-applied");
            });
            await Assert.That(status).Contains(SetupTerminalText.Get("ImportFailed"));
            await Assert.That(status).DoesNotContain("synthetic-private");
            if (failure is "oversize" or "invalid") await Assert.That(bytes.All(value => value == 0)).IsTrue();
        }
        finally { await owner.InvokeAsync(() => harness?.Dispose()); }
    }

    private sealed class Harness : IDisposable
    {
        private readonly IApplication _application;
        private readonly SetupPresentationSession _session;
        private SessionToken? _run;
        internal Harness(ControlledTerminalSynchronizationContext owner,
            Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>> reader,
            TimeProvider? timeProvider = null, Action<Action>? dispatch = null,
            Func<CancellationToken, Task>? beforePublication = null, bool nativeDispatch = false)
        {
            _application = Application.Create();
            _application.Init("dotnet");
            Form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
            Secret = new SetupTerminalSecretBuffer(262144, urlSafeOnly: false);
            Operation = new SetupTerminalArtifactOperation(Form, new ProtectedArtifactWriter(), Path.GetTempPath(), beforePublication);
            _session = new SetupPresentationSession(new StrongReferenceMessenger());
            SetupWorkspaceId.TryCreate("import", out var id);
            Workspace = _session.CreateWorkspace(id, Operation);
            Workspace.Activate();
            Window = new SetupTerminalWindow(_application, Workspace, Operation, Secret, reader,
                nativeDispatch ? null : dispatch ?? owner.Dispatch, timeProvider);
            Window.Frame = new Rectangle(0, 0, 100, 30);
            Window.ApplyViewportPolicy(Window.Frame.Size);
        }

        internal DefinitionFormModel Form { get; }
        internal SetupTerminalSecretBuffer Secret { get; }
        internal SetupTerminalArtifactOperation Operation { get; }
        internal SetupPresentationWorkspace Workspace { get; }
        internal SetupTerminalWindow Window { get; }
        internal TextField ImportPath => Window.SubViews.OfType<TextField>().Single(input => input.Y == 14);

        internal void Import()
        {
            ImportPath.Text = "synthetic-import.json";
            Button("ImportSelected").InvokeCommand(Command.Accept);
        }

        internal Button Button(string key) => Window.SubViews.OfType<Button>()
            .Single(button => button.Text == SetupTerminalText.Get(key));

        internal void Begin() => _run = _application.Begin(Window);
        internal void QueueFrameworkStop() => _application.Invoke(() => _application.RequestStop(Window));
        internal void RunImport()
        {
            _application.AddTimeout(TimeSpan.Zero, () => { Import(); return false; });
            _application.Run(Window);
        }

        public void Dispose()
        {
            if (_run is not null) { _application.End(_run); _run = null; }
            Window.Dispose();
            Workspace.Dispose();
            Operation.Dispose();
            Secret.Dispose();
            Form.Dispose();
            _session.Dispose();
            _application.Dispose();
        }
    }

    [Test]
    public async Task OffOwnerSignalClearsBufferImmediatelyAndChangesModelOnlyOnOwner()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        Harness? harness = null;
        SetupTerminalSignalScope? signals = null;
        var cleared = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty));
                harness.Form.Fields.Single(field => field.Definition.Id == "token").Text = "synthetic-private";
                harness.Secret.TryReplace("synthetic-private");
                harness.Form.InputChanged += (_, _) => cleared.TrySetResult(Environment.CurrentManagedThreadId);
                signals = new SetupTerminalSignalScope(harness.Secret, harness.Window.RequestStopFromSignal);
            });
            await Task.Run(() => { signals!.RequestStop(); signals.RequestStop(); });
            await Assert.That(harness!.Secret.Count).IsEqualTo(0);
            await Assert.That(await cleared.Task.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo(owner.OwnerThreadId);
        }
        finally
        {
            signals?.Dispose();
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    [Test]
    public async Task CloseKeepsOwnerSessionRunningWhileCancelledReadDrains()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Harness? harness = null;
        bool running = false;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, async (_, _) =>
                {
                    reached.TrySetResult();
                    await release.Task.ConfigureAwait(false);
                    return Encoding.UTF8.GetBytes("{}");
                });
                harness.Begin();
                harness.Import();
            });
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                harness!.Button("Close").InvokeCommand(Command.Accept);
                running = harness.Window.IsRunning && !harness.Window.StopRequested;
            });
            await Assert.That(running).IsTrue();
        }
        finally
        {
            release.TrySetResult();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    [Test]
    public async Task CloseDeadlineDetachesUiWhileOwnedReadEventuallyClearsBytes()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var clock = new CloseClock();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = Encoding.UTF8.GetBytes("""{"name":"late-answer","token":"synthetic-late-private"}""");
        Harness? harness = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, async (_, _) =>
                {
                    reached.TrySetResult();
                    await release.Task.ConfigureAwait(false);
                    return bytes;
                }, clock);
                harness.Begin();
                harness.Import();
            });
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness!.Button("Close").InvokeCommand(Command.Accept));
            clock.Advance(TimeSpan.FromMilliseconds(1999));
            bool stopped = true;
            await owner.InvokeAsync(() => stopped = harness!.Window.StopRequested);
            await Assert.That(stopped).IsFalse();
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await harness!.Window.ShutdownCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            string status = string.Empty;
            await owner.InvokeAsync(() =>
            {
                stopped = harness.Window.StopRequested;
                status = harness.Window.StatusText;
                harness.Window.Dispose();
            });
            await Assert.That(stopped).IsTrue();
            await Assert.That(status).IsEqualTo(SetupTerminalText.Get("ShutdownUnknown"));
            release.TrySetResult();
            await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(bytes.All(value => value == 0)).IsTrue();
        }
        finally
        {
            release.TrySetResult();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    [Test]
    public async Task QueuedImportAdmissionAfterDisposalCannotTouchFormOrView()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var queued = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = Encoding.UTF8.GetBytes("""{"name":"late-answer","token":"synthetic-late-private"}""");
        Harness? harness = null;
        int mutations = 0;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(bytes),
                    dispatch: action => queued.TrySetResult(action));
                harness.Import();
            });
            Action pending = await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                harness!.Window.Dispose();
                harness.Form.InputChanged += (_, _) => mutations++;
                pending();
            });
            await harness!.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(mutations).IsEqualTo(0);
            await Assert.That(bytes.All(value => value == 0)).IsTrue();
        }
        finally { await owner.InvokeAsync(() => harness?.Dispose()); }
    }

    [Test]
    public async Task SaveCommandObservesUnexpectedWorkerFaultWithoutExposingExceptionText()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var owner = new ControlledTerminalSynchronizationContext();
        Harness? harness = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty),
                    beforePublication: _ => throw new InvalidOperationException("synthetic-private-path"));
                harness.Window.PublicInput.Text = "sample";
                harness.Form.Fields.Single(input => input.Definition.Id == "token").Text = "synthetic-private";
                harness.Window.Prepare();
                harness.Button("SaveProtected").InvokeCommand(Command.Accept);
            });
            await harness!.Window.SaveCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            string status = string.Empty;
            await owner.InvokeAsync(() => status = harness.Window.StatusText);
            await Assert.That(harness.Workspace.IsBusy).IsFalse();
            await Assert.That(status).Contains("terminal-operation-failed");
            await Assert.That(status).DoesNotContain("synthetic-private-path");
        }
        finally { await owner.InvokeAsync(() => harness?.Dispose()); }
    }

    [Test]
    public async Task CancellationCannotRetireReadOwnershipWhileOwnerAdmissionIsExecuting()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var applying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = Encoding.UTF8.GetBytes("""{"name":"imported","token":"synthetic-private"}""");
        Harness? harness = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(bytes));
                harness.Form.InputChanged += (_, _) =>
                {
                    applying.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                };
                harness.Import();
            });
            await applying.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Run(() => harness!.Window.RequestStopFromSignal());
            await Assert.That(async () => await harness!.Window.ImportCompletion.WaitAsync(TimeSpan.FromMilliseconds(250)))
                .Throws<TimeoutException>();
            await Assert.That(harness!.Window.IsImportBusy).IsTrue();
        }
        finally
        {
            release.TrySetResult();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    [Test]
    public async Task FrameworkStopUsesTheSameBoundedReadDrainAsCloseButton()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Harness? harness = null;
        Task? running = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, async (_, _) =>
                {
                    reached.TrySetResult();
                    await release.Task.ConfigureAwait(false);
                    return Encoding.UTF8.GetBytes("{}");
                }, nativeDispatch: true);
            });
            running = owner.InvokeAsync(() => harness!.RunImport());
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness!.QueueFrameworkStop();
            await Assert.That(async () => await running.WaitAsync(TimeSpan.FromMilliseconds(250))).Throws<TimeoutException>();
        }
        finally
        {
            if (running?.IsCompleted == true)
                await owner.InvokeAsync(() => harness?.Window.RequestStopFromSignal());
            release.TrySetResult();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            if (running is not null) await running.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    private sealed class CloseClock : TimeProvider
    {
        private readonly List<CloseTimer> _timers = [];
        private TimeSpan _elapsed;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new CloseTimer(this, callback, state, _elapsed + dueTime);
            lock (_timers) _timers.Add(timer);
            return timer;
        }

        internal void Advance(TimeSpan elapsed)
        {
            CloseTimer[] due;
            lock (_timers)
            {
                _elapsed += elapsed;
                due = _timers.Where(timer => !timer.Disposed && timer.Due <= _elapsed).ToArray();
            }
            foreach (CloseTimer timer in due) timer.Fire();
        }

        private sealed class CloseTimer(CloseClock clock, TimerCallback callback, object? state, TimeSpan due) : ITimer
        {
            internal TimeSpan Due { get; private set; } = due;
            internal bool Disposed { get; private set; }
            internal void Fire() { Dispose(); callback(state); }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (clock._timers)
                {
                    if (Disposed) return false;
                    Due = clock._elapsed + dueTime;
                    return true;
                }
            }
            public void Dispose() { lock (clock._timers) Disposed = true; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
