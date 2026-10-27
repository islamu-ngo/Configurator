namespace Configurator.Desktop.Tests;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

public sealed class DesktopLocalSelectionTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    [Arguments("cancel")]
    [Arguments("unavailable")]
    [Arguments("close")]
    [Arguments("replacement")]
    public async Task RealSelectionControlsReturnFocusAndRejectLateWindowResults(string outcome)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopLocalSelectionTests));
        DesktopShellViewModel? shell = null;
        MainWindow? window = null;
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var observed = await session.Dispatch(async () =>
            {
                shell = DesktopTestContext.Root(select: (_, _) => outcome switch
                {
                    "cancel" => Task.FromResult<Uri?>(null),
                    "unavailable" => Task.FromException<Uri?>(new NotSupportedException("synthetic-private-path")),
                    _ => selected.Task
                });
                shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                window = new MainWindow { DataContext = shell };
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                window.UpdateLayout();
                shell.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(shell.IsSelecting) && !shell.IsSelecting)
                        settled.TrySetResult();
                };
                var button = window.GetControl<Button>("SelectOutput");
                button.Focus();
                button.Command!.Execute(button.CommandParameter);
                if (outcome == "close") window.Close();
                if (outcome == "replacement") shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2));
                string directory = shell.Publisher.OutputDirectory;
                if (outcome is "close" or "replacement") selected.SetResult(new Uri(Path.GetTempPath()));
                await settled.Task.WaitAsync(TimeSpan.FromSeconds(10));
                if (outcome == "close") await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Dispatcher.UIThread.RunJobs();
                MainWindow current = window!;
                return (focused: outcome == "replacement"
                        ? current.GetLogicalDescendants().OfType<TextBox>().Any(control =>
                            AutomationProperties.GetName(control) == "Service name" && control.IsFocused)
                        : current.GetControl<Button>("SelectOutput").IsFocused,
                    directoryUnchanged: shell.Publisher.OutputDirectory == directory,
                    closedCleanly: outcome != "close" || current.DataContext is null && shell.Workspace.Form is null,
                    safe: current.GetLogicalDescendants().OfType<Control>().All(control =>
                        !(AutomationProperties.GetName(control)?.Contains("synthetic-private", StringComparison.Ordinal) ?? false) &&
                        !(AutomationProperties.GetHelpText(control)?.Contains("synthetic-private", StringComparison.Ordinal) ?? false)) &&
                        !(current.GetControl<TextBlock>("Status").Text?.Contains("synthetic-private", StringComparison.Ordinal) ?? false) &&
                        !(current.GetControl<TextBlock>("StatusHelp").Text?.Contains("synthetic-private", StringComparison.Ordinal) ?? false));
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(observed.directoryUnchanged).IsTrue();
            await Assert.That(observed.safe).IsTrue();
            await Assert.That(observed.closedCleanly).IsTrue();
            if (outcome != "close") await Assert.That(observed.focused).IsTrue();
        }
        finally
        {
            selected.TrySetResult(null);
            await session.Dispatch(() => { window?.Close(); shell?.Dispose(); }, CancellationToken.None);
            await Task.Run(session.Dispose);
        }
    }

    [Test]
    [NotInParallel]
    public async Task ClosingRealWindowCancelsStagedNativeSaveBeforeCommit()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopLocalSelectionTests));
        try
        {
            var observed = await session.Dispatch(async () =>
            {
                var staged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var shell = DesktopTestContext.Root(async (path, bytes, cancellation) =>
                {
                    using var output = await new ProtectedArtifactWriter().PrepareAsync(
                        SetupArtifactKind.Unknown, path, bytes, cancellation);
                    staged.SetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                    return await output.CommitAsync(cancellation);
                }, directory.Path, protectedOutputAvailable: true);
                shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    await shell.Publisher.PrepareAsync();
                    Task<ProtectedArtifactCommitResult> saving = shell.Publisher.SaveAsync();
                    await staged.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    window.Close();
                    string status = shell.Status;
                    release.SetResult();
                    await saving.WaitAsync(TimeSpan.FromSeconds(10));
                    return (noFiles: Directory.GetFiles(directory.Path).Length == 0,
                        currentStatus: shell.Status == status, noPreparation: !shell.Publisher.CanSave && !shell.Publisher.IsSaving);
                }
                finally { release.TrySetResult(); window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(observed.noFiles).IsTrue();
            await Assert.That(observed.currentStatus).IsTrue();
            await Assert.That(observed.noPreparation).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    public async Task LocalSelectionFeedsBoundedDefinitionImportAndDestinationOperations()
    {
        using var directory = new TemporaryDirectory();
        string definition = Path.Combine(directory.Path, "setup.json");
        string existing = Path.Combine(directory.Path, "existing.json");
        await File.WriteAllBytesAsync(definition, DesktopApplicationTests.Definition());
        await File.WriteAllTextAsync(existing, """{"owner":{"name":"synthetic-private-answer"}}""");
        using var shell = DesktopTestContext.Root(select: (kind, _) => Task.FromResult<Uri?>(new Uri(kind switch
        {
            DesktopPathSelectionKind.Definition => definition,
            DesktopPathSelectionKind.Import => existing,
            _ => directory.Path
        })));
        await Assert.That(await shell.Loader.SelectDefinitionAsync()).IsTrue();
        await Assert.That(shell.Workspace.Form is not null).IsTrue();
        await Assert.That(await shell.Publisher.SelectImportAsync()).IsTrue();
        await Assert.That(shell.Workspace.Form!.Fields.Single(input => input.Definition.Id == "owner").Text)
            .Contains("synthetic-private-answer");
        await shell.Publisher.PrepareAsync();
        await Assert.That(await shell.Publisher.SelectOutputDirectoryAsync()).IsTrue();
        await Assert.That(shell.Publisher.OutputDirectory).IsEqualTo(directory.Path);
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Status.Contains("synthetic-private", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments("cancel")]
    [Arguments("unavailable")]
    [Arguments("remote")]
    [Arguments("remote-file")]
    [Arguments("error")]
    public async Task CancelledUnavailableAndNonlocalSelectionsPreserveAnswersAndPreparation(string result)
    {
        using var shell = DesktopTestContext.Root(select: (_, _) => result switch
        {
            "cancel" => Task.FromResult<Uri?>(null),
            "unavailable" => Task.FromException<Uri?>(new NotSupportedException("synthetic-private-path")),
            "remote" => Task.FromResult<Uri?>(new Uri("https://example.invalid/synthetic-private-path")),
            "remote-file" => Task.FromResult<Uri?>(new Uri("file://example.invalid/synthetic-private-path")),
            _ => Task.FromException<Uri?>(new IOException("synthetic-private-path"))
        });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.Publisher.PrepareAsync();
        var form = shell.Workspace.Form;
        var preparation = shell.Publisher.Preparation;
        string path = shell.Publisher.OutputDirectory;
        await Assert.That(await shell.Publisher.SelectOutputDirectoryAsync()).IsFalse();
        await Assert.That(ReferenceEquals(shell.Workspace.Form, form)).IsTrue();
        await Assert.That(ReferenceEquals(shell.Publisher.Preparation, preparation)).IsTrue();
        await Assert.That(shell.Publisher.OutputDirectory).IsEqualTo(path);
        await Assert.That(shell.Status.Contains("synthetic-private", StringComparison.Ordinal)).IsFalse();
        if (result != "cancel") await Assert.That(shell.Status.StartsWith("selection-", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("replacement")]
    [Arguments("destination")]
    [Arguments("cancel")]
    [Arguments("dispose")]
    public async Task LatePickerAcceptanceCannotChangeNewState(string change)
    {
        using var directory = new TemporaryDirectory();
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root(select: (_, _) => selected.Task);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        Task<bool> pending = shell.Publisher.SelectOutputDirectoryAsync();
        switch (change)
        {
            case "edit": shell.Workspace.Form!.Fields[0].Text = "corrected"; break;
            case "replacement": shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
            case "destination": shell.Publisher.OutputDirectory = directory.Path; break;
            case "cancel": shell.Cancel(); break;
            case "dispose": shell.Dispose(); break;
        }
        string path = shell.Publisher.OutputDirectory;
        string status = shell.Status;
        selected.SetResult(new Uri(Path.Combine(directory.Path, "late-selection")));
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.Publisher.OutputDirectory).IsEqualTo(path);
        await Assert.That(shell.Status).IsEqualTo(status);
    }

    [Test]
    public async Task LocalCompactSourceLoadsBeforeAnswersAndInvalidReloadDropsPreparation()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "source.json");
        await File.WriteAllTextAsync(path, """
            {"$schema":"urn:islamu:configurator:authoring:v1",
             "identity":{"familyId":"f93a6b9f-c210-47a9-b90b-434954040282","revision":1},
             "project":{"id":"local.service","name":"Local Service"},"applicationVersions":["1.0"],
             "fields":{"HOST":{"type":"string","sensitivity":"public","default":"example.test"}},
             "outputs":{"settings.json":{"format":"json"}}}
            """);
        using var shell = DesktopTestContext.Root();
        shell.Loader.DefinitionPath = path;
        await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsTrue();
        await Assert.That(shell.Workspace.Form!.Definition.ApplicationId).IsEqualTo("local.service");
        var digest = shell.Workspace.Form.Definition.Digest;
        string yamlPath = Path.Combine(directory.Path, "source.yaml");
        await File.WriteAllTextAsync(yamlPath, """
            $schema: urn:islamu:configurator:authoring:v1
            identity:
              familyId: f93a6b9f-c210-47a9-b90b-434954040282
              revision: 1
            project:
              id: local.service
              name: Local Service
            applicationVersions: ["1.0"]
            fields:
              HOST:
                type: string
                sensitivity: public
                default: example.test
            outputs:
              settings.json:
                format: json
            """);
        shell.Loader.DefinitionPath = yamlPath;
        await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsTrue();
        await Assert.That(shell.Workspace.Form!.Definition.Digest).IsEqualTo(digest);
        await shell.Publisher.PrepareAsync();
        await Assert.That(shell.Publisher.Preparation).IsNotNull();
        await File.WriteAllTextAsync(yamlPath, "synthetic-private-invalid-source");
        await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsFalse();
        await Assert.That(shell.Workspace.Form).IsNull();
        await Assert.That(shell.Publisher.Preparation).IsNull();
        await Assert.That(shell.Status).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task UndeclaredLocalSyntaxCannotCreateWorkspace()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "source.txt");
        await File.WriteAllBytesAsync(path, DesktopApplicationTests.Definition());
        using var shell = DesktopTestContext.Root();
        shell.Loader.DefinitionPath = path;
        await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsFalse();
        await Assert.That(shell.Workspace.Form).IsNull();
        await Assert.That(shell.Status).IsEqualTo("definition-invalid:source-format-invalid");
    }

    [Test]
    public async Task LateDefinitionPickerCannotReplaceNewDefinition()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "late.json");
        await File.WriteAllTextAsync(path, "synthetic-private-invalid-source");
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root(select: (_, _) => selected.Task);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        Task<bool> pending = shell.Loader.SelectDefinitionAsync();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2));
        var replacement = shell.Workspace.Form!.Definition.Digest;
        string status = shell.Status;
        selected.SetResult(new Uri(path));
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(shell.Workspace.Form!.Definition.Digest).IsEqualTo(replacement);
        await Assert.That(shell.Status).IsEqualTo(status);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("configurator-desktop-selection-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
