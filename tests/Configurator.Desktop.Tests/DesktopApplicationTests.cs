namespace Configurator.Desktop.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;

public sealed class DesktopApplicationTests
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Configurator.Desktop.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    [Test]
    [NotInParallel]
    public async Task ArtifactAndFilenameControlsRemainSeparateAtMinimumWindowWidth()
    {
        var result = await InWindow(window =>
        {
            window.Width = window.MinWidth;
            Dispatcher.UIThread.RunJobs();
            var artifact = window.GetControl<ComboBox>("Artifact");
            var filename = window.GetControl<TextBox>("FileName");
            Point left = artifact.TranslatePoint(default, window)!.Value;
            Point right = filename.TranslatePoint(default, window)!.Value;
            return (artifact.Bounds.Width, filename.Bounds.Width,
                separate: left.X + artifact.Bounds.Width <= right.X,
                selected: artifact.SelectedItem is SetupArtifactDefinition,
                experienceSelected: ReferenceEquals(window.GetControl<ComboBox>("Experience").SelectedItem,
                    ((DesktopShellViewModel)window.DataContext!).Workspace.SelectedExperience));
        });
        await Assert.That(result.Item1 > 0).IsTrue();
        await Assert.That(result.Item2 > 0).IsTrue();
        await Assert.That(result.separate).IsTrue();
        await Assert.That(result.selected).IsTrue();
        await Assert.That(result.experienceSelected).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task RealControlsEditNestedFieldsAndListsThenInvalidatePreparedOutput()
    {
        var result = await InWindow(window =>
        {
            var shell = (DesktopShellViewModel)window.DataContext!;
            TextBox name = Input<TextBox>(window, "Service name");
            name.Text = "Desktop service";
            Input<ComboBox>(window, "Mode").SelectedItem = "remote";
            Input<CheckBox>(window, "Enabled").IsChecked = false;
            Input<TextBox>(window, "Port").Text = "8443";
            Input<TextBox>(window, "Ratio").Text = "1.25";
            Input<TextBox>(window, "Owner name").Text = "Private owner";
            Click(window.GetLogicalDescendants().OfType<Button>().Single(button =>
                AutomationProperties.GetName(button) == "Add item to Hosts"));
            Dispatcher.UIThread.RunJobs();
            Input<TextBox>(window, "Hosts item").Text = "alpha.example";
            Dispatcher.UIThread.RunJobs();
            Click(window.GetControl<Button>("Validate"));
            Click(window.GetControl<Button>("Prepare"));
            Dispatcher.UIThread.RunJobs();
            bool prepared = shell.Publisher.CanSave;
            string preparationStatus = shell.Status;
            using var snapshot = JsonDocument.Parse(JsonSerializer.Serialize(shell.Workspace.Form!.Snapshot().Values));
            bool values = snapshot.RootElement.GetProperty("enabled").GetBoolean() == false &&
                snapshot.RootElement.GetProperty("port").GetInt64() == 8443 &&
                snapshot.RootElement.GetProperty("ratio").GetDecimal() == 1.25m &&
                snapshot.RootElement.GetProperty("owner").GetProperty("name").GetString() == "Private owner" &&
                snapshot.RootElement.GetProperty("hosts")[0].GetString() == "alpha.example";
            bool masked = Input<TextBox>(window, "Owner name").PasswordChar != '\0';
            name.Text = "Changed service";
            Dispatcher.UIThread.RunJobs();
            return (prepared, preparationStatus, values, masked, invalidated: !shell.Publisher.CanSave);
        });
        await Assert.That(result.preparationStatus).StartsWith("artifact-ready:");
        await Assert.That(result.prepared).IsTrue();
        await Assert.That(result.values).IsTrue();
        await Assert.That(result.masked).IsTrue();
        await Assert.That(result.invalidated).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task KeyboardTabAndValidationMoveActualFocusToNamedControls()
    {
        var result = await InWindow(window =>
        {
            var path = window.GetControl<TextBox>("DefinitionPath");
            path.Focus();
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            bool tabMoved = window.GetControl<Button>("SelectDefinition").IsFocused;
            Input<TextBox>(window, "Service name").Text = " ";
            Dispatcher.UIThread.RunJobs();
            Click(window.GetControl<Button>("Validate"));
            bool errorFocused = Input<TextBox>(window, "Service name").IsFocused;
            Input<TextBox>(window, "Service name").Text = "Keyboard service";
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            bool prepared = ((DesktopShellViewModel)window.DataContext!).Publisher.CanSave;
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            return (tabMoved, errorFocused, prepared,
                discarded: !((DesktopShellViewModel)window.DataContext!).Publisher.CanSave);
        });
        await Assert.That(result.tabMoved).IsTrue();
        await Assert.That(result.errorFocused).IsTrue();
        await Assert.That(result.prepared).IsTrue();
        await Assert.That(result.discarded).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task OptionalEditorControlRestoresDefaultsWithoutHidingActiveCorrections()
    {
        var result = await InWindow(window =>
        {
            var shell = (DesktopShellViewModel)window.DataContext!;
            var fields = window.GetControl<ItemsControl>("Fields").GetLogicalDescendants()
                .OfType<StackPanel>().Where(item => item.DataContext is FieldViewModel).ToArray();
            StackPanel advanced = fields.First(item => ((FieldViewModel)item.DataContext!).Title == "Advanced note");
            StackPanel protectedField = fields.First(item => ((FieldViewModel)item.DataContext!).Title == "Owner");
            StackPanel consequence = fields.First(item => ((FieldViewModel)item.DataContext!).Title == "Mode");
            bool authoredDefaults = !advanced.IsVisible && protectedField.IsVisible && consequence.IsVisible;
            var optional = window.GetControl<CheckBox>("OptionalEditors");
            optional.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            bool shown = advanced.IsVisible;
            Input<TextBox>(window, "Advanced note").Text = new string('a', 9);
            Dispatcher.UIThread.RunJobs();
            optional.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            bool invalidShown = advanced.IsVisible;
            bool ready = shell.Workspace.Validate();
            shell.Workspace.Fields.Single(field => field.Id == "note").ClearCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            return (authoredDefaults, shown, invalidShown, ready, restored: !advanced.IsVisible);
        });
        await Assert.That(result.authoredDefaults).IsTrue();
        await Assert.That(result.shown).IsTrue();
        await Assert.That(result.invalidShown).IsTrue();
        await Assert.That(result.ready).IsFalse();
        await Assert.That(result.restored).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task AuthoredExperienceControlPreservesEditorsAnswersAndPreparedOutput()
    {
        var result = await InWindow(window =>
        {
            var shell = (DesktopShellViewModel)window.DataContext!;
            shell.Loader.LoadDefinition(DefinitionWithExperiences());
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var experience = window.GetControl<ComboBox>("Experience");
            var optional = window.GetControl<CheckBox>("OptionalEditors");
            var note = shell.Workspace.Fields.Single(field => field.Id == "note");
            TextBox editor = Input<TextBox>(window, "Advanced note");
            Click(window.GetControl<Button>("Prepare"));
            Dispatcher.UIThread.RunJobs();
            var preparation = shell.Publisher.Preparation;
            long revision = shell.Workspace.Form!.InputRevision;
            string status = shell.Status;
            bool initiallyHidden = !note.IsVisible;
            experience.SelectedItem = shell.Workspace.Experiences.Single(item => item.Id == "inspection");
            Dispatcher.UIThread.RunJobs();
            bool selected = shell.Workspace.Form.SelectedExperienceId == "inspection" && note.IsVisible;
            experience.SelectedItem = shell.Workspace.Experiences.Single(item => item.Id == "deployment");
            Dispatcher.UIThread.RunJobs();
            bool defaultsRestored = !note.IsVisible;
            optional.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            bool expanded = note.IsVisible;
            optional.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            return (initiallyHidden, selected, defaultsRestored, expanded,
                restored: !note.IsVisible,
                sameEditor: ReferenceEquals(editor, Input<TextBox>(window, "Advanced note")),
                revisionPreserved: revision == shell.Workspace.Form.InputRevision,
                preparationPreserved: preparation is not null && ReferenceEquals(preparation, shell.Publisher.Preparation),
                statusPreserved: status == shell.Status,
                titles: experience.GetVisualDescendants().OfType<TextBlock>()
                    .Any(text => text.Text == "Prepare deployment" && text.IsEffectivelyVisible && text.Bounds.Width > 0));
        });
        await Assert.That(result.initiallyHidden).IsTrue();
        await Assert.That(result.selected).IsTrue();
        await Assert.That(result.defaultsRestored).IsTrue();
        await Assert.That(result.expanded).IsTrue();
        await Assert.That(result.restored).IsTrue();
        await Assert.That(result.sameEditor).IsTrue();
        await Assert.That(result.revisionPreserved).IsTrue();
        await Assert.That(result.preparationPreserved).IsTrue();
        await Assert.That(result.statusPreserved).IsTrue();
        await Assert.That(result.titles).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task InactiveInvalidControlStaysHiddenEvenWhenOptionalEditorsExpand()
    {
        var result = await InWindow(window =>
        {
            var shell = (DesktopShellViewModel)window.DataContext!;
            Input<ComboBox>(window, "Mode").SelectedItem = "remote";
            Input<TextBox>(window, "Port").Text = "invalid-number";
            Dispatcher.UIThread.RunJobs();
            bool activeInvalid = shell.Workspace.Form!.HasInputErrors;
            Input<ComboBox>(window, "Mode").SelectedItem = "local";
            window.GetControl<CheckBox>("OptionalEditors").IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            var port = shell.Workspace.Fields.Single(field => field.Id == "port");
            return (activeInvalid, hidden: !Input<TextBox>(window, "Port").IsEffectivelyVisible,
                visible: port.IsVisible, error: port.HasError, ready: shell.Workspace.Validate());
        });
        await Assert.That(result.activeInvalid).IsTrue();
        await Assert.That(result.hidden).IsTrue();
        await Assert.That(result.visible).IsFalse();
        await Assert.That(result.error).IsFalse();
        await Assert.That(result.ready).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task InvalidStructuredNumericInputCannotBeSilentlyOmitted()
    {
        var result = await InWindow(window =>
        {
            var shell = (DesktopShellViewModel)window.DataContext!;
            Input<TextBox>(window, "Owner age").Text = "not-an-integer";
            Dispatcher.UIThread.RunJobs();
            Click(window.GetControl<Button>("Prepare"));
            Dispatcher.UIThread.RunJobs();
            return (shell.Publisher.CanSave, shell.Status, shell.Workspace.Form!.HasInputErrors);
        });
        await Assert.That(result.CanSave).IsFalse();
        await Assert.That(result.HasInputErrors).IsTrue();
        await Assert.That(result.Status).Contains("owner:input-invalid");
    }

    [Test]
    [Arguments("config")]
    [Arguments("environment")]
    [Arguments("yaml")]
    [Arguments("notes")]
    public async Task GenericArtifactIdsSaveAllDeclaredFormats(string artifactId)
    {
        byte[] written = [];
        using var shell = Shell((_, bytes, _) =>
        {
            written = bytes.ToArray();
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        });
        try
        {
            await Assert.That(shell.Loader.LoadDefinition(Definition())).IsTrue();
            shell.Publisher.ArtifactId = artifactId;
            await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
            await Assert.That(shell.Publisher.Preparation!.ArtifactId).IsEqualTo(artifactId);
            await Assert.That(shell.Publisher.Preparation.DefinitionDigest).IsEqualTo(shell.Workspace.Form!.Definition.Digest);
            await Assert.That((await shell.Publisher.SaveAsync()).Succeeded).IsTrue();
            string output = Encoding.UTF8.GetString(written);
            await Assert.That(output).Contains("Example service");
            if (artifactId == "config")
            {
                using var parsed = JsonDocument.Parse(written);
                await Assert.That(parsed.RootElement.GetProperty("name").GetString()).IsEqualTo("Example service");
            }
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            await Assert.That(shell.Publisher.Preparation).IsNull();
            await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        }
        finally { CryptographicOperations.ZeroMemory(written); }
    }

    [Test]
    public async Task InputsTargetsAndDefinitionReplacementInvalidatePreparations()
    {
        using var shell = Shell();
        shell.Loader.LoadDefinition(Definition());
        await shell.Publisher.PrepareAsync();
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Updated";
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await shell.Publisher.PrepareAsync();
        shell.Publisher.FileName = "other.json";
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await shell.Publisher.PrepareAsync();
        shell.Publisher.OutputDirectory = Path.Combine(Path.GetTempPath(), "different");
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await shell.Publisher.PrepareAsync();
        shell.Publisher.ArtifactId = "notes";
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await shell.Publisher.PrepareAsync();
        string pin = shell.Loader.DefinitionPin;
        shell.Loader.LoadDefinition(Definition(revision: 2));
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Loader.DefinitionPin == pin).IsFalse();
        await Assert.That(shell.Workspace.Form!.Definition.Revision).IsEqualTo(2L);
        await Assert.That(shell.Workspace.Form.Fields.Single(item => item.Definition.Id == "name").Text).IsEqualTo("Example service");
        await shell.Publisher.PrepareAsync();
        await Assert.That(shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes("{}"))).IsFalse();
        await Assert.That(shell.Workspace.Form).IsNull();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
    }

    [Test]
    public async Task FailedProtectedSaveConsumesOutputAndZeroesNativeBuffer()
    {
        ReadOnlyMemory<byte> observed = default;
        using var shell = Shell((_, bytes, _) =>
        {
            observed = bytes;
            return Task.FromResult(ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsafeTarget));
        });
        shell.Loader.LoadDefinition(Definition());
        await shell.Publisher.PrepareAsync();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("save-failed:UnsafeTarget");
        await Assert.That(observed.Span.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
    }

    [Test]
    public async Task StaleSaveCompletionCannotOverwriteNewDefinitionState()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        using var shell = Shell(async (_, _, cancellation) =>
        {
            observed = cancellation;
            started.SetResult();
            await finish.Task;
            return ProtectedArtifactCommitResult.Installed(true);
        });
        shell.Loader.LoadDefinition(Definition());
        await shell.Publisher.PrepareAsync();
        Task<ProtectedArtifactCommitResult> pending = shell.Publisher.SaveAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        shell.Loader.LoadDefinition(Definition(revision: 2));
        await Assert.That(observed.IsCancellationRequested).IsTrue();
        finish.SetResult();
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(5))).Succeeded).IsTrue();
        await Assert.That(shell.Status).IsEqualTo("definition-loaded");
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Workspace.Form!.Definition.Revision).IsEqualTo(2L);
    }

    [Test]
    public async Task CancellationConsumesOutputWithoutRestoringSave()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = Shell(async (_, _, cancellation) =>
        {
            using var subscription = cancellation.Register(() => cancelled.TrySetResult());
            started.SetResult();
            await cancelled.Task;
            cancellation.ThrowIfCancellationRequested();
            return ProtectedArtifactCommitResult.Installed(true);
        });
        shell.Loader.LoadDefinition(Definition());
        await shell.Publisher.PrepareAsync();
        var pending = shell.Publisher.SaveAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        shell.Cancel();
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(5))).Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        await Assert.That(shell.Status).IsEqualTo("operation-cancelled");
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Publisher.IsSaving).IsFalse();
    }

    [Test]
    [Arguments("../output.json")]
    [Arguments("-")]
    [Arguments("/tmp/output.json")]
    public async Task UnsafeNamesNeverReachNativeOutput(string fileName)
    {
        bool reached = false;
        using var shell = Shell((_, _, _) =>
        {
            reached = true;
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        });
        shell.Loader.LoadDefinition(Definition());
        shell.Publisher.FileName = fileName;
        await shell.Publisher.PrepareAsync();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
        await Assert.That(reached).IsFalse();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
    }

    [Test]
    public async Task UnsupportedHostHasNoPrivateSaveFallback()
    {
        bool reached = false;
        using var shell = DesktopTestContext.Root((_, _, _) =>
        {
            reached = true;
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(true));
        }, Path.GetTempPath(), protectedOutputAvailable: false);
        shell.Loader.LoadDefinition(Definition());
        await shell.Publisher.PrepareAsync();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.UnsupportedPlatform);
        await Assert.That(reached).IsFalse();
        using var native = DesktopTestContext.Root();
        await Assert.That(native.Publisher.ProtectedSaveAvailable).IsEqualTo(OperatingSystem.IsLinux());
    }

    [Test]
    public async Task ExplicitLocalPathPinsAdmittedBytesUntilUserReloads()
    {
        string path = Path.Combine(Path.GetTempPath(), $"configurator-definition-{Guid.CreateVersion7():N}.json");
        using var shell = Shell();
        try
        {
            await File.WriteAllBytesAsync(path, Definition());
            shell.Loader.DefinitionPath = path;
            await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsTrue();
            var pin = shell.Workspace.Form!.Definition.Digest;
            await File.WriteAllBytesAsync(path, Definition(revision: 2));
            await shell.Publisher.PrepareAsync();
            await Assert.That(shell.Publisher.Preparation!.DefinitionDigest).IsEqualTo(pin);
            await Assert.That(await shell.Loader.LoadDefinitionAsync()).IsTrue();
            await Assert.That(shell.Workspace.Form!.Definition.Revision).IsEqualTo(2L);
            await Assert.That(shell.Publisher.CanSave).IsFalse();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task NoDefinitionMeansNoDefaultTargetOrArtifact()
    {
        using var shell = Shell();
        await Assert.That(shell.Workspace.Form).IsNull();
        await Assert.That(shell.Publisher.Artifacts.Count).IsEqualTo(0);
        await Assert.That(shell.Publisher.CanPrepare).IsFalse();
        await Assert.That(await shell.Publisher.PrepareAsync()).IsFalse();
        await Assert.That((await shell.Publisher.SaveAsync()).Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
    }

    [Test]
    public async Task PreparationMetadataAndDiagnosticsNeverContainProtectedAnswersOrBytes()
    {
        string marker = "Private-" + Guid.CreateVersion7().ToString("N");
        using var shell = Shell();
        shell.Loader.LoadDefinition(Definition());
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "owner").Text =
            JsonSerializer.Serialize(new { name = marker });
        await shell.Publisher.PrepareAsync();
        await Assert.That(shell.Publisher.Preparation).IsNotNull();
        await Assert.That(JsonSerializer.Serialize(shell.Publisher.Preparation)).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.Status).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.ToString()).DoesNotContain(marker, StringComparison.Ordinal);
        shell.Workspace.Form.Fields.Single(item => item.Definition.Id == "owner").Text = marker;
        shell.Workspace.Validate();
        await Assert.That(shell.Status).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.Publisher.CanSave).IsFalse();
    }

    [Test]
    public async Task PendingExistingFileImportCannotReplaceLaterEdit()
    {
        var pending = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = Shell();
        shell.Loader.LoadDefinition(Definition());
        Task<bool> import = shell.Publisher.ImportAsync(_ => pending.Task);
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Later edit";
        pending.SetResult(Encoding.UTF8.GetBytes("{\"name\":\"Example service\"}"));
        await Assert.That(await import.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(shell.Workspace.Form.Fields.Single(item => item.Definition.Id == "name").Text).IsEqualTo("Later edit");
        await Assert.That(shell.Status).DoesNotContain("Example service", StringComparison.Ordinal);
    }

    [Test]
    public async Task ChangingExistingFilePathDiscardsPendingImportWithoutChangingAnswers()
    {
        var pending = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = Shell();
        shell.Loader.LoadDefinition(Definition());
        shell.Publisher.ExistingFilePath = "first.json";
        Task<bool> import = shell.Publisher.ImportAsync(_ => pending.Task);
        shell.Publisher.ExistingFilePath = "second.json";
        pending.SetResult(Encoding.UTF8.GetBytes("{\"owner\":{\"name\":\"Synthetic private\"}}"));
        await Assert.That(await import.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "owner").Text).IsEmpty();
    }

    [Test]
    public async Task SourceChangeAfterImportCommitDoesNotReportStaleReadyStatus()
    {
        using var shell = Shell();
        shell.Loader.LoadDefinition(Definition());
        shell.Workspace.Form!.InputChanged += (_, _) => shell.Publisher.ExistingFilePath = "next.json";
        bool committed = await shell.Publisher.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(
            Encoding.UTF8.GetBytes("{\"owner\":{\"name\":\"Synthetic private\"}}")));
        await Assert.That(committed).IsTrue();
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(shell.Workspace.Form.Fields.Single(item => item.Definition.Id == "owner").Text)
            .Contains("Synthetic private");
    }

    [Test]
    public async Task ExistingFileImportAppliesSupportedValuesWithoutShowingPrivateAnswer()
    {
        using var directory = new TemporaryDirectory();
        string marker = "Synthetic-" + Guid.CreateVersion7().ToString("N");
        string path = Path.Combine(directory.Path, "existing.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { owner = new { name = marker } }));
        using var shell = Shell();
        shell.Loader.LoadDefinition(Definition());
        shell.Publisher.ExistingFilePath = path;
        await Assert.That(await shell.Publisher.ImportFileAsync()).IsTrue();
        await Assert.That(shell.Status).IsEqualTo("import-ready");
        await Assert.That(shell.Status).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "owner").Text).Contains(marker);
    }

    [Test]
    public async Task DeclaredBundleSelectionAndCapabilityHaveValueFreeStatus()
    {
        using var shell = Shell();
        shell.Loader.LoadDefinition(Definition());
        shell.Publisher.SelectArtifact("environment", true);
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        await Assert.That(shell.Publisher.SelectedArtifactIds).Contains("environment");
        await Assert.That(shell.Publisher.CanSaveBundle).IsTrue();
        await Assert.That(shell.Status).DoesNotContain("Example service", StringComparison.Ordinal);
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Later edit";
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        using var blocked = DesktopTestContext.Root((_, _, _) =>
            Task.FromResult(ProtectedArtifactCommitResult.Installed(true)), Path.GetTempPath(), protectedOutputAvailable: false);
        blocked.Loader.LoadDefinition(Definition());
        await Assert.That(blocked.Publisher.ProtectedSaveAvailable).IsFalse();
        await Assert.That(blocked.Publisher.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task ExistingBundleDestinationBlocksAllFilesAndRecoveryCreatesSelectedOutputs()
    {
        using var directory = new TemporaryDirectory();
        string existing = Path.Combine(directory.Path, "service.env");
        await File.WriteAllTextAsync(existing, "EXISTING=unchanged\n");
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(Definition());
        shell.Publisher.OutputDirectory = directory.Path;
        shell.Publisher.SelectArtifact("environment", true);
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        var blocked = await shell.Publisher.SaveBundleAsync();
        await Assert.That(blocked!.Succeeded).IsFalse();
        await Assert.That(shell.Status).StartsWith("bundle-save-blocked:");
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.json"))).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(existing)).IsEqualTo("EXISTING=unchanged\n");
        await Assert.That(shell.Status).DoesNotContain(directory.Path, StringComparison.Ordinal);
        File.Delete(existing);
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        var saved = await shell.Publisher.SaveBundleAsync();
        await Assert.That(saved!.Succeeded).IsTrue();
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.json"))).IsTrue();
        await Assert.That(File.Exists(existing)).IsTrue();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task EditingDuringBundlePublicationCancelsAndFencesItsResult()
    {
        using var directory = new TemporaryDirectory();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root((_, _, _) =>
            Task.FromResult(ProtectedArtifactCommitResult.Installed(true)), directory.Path, protectedOutputAvailable: true,
            async (artifacts, output, cancellation) =>
            {
                started.SetResult();
                await finish.Task;
                return await ProtectedArtifactBundlePublisher.PublishAsync(new ProtectedArtifactWriter(),
                    artifacts, output, cancellationToken: cancellation);
            });
        shell.Loader.LoadDefinition(Definition());
        shell.Publisher.SelectArtifact("environment", true);
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        Task<ProtectedArtifactBundleResult?> pending = shell.Publisher.SaveBundleAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        shell.Workspace.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Later edit";
        finish.SetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result!.Succeeded).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.json"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.env"))).IsFalse();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task PublisherIoFailureClearsBusyStateWithoutShowingPrivatePath()
    {
        string privatePath = "/synthetic/private/output";
        using var shell = DesktopTestContext.Root((_, _, _) =>
            Task.FromResult(ProtectedArtifactCommitResult.Installed(true)), Path.GetTempPath(), protectedOutputAvailable: true,
            (_, _, _) => throw new IOException(privatePath));
        shell.Loader.LoadDefinition(Definition());
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        await Assert.That(await shell.Publisher.SaveBundleAsync()).IsNull();
        await Assert.That(shell.Status).IsEqualTo("bundle-save-blocked:IoFailure");
        await Assert.That(shell.Status).DoesNotContain(privatePath, StringComparison.Ordinal);
        await Assert.That(shell.Publisher.IsSaving).IsFalse();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task GuidedControlsExposeImportBundleAndProtectedCapability()
    {
        var result = await InWindow(window =>
        {
            var shell = (DesktopShellViewModel)window.DataContext!;
            return (import: window.GetControl<Button>("Import").IsVisible,
                bundle: window.GetControl<Button>("PrepareBundle").IsVisible,
                capability: window.GetControl<TextBlock>("ProtectedCapability").Text,
                selections: window.GetControl<ItemsControl>("ArtifactSelections").ItemCount,
                saveEnabled: window.GetControl<Button>("SaveBundle").IsEffectivelyEnabled,
                available: shell.Publisher.ProtectedSaveAvailable);
        });
        await Assert.That(result.import).IsTrue();
        await Assert.That(result.bundle).IsTrue();
        await Assert.That(result.capability).IsNotNull();
        await Assert.That(result.selections).IsGreaterThan(0);
        await Assert.That(result.saveEnabled).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task RenderedImportRefreshesMaskedFieldWithoutDisplayingPrivateAnswer()
    {
        string marker = "Synthetic-" + Guid.CreateVersion7().ToString("N");
        byte[] content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { owner = new { name = marker } }));
        var result = await InWindow(window =>
        {
            var shell = (DesktopShellViewModel)window.DataContext!;
            bool imported = shell.Publisher.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(content))
                .GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();
            return (success: imported, masked: Input<TextBox>(window, "Owner name").PasswordChar != '\0',
                displayed: Input<TextBox>(window, "Owner name").Text == marker,
                status: window.GetControl<TextBlock>("Status").Text);
        });
        CryptographicOperations.ZeroMemory(content);
        await Assert.That(result.status).IsEqualTo("import-ready");
        await Assert.That(result.success).IsTrue();
        await Assert.That(result.masked).IsTrue();
        await Assert.That(result.displayed).IsTrue();
        await Assert.That(result.status).DoesNotContain(marker, StringComparison.Ordinal);
    }

    private static DesktopShellViewModel Shell(
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactCommitResult>>? save = null) =>
        DesktopTestContext.Root(save ?? ((_, _, _) => Task.FromResult(ProtectedArtifactCommitResult.Installed(true))),
            Path.GetTempPath(), protectedOutputAvailable: true);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("configurator-desktop-bundle-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    [Test]
    [NotInParallel]
    public async Task ObjectListControlsPreserveDeclaredPropertiesAndRemoveExactlyOneRow()
    {
        var result = await InWindow(window =>
        {
            using JsonDocument schema = JsonDocument.Parse("""
            {"id":"rows","title":"Rows","type":"list","itemType":"object","sensitivity":"public","disclosure":"guided","maxItems":2,
             "properties":[
               {"id":"name","title":"Row name","type":"string","sensitivity":"public","disclosure":"guided","required":true},
               {"id":"password","title":"Row password","type":"string","sensitivity":"secret","disclosure":"guided"},
               {"id":"aliases","title":"Aliases","type":"list","itemType":"string","required":true,"sensitivity":"public","disclosure":"guided"},
               {"id":"localizations","title":"Localizations","type":"list","itemType":"object","required":true,"sensitivity":"public","disclosure":"guided","properties":[
                 {"id":"locale","title":"Locale","type":"string","sensitivity":"public","disclosure":"guided"}]},
               {"id":"settings","title":"Settings","type":"object","sensitivity":"public","disclosure":"guided","required":true,"properties":[]}]}
            """);
            JsonObject source = JsonNode.Parse(Definition())!.AsObject();
            source["fields"] = new JsonArray(JsonNode.Parse(schema.RootElement.GetRawText()));
            source["artifacts"] = JsonNode.Parse("""
            [{"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"process",
              "projection":{"op":"object","members":{"rows":{"op":"copy","field":"rows"}}}}]
            """);
            var shell = (DesktopShellViewModel)window.DataContext!;
            shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()));
            var editor = window.GetControl<ItemsControl>("Fields");
            window.UpdateLayout();
            Click(editor.GetLogicalDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Add item to Rows"));
            Dispatcher.UIThread.RunJobs();
            TextBox name = editor.GetLogicalDescendants().OfType<TextBox>().Single(input => AutomationProperties.GetName(input) == "Row name");
            TextBox password = editor.GetLogicalDescendants().OfType<TextBox>().Single(input => AutomationProperties.GetName(input) == "Row password");
            name.Text = "first";
            password.Text = "synthetic-secret";
            Click(editor.GetLogicalDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Add item to Aliases"));
            Dispatcher.UIThread.RunJobs();
            editor.GetLogicalDescendants().OfType<TextBox>().Single(input => AutomationProperties.GetName(input) == "Aliases item").Text = "alias";
            Click(editor.GetLogicalDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Add item to Localizations"));
            Dispatcher.UIThread.RunJobs();
            editor.GetLogicalDescendants().OfType<TextBox>().Single(input => AutomationProperties.GetName(input) == "Locale").Text = "en";
            Dispatcher.UIThread.RunJobs();
            using JsonDocument before = JsonDocument.Parse(shell.Workspace.Form!.Fields.Single().Text);
            bool typed = before.RootElement[0].GetProperty("name").GetString() == "first" &&
                before.RootElement[0].GetProperty("settings").ValueKind == JsonValueKind.Object &&
                before.RootElement[0].GetProperty("aliases")[0].GetString() == "alias" &&
                before.RootElement[0].GetProperty("localizations")[0].GetProperty("locale").GetString() == "en";
            Click(editor.GetLogicalDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Remove item from Aliases"));
            Dispatcher.UIThread.RunJobs();
            using JsonDocument nestedRemoval = JsonDocument.Parse(shell.Workspace.Form!.Fields.Single().Text);
            typed &= nestedRemoval.RootElement[0].GetProperty("aliases").GetArrayLength() == 0 &&
                nestedRemoval.RootElement[0].GetProperty("name").GetString() == "first";
            bool masked = password.PasswordChar != '\0';
            Click(editor.GetLogicalDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Remove item from Rows"));
            Dispatcher.UIThread.RunJobs();
            return (typed, masked, answer: shell.Workspace.Form!.Fields.Single().Text);
        });
        await Assert.That(result.typed).IsTrue();
        await Assert.That(result.masked).IsTrue();
        await Assert.That(result.answer).IsEqualTo("[]");
    }

    private static T Input<T>(MainWindow window, string title) where T : Control =>
        window.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetName(control) == title);
    private static void Click(Button button)
    {
        var command = button.Command ?? throw new InvalidOperationException("action-unbound");
        if (button.IsEffectivelyEnabled && command.CanExecute(button.CommandParameter))
        {
            button.Focus();
            command.Execute(button.CommandParameter);
        }
    }

    private static async Task<T> InWindow<T>(Func<MainWindow, T> action)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            return await session.Dispatch(() =>
            {
                using var shell = Shell();
                shell.Loader.LoadDefinition(Definition());
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    return action(window);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        finally { await Task.Run(session.Dispose).ConfigureAwait(false); }
    }

    internal static byte[] Definition(long revision = 1) => Encoding.UTF8.GetBytes(
        """
        {
          "schemaVersion":1,"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726",
          "revision":REVISION,"applicationId":"example.service","applicationVersions":["1.0"],
          "publisher":"example","title":"Example setup","entryPoints":["setup"],"locales":["en"],
          "fields":[
            {"id":"name","type":"string","title":"Service name","required":true,"default":"Example service","sensitivity":"public","disclosure":"essential"},
            {"id":"mode","type":"enum","title":"Mode","choices":["local","remote"],"default":"local","sensitivity":"public","disclosure":"guided","consequence":"Remote mode needs a declared port."},
            {"id":"enabled","type":"boolean","title":"Enabled","default":true,"sensitivity":"public","disclosure":"guided"},
            {"id":"port","type":"integer","title":"Port","minimum":1,"maximum":65535,"default":443,"sensitivity":"public","disclosure":"guided","activeWhen":{"op":"equals","field":"mode","value":"remote"}},
            {"id":"ratio","type":"number","title":"Ratio","default":1,"sensitivity":"public","disclosure":"guided"},
            {"id":"owner","type":"object","title":"Owner","sensitivity":"public","disclosure":"guided","properties":[
              {"id":"name","type":"string","title":"Owner name","sensitivity":"sensitive","disclosure":"guided"},
              {"id":"age","type":"integer","title":"Owner age","sensitivity":"public","disclosure":"guided"}
            ]},
            {"id":"hosts","type":"list","itemType":"string","title":"Hosts","sensitivity":"public","disclosure":"guided","maxItems":3},
            {"id":"note","type":"string","title":"Advanced note","sensitivity":"public","disclosure":"advanced","maxLength":8}
          ],
          "artifacts":[
            {"id":"config","format":"json","fileName":"service.json","applySemantics":"create","restart":"process","projection":{"op":"object","members":{"name":{"op":"copy","field":"name"},"owner":{"op":"copy","field":"owner"},"hosts":{"op":"copy","field":"hosts"}}}},
            {"id":"environment","format":"dotenv","fileName":"service.env","applySemantics":"create","restart":"process","projection":{"op":"object","members":{"NAME":{"op":"copy","field":"name"}}}},
            {"id":"yaml","format":"yaml","fileName":"service.yaml","applySemantics":"replace","restart":"none","projection":{"op":"object","members":{"name":{"op":"copy","field":"name"}}}},
            {"id":"notes","format":"markdown","fileName":"notes.md","applySemantics":"generationOnly","restart":"none","projection":{"op":"list","items":[{"op":"copy","field":"name"}]}}
          ]
        }
        """.Replace("REVISION", revision.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));

    internal static byte[] DefinitionWithExperiences()
    {
        JsonObject source = JsonNode.Parse(Definition(2))!.AsObject();
        source["presentation"] = JsonNode.Parse("""
            {"defaultExperience":"deployment",
             "sections":[{"id":"service","title":"Service settings","fields":["name","mode","enabled","port","ratio","owner","hosts","note"]}],
             "experiences":[
               {"id":"deployment","title":"Prepare deployment","initialDisclosure":"guided","navigation":"sections","sections":["service"]},
               {"id":"inspection","title":"Inspect all settings","initialDisclosure":"advanced","navigation":"sections","sections":["service"]}]}
            """);
        return Encoding.UTF8.GetBytes(source.ToJsonString());
    }
}
