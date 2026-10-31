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
    public async Task ProductionCheckpointSuspendsTheCurrentOwnerSlice()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        Task checkpoint = Task.CompletedTask;
        await owner.InvokeAsync(() =>
        {
            checkpoint = SetupTerminalWindow.YieldToApplicationAsync(CancellationToken.None).AsTask();
            // The owner cannot process its posted continuation until this action returns.
            if (checkpoint.IsCompleted) throw new InvalidOperationException("checkpoint-did-not-suspend-owner");
        });
        await checkpoint.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConsumedOrClearedImportPathCannotBeRestoredByUndo(bool compare)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Harness? harness = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => read.Task);
                harness.ImportPath.Text = "synthetic-private-import.json";
                if (compare) harness.Button("ImportSelected").InvokeCommand(Command.Accept);
                else harness.Window.ClearPrivateState();
                harness.ImportPath.Undo();
                if (harness.ImportPath.Text.Length != 0)
                    throw new InvalidOperationException("discarded-import-path-restored");
            });
        }
        finally
        {
            read.TrySetResult("""{"name":"synthetic"}"""u8.ToArray());
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    [Test]
    public async Task ClearedPublicDraftCannotBeRestoredByUndo()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        Harness? harness = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty));
                harness.Window.PublicInput.Text = "synthetic-private-draft";
                harness.Window.ClearPrivateState();
                string cleared = harness.Window.PublicInput.Text;
                harness.Window.PublicInput.Undo();
                if (harness.Window.PublicInput.Text != cleared)
                    throw new InvalidOperationException("discarded-public-draft-restored");
            });
        }
        finally { await owner.InvokeAsync(() => harness?.Dispose()); }
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    [Arguments(4 * 1024 * 1024)]
    public async Task ChangedOrOversizedNativeImportRejectsBeforeOwnerAdmission(int lengthOffset)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        using var form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
        using var stream = new ChangedInputStream(lengthOffset);
        using var operation = new SetupTerminalImportOperation(form, owner.Dispatch, openRead: _ => stream);
        var settled = new TaskCompletionSource<SetupTerminalImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Settled += (_, result) => settled.TrySetResult(result);
        await owner.InvokeAsync(() =>
        {
            if (!operation.Start("synthetic-private.json", "config")) throw new InvalidOperationException("import-not-started");
        });
        SetupTerminalImportResult result = await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("import-size-invalid");
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
        await owner.InvokeAsync(() =>
        {
            if (form.Snapshot().Values.ContainsKey("name")) throw new InvalidOperationException("changed-input-admitted");
        });
    }

    private sealed class ChangedInputStream(int lengthOffset) : MemoryStream("""{"name":"late-native"}"""u8.ToArray())
    {
        public override long Length => base.Length + lengthOffset;
        internal Memory<byte> Borrowed { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == 0) Borrowed = buffer;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    [Test]
    public async Task RegistryRejectsTheFirstCountOverflowBeforeReadingAnyCandidate()
    {
        string directory = Directory.CreateTempSubdirectory("terminal-registry-count-").FullName;
        try
        {
            for (int index = 0; index < 4097; index++)
                File.WriteAllBytes(Path.Combine(directory, $"{index}.json"), []);
            var selection = new SetupTerminalDefinitionSelection(directory,
                Guid.Parse("69cdb122-41d2-4fab-9b82-7929a889c726"));
            try
            {
                selection.Load();
                throw new InvalidDataException("registry-count-overflow-admitted");
            }
            catch (InvalidOperationException exception)
            {
                await Assert.That(exception.Message).IsEqualTo("registry-limit");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    [Arguments("open")]
    [Arguments("stat")]
    [Arguments("read")]
    public async Task NativeInputCancellationLeavesOwnerResponsiveUntilPhysicalCleanup(string boundary)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        using var stream = new NativeInputStream(boundary);
        using var form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
        using var operation = new SetupTerminalImportOperation(form, owner.Dispatch, openRead: _ =>
        {
            if (boundary == "open") stream.Hold();
            return stream;
        });
        var settled = new TaskCompletionSource<SetupTerminalImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Settled += (_, result) => settled.TrySetResult(result);
        try
        {
            await owner.InvokeAsync(() =>
            {
                if (!operation.Start("synthetic-native.json", "config"))
                    throw new InvalidOperationException("import-not-started");
            }).WaitAsync(TimeSpan.FromSeconds(5));
            await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                operation.RequestCancellationAsync();
                form.Fields.Single(field => field.Definition.Id == "name").Text = "current-answer";
                if (!operation.IsBusy || operation.Completion.IsCompleted)
                    throw new InvalidOperationException("physical-input-retired-early");
                if (boundary == "read" && !stream.Borrowed.Span[..stream.Input.Length].SequenceEqual(stream.Input))
                    throw new InvalidOperationException("physical-input-cleared-early");
            }).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { stream.Release.TrySetResult(); }
        await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        SetupTerminalImportResult result = await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.IsCurrent || result.Succeeded).IsFalse();
        await Assert.That(stream.CloseCount).IsEqualTo(1);
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
        await owner.InvokeAsync(() =>
        {
            if (form.Snapshot().Values["name"].GetString() != "current-answer")
                throw new InvalidOperationException("stale-native-input-applied");
        });
    }

    [Test]
    public async Task NativeImportUsesSharedAcquisitionAndAdmitsOnlyOnOwner()
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        using var form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
        using var stream = new MemoryStream("""{"name":"native-answer","token":"synthetic-private"}"""u8.ToArray());
        using var operation = new SetupTerminalImportOperation(form, owner.Dispatch, openRead: _ => stream);
        int changedOn = 0;
        form.InputChanged += (_, _) => changedOn = Environment.CurrentManagedThreadId;
        var settled = new TaskCompletionSource<SetupTerminalImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Settled += (_, result) => settled.TrySetResult(result);
        await owner.InvokeAsync(() =>
        {
            if (!operation.Start("synthetic-native.json", "config")) throw new InvalidOperationException("import-not-started");
        });
        SetupTerminalImportResult result = await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await operation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(changedOn).IsEqualTo(owner.OwnerThreadId);
        await owner.InvokeAsync(() =>
        {
            if (form.Snapshot().Values["name"].GetString() != "native-answer")
                throw new InvalidOperationException("native-import-not-applied");
        });
    }

    [Test]
    public async Task RegistryKeepsPerFileBoundsRatherThanAddingAggregateAdmission()
    {
        string directory = Directory.CreateTempSubdirectory("terminal-native-registry-").FullName;
        try
        {
            string definition = TerminalDefinitionFixture.Json;
            // Three individually admitted sources exceed 4 MiB together.
            string padding = new(' ', 1500000);
            for (int revision = 1; revision <= 3; revision++)
                await File.WriteAllTextAsync(Path.Combine(directory, $"{revision}.json"),
                    definition.Replace("\"revision\":1", $"\"revision\":{revision}", StringComparison.Ordinal) + padding);
            var selection = new SetupTerminalDefinitionSelection(directory,
                Guid.Parse("69cdb122-41d2-4fab-9b82-7929a889c726"), "1.0");
            await Assert.That(selection.Load().Revision).IsEqualTo(3);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class NativeInputStream(string boundary)
        : MemoryStream("""{"name":"late-native-answer","token":"synthetic-private"}"""u8.ToArray())
    {
        internal byte[] Input { get; } = """{"name":"late-native-answer","token":"synthetic-private"}"""u8.ToArray();
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Memory<byte> Borrowed { get; private set; }
        internal int CloseCount { get; private set; }
        internal void Hold()
        {
            Entered.TrySetResult();
            Release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        public override long Length
        {
            get
            {
                if (boundary == "stat") Hold();
                return base.Length;
            }
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, CancellationToken.None);
            if (count != 0 && boundary == "read")
            {
                Borrowed = buffer;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            }
            return count;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) CloseCount++;
            base.Dispose(disposing);
        }
    }

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

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task FillCommandReturnsDuringCooperativeAdoptionAndCloseDrainsIt(bool retained, bool close)
    {
        using var owner = new ControlledTerminalSynchronizationContext();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Harness? harness = null;
        bool suspend = !retained;
        int reads = 0;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) =>
                {
                    Interlocked.Increment(ref reads);
                    return Task.FromResult<ReadOnlyMemory<byte>>("""{"name":"filled-value"}"""u8.ToArray());
                }, timeProvider: new CloseClock(), cooperativeYield: async token =>
                {
                    if (!suspend || harness!.Form.ImportComparison is null) return;
                    entered.TrySetResult();
                    try { await release.Task.WaitAsync(token); }
                    finally
                    {
                        exiting.TrySetResult();
                        await cleanup.Task;
                    }
                });
                if (retained)
                {
                    harness.ImportPath.Text = "synthetic-import.json";
                    harness.Button("ImportSelected").InvokeCommand(Command.Accept);
                }
                else harness.Import();
            });
            if (retained)
            {
                await harness!.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
                await owner.InvokeAsync(() =>
                {
                    var report = harness.Form.ImportComparison;
                    if (report is null) throw new InvalidOperationException("comparison-not-retained");
                    suspend = true;
                    harness.Button("FillFields").InvokeCommand(Command.Accept);
                    if (!ReferenceEquals(report, harness.Form.ImportComparison))
                        throw new InvalidOperationException("fill-replaced-retained-report");
                    if (harness.Window.StatusText != SetupTerminalText.Get("ImportAdopting"))
                        throw new InvalidOperationException("retained-adoption-status-not-shown");
                });
            }
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                if (!harness!.Window.IsImportBusy || harness.Window.ImportCompletion.IsCompleted ||
                    harness.Form.InputRevision != 0)
                    throw new InvalidOperationException("fill-command-did-not-return-during-adoption");
                if (close) harness.Window.RequestStopFromSignal();
                else release.TrySetResult();
            });
            await exiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(harness!.Window.ImportCompletion.IsCompleted).IsFalse();
            if (close) await Assert.That(harness.Window.ShutdownCompletion.IsCompleted).IsFalse();
            cleanup.TrySetResult();
            await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            if (close) await harness.Window.ShutdownCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(reads).IsEqualTo(1);
            await owner.InvokeAsync(() =>
            {
                if (close)
                {
                    if (harness.Form.Snapshot().Values.Count != 0 || harness.Form.ImportComparison is not null)
                        throw new InvalidOperationException("closed-adoption-published");
                }
                else if (harness.Form.Snapshot().Values["name"].GetString() != "filled-value" ||
                    harness.Window.StatusText != SetupTerminalText.Get("ImportReady"))
                    throw new InvalidOperationException("fill-did-not-settle-on-owner");
            });
        }
        finally
        {
            release.TrySetResult();
            cleanup.TrySetResult();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly IApplication _application;
        private readonly SetupPresentationSession _session;
        private SessionToken? _run;
        internal Harness(ControlledTerminalSynchronizationContext owner,
            Func<string, CancellationToken, Task<ReadOnlyMemory<byte>>> reader,
            TimeProvider? timeProvider = null, Action<Action>? dispatch = null,
            Func<CancellationToken, Task>? beforePublication = null, bool nativeDispatch = false,
            string? outputDirectory = null, Action<ProtectedArtifactBundleOutcome>? outcomeObserver = null,
            Func<CancellationToken, ValueTask>? cooperativeYield = null)
        {
            _application = Application.Create();
            _application.Init("dotnet");
            Form = new DefinitionFormModel(TerminalDefinitionFixture.Definition());
            Secret = new SetupTerminalSecretBuffer(262144, urlSafeOnly: false);
            Operation = new SetupTerminalArtifactOperation(Form, new ProtectedArtifactWriter(), outputDirectory ?? Path.GetTempPath(),
                beforePublication, outcomeObserver);
            _session = new SetupPresentationSession(new StrongReferenceMessenger());
            SetupWorkspaceId.TryCreate("import", out var id);
            Workspace = _session.CreateWorkspace(id, Operation);
            Workspace.Activate();
            Window = new SetupTerminalWindow(_application, Workspace, Operation, Secret, reader,
                nativeDispatch ? null : dispatch ?? owner.Dispatch, timeProvider,
                cooperativeYield: nativeDispatch ? null : cooperativeYield ?? (_ => ValueTask.CompletedTask));
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
            Button("FillFields").InvokeCommand(Command.Accept);
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
    [Arguments(false)]
    [Arguments(true)]
    public async Task CloseDeadlineDetachesUiWhileOwnedReadEventuallyClearsBytes(bool previousSave)
    {
        if (previousSave && !OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("terminal-close-import-").FullName;
        using var owner = new ControlledTerminalSynchronizationContext();
        var clock = new CloseClock();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = Encoding.UTF8.GetBytes("""{"name":"late-answer","token":"synthetic-late-private"}""");
        Harness? harness = null;
        Task publication = Task.CompletedTask;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, async (_, _) =>
                {
                    reached.TrySetResult();
                    await release.Task.ConfigureAwait(false);
                    return bytes;
                }, clock, outputDirectory: directory);
                if (previousSave)
                {
                    harness.Form.Fields.Single(field => field.Definition.Id == "name").Text = "sample";
                    harness.Form.Fields.Single(field => field.Definition.Id == "token").Text = "synthetic-private";
                    harness.Operation.PrepareAsync().GetAwaiter().GetResult();
                    publication = harness.Operation.ExecuteAsync(CancellationToken.None);
                }
            });
            await publication.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() =>
            {
                harness!.Window.PublicInput.Text = "sample";
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
            if (previousSave)
            {
                await Assert.That(status).Contains(SetupTerminalText.Get("ShutdownUnknown"));
                await Assert.That(status).Contains("config: Written");
            }
            else await Assert.That(status).IsEqualTo(SetupTerminalText.Get("ShutdownUnknown"));
            release.TrySetResult();
            await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(bytes.All(value => value == 0)).IsTrue();
        }
        finally
        {
            release.TrySetResult();
            if (harness is not null) await harness.Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task CloseDeadlineRetainsAttemptAndKnownWrittenPrefixBesideUnresolvedOutput()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("terminal-close-receipt-").FullName;
        using var owner = new ControlledTerminalSynchronizationContext();
        var clock = new CloseClock();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Harness? harness = null;
        Task? publication = null;
        try
        {
            await owner.InvokeAsync(() =>
            {
                harness = new Harness(owner, (_, _) => Task.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty),
                    clock, outputDirectory: directory, outcomeObserver: outcome =>
                    {
                        if (outcome.Result is not { InstallationEstablished: true }) return;
                        reached.TrySetResult();
                        release.Task.GetAwaiter().GetResult();
                    });
                harness.Window.PublicInput.Text = "sample";
                harness.Form.Fields.Single(input => input.Definition.Id == "token").Text = "synthetic-private";
                harness.Operation.ArtifactId = "environment";
                harness.Operation.ToggleCurrentArtifact();
                harness.Window.Prepare();
                harness.Begin();
            });
            publication = Task.Run(() => harness!.Workspace.ExecuteAsync(Guid.CreateVersion7()));
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Guid attempt = harness!.Operation.CurrentPublication!.AttemptId;
            await Assert.That(Directory.GetFiles(directory)).Count().IsEqualTo(1);
            await owner.InvokeAsync(() => harness.Button("Close").InvokeCommand(Command.Accept));
            clock.Advance(TimeSpan.FromSeconds(2));
            await harness.Window.ShutdownCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            string status = string.Empty;
            await owner.InvokeAsync(() => status = harness.Window.StatusText);
            await Assert.That(harness.Operation.IsPublishing).IsTrue();
            await Assert.That(harness.Operation.CurrentPublication!.IsUnknown).IsTrue();
            await Assert.That(status).Contains(attempt.ToString("N"));
            await Assert.That(status).Contains("config");
            await Assert.That(status).Contains(nameof(ProtectedArtifactStatus.Written));
            await Assert.That(status).Contains("environment");
            await Assert.That(status).Contains(SetupTerminalText.Get("PublicationUnknown"));
            await Assert.That(status).DoesNotContain(directory);
            await Assert.That(status).DoesNotContain("synthetic-private");
        }
        finally
        {
            release.TrySetResult();
            if (publication is not null) await publication.WaitAsync(TimeSpan.FromSeconds(5));
            if (harness is not null) await harness.Workspace.WaitForPendingOperationsAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await owner.InvokeAsync(() => harness?.Dispose());
            Directory.Delete(directory, true);
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
