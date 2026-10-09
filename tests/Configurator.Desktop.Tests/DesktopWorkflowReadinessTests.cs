namespace Configurator.Desktop.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Desktop.ViewModels;

public sealed class DesktopWorkflowReadinessTests
{
    [Test]
    public async Task InactiveBundleSelectionCannotEnableSaveAndCorrectionPreparesCurrentOutput()
    {
        JsonObject source = JsonNode.Parse(DesktopApplicationTests.Definition())!.AsObject();
        source["artifacts"]![0]!["projection"]!["when"] = JsonNode.Parse("""
            {"op":"equals","field":"mode","value":"remote"}
            """);
        using var shell = new SetupDesktopShellViewModel();
        shell.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await Assert.That(shell.PrepareBundle()).IsFalse();
        await Assert.That(shell.CanSaveBundle).IsFalse();
        shell.Form!.Fields.Single(input => input.Definition.Id == "mode").Text = "remote";
        await Assert.That(shell.PrepareBundle()).IsTrue();
        await Assert.That(shell.CanSaveBundle).IsEqualTo(shell.ProtectedSaveAvailable);
        shell.SelectArtifact("config", false);
        await Assert.That(shell.PrepareBundle()).IsFalse();
        await Assert.That(shell.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task ConflictingBundleFileNamesCannotEnableSave()
    {
        JsonObject source = JsonNode.Parse(DesktopApplicationTests.Definition())!.AsObject();
        source["artifacts"]![1]!["fileName"] = "SERVICE.JSON";
        using var shell = new SetupDesktopShellViewModel();
        await Assert.That(shell.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()))).IsFalse();
        await Assert.That(shell.PrepareBundle()).IsFalse();
        await Assert.That(shell.CanSaveBundle).IsFalse();
        shell.LoadDefinition(DesktopApplicationTests.Definition());
        await Assert.That(shell.PrepareBundle()).IsTrue();
    }

    [Test]
    public async Task DisabledProtectedCapabilityCannotPublishBundleEvenThroughDirectSave()
    {
        using var directory = new TemporaryDirectory();
        using var shell = new SetupDesktopShellViewModel(
            (_, _, _) => Task.FromResult(ProtectedArtifactStatus.UnsupportedPlatform),
            directory.Path, protectedOutputAvailable: false);
        shell.LoadDefinition(DesktopApplicationTests.Definition());
        await Assert.That(shell.PrepareBundle()).IsTrue();
        await shell.SaveBundleAsync();
        await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
        await Assert.That(shell.CanSaveBundle).IsFalse();
        await Assert.That(shell.IsSaving).IsFalse();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("definition")]
    [Arguments("destination")]
    [Arguments("cancel")]
    [Arguments("dispose")]
    public async Task InvalidationBeforeRealNativeCommitCannotPublishOrRestoreStatus(string change)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ReadOnlyMemory<byte> heldBytes = default;
        using var shell = new SetupDesktopShellViewModel(async (path, bytes, cancellation) =>
        {
            heldBytes = bytes;
            using var output = await new ProtectedArtifactWriter().PrepareAsync(
                SetupArtifactKind.Unknown, path, bytes, cancellation);
            prepared.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return await output.CommitAsync(cancellation);
        }, directory.Path, protectedOutputAvailable: true);
        shell.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Form!.Fields.Single(input => input.Definition.Id == "name").Text = "synthetic-private-answer";
        await shell.PrepareAsync();
        Task<ProtectedArtifactStatus> save = shell.SaveAsync();
        await prepared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (change)
        {
            case "edit": shell.Form!.Fields[0].Text = "corrected"; break;
            case "definition": shell.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
            case "destination": shell.OutputDirectory = directory.Path + "-synthetic-private-path"; break;
            case "cancel": shell.Cancel(); break;
            case "dispose": shell.Dispose(); break;
        }
        string currentStatus = shell.Status;
        release.SetResult();
        await save.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
        await Assert.That(shell.Status).IsEqualTo(currentStatus);
        await Assert.That(shell.Status.Contains("synthetic-private", StringComparison.Ordinal)).IsFalse();
        await Assert.That(shell.CanSave).IsFalse();
        await Assert.That(shell.IsSaving).IsFalse();
        await Assert.That(heldBytes.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("definition")]
    [Arguments("destination")]
    [Arguments("cancel")]
    [Arguments("dispose")]
    public async Task InvalidatedBundleCannotEnterNativePublicationOrRestoreStatus(string change)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = new SetupDesktopShellViewModel(
            (_, _, _) => Task.FromResult(ProtectedArtifactStatus.InvalidRequest),
            directory.Path, protectedOutputAvailable: true, async (artifacts, destination, cancellation) =>
            {
                started.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                return await ProtectedArtifactBundlePublisher.PublishAsync(
                    new ProtectedArtifactWriter(), artifacts, destination, cancellation);
            });
        shell.LoadDefinition(DesktopApplicationTests.Definition());
        shell.SelectArtifact("environment", true);
        shell.PrepareBundle();
        Task<ProtectedArtifactBundleResult?> save = shell.SaveBundleAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (change)
        {
            case "edit": shell.Form!.Fields[0].Text = "corrected"; break;
            case "definition": shell.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
            case "destination": shell.OutputDirectory = directory.Path + "-synthetic-private-path"; break;
            case "cancel": shell.Cancel(); break;
            case "dispose": shell.Dispose(); break;
        }
        string status = shell.Status;
        release.SetResult();
        await save.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
        await Assert.That(shell.Status).IsEqualTo(status);
        await Assert.That(shell.CanSaveBundle).IsFalse();
        await Assert.That(shell.IsSaving).IsFalse();
    }

    [Test]
    public async Task LateConflictPreservesExistingBytesAndRequiresFreshPreparation()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        using var shell = new SetupDesktopShellViewModel(async (path, bytes, cancellation) =>
        {
            using var output = await new ProtectedArtifactWriter().PrepareAsync(
                SetupArtifactKind.Unknown, path, bytes, cancellation);
            await File.WriteAllTextAsync(path, "existing-operator-data", cancellation);
            return await output.CommitAsync(cancellation);
        }, directory.Path, protectedOutputAvailable: true);
        shell.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.PrepareAsync();
        await Assert.That(await shell.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.TargetChanged);
        await Assert.That(await File.ReadAllTextAsync(System.IO.Path.Combine(directory.Path, "service.json")))
            .IsEqualTo("existing-operator-data");
        await Assert.That(shell.CanSave).IsFalse();
        await Assert.That(shell.PrepareBundle()).IsTrue();
        await Assert.That((await shell.SaveBundleAsync())!.Succeeded).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(System.IO.Path.Combine(directory.Path, "service.json")))
            .IsEqualTo("existing-operator-data");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("configurator-desktop-readiness-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
