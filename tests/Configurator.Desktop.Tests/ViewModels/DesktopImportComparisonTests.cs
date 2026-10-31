namespace Configurator.Desktop.Tests.ViewModels;

using System.Text.Json;
using Avalonia.Headless;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Services;
using Configurator.Desktop.Tests.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;

public sealed class DesktopImportComparisonTests
{
    [Test]
    [Arguments("complete")]
    [Arguments("cancel")]
    [Arguments("purpose")]
    public async Task ProviderImportUsesItsStreamWithoutNativePathAndOwnsCleanupThroughCancellation(string retirement)
    {
        using var stream = new HeldReadStream();
        bool disposed = false;
        IStorageFile file = StorageDialogServiceTests.StorageProxy.Create<IStorageFile>((method, _) => method switch
        {
            "OpenReadAsync" => Task.FromResult<Stream>(stream),
            "Dispose" => DisposeItem(),
            // A provider need not expose a native path. Reading it is an observable contract failure.
            _ => throw new InvalidOperationException("native-path-assumed")
        });
        IStorageProvider provider = StorageDialogServiceTests.StorageProxy.Create<IStorageProvider>((method, _) => method switch
        {
            "get_CanOpen" => true,
            "OpenFilePickerAsync" => Task.FromResult<IReadOnlyList<IStorageFile>>([file]),
            _ => throw new NotSupportedException()
        });
        var messenger = new WeakReferenceMessenger();
        var dialogs = new AvaloniaStorageDialogService(() => provider);
        using var loader = new DefinitionLoaderViewModel(messenger, dialogs);
        using var workspace = new FormWorkspaceViewModel(messenger);
        using var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter(),
            _ => ValueTask.CompletedTask);
        using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher);
        loader.LoadDefinition(DesktopApplicationTests.Definition());
        long revision = workspace.Form!.InputRevision;
        Task<bool> pending = publisher.SelectImportAsync();
        try
        {
            await Task.WhenAny(stream.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(stream.Entered.Task.IsCompletedSuccessfully).IsTrue();
            await Assert.That(disposed).IsFalse();
            await Assert.That(shell.IsBusy).IsTrue();
            if (retirement == "cancel") shell.Cancel();
            else if (retirement == "purpose") publisher.ImportPurposeIndex = 1;
            await Assert.That(shell.ActiveCompletion.IsCompleted).IsFalse();
        }
        finally { stream.Release.TrySetResult(); }
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(retirement == "complete");
        await Assert.That(disposed).IsTrue();
        await Assert.That(stream.CloseCount).IsEqualTo(1);
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(workspace.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(workspace.Form.ImportComparison is not null).IsEqualTo(retirement == "complete");
        await Assert.That(shell.IsBusy).IsFalse();

        object? DisposeItem() { disposed = true; return null; }
    }

    [Test]
    public async Task DefaultFileReviewPreservesAnswersRevisionEditorsAndPreparedOutput()
    {
        using var shell = DesktopTestContext.Root(openImport: _ =>
            new MemoryStream("""{"name":"imported-private-sentinel"}"""u8.ToArray()));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ExistingFilePath = "synthetic.json";
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
        var form = shell.Workspace.Form!;
        var preparation = shell.Publisher.Preparation;
        var editor = (StringFieldViewModel)shell.Workspace.Fields.Single(field => field.Id == "name");
        long revision = form.InputRevision;
        await Assert.That(await shell.Publisher.ImportFileAsync()).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values.ContainsKey("name")).IsFalse();
        await Assert.That(ReferenceEquals(shell.Publisher.Preparation, preparation)).IsTrue();
        await Assert.That(ReferenceEquals(editor, shell.Workspace.Fields.Single(field => field.Id == "name"))).IsTrue();
        await Assert.That(shell.Publisher.CanSave).IsTrue();
        await Assert.That(form.ImportComparison).IsNotNull();
        await Assert.That(shell.Status).DoesNotContain("imported-private-sentinel", StringComparison.Ordinal);
    }

    [Test]
    [Arguments("valid-current")]
    [Arguments("")]
    public async Task DefaultFileReviewLeavesCurrentEditorTextAndAcceptedValueUntouched(string draft)
    {
        using var shell = DesktopTestContext.Root(openImport: _ =>
            new MemoryStream("""{"name":"imported-private-sentinel"}"""u8.ToArray()));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ExistingFilePath = "synthetic.json";
        var editor = (StringFieldViewModel)shell.Workspace.Fields.Single(field => field.Id == "name");
        editor.Text = draft;
        var form = shell.Workspace.Form!;
        string before = form.Snapshot().Values["name"].GetRawText();
        long revision = form.InputRevision;
        await Assert.That(await shell.Publisher.ImportFileAsync()).IsTrue();
        await Assert.That(editor.Text).IsEqualTo(draft);
        await Assert.That(form.Snapshot().Values["name"].GetRawText()).IsEqualTo(before);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(ReferenceEquals(editor, shell.Workspace.Fields.Single(field => field.Id == "name"))).IsTrue();
    }

    [Test]
    public async Task ReorderedRowsAndDifferentMapKeysOnlyExposeDeclaredSchemaWithoutNavigationOrMutation()
    {
        using var shell = DesktopTestContext.Root(openImport: _ => new MemoryStream(
            """{"rows":[{"name":"imported-b","count":0},{"name":"imported-a","count":0}],"named":{"private-imported-key":0}}"""u8.ToArray()));
        shell.Loader.LoadDefinition(StructuredDefinition().CanonicalBytes);
        shell.Publisher.ExistingFilePath = "synthetic.json";
        var form = shell.Workspace.Form!;
        form.SetValue(["rows"], JsonSerializer.SerializeToElement(new[]
        {
            new { name = "current-a", count = 7 }, new { name = "current-b", count = 8 }
        }));
        form.SetValue(["named"], JsonSerializer.SerializeToElement(new Dictionary<string, int> { ["current-only"] = 9 }));
        form.SetValue(["optional"], null);
        var rows = (ListFieldViewModel)shell.Workspace.Fields.Single(field => field.Id == "rows");
        var named = (MapFieldViewModel)shell.Workspace.Fields.Single(field => field.Id == "named");
        FieldViewModel firstRow = rows.Items[0];
        FieldViewModel currentEntry = named.Entries.Single();
        int focus = firstRow.FocusRequestVersion + currentEntry.FocusRequestVersion;
        var before = form.Snapshot();
        var rowOrigin = form.AnswerOrigin(["rows", "0", "count"]);
        var mapOrigin = form.AnswerOrigin(["named", "current-only"]);
        var omittedOrigin = form.AnswerOrigin(["optional"]);
        int inputEvents = 0;
        int viewEvents = 0;
        form.InputChanged += (_, _) => inputEvents++;
        form.ViewChanged += (_, _) => viewEvents++;
        await Assert.That(await shell.Publisher.ImportFileAsync()).IsTrue();
        var report = form.ImportComparison!;
        await Assert.That(report.Findings.Any(finding => finding.Schema?.FieldId == "rows")).IsTrue();
        await Assert.That(report.Findings.Any(finding => finding.Schema?.FieldId == "named")).IsTrue();
        foreach (var finding in report.Findings.Where(finding => finding.Schema is not null))
        {
            var explanation = form.ExplainImportFinding(finding);
            await Assert.That(explanation).IsNotNull();
            await Assert.That(explanation!.Path.Count).IsEqualTo(1);
        }
        var after = form.Snapshot();
        await Assert.That(after.InputRevision).IsEqualTo(before.InputRevision);
        await Assert.That(after.Omitted.SetEquals(before.Omitted)).IsTrue();
        await Assert.That(JsonElement.DeepEquals(after.Values["rows"], before.Values["rows"])).IsTrue();
        await Assert.That(JsonElement.DeepEquals(after.Values["named"], before.Values["named"])).IsTrue();
        await Assert.That(ReferenceEquals(firstRow, rows.Items[0])).IsTrue();
        await Assert.That(ReferenceEquals(currentEntry, named.Entries.Single())).IsTrue();
        await Assert.That(firstRow.FocusRequestVersion + currentEntry.FocusRequestVersion).IsEqualTo(focus);
        await Assert.That(inputEvents).IsEqualTo(0);
        await Assert.That(viewEvents).IsEqualTo(0);
        await Assert.That(form.AnswerOrigin(["rows", "0", "count"])).IsEqualTo(rowOrigin);
        await Assert.That(form.AnswerOrigin(["named", "current-only"])).IsEqualTo(mapOrigin);
        await Assert.That(form.AnswerOrigin(["optional"])).IsEqualTo(omittedOrigin);
        await Assert.That(JsonSerializer.Serialize(report)).DoesNotContain("private-imported-key", StringComparison.Ordinal);
    }

    [Test]
    [Arguments("purpose")]
    [Arguments("source")]
    [Arguments("artifact")]
    [Arguments("discard")]
    public async Task ImportSelectionChangesRetirePendingComparisonWithoutLatePublication(string change)
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var supplied = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] bytes = """{"name":"late-private-value"}"""u8.ToArray();
        Task<bool> pending = shell.Publisher.CompareAsync(_ => supplied.Task);
        switch (change)
        {
            case "purpose": shell.Publisher.ImportPurposeIndex = 1; break;
            case "source": shell.Publisher.ExistingFilePath = "replacement.json"; break;
            case "artifact": shell.Publisher.ArtifactId = "yaml"; break;
            case "discard": shell.Publisher.ClearImportComparison(); break;
        }
        supplied.SetResult(bytes);
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.Workspace.Form!.ImportComparison).IsNull();
        await Assert.That(await shell.Publisher.FillImportFieldsAsync()).IsFalse();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task ChangingComparisonSourceRetiresReadWithoutDiscardingCurrentPreparedOutput()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ExistingFilePath = "first.json";
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
        var preparation = shell.Publisher.Preparation;
        long revision = shell.Workspace.Form!.InputRevision;
        var supplied = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> pending = shell.Publisher.CompareAsync(_ => supplied.Task);
        shell.Publisher.ExistingFilePath = "replacement.json";
        await Assert.That(ReferenceEquals(preparation, shell.Publisher.Preparation)).IsTrue();
        supplied.SetResult("""{"name":"obsolete"}"""u8.ToArray());
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.Publisher.CanSave).IsTrue();
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(shell.Workspace.Form.ImportComparison).IsNull();
    }

    [Test]
    public async Task DeliberateFillAdoptsAtomicallyAndAnEditRetiresItsFormerAuthority()
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var form = shell.Workspace.Form!;
        await Assert.That(await shell.Publisher.CompareAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(
            """{"name":"adopted"}"""u8.ToArray()))).IsTrue();
        await Assert.That(shell.Publisher.CanFillImportFields).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("name")).IsFalse();
        long adoptedRevision = form.InputRevision;
        int focus = shell.Workspace.FirstVisible!.FocusRequestVersion;
        int inputEvents = 0;
        form.InputChanged += (_, _) => inputEvents++;
        await Assert.That(await shell.Publisher.FillImportFieldsAsync()).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(adoptedRevision + 1);
        await Assert.That(inputEvents).IsEqualTo(1);
        await Assert.That(shell.Status).IsEqualTo("import-ready");
        await Assert.That(shell.Workspace.FirstVisible!.FocusRequestVersion).IsEqualTo(focus + 1);
        await Assert.That(shell.IsBusy).IsFalse();
        await Assert.That(form.Snapshot().Values["name"].GetString()).IsEqualTo("adopted");
        await Assert.That(form.AnswerOrigin(["name"])).IsEqualTo(Configurator.Presentation.DefinitionAnswerOrigin.Imported);
        await Assert.That(form.ImportComparison).IsNull();
        form.ResetValue(["name"]);
        await Assert.That(await shell.Publisher.CompareAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(
            """{"name":"must-not-replace-current"}"""u8.ToArray()))).IsTrue();
        shell.Workspace.Fields.Single(field => field.Id == "name").Text = "current";
        long revision = form.InputRevision;
        await Assert.That(await shell.Publisher.FillImportFieldsAsync()).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["name"].GetString()).IsEqualTo("current");
    }

    [Test]
    [Arguments(false, "cancel")]
    [Arguments(false, "edit")]
    [Arguments(true, "cancel")]
    [Arguments(true, "edit")]
    public async Task CooperativeImportRetirementKeepsLeaseUntilYieldSettlesWithoutLateStatusOrFocus(
        bool fill, string retirement)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool suspend = !fill;
        using var shell = DesktopTestContext.Root(cooperativeYield: async _ =>
        {
            if (!suspend) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var form = shell.Workspace.Form!;
        byte[] bytes = """{"name":"obsolete-private-import"}"""u8.ToArray();
        if (fill)
        {
            await Assert.That(await shell.Publisher.CompareAsync(
                _ => Task.FromResult<ReadOnlyMemory<byte>>(bytes))).IsTrue();
            suspend = true;
        }
        Task<bool> pending = fill ? shell.Publisher.FillImportFieldsAsync()
            : shell.Publisher.CompareAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(bytes));
        Task settlement = shell.ActiveCompletion;
        string status;
        int focus;
        long revision;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(pending.IsCompleted).IsFalse();
            await Assert.That(shell.IsBusy).IsTrue();
            if (retirement == "cancel") shell.Cancel();
            else shell.Workspace.Fields.Single(field => field.Id == "name").Text = "current";
            status = shell.Status;
            focus = shell.Workspace.FirstVisible!.FocusRequestVersion;
            revision = form.InputRevision;
            await Assert.That(settlement.IsCompleted).IsFalse();
            await Assert.That(shell.IsBusy).IsTrue();
        }
        finally { release.TrySetResult(); }
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await settlement.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(shell.IsBusy).IsFalse();
        await Assert.That(form.ImportComparison).IsNull();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(shell.Status).IsEqualTo(status);
        await Assert.That(shell.Workspace.FirstVisible!.FocusRequestVersion).IsEqualTo(focus);
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
        if (retirement == "edit")
            await Assert.That(form.Snapshot().Values["name"].GetString()).IsEqualTo("current");
        else
            await Assert.That(form.Snapshot().Values.ContainsKey("name")).IsFalse();
    }

    [Test]
    [Arguments("input")]
    [Arguments("status")]
    public async Task EditDuringAdoptionNotificationCannotRestoreImportStatusOrFocus(string notification)
    {
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        var form = shell.Workspace.Form!;
        await Assert.That(await shell.Publisher.CompareAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(
            """{"name":"adopted"}"""u8.ToArray()))).IsTrue();
        int inputEvents = 0;
        form.InputChanged += (_, _) =>
        {
            if (++inputEvents == 1 && notification == "input")
                form.SetValue(["name"], JsonSerializer.SerializeToElement("later"));
        };
        shell.PropertyChanged += (_, args) =>
        {
            if (notification == "status" && args.PropertyName == nameof(shell.Status) && shell.Status == "import-ready")
                form.SetValue(["name"], JsonSerializer.SerializeToElement("later"));
        };
        int focus = shell.Workspace.FirstVisible!.FocusRequestVersion;
        await Assert.That(await shell.Publisher.FillImportFieldsAsync()).IsTrue();
        await Assert.That(inputEvents).IsEqualTo(2);
        await Assert.That(form.Snapshot().Values["name"].GetString()).IsEqualTo("later");
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(shell.Workspace.FirstVisible!.FocusRequestVersion).IsEqualTo(focus);
    }

    [Test]
    [NotInParallel]
    public async Task NativePublisherDefaultYieldLetsQueuedInputCancelComparison()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            await session.Dispatch(async () =>
            {
                var messenger = new WeakReferenceMessenger();
                var dialogs = new AvaloniaStorageDialogService(() => throw new InvalidOperationException("picker-not-used"));
                using var loader = new DefinitionLoaderViewModel(messenger, dialogs);
                using var workspace = new FormWorkspaceViewModel(messenger);
                using var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher);
                loader.LoadDefinition(DesktopApplicationTests.Definition());
                var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                bool heldLease = false;
                Dispatcher.UIThread.Post(() =>
                {
                    heldLease = shell.IsBusy && !shell.ActiveCompletion.IsCompleted;
                    shell.Cancel();
                    cancelled.TrySetResult();
                }, DispatcherPriority.Input);
                byte[] bytes = """{"name":"obsolete-private-import"}"""u8.ToArray();
                Task<bool> pending = publisher.CompareAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(bytes));
                await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
                await Assert.That(heldLease).IsTrue();
                await Assert.That(workspace.Form!.ImportComparison).IsNull();
                await Assert.That(shell.Status).IsEqualTo("operation-cancelled");
                await Assert.That(bytes.All(value => value == 0)).IsTrue();
                await Assert.That(shell.IsBusy).IsFalse();
                return true;
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task ComparisonPublishesOnlyReportBindingsOnTheDispatcherOwner()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            await session.Dispatch(async () =>
            {
                using var shell = DesktopTestContext.Root(openImport: _ =>
                    new MemoryStream("""{"name":7}"""u8.ToArray()));
                shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                shell.Publisher.ExistingFilePath = "synthetic.json";
                bool owner = true;
                int publications = 0;
                int inputEvents = 0;
                int viewEvents = 0;
                shell.Publisher.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName != nameof(shell.Publisher.ImportFacts)) return;
                    publications++;
                    owner &= Dispatcher.UIThread.CheckAccess();
                };
                shell.Workspace.Form!.InputChanged += (_, _) => inputEvents++;
                shell.Workspace.Form.ViewChanged += (_, _) => viewEvents++;
                await Assert.That(await shell.Publisher.ImportFileAsync()).IsTrue();
                await Assert.That(shell.Publisher.ImportFindings.Count > 0).IsTrue();
                shell.Publisher.ImportFindings.First(item => item.CanExplain).ExplainCommand.Execute(null);
                await Assert.That(shell.Publisher.ImportExplanation.Length > 0).IsTrue();
                await Assert.That(publications > 0).IsTrue();
                await Assert.That(owner).IsTrue();
                await Assert.That(inputEvents).IsEqualTo(0);
                await Assert.That(viewEvents).IsEqualTo(0);
                return true;
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    [Arguments("cancel")]
    [Arguments("edit")]
    [Arguments("replacement")]
    [Arguments("dispose")]
    public async Task RetiredReviewCannotPublishAndPhysicalReadOwnsStorageUntilSettlement(string retirement)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        using var stream = new HeldReadStream();
        Configurator.Desktop.ViewModels.DesktopShellViewModel? shell = null;
        var retired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? pending = null;
        try
        {
            pending = session.Dispatch(async () =>
            {
                shell = DesktopTestContext.Root(openImport: _ => stream);
                shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                shell.Publisher.ExistingFilePath = "synthetic.json";
                var form = shell.Workspace.Form!;
                Task<bool> reviewing = shell.Publisher.ImportFileAsync();
                await stream.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    switch (retirement)
                    {
                        case "cancel": shell.Cancel(); break;
                        case "edit": shell.Workspace.Fields.Single(field => field.Id == "name").Text = "current"; break;
                        case "replacement": shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
                        case "dispose": shell.Dispose(); break;
                    }
                    if (!shell.IsBusy || shell.ActiveCompletion.IsCompleted)
                        throw new InvalidOperationException("physical-input-retired-early");
                    if (stream.Borrowed.Span[0] != (byte)'{')
                        throw new InvalidOperationException("input-cleared-before-read-settled");
                    retired.TrySetResult();
                });
                await Assert.That(await reviewing).IsFalse();
                await Assert.That(form.ImportComparison).IsNull();
                await Assert.That(shell.IsBusy).IsFalse();
                return true;
            }, CancellationToken.None);
            await retired.Task.WaitAsync(TimeSpan.FromSeconds(10));
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

    [Test]
    [Arguments("open")]
    [Arguments("read")]
    [Arguments("dispose")]
    public async Task InputBoundaryFailuresAreValueFreeAndDoNotPublishOrChangeAnswers(string boundary)
    {
        using var stream = new FailingInputStream(boundary);
        using var shell = DesktopTestContext.Root(openImport: _ =>
            boundary == "open" ? throw new IOException("private-input-sentinel") : stream);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.ExistingFilePath = "synthetic.json";
        long revision = shell.Workspace.Form!.InputRevision;
        await Assert.That(await shell.Publisher.ImportFileAsync()).IsFalse();
        await Assert.That(shell.Workspace.Form.ImportComparison).IsNull();
        await Assert.That(shell.Workspace.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(shell.Status).IsEqualTo("import-unreadable");
        await Assert.That(shell.Status).DoesNotContain("private-input-sentinel", StringComparison.Ordinal);
        await Assert.That(shell.IsBusy).IsFalse();
        await Assert.That(stream.Borrowed.ToArray().All(value => value == 0)).IsTrue();
    }

    private sealed class HeldReadStream() : MemoryStream("""{"name":"late-private-input"}"""u8.ToArray())
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Memory<byte> Borrowed { get; private set; }
        internal int CloseCount { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, CancellationToken.None);
            if (count > 0)
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

    private sealed class FailingInputStream(string boundary) : MemoryStream("""{"name":"private-input-sentinel"}"""u8.ToArray())
    {
        internal Memory<byte> Borrowed { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == 0) Borrowed = buffer;
            int count = await base.ReadAsync(buffer, cancellationToken);
            if (boundary == "read") throw new IOException("private-input-sentinel");
            return count;
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (boundary == "dispose") throw new IOException("private-input-sentinel");
        }
    }

    private static SetupDefinition StructuredDefinition() => SetupDefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"380d5cdd-e1a1-4ddd-91c1-05f42486c0e8","revision":1},
         "project":{"id":"desktop-comparison","name":"Desktop comparison"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
           "rows":{"type":"array","title":"Rows","items":{"type":"object","additionalProperties":false,"properties":{
             "name":{"type":"string"},"count":{"type":"integer","minimum":1}}}},
           "named":{"type":"object","title":"Named settings","additionalProperties":{"type":"integer","minimum":1}},
           "optional":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"untouched"}}}},
         "outputs":{"config.json":{"id":"config","format":"json"}}}
        """u8.ToArray());
}
