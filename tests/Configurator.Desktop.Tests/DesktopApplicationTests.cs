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
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.ViewModels;
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
                modeSelected: window.GetControl<ComboBox>("DisplayMode").SelectedIndex
                    == (int)((SetupDesktopShellViewModel)window.DataContext!).DisplayMode);
        });
        await Assert.That(result.Item1 > 0).IsTrue();
        await Assert.That(result.Item2 > 0).IsTrue();
        await Assert.That(result.separate).IsTrue();
        await Assert.That(result.selected).IsTrue();
        await Assert.That(result.modeSelected).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task RealControlsEditNestedFieldsAndListsThenInvalidatePreparedOutput()
    {
        var result = await InWindow(window =>
        {
            var shell = (SetupDesktopShellViewModel)window.DataContext!;
            TextBox name = Input<TextBox>(window, "Service name");
            name.Text = "Desktop service";
            Input<ComboBox>(window, "Mode").SelectedItem = "remote";
            Input<ComboBox>(window, "Enabled").SelectedItem = "false";
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
            bool prepared = shell.CanSave;
            string preparationStatus = shell.Status;
            using var snapshot = JsonDocument.Parse(JsonSerializer.Serialize(shell.Form!.Snapshot().Values));
            bool values = snapshot.RootElement.GetProperty("enabled").GetBoolean() == false &&
                snapshot.RootElement.GetProperty("port").GetInt64() == 8443 &&
                snapshot.RootElement.GetProperty("ratio").GetDecimal() == 1.25m &&
                snapshot.RootElement.GetProperty("owner").GetProperty("name").GetString() == "Private owner" &&
                snapshot.RootElement.GetProperty("hosts")[0].GetString() == "alpha.example";
            bool masked = Input<TextBox>(window, "Owner name").PasswordChar != '\0';
            name.Text = "Changed service";
            Dispatcher.UIThread.RunJobs();
            return (prepared, preparationStatus, values, masked, invalidated: !shell.CanSave);
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
            bool prepared = ((SetupDesktopShellViewModel)window.DataContext!).CanSave;
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            return (tabMoved, errorFocused, prepared,
                discarded: !((SetupDesktopShellViewModel)window.DataContext!).CanSave);
        });
        await Assert.That(result.tabMoved).IsTrue();
        await Assert.That(result.errorFocused).IsTrue();
        await Assert.That(result.prepared).IsTrue();
        await Assert.That(result.discarded).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task DisplayModesPreserveProtectedConsequentialAndInvalidFields()
    {
        var result = await InWindow(window =>
        {
            var shell = (SetupDesktopShellViewModel)window.DataContext!;
            var fields = window.GetLogicalDescendants().OfType<FieldEditor>().ToArray();
            FieldEditor advanced = fields.Single(item => item.Title == "Advanced note");
            FieldEditor protectedField = fields.Single(item => item.Title == "Owner");
            FieldEditor consequence = fields.Single(item => item.Title == "Mode");
            bool guidedHidden = !advanced.IsVisible;
            shell.DisplayMode = DesktopDisplayMode.Essential;
            bool essentialSafe = !advanced.IsVisible && protectedField.IsVisible && consequence.IsVisible;
            shell.DisplayMode = DesktopDisplayMode.Advanced;
            bool shown = advanced.IsVisible;
            Input<TextBox>(window, "Advanced note").Text = new string('a', 9);
            Dispatcher.UIThread.RunJobs();
            shell.DisplayMode = DesktopDisplayMode.Essential;
            return (guidedHidden, essentialSafe, shown, invalidShown: advanced.IsVisible, ready: shell.Validate());
        });
        await Assert.That(result.guidedHidden).IsTrue();
        await Assert.That(result.essentialSafe).IsTrue();
        await Assert.That(result.shown).IsTrue();
        await Assert.That(result.invalidShown).IsTrue();
        await Assert.That(result.ready).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task InvalidStructuredNumericInputCannotBeSilentlyOmitted()
    {
        var result = await InWindow(window =>
        {
            var shell = (SetupDesktopShellViewModel)window.DataContext!;
            Input<TextBox>(window, "Owner age").Text = "not-an-integer";
            Dispatcher.UIThread.RunJobs();
            Click(window.GetControl<Button>("Prepare"));
            Dispatcher.UIThread.RunJobs();
            return (shell.CanSave, shell.Status, shell.Form!.HasInputErrors);
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
            return Task.FromResult(ProtectedArtifactStatus.Written);
        });
        try
        {
            await Assert.That(shell.LoadDefinition(Definition())).IsTrue();
            shell.ArtifactId = artifactId;
            await Assert.That(await shell.PrepareAsync()).IsTrue();
            await Assert.That(shell.Preparation!.ArtifactId).IsEqualTo(artifactId);
            await Assert.That(shell.Preparation.DefinitionDigest).IsEqualTo(shell.Form!.Definition.Digest);
            await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.Written);
            string output = Encoding.UTF8.GetString(written);
            await Assert.That(output).Contains("Example service");
            if (artifactId == "config")
            {
                using var parsed = JsonDocument.Parse(written);
                await Assert.That(parsed.RootElement.GetProperty("name").GetString()).IsEqualTo("Example service");
            }
            await Assert.That(shell.CanSave).IsFalse();
            await Assert.That(shell.Preparation).IsNull();
            await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        }
        finally { CryptographicOperations.ZeroMemory(written); }
    }

    [Test]
    public async Task InputsTargetsAndDefinitionReplacementInvalidatePreparations()
    {
        using var shell = Shell();
        shell.LoadDefinition(Definition());
        await shell.PrepareAsync();
        shell.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Updated";
        await Assert.That(shell.CanSave).IsFalse();
        await shell.PrepareAsync();
        shell.FileName = "other.json";
        await Assert.That(shell.CanSave).IsFalse();
        await shell.PrepareAsync();
        shell.OutputDirectory = Path.Combine(Path.GetTempPath(), "different");
        await Assert.That(shell.CanSave).IsFalse();
        await shell.PrepareAsync();
        shell.ArtifactId = "notes";
        await Assert.That(shell.CanSave).IsFalse();
        await shell.PrepareAsync();
        string pin = shell.DefinitionPin;
        shell.LoadDefinition(Definition(revision: 2));
        await Assert.That(shell.CanSave).IsFalse();
        await Assert.That(shell.DefinitionPin == pin).IsFalse();
        await Assert.That(shell.Form!.Definition.Revision).IsEqualTo(2L);
        await Assert.That(shell.Form.Fields.Single(item => item.Definition.Id == "name").Text).IsEqualTo("Example service");
        await shell.PrepareAsync();
        await Assert.That(shell.LoadDefinition(Encoding.UTF8.GetBytes("{}"))).IsFalse();
        await Assert.That(shell.Form).IsNull();
        await Assert.That(shell.CanSave).IsFalse();
    }

    [Test]
    public async Task FailedProtectedSaveConsumesOutputAndZeroesNativeBuffer()
    {
        ReadOnlyMemory<byte> observed = default;
        using var shell = Shell((_, bytes, _) =>
        {
            observed = bytes;
            return Task.FromResult(ProtectedArtifactStatus.UnsafeTarget);
        });
        shell.LoadDefinition(Definition());
        await shell.PrepareAsync();
        await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
        await Assert.That(shell.CanSave).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("save-failed:UnsafeTarget");
        await Assert.That(observed.Span.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
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
            return ProtectedArtifactStatus.Written;
        });
        shell.LoadDefinition(Definition());
        await shell.PrepareAsync();
        Task<ProtectedArtifactStatus> pending = shell.SaveAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(shell.CanSave).IsFalse();
        shell.LoadDefinition(Definition(revision: 2));
        await Assert.That(observed.IsCancellationRequested).IsTrue();
        finish.SetResult();
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        await Assert.That(shell.Status).IsEqualTo("definition-loaded");
        await Assert.That(shell.CanSave).IsFalse();
        await Assert.That(shell.Form!.Definition.Revision).IsEqualTo(2L);
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
            return ProtectedArtifactStatus.Written;
        });
        shell.LoadDefinition(Definition());
        await shell.PrepareAsync();
        var pending = shell.SaveAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        shell.Cancel();
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(5))).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        await Assert.That(shell.Status).IsEqualTo("operation-cancelled");
        await Assert.That(shell.CanSave).IsFalse();
        await Assert.That(shell.IsSaving).IsFalse();
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
            return Task.FromResult(ProtectedArtifactStatus.Written);
        });
        shell.LoadDefinition(Definition());
        shell.FileName = fileName;
        await shell.PrepareAsync();
        await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
        await Assert.That(reached).IsFalse();
        await Assert.That(shell.CanSave).IsFalse();
    }

    [Test]
    public async Task UnsupportedHostHasNoPrivateSaveFallback()
    {
        bool reached = false;
        using var shell = new SetupDesktopShellViewModel((_, _, _) =>
        {
            reached = true;
            return Task.FromResult(ProtectedArtifactStatus.Written);
        }, Path.GetTempPath(), protectedOutputAvailable: false);
        shell.LoadDefinition(Definition());
        await shell.PrepareAsync();
        await Assert.That(shell.CanSave).IsFalse();
        await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.UnsupportedPlatform);
        await Assert.That(reached).IsFalse();
        using var native = new SetupDesktopShellViewModel();
        await Assert.That(native.ProtectedSaveAvailable).IsEqualTo(OperatingSystem.IsLinux());
    }

    [Test]
    public async Task ExplicitLocalPathPinsAdmittedBytesUntilUserReloads()
    {
        string path = Path.Combine(Path.GetTempPath(), $"configurator-definition-{Guid.CreateVersion7():N}.json");
        using var shell = Shell();
        try
        {
            await File.WriteAllBytesAsync(path, Definition());
            shell.DefinitionPath = path;
            await Assert.That(await shell.LoadDefinitionAsync()).IsTrue();
            var pin = shell.Form!.Definition.Digest;
            await File.WriteAllBytesAsync(path, Definition(revision: 2));
            await shell.PrepareAsync();
            await Assert.That(shell.Preparation!.DefinitionDigest).IsEqualTo(pin);
            await Assert.That(await shell.LoadDefinitionAsync()).IsTrue();
            await Assert.That(shell.Form!.Definition.Revision).IsEqualTo(2L);
            await Assert.That(shell.CanSave).IsFalse();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task NoDefinitionMeansNoDefaultTargetOrArtifact()
    {
        using var shell = Shell();
        await Assert.That(shell.Form).IsNull();
        await Assert.That(shell.Artifacts.Count).IsEqualTo(0);
        await Assert.That(shell.CanPrepare).IsFalse();
        await Assert.That(await shell.PrepareAsync()).IsFalse();
        await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
    }

    [Test]
    public async Task PreparationMetadataAndDiagnosticsNeverContainProtectedAnswersOrBytes()
    {
        string marker = "Private-" + Guid.CreateVersion7().ToString("N");
        using var shell = Shell();
        shell.LoadDefinition(Definition());
        shell.Form!.Fields.Single(item => item.Definition.Id == "owner").Text =
            JsonSerializer.Serialize(new { name = marker });
        await shell.PrepareAsync();
        await Assert.That(shell.Preparation).IsNotNull();
        await Assert.That(JsonSerializer.Serialize(shell.Preparation)).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.Status).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.ToString()).DoesNotContain(marker, StringComparison.Ordinal);
        shell.Form.Fields.Single(item => item.Definition.Id == "owner").Text = marker;
        shell.Validate();
        await Assert.That(shell.Status).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.CanSave).IsFalse();
    }

    [Test]
    public async Task PendingExistingFileImportCannotReplaceLaterEdit()
    {
        var pending = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = Shell();
        shell.LoadDefinition(Definition());
        Task<bool> import = shell.ImportAsync(_ => pending.Task);
        shell.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Later edit";
        pending.SetResult(Encoding.UTF8.GetBytes("{\"name\":\"Example service\"}"));
        await Assert.That(await import.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(shell.Form.Fields.Single(item => item.Definition.Id == "name").Text).IsEqualTo("Later edit");
        await Assert.That(shell.Status).DoesNotContain("Example service", StringComparison.Ordinal);
    }

    [Test]
    public async Task ChangingExistingFilePathDiscardsPendingImportWithoutChangingAnswers()
    {
        var pending = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = Shell();
        shell.LoadDefinition(Definition());
        shell.ExistingFilePath = "first.json";
        Task<bool> import = shell.ImportAsync(_ => pending.Task);
        shell.ExistingFilePath = "second.json";
        pending.SetResult(Encoding.UTF8.GetBytes("{\"owner\":{\"name\":\"Synthetic private\"}}"));
        await Assert.That(await import.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(shell.Form!.Fields.Single(item => item.Definition.Id == "owner").Text).IsEmpty();
    }

    [Test]
    public async Task SourceChangeAfterImportCommitDoesNotReportStaleReadyStatus()
    {
        using var shell = Shell();
        shell.LoadDefinition(Definition());
        shell.Form!.InputChanged += (_, _) => shell.ExistingFilePath = "next.json";
        bool committed = await shell.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(
            Encoding.UTF8.GetBytes("{\"owner\":{\"name\":\"Synthetic private\"}}")));
        await Assert.That(committed).IsTrue();
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(shell.Form.Fields.Single(item => item.Definition.Id == "owner").Text)
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
        shell.LoadDefinition(Definition());
        shell.ExistingFilePath = path;
        await Assert.That(await shell.ImportFileAsync()).IsTrue();
        await Assert.That(shell.Status).IsEqualTo("import-ready");
        await Assert.That(shell.Status).DoesNotContain(marker, StringComparison.Ordinal);
        await Assert.That(shell.Form!.Fields.Single(item => item.Definition.Id == "owner").Text).Contains(marker);
    }

    [Test]
    public async Task DeclaredBundleSelectionAndCapabilityHaveValueFreeStatus()
    {
        using var shell = Shell();
        shell.LoadDefinition(Definition());
        shell.SelectArtifact("environment", true);
        await Assert.That(shell.PrepareBundle()).IsTrue();
        await Assert.That(shell.SelectedArtifactIds).Contains("environment");
        await Assert.That(shell.CanSaveBundle).IsTrue();
        await Assert.That(shell.Status).DoesNotContain("Example service", StringComparison.Ordinal);
        shell.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Later edit";
        await Assert.That(shell.CanSaveBundle).IsFalse();
        using var blocked = new SetupDesktopShellViewModel((_, _, _) =>
            Task.FromResult(ProtectedArtifactStatus.Written), Path.GetTempPath(), protectedOutputAvailable: false);
        blocked.LoadDefinition(Definition());
        await Assert.That(blocked.ProtectedSaveAvailable).IsFalse();
        await Assert.That(blocked.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task ExistingBundleDestinationBlocksAllFilesAndRecoveryCreatesSelectedOutputs()
    {
        using var directory = new TemporaryDirectory();
        string existing = Path.Combine(directory.Path, "service.env");
        await File.WriteAllTextAsync(existing, "EXISTING=unchanged\n");
        using var shell = new SetupDesktopShellViewModel();
        shell.LoadDefinition(Definition());
        shell.OutputDirectory = directory.Path;
        shell.SelectArtifact("environment", true);
        await Assert.That(shell.PrepareBundle()).IsTrue();
        var blocked = await shell.SaveBundleAsync();
        await Assert.That(blocked!.Succeeded).IsFalse();
        await Assert.That(shell.Status).StartsWith("bundle-save-blocked:");
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.json"))).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(existing)).IsEqualTo("EXISTING=unchanged\n");
        await Assert.That(shell.Status).DoesNotContain(directory.Path, StringComparison.Ordinal);
        File.Delete(existing);
        await Assert.That(shell.PrepareBundle()).IsTrue();
        var saved = await shell.SaveBundleAsync();
        await Assert.That(saved!.Succeeded).IsTrue();
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.json"))).IsTrue();
        await Assert.That(File.Exists(existing)).IsTrue();
        await Assert.That(shell.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task EditingDuringBundlePublicationCancelsAndFencesItsResult()
    {
        using var directory = new TemporaryDirectory();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = new SetupDesktopShellViewModel((_, _, _) =>
            Task.FromResult(ProtectedArtifactStatus.Written), directory.Path, protectedOutputAvailable: true,
            async (artifacts, output, cancellation) =>
            {
                started.SetResult();
                await finish.Task;
                return await ProtectedArtifactBundlePublisher.PublishAsync(new ProtectedArtifactWriter(),
                    artifacts, output, cancellation);
            });
        shell.LoadDefinition(Definition());
        shell.SelectArtifact("environment", true);
        await Assert.That(shell.PrepareBundle()).IsTrue();
        Task<ProtectedArtifactBundleResult?> pending = shell.SaveBundleAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        shell.Form!.Fields.Single(item => item.Definition.Id == "name").Text = "Later edit";
        finish.SetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result!.Succeeded).IsFalse();
        await Assert.That(shell.Status).IsEqualTo("inputs-changed");
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.json"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(directory.Path, "service.env"))).IsFalse();
        await Assert.That(shell.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task PublisherIoFailureClearsBusyStateWithoutShowingPrivatePath()
    {
        string privatePath = "/synthetic/private/output";
        using var shell = new SetupDesktopShellViewModel((_, _, _) =>
            Task.FromResult(ProtectedArtifactStatus.Written), Path.GetTempPath(), protectedOutputAvailable: true,
            (_, _, _) => throw new IOException(privatePath));
        shell.LoadDefinition(Definition());
        await Assert.That(shell.PrepareBundle()).IsTrue();
        await Assert.That(await shell.SaveBundleAsync()).IsNull();
        await Assert.That(shell.Status).IsEqualTo("bundle-save-blocked:IoFailure");
        await Assert.That(shell.Status).DoesNotContain(privatePath, StringComparison.Ordinal);
        await Assert.That(shell.IsSaving).IsFalse();
        await Assert.That(shell.CanSaveBundle).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task GuidedControlsExposeImportBundleAndProtectedCapability()
    {
        var result = await InWindow(window =>
        {
            var shell = (SetupDesktopShellViewModel)window.DataContext!;
            return (import: window.GetControl<Button>("Import").IsVisible,
                bundle: window.GetControl<Button>("PrepareBundle").IsVisible,
                capability: window.GetControl<TextBlock>("ProtectedCapability").Text,
                selections: window.GetControl<WrapPanel>("ArtifactSelections").Children.Count,
                saveEnabled: window.GetControl<Button>("SaveBundle").IsEnabled,
                available: shell.ProtectedSaveAvailable);
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
            var shell = (SetupDesktopShellViewModel)window.DataContext!;
            bool imported = shell.ImportAsync(_ => Task.FromResult<ReadOnlyMemory<byte>>(content))
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

    private static SetupDesktopShellViewModel Shell(
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task<ProtectedArtifactStatus>>? save = null) =>
        new(save ?? ((_, _, _) => Task.FromResult(ProtectedArtifactStatus.Written)),
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
            SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
            var editor = new FieldEditor(definition.Fields.Single().Constraints, false, null);
            window.Content = editor;
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
            using JsonDocument before = JsonDocument.Parse(editor.AnswerText);
            bool typed = before.RootElement[0].GetProperty("name").GetString() == "first" &&
                before.RootElement[0].GetProperty("settings").ValueKind == JsonValueKind.Object &&
                before.RootElement[0].GetProperty("aliases")[0].GetString() == "alias" &&
                before.RootElement[0].GetProperty("localizations")[0].GetProperty("locale").GetString() == "en";
            Click(editor.GetLogicalDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Remove item from Aliases"));
            Dispatcher.UIThread.RunJobs();
            using JsonDocument nestedRemoval = JsonDocument.Parse(editor.AnswerText);
            typed &= nestedRemoval.RootElement[0].GetProperty("aliases").GetArrayLength() == 0 &&
                nestedRemoval.RootElement[0].GetProperty("name").GetString() == "first";
            bool masked = password.PasswordChar != '\0';
            Click(editor.GetLogicalDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Remove item from Rows"));
            Dispatcher.UIThread.RunJobs();
            return (typed, masked, answer: editor.AnswerText);
        });
        await Assert.That(result.typed).IsTrue();
        await Assert.That(result.masked).IsTrue();
        await Assert.That(result.answer).IsEqualTo("[]");
    }

    private static T Input<T>(MainWindow window, string title) where T : Control =>
        window.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetName(control) == title);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task<T> InWindow<T>(Func<MainWindow, T> action)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopApplicationTests));
        try
        {
            return await session.Dispatch(() =>
            {
                using var shell = Shell();
                shell.LoadDefinition(Definition());
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
}
