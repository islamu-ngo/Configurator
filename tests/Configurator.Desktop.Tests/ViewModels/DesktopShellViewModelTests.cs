namespace Configurator.Desktop.Tests.ViewModels;

using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Headless;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;

public sealed class DesktopShellViewModelTests
{
    [Test]
    public async Task NativeImportClearsOwnedStorageBeforeReleasingThePhysicalBusyLease()
    {
        byte[] bytes = """{"name":"imported-before-settlement"}"""u8.ToArray();
        using var stream = new ChangedInputStream(bytes, bytes.Length);
        using var shell = DesktopTestContext.Root(openImport: _ => stream);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ExistingFilePath = "synthetic.json";
        bool? clearedAtRelease = null;
        shell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(shell.IsBusy) && !shell.IsBusy)
                clearedAtRelease = stream.Borrowed.Length > 0 && stream.Borrowed.ToArray().All(value => value == 0);
        };
        await Assert.That(await shell.Publisher.ImportFileAsync()).IsTrue();
        await Assert.That(clearedAtRelease).IsEqualTo(true);
        await Assert.That(shell.Workspace.Form!.Snapshot().Values["name"].GetString())
            .IsEqualTo("imported-before-settlement");
    }

    [Test]
    [Arguments(true, -1)]
    [Arguments(false, -1)]
    [Arguments(true, 1)]
    [Arguments(false, 1)]
    [Arguments(true, 4 * 1024 * 1024)]
    [Arguments(false, 4 * 1024 * 1024)]
    public async Task ChangedOrOversizedNativeInputCannotReachDesktopAdmission(bool definition, int lengthOffset)
    {
        byte[] bytes = definition ? DesktopApplicationTests.Definition() : """{"name":"late-import"}"""u8.ToArray();
        using var stream = new ChangedInputStream(bytes, bytes.Length + (long)lengthOffset);
        using var shell = DesktopTestContext.Root(openDefinition: definition ? _ => stream : null,
            openImport: definition ? null : _ => stream);
        if (!definition) shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Loader.DefinitionPath = "synthetic-private.json";
        shell.Publisher.ExistingFilePath = "synthetic-private.json";
        bool accepted = definition ? await shell.Loader.LoadDefinitionAsync() : await shell.Publisher.ImportFileAsync();
        await Assert.That(accepted).IsFalse();
        await Assert.That(shell.Status).IsEqualTo(definition ? "definition-invalid:source-limit" : "import-unreadable");
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
        if (definition) await Assert.That(shell.Workspace.Form).IsNull();
        else await Assert.That(shell.Workspace.Form!.Snapshot().Values["name"].GetString()).IsEqualTo("Example service");
    }

    private sealed class ChangedInputStream(byte[] bytes, long reportedLength) : MemoryStream(bytes)
    {
        public override long Length => reportedLength;
        internal Memory<byte> Borrowed { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == 0) Borrowed = buffer;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    [Test]
    [NotInParallel]
    [Arguments(true, "open")]
    [Arguments(false, "open")]
    [Arguments(true, "stat")]
    [Arguments(false, "stat")]
    [Arguments(true, "read")]
    [Arguments(false, "read")]
    public async Task NativeAcquisitionKeepsDispatcherResponsiveAndDrainsCancelledInput(bool definition, string boundary)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        byte[] bytes = definition ? DesktopApplicationTests.Definition() : """{"name":"late-import"}"""u8.ToArray();
        using var stream = new AcquisitionStream(bytes, boundary);
        var responsive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DesktopShellViewModel? shell = null;
        Task? pending = null;
        try
        {
            pending = session.Dispatch(async () =>
            {
                Stream Open(string _)
                {
                    if (boundary == "open") stream.Hold();
                    return stream;
                }
                shell = DesktopTestContext.Root(openDefinition: definition ? Open : null,
                    openImport: definition ? null : Open);
                if (!definition) shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                shell.Loader.DefinitionPath = "synthetic.json";
                shell.Publisher.ExistingFilePath = "synthetic.json";
                Task<bool> acquiring = definition ? shell.Loader.LoadDefinitionAsync() : shell.Publisher.ImportFileAsync();
                await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!Dispatcher.UIThread.CheckAccess()) throw new InvalidOperationException("not-ui-owner");
                    shell.Cancel();
                    if (!shell.IsBusy || shell.ActiveCompletion.IsCompleted)
                        throw new InvalidOperationException("physical-read-retired-early");
                    if (boundary == "read" && !stream.Borrowed.Span[..bytes.Length].SequenceEqual(bytes))
                        throw new InvalidOperationException("borrowed-read-cleared-early");
                    responsive.TrySetResult();
                });
                await Assert.That(await acquiring).IsFalse();
                await Assert.That(shell.IsBusy).IsFalse();
                if (definition) await Assert.That(shell.Workspace.Form).IsNull();
                else await Assert.That(shell.Workspace.Form!.Snapshot().Values["name"].GetString())
                    .IsEqualTo("Example service");
                return true;
            }, CancellationToken.None);
            await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await responsive.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            stream.Release.TrySetResult();
            try
            {
                if (pending is not null) await pending.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(stream.CloseCount).IsEqualTo(1);
                await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
            }
            finally
            {
                await session.Dispatch(() => { shell?.Dispose(); return true; }, CancellationToken.None);
                await Task.Run(session.Dispose);
            }
        }
    }

    private sealed class AcquisitionStream(byte[] bytes, string boundary) : MemoryStream(bytes)
    {
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
    public async Task DefinitionIntakePopulatesOnlyItsOwnMessengerWorkspace()
    {
        using var first = CreateShell();
        using var second = CreateShell();
        first.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await Assert.That(first.Workspace.Form).IsNotNull();
        await Assert.That(second.Workspace.Form).IsNull();
        await Assert.That(first.Status).IsEqualTo("definition-loaded");
    }

    [Test]
    public async Task SaveCommandFailsClosedWithoutPreparedOutput()
    {
        using var shell = CreateShell();
        await Assert.That(shell.Publisher.SaveCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task CancellationDiscardsCurrentOperationAndReportsStableStatus()
    {
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.CancelCommand.Execute(null);
        await Assert.That(shell.Status).IsEqualTo("operation-cancelled");
        await Assert.That(shell.CancelCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task CancelledGenerationDiscardsLatePickerWithoutChangingDestination()
    {
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = CreateShell(new Dialogs((_, _) => selected.Task));
        string destination = shell.Publisher.OutputDirectory;
        Task<bool> pending = shell.Publisher.SelectOutputDirectoryAsync();
        shell.CancelCommand.Execute(null);
        selected.SetResult(new Uri(Path.Combine(Path.GetTempPath(), "discarded")));
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.Publisher.OutputDirectory).IsEqualTo(destination);
        await Assert.That(shell.Status).IsEqualTo("operation-cancelled");
    }

    [Test]
    public async Task SharedBusyGateDisablesEveryActionExceptCancellationAndRecoversReactively()
    {
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = CreateShell(new Dialogs((_, _) => selected.Task));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.Publisher.PrepareAsync();
        int changes = 0;
        shell.Publisher.SaveCommand.CanExecuteChanged += (_, _) => changes++;
        Task<bool> pending = shell.Publisher.SelectOutputDirectoryAsync();
        await Assert.That(shell.IsBusy).IsTrue();
        await Assert.That(shell.IsSelecting).IsTrue();
        await Assert.That(shell.Loader.LoadDefinitionCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Loader.SelectDefinitionCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Workspace.ValidateCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Publisher.PrepareCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Publisher.PrepareBundleCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Publisher.SaveCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Publisher.SaveBundleCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Publisher.ImportFileCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Publisher.SelectImportCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.Publisher.SelectOutputDirectoryCommand.CanExecute(null)).IsFalse();
        await Assert.That(shell.CancelCommand.CanExecute(null)).IsTrue();
        selected.SetResult(null);
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.IsBusy).IsFalse();
        await Assert.That(shell.Publisher.SaveCommand.CanExecute(null)).IsEqualTo(shell.Publisher.ProtectedSaveAvailable);
        await Assert.That(changes > 0).IsTrue();
        await Assert.That(shell.FocusTarget).IsEqualTo("SelectOutput");
    }

    [Test]
    [Arguments("edit")]
    [Arguments("definition")]
    [Arguments("definition-path")]
    [Arguments("artifact")]
    [Arguments("filename")]
    [Arguments("destination")]
    [Arguments("import-path")]
    [Arguments("bundle-selection")]
    [Arguments("cancel")]
    [Arguments("dispose")]
    public async Task EveryInvalidationFencesNativeCommitAndZeroesItsOwnedBuffer(string change)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadOnlyMemory<byte> held = default;
        using var shell = CreateShell(save: async (path, bytes, token) =>
        {
            held = bytes;
            using ProtectedArtifactPreparation staged = await new ProtectedArtifactWriter()
                .PrepareAsync(SetupArtifactKind.Unknown, path, bytes, token);
            started.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return await staged.CommitAsync(token);
        }, outputDirectory: directory.Path);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "owner").Text =
            """{"name":"synthetic-private-answer"}""";
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
        Task<ProtectedArtifactCommitResult> pending = shell.Publisher.SaveAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await Assert.That(held.IsEmpty).IsFalse();
            Change(shell, change);
            string status = shell.Status;
            await Assert.That(held.ToArray().Any(value => value != 0)).IsTrue();
            release.SetResult();
            await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Status)
                .IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
            await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
            await Assert.That(shell.Status).IsEqualTo(status);
            await Assert.That(shell.Status).DoesNotContain("synthetic-private");
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            await Assert.That(shell.IsBusy).IsFalse();
            await Assert.That(held.ToArray().All(value => value == 0)).IsTrue();
        }
        finally { release.TrySetResult(); }
    }

    [Test]
    [Arguments("edit")]
    [Arguments("definition")]
    [Arguments("destination")]
    [Arguments("cancel")]
    [Arguments("dispose")]
    public async Task BundleInvalidationCancelsPublicationWithoutRestoringReadiness(string change)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new PausingWriter(started, release);
        using var shell = CreateNativeShell(directory.Path, writer);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.SelectArtifact("environment", true);
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        Task<ProtectedArtifactBundleResult?> pending = shell.Publisher.SaveBundleAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Change(shell, change);
            string status = shell.Status;
            release.SetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
            await Assert.That(shell.Status).IsEqualTo(status);
            await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
            await Assert.That(shell.IsBusy).IsFalse();
            await Assert.That(writer.Bytes.ToArray().All(value => value == 0)).IsTrue();
        }
        finally { release.TrySetResult(); }
    }

    [Test]
    [Arguments("edit")]
    [Arguments("definition")]
    [Arguments("definition-path")]
    [Arguments("artifact")]
    [Arguments("filename")]
    [Arguments("destination")]
    [Arguments("import-path")]
    [Arguments("bundle-selection")]
    [Arguments("cancel")]
    [Arguments("dispose")]
    public async Task LatePickerCannotReplaceAnyInvalidatedWorkspace(string change)
    {
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = CreateShell(new Dialogs((_, _) => selected.Task));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        string destination = shell.Publisher.OutputDirectory;
        Task<bool> pending = shell.Publisher.SelectOutputDirectoryAsync();
        Change(shell, change);
        string currentDestination = shell.Publisher.OutputDirectory;
        string status = shell.Status;
        selected.SetResult(new Uri(Path.Combine(Path.GetTempPath(), "discarded")));
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.Publisher.OutputDirectory).IsEqualTo(currentDestination);
        await Assert.That(shell.Status).IsEqualTo(status);
        if (change != "destination") await Assert.That(currentDestination).IsEqualTo(destination);
    }

    [Test]
    [Arguments("config")]
    [Arguments("environment")]
    [Arguments("yaml")]
    [Arguments("notes")]
    public async Task DeclaredFormatsPrepareAndConsumeExactlyOneRestrictedOutput(string artifact)
    {
        ReadOnlyMemory<byte> held = default;
        string output = string.Empty;
        using var shell = CreateShell(save: (_, bytes, _) =>
        {
            held = bytes;
            output = Encoding.UTF8.GetString(bytes.Span);
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ArtifactId = artifact;
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
        await Assert.That(shell.Publisher.Preparation!.ArtifactId).IsEqualTo(artifact);
        await Assert.That(shell.Publisher.Preparation.DefinitionDigest).IsEqualTo(shell.Workspace.Form!.Definition.Digest);
        await Assert.That(shell.Publisher.SaveCommand.CanExecute(null)).IsTrue();
        await Assert.That((await shell.Publisher.SaveAsync()).Succeeded).IsTrue();
        await Assert.That(output).Contains("Example service");
        await Assert.That(held.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(shell.Publisher.Preparation).IsNull();
        await Assert.That(shell.Publisher.SaveCommand.CanExecute(null)).IsFalse();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
    }

    [Test]
    public async Task AtomicImportRejectsConflictAndCommitsOneRevisionWithoutLeakingAnswers()
    {
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var form = shell.Workspace.Form!;
        var name = shell.Workspace.Fields.Single(item => item.Id == "name");
        name.Text = "Explicit service";
        name.Text = "Example service";
        long revision = form.InputRevision;
        byte[] conflict = Encoding.UTF8.GetBytes("""{"name":"conflict","owner":{"name":"synthetic-private"}}""");
        await Assert.That(await shell.Publisher.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(conflict))).IsFalse();
        await Assert.That(form.Fields.Single(item => item.Definition.Id == "owner").Text).IsEmpty();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(shell.Status).IsEqualTo("answer-conflict");
        byte[] accepted = Encoding.UTF8.GetBytes("""{"name":"Example service","owner":{"name":"synthetic-private"}}""");
        await Assert.That(await shell.Publisher.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(accepted))).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.Fields.Single(item => item.Definition.Id == "owner").Text).Contains("synthetic-private");
        await Assert.That(shell.Status).IsEqualTo("import-ready");
        await Assert.That(accepted.All(value => value == 0) && conflict.All(value => value == 0)).IsTrue();
        await Assert.That(shell.ToString()).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task AtomicImportReplacesPristineDefaultInOneRevision()
    {
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var form = shell.Workspace.Form!;
        long revision = form.InputRevision;
        byte[] source = Encoding.UTF8.GetBytes("""{"name":"Imported service"}""");

        await Assert.That(await shell.Publisher.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(source))).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(shell.Workspace.Fields.Single(item => item.Id == "name").Text).IsEqualTo("Imported service");
        await Assert.That(shell.Status).IsEqualTo("import-ready");
        await Assert.That(source.All(value => value == 0)).IsTrue();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("definition")]
    [Arguments("destination")]
    [Arguments("import-path")]
    [Arguments("cancel")]
    [Arguments("dispose")]
    public async Task PendingImportCannotCommitAcrossInvalidationAndErasesSource(string change)
    {
        var supplied = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        Task<bool> pending = shell.Publisher.ImportAsync(_ => supplied.Task);
        Change(shell, change);
        string status = shell.Status;
        byte[] bytes = Encoding.UTF8.GetBytes("""{"owner":{"name":"synthetic-private"}}""");
        supplied.SetResult(bytes);
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.Status).IsEqualTo(status);
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
        if (shell.Workspace.Form is { } form)
            await Assert.That(form.Fields.Single(item => item.Definition.Id == "owner").Text).IsEmpty();
    }

    [Test]
    public async Task SourceChangeAfterAtomicImportRetainsLaterStatus()
    {
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Workspace.Form!.InputChanged += (_, _) => shell.Publisher.ExistingFilePath = "next.json";
        int focusVersion = shell.Workspace.FirstVisible!.FocusRequestVersion;
        byte[] source = Encoding.UTF8.GetBytes("""{"owner":{"name":"synthetic-private"}}""");
        await Assert.That(await shell.Publisher.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(source))).IsTrue();
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(shell.Workspace.FirstVisible!.FocusRequestVersion).IsEqualTo(focusVersion);
    }

    [Test]
    public async Task UnsupportedHostRefusesDirectSingleAndBundleSaveWithoutNativeFallback()
    {
        int writes = 0;
        using var shell = CreateShell(save: (_, _, _) =>
        {
            writes++;
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        }, protectedOutputAvailable: false);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
        await Assert.That(shell.Publisher.SaveCommand.CanExecute(null)).IsFalse();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.UnsupportedPlatform);
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        await Assert.That(shell.Publisher.SaveBundleCommand.CanExecute(null)).IsFalse();
        await Assert.That(await shell.Publisher.SaveBundleAsync()).IsNull();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        await Assert.That(writes).IsEqualTo(0);
    }

    [Test]
    [Arguments("../escape.json")]
    [Arguments("/tmp/escape.json")]
    [Arguments("-")]
    public async Task UnsafeFilenameCannotReachProtectedWriter(string name)
    {
        bool reached = false;
        using var shell = CreateShell(save: (_, _, _) =>
        {
            reached = true;
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.FileName = name;
        await shell.Publisher.PrepareAsync();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
        await Assert.That(reached).IsFalse();
    }

    [Test]
    public async Task InactiveAndEmptyBundleSelectionsCannotEnableSave()
    {
        JsonObject source = JsonNode.Parse(DesktopApplicationTests.Definition())!.AsObject();
        source["artifacts"]![0]!["projection"]!["when"] =
            JsonNode.Parse("""{"op":"equals","field":"mode","value":"remote"}""");
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await Assert.That(shell.Publisher.PrepareBundle()).IsFalse();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "mode").Text = "remote";
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        shell.Publisher.ArtifactSelections.Single(item => item.Id == "config").IsSelected = false;
        await Assert.That(shell.Publisher.SelectedArtifactIds.Count).IsEqualTo(0);
        await Assert.That(shell.Publisher.PrepareBundle()).IsFalse();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task InvalidStructuredInputsDisableSaveAndRequestErrorFocus()
    {
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.Publisher.PrepareAsync();
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "owner").Text = """{"age":"invalid"}""";
        await Assert.That(shell.Workspace.Validate()).IsFalse();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Workspace.FirstError).IsNotNull();
        var owner = (ObjectFieldViewModel)shell.Workspace.FirstError!;
        await Assert.That(owner.Children.Single(item => item.Id == "name").FocusRequestVersion > 0).IsTrue();
        await Assert.That(await shell.Publisher.PrepareAsync()).IsFalse();
    }

    [Test]
    public async Task CreateOnlySingleAndBundlePublicationPreserveExistingDestination()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        using var shell = CreateNativeShell(directory.Path);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        string existing = Path.Combine(directory.Path, "service.json");
        await File.WriteAllTextAsync(existing, "existing-data");
        await shell.Publisher.PrepareAsync();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.TargetExists);
        shell.Publisher.SelectArtifact("environment", true);
        shell.Publisher.PrepareBundle();
        await Assert.That((await shell.Publisher.SaveBundleAsync())!.Succeeded).IsFalse();
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.env"))).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(existing)).IsEqualTo("existing-data");
        File.Delete(existing);
        shell.Publisher.PrepareBundle();
        await Assert.That((await shell.Publisher.SaveBundleAsync())!.Succeeded).IsTrue();
        await Assert.That(File.Exists(existing)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.env"))).IsTrue();
    }

    [Test]
    public async Task LocalDefinitionPinsBytesAndFailedReloadUnloadsPrivateWorkspace()
    {
        using var directory = new TemporaryDirectory();
        using var shell = CreateShell();
        string path = Path.Combine(directory.Path, "definition.json");
        await File.WriteAllBytesAsync(path, DesktopApplicationTests.Definition());
        shell.Loader.DefinitionPath = path;
        await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsTrue();
        var digest = shell.Workspace.Form!.Definition.Digest;
        await File.WriteAllBytesAsync(path, DesktopApplicationTests.Definition(2));
        await shell.Publisher.PrepareAsync();
        await Assert.That(shell.Publisher.Preparation!.DefinitionDigest).IsEqualTo(digest);
        await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsTrue();
        await Assert.That(shell.Workspace.Form!.Definition.Revision).IsEqualTo(2L);
        await File.WriteAllTextAsync(path, "synthetic-private-invalid");
        await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsFalse();
        await Assert.That(shell.Workspace.Form).IsNull();
        await Assert.That(shell.Publisher.Artifacts.Count).IsEqualTo(0);
        await Assert.That(shell.Publisher.Preparation).IsNull();
        await Assert.That(shell.Status).DoesNotContain("synthetic-private");
    }

    [Test]
    [Arguments("https://example.invalid/private")]
    [Arguments("file://example.invalid/private")]
    [Arguments("file:///tmp/private?answer=x")]
    public async Task RemoteSelectionFailsClosedWithoutDiscardingPreparation(string uri)
    {
        using var shell = CreateShell(new Dialogs((_, _) => Task.FromResult<Uri?>(new Uri(uri))));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.Publisher.PrepareAsync();
        var preparation = shell.Publisher.Preparation;
        string destination = shell.Publisher.OutputDirectory;
        await Assert.That(await shell.Publisher.SelectOutputDirectoryAsync()).IsFalse();
        await Assert.That(ReferenceEquals(shell.Publisher.Preparation, preparation)).IsTrue();
        await Assert.That(shell.Publisher.OutputDirectory).IsEqualTo(destination);
        await Assert.That(shell.Status).IsEqualTo("selection-nonlocal");
    }

    [Test]
    [Arguments("definition")]
    [Arguments("import")]
    public async Task AcceptedLocalSelectionRequestsFieldFocusWithoutReturningToPickerButton(string kind)
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, kind + ".json");
        await File.WriteAllBytesAsync(path, kind == "definition" ? DesktopApplicationTests.Definition()
            : Encoding.UTF8.GetBytes("""{"owner":{"name":"synthetic-private"}}"""));
        using var shell = CreateShell(new Dialogs((_, _) => Task.FromResult<Uri?>(new Uri(path))));
        if (kind == "import") shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        bool accepted = kind == "definition" ? await shell.Loader.SelectDefinitionAsync()
            : await shell.Publisher.SelectImportAsync();
        await Assert.That(accepted).IsTrue();
        await Assert.That(shell.FocusTarget).IsEqualTo("DefinitionPath");
        await Assert.That(shell.Workspace.FirstVisible!.FocusRequestVersion > 0).IsTrue();
        await Assert.That(shell.IsBusy).IsFalse();
    }

    [Test]
    [Arguments("definition")]
    [Arguments("import")]
    public async Task StaleSelectionCompletionCannotChangeNewWorkspaceFocus(string kind)
    {
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = CreateShell(new Dialogs((_, _) => selected.Task));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        Task<bool> pending = kind == "definition" ? shell.Loader.SelectDefinitionAsync()
            : shell.Publisher.SelectImportAsync();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2));
        int focusGeneration = shell.FocusRequestGeneration;
        selected.SetResult(new Uri(Path.Combine(Path.GetTempPath(), "discarded.json")));
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.FocusRequestGeneration).IsEqualTo(focusGeneration);
    }

    [Test]
    [Arguments("argument")]
    [Arguments("sentinel")]
    public async Task NonlocalServiceRefusalUsesStableDiagnosticWithoutExceptionContent(string refusal)
    {
        using var shell = CreateShell(new Dialogs((_, _) => Task.FromException<Uri?>(refusal == "argument"
            ? new ArgumentException("synthetic-private") : new InvalidOperationException("selection-nonlocal"))));
        await Assert.That(await shell.Loader.SelectDefinitionAsync()).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("selection-nonlocal");
        await Assert.That(shell.FocusTarget).IsEqualTo("SelectDefinition");
    }

    [Test]
    public async Task FailedSaveErasesNativeBufferAndCannotBeRetriedWithoutPreparation()
    {
        ReadOnlyMemory<byte> held = default;
        using var shell = CreateShell(save: (_, bytes, _) =>
        {
            held = bytes;
            throw new IOException("synthetic-private-path");
        });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.Publisher.PrepareAsync();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.IoFailure);
        await Assert.That(held.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(shell.Status).IsEqualTo("save-failed:IoFailure");
        await Assert.That(shell.IsBusy).IsFalse();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
    }

    [Test]
    public async Task ExperienceAndExpansionChangesPreservePinnedInputsAndPreparation()
    {
        using var shell = CreateShell();
        shell.Loader.LoadDefinition(DesktopApplicationTests.DefinitionWithExperiences());
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
        var form = shell.Workspace.Form!;
        var preparation = shell.Publisher.Preparation;
        long revision = form.InputRevision;
        var editors = shell.Workspace.Fields;
        var owner = (ObjectFieldViewModel)editors.Single(item => item.Id == "owner");
        var child = owner.Children[0];
        string status = shell.Status;
        await Assert.That(shell.Workspace.Fields.Single(item => item.Id == "note").IsVisible).IsFalse();
        await Assert.That(shell.Workspace.Fields.Single(item => item.Id == "owner").IsVisible).IsTrue();
        shell.Workspace.SelectedExperience = shell.Workspace.Experiences.Single(item => item.Id == "inspection");
        await Assert.That(shell.Workspace.Fields.Single(item => item.Id == "note").IsVisible).IsTrue();
        await Assert.That(form.SelectedExperienceId).IsEqualTo("inspection");
        form.SelectExperience("deployment");
        await Assert.That(shell.Workspace.SelectedExperience!.Id).IsEqualTo("deployment");
        await Assert.That(shell.Workspace.Fields.Single(item => item.Id == "note").IsVisible).IsFalse();
        shell.Workspace.OptionalEditorsExpanded = true;
        await Assert.That(shell.Workspace.Fields.Single(item => item.Id == "note").IsVisible).IsTrue();
        shell.Workspace.OptionalEditorsExpanded = false;
        await Assert.That(shell.Workspace.Fields.Single(item => item.Id == "note").IsVisible).IsFalse();
        shell.Workspace.IsRightToLeft = true;
        await Assert.That(shell.Workspace.FlowDirection).IsEqualTo(Avalonia.Media.FlowDirection.RightToLeft);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(ReferenceEquals(preparation, shell.Publisher.Preparation)).IsTrue();
        await Assert.That(ReferenceEquals(editors, shell.Workspace.Fields)).IsTrue();
        await Assert.That(ReferenceEquals(child, owner.Children[0])).IsTrue();
        await Assert.That(shell.Status).IsEqualTo(status);
    }

    private static void Change(TestShell shell, string change)
    {
        switch (change)
        {
            case "edit": shell.Workspace.Form!.Fields[0].Text = "later-edit"; break;
            case "definition": shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
            case "definition-path": shell.Loader.DefinitionPath = "later.json"; break;
            case "artifact": shell.Publisher.ArtifactId = "yaml"; break;
            case "filename": shell.Publisher.FileName = "later.json"; break;
            case "destination": shell.Publisher.OutputDirectory = Path.Combine(Path.GetTempPath(), "later"); break;
            case "import-path": shell.Publisher.ExistingFilePath = "later.json"; break;
            case "bundle-selection": shell.Publisher.SelectArtifact("environment", true); break;
            case "cancel": shell.Cancel(); break;
            case "dispose": shell.Dispose(); break;
        }
    }

    private static TestShell CreateShell(IStorageDialogService? dialogs = null,
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>>? save = null,
        string? outputDirectory = null, bool protectedOutputAvailable = true,
        Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken, Task<ProtectedArtifactBundleResult>>? publishBundle = null)
        => new(dialogs, save, outputDirectory, protectedOutputAvailable, publishBundle);

    private static TestShell CreateNativeShell(string directory, IProtectedArtifactWriter? writer = null) =>
        new(outputDirectory: directory, native: true, writer: writer);

    private sealed class PausingWriter(TaskCompletionSource started, TaskCompletionSource release)
        : IProtectedArtifactWriter
    {
        public bool IsAvailable => OperatingSystem.IsLinux();
        public ReadOnlyMemory<byte> Bytes { get; private set; }
        public async Task<ProtectedArtifactPreparation> PrepareAsync(SetupArtifactKind kind, string targetPath,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            Bytes = bytes;
            ProtectedArtifactPreparation staged = await new ProtectedArtifactWriter()
                .PrepareAsync(kind, targetPath, bytes, cancellationToken);
            try
            {
                started.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                return staged;
            }
            catch
            {
                staged.Dispose();
                throw;
            }
        }
    }

    private sealed class TestShell : IDisposable
    {
        private readonly DefinitionLoaderViewModel _loader;
        private readonly FormWorkspaceViewModel _workspace;
        private readonly ArtifactPublisherViewModel _publisher;
        private readonly DesktopShellViewModel _root;

        public TestShell(IStorageDialogService? dialogs = null,
            Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>>? save = null,
            string? outputDirectory = null, bool protectedOutputAvailable = true,
            Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>, CancellationToken, Task<ProtectedArtifactBundleResult>>? publishBundle = null,
            bool native = false, IProtectedArtifactWriter? writer = null)
        {
            var messenger = new StrongReferenceMessenger();
            dialogs ??= new Dialogs((_, _) => Task.FromResult<Uri?>(null));
            _loader = new DefinitionLoaderViewModel(messenger, dialogs);
            _workspace = new FormWorkspaceViewModel(messenger);
            _publisher = native ? new ArtifactPublisherViewModel(messenger, dialogs, writer ?? new ProtectedArtifactWriter())
                : new ArtifactPublisherViewModel(messenger, dialogs,
                    save ?? ((_, _, _) => Task.FromResult(ProtectedArtifactCommitResult.Installed(true))),
                    outputDirectory ?? Path.GetTempPath(), protectedOutputAvailable, publishBundle);
            _root = new DesktopShellViewModel(messenger, _loader, _workspace, _publisher);
            if (native) _publisher.OutputDirectory = outputDirectory!;
        }

        public DefinitionLoaderViewModel Loader => _loader;
        public FormWorkspaceViewModel Workspace => _workspace;
        public ArtifactPublisherViewModel Publisher => _publisher;
        public string Status => _root.Status;
        public string FocusTarget => _root.FocusTarget;
        public int FocusRequestGeneration => _root.FocusRequestGeneration;
        public bool IsBusy => _root.IsBusy;
        public bool IsSelecting => _root.IsSelecting;
        public IRelayCommand CancelCommand => _root.CancelCommand;
        public void Cancel() => _root.Cancel();
        public void Dispose()
        {
            _root.Dispose();
            _loader.Dispose();
            _workspace.Dispose();
            _publisher.Dispose();
        }
        public override string ToString() => _root.ToString();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("configurator-focused-workflow-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class Dialogs(Func<DesktopPathSelectionKind, CancellationToken, Task<Uri?>> select)
        : IStorageDialogService
    {
        public Task<Uri?> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            select(kind, cancellationToken);
    }
}
