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
        SetupDesktopShellViewModel? shell = null;
        MainWindow? window = null;
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var observed = await session.Dispatch(async () =>
            {
                shell = new SetupDesktopShellViewModel();
                shell.LoadDefinition(DesktopApplicationTests.Definition());
                window = new MainWindow(_ => outcome switch
                {
                    "cancel" => Task.FromResult<Uri?>(null),
                    "unavailable" => Task.FromException<Uri?>(new NotSupportedException("synthetic-private-path")),
                    _ => selected.Task
                })
                { DataContext = shell };
                window.Show();
                window.UpdateLayout();
                shell.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(shell.IsSelecting) && !shell.IsSelecting)
                        settled.TrySetResult();
                };
                window.GetControl<Button>("SelectOutput").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (outcome == "close") window.Close();
                if (outcome == "replacement") shell.LoadDefinition(DesktopApplicationTests.Definition(2));
                string directory = shell.OutputDirectory;
                if (outcome is "close" or "replacement") selected.SetResult(new Uri(Path.GetTempPath()));
                await settled.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Dispatcher.UIThread.RunJobs();
                MainWindow current = window!;
                return (focused: current.GetControl<Button>("SelectOutput").IsFocused,
                    directoryUnchanged: shell.OutputDirectory == directory,
                    safe: current.GetLogicalDescendants().OfType<Control>().All(control =>
                        !(AutomationProperties.GetName(control)?.Contains("synthetic-private", StringComparison.Ordinal) ?? false) &&
                        !(AutomationProperties.GetHelpText(control)?.Contains("synthetic-private", StringComparison.Ordinal) ?? false)) &&
                        !current.GetControl<TextBlock>("Status").Text!.Contains("synthetic-private", StringComparison.Ordinal) &&
                        !current.GetControl<TextBlock>("StatusHelp").Text!.Contains("synthetic-private", StringComparison.Ordinal));
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(observed.directoryUnchanged).IsTrue();
            await Assert.That(observed.safe).IsTrue();
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
                using var shell = new SetupDesktopShellViewModel(async (path, bytes, cancellation) =>
                {
                    using var output = await new ProtectedArtifactWriter().PrepareAsync(
                        SetupArtifactKind.Unknown, path, bytes, cancellation);
                    staged.SetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                    return await output.CommitAsync(cancellation);
                }, directory.Path, protectedOutputAvailable: true);
                shell.LoadDefinition(DesktopApplicationTests.Definition());
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    await shell.PrepareAsync();
                    Task<ProtectedArtifactStatus> saving = shell.SaveAsync();
                    await staged.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    window.Close();
                    string status = shell.Status;
                    release.SetResult();
                    await saving.WaitAsync(TimeSpan.FromSeconds(10));
                    return (noFiles: Directory.GetFiles(directory.Path).Length == 0,
                        currentStatus: shell.Status == status, noPreparation: !shell.CanSave && !shell.IsSaving);
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
        using var shell = new SetupDesktopShellViewModel();
        await Assert.That(await shell.SelectPathAsync(DesktopPathSelectionKind.Definition,
            _ => Task.FromResult<Uri?>(new Uri(definition)))).IsTrue();
        await Assert.That(shell.Form is not null).IsTrue();
        await Assert.That(await shell.SelectPathAsync(DesktopPathSelectionKind.Import,
            _ => Task.FromResult<Uri?>(new Uri(existing)))).IsTrue();
        await Assert.That(shell.Form!.Fields.Single(input => input.Definition.Id == "owner").Text)
            .Contains("synthetic-private-answer");
        await shell.PrepareAsync();
        await Assert.That(await shell.SelectPathAsync(DesktopPathSelectionKind.OutputDirectory,
            _ => Task.FromResult<Uri?>(new Uri(directory.Path)))).IsTrue();
        await Assert.That(shell.OutputDirectory).IsEqualTo(directory.Path);
        await Assert.That(shell.CanSave).IsFalse();
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
        using var shell = new SetupDesktopShellViewModel();
        shell.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.PrepareAsync();
        var form = shell.Form;
        var preparation = shell.Preparation;
        string path = shell.OutputDirectory;
        await Assert.That(await shell.SelectPathAsync(DesktopPathSelectionKind.OutputDirectory, _ => result switch
        {
            "cancel" => Task.FromResult<Uri?>(null),
            "unavailable" => Task.FromException<Uri?>(new NotSupportedException("synthetic-private-path")),
            "remote" => Task.FromResult<Uri?>(new Uri("https://example.invalid/synthetic-private-path")),
            "remote-file" => Task.FromResult<Uri?>(new Uri("file://example.invalid/synthetic-private-path")),
            _ => Task.FromException<Uri?>(new IOException("synthetic-private-path"))
        })).IsFalse();
        await Assert.That(ReferenceEquals(shell.Form, form)).IsTrue();
        await Assert.That(ReferenceEquals(shell.Preparation, preparation)).IsTrue();
        await Assert.That(shell.OutputDirectory).IsEqualTo(path);
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
        using var shell = new SetupDesktopShellViewModel();
        shell.LoadDefinition(DesktopApplicationTests.Definition());
        var selected = new TaskCompletionSource<Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> pending = shell.SelectPathAsync(DesktopPathSelectionKind.OutputDirectory, _ => selected.Task);
        switch (change)
        {
            case "edit": shell.Form!.Fields[0].Text = "corrected"; break;
            case "replacement": shell.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
            case "destination": shell.OutputDirectory = directory.Path; break;
            case "cancel": shell.Cancel(); break;
            case "dispose": shell.Dispose(); break;
        }
        string path = shell.OutputDirectory;
        string status = shell.Status;
        selected.SetResult(new Uri(Path.Combine(directory.Path, "late-selection")));
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(shell.OutputDirectory).IsEqualTo(path);
        await Assert.That(shell.Status).IsEqualTo(status);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("configurator-desktop-selection-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
