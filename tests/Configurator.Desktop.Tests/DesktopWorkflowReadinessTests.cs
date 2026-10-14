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
        using var shell = DesktopTestContext.Root();
        shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await Assert.That(shell.Publisher.PrepareBundle()).IsFalse();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        shell.Workspace.Form!.Fields.Single(input => input.Definition.Id == "mode").Text = "remote";
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        await Assert.That(shell.Publisher.CanSaveBundle).IsEqualTo(shell.Publisher.ProtectedSaveAvailable);
        shell.Publisher.SelectArtifact("config", false);
        await Assert.That(shell.Publisher.PrepareBundle()).IsFalse();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
    }

    [Test]
    public async Task ConflictingBundleFileNamesCannotEnableSave()
    {
        JsonObject source = JsonNode.Parse(DesktopApplicationTests.Definition())!.AsObject();
        source["artifacts"]![1]!["fileName"] = "SERVICE.JSON";
        using var shell = DesktopTestContext.Root();
        await Assert.That(shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(source.ToJsonString()))).IsFalse();
        await Assert.That(shell.Publisher.PrepareBundle()).IsFalse();
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
    }

    [Test]
    public async Task DisabledProtectedCapabilityCannotPublishBundleEvenThroughDirectSave()
    {
        using var directory = new TemporaryDirectory();
        using var shell = DesktopTestContext.Root(
            (_, _, _) => Task.FromResult(ProtectedArtifactStatus.UnsupportedPlatform),
            directory.Path, protectedOutputAvailable: false);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        await shell.Publisher.SaveBundleAsync();
        await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        await Assert.That(shell.Publisher.IsSaving).IsFalse();
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
        using var shell = DesktopTestContext.Root(async (path, bytes, cancellation) =>
        {
            heldBytes = bytes;
            using var output = await new ProtectedArtifactWriter().PrepareAsync(
                SetupArtifactKind.Unknown, path, bytes, cancellation);
            prepared.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return await output.CommitAsync(cancellation);
        }, directory.Path, protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Workspace.Form!.Fields.Single(input => input.Definition.Id == "name").Text = "synthetic-private-answer";
        await shell.Publisher.PrepareAsync();
        Task<ProtectedArtifactStatus> save = shell.Publisher.SaveAsync();
        await prepared.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (change)
        {
            case "edit": shell.Workspace.Form!.Fields[0].Text = "corrected"; break;
            case "definition": shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
            case "destination": shell.Publisher.OutputDirectory = directory.Path + "-synthetic-private-path"; break;
            case "cancel": shell.Cancel(); break;
            case "dispose": shell.Dispose(); break;
        }
        string currentStatus = shell.Status;
        release.SetResult();
        await save.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
        await Assert.That(shell.Status).IsEqualTo(currentStatus);
        await Assert.That(shell.Status.Contains("synthetic-private", StringComparison.Ordinal)).IsFalse();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Publisher.IsSaving).IsFalse();
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
        using var shell = DesktopTestContext.Root(
            (_, _, _) => Task.FromResult(ProtectedArtifactStatus.InvalidRequest),
            directory.Path, protectedOutputAvailable: true, async (artifacts, destination, cancellation) =>
            {
                started.SetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                return await ProtectedArtifactBundlePublisher.PublishAsync(
                    new ProtectedArtifactWriter(), artifacts, destination, cancellationToken: cancellation);
            });
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        shell.Publisher.SelectArtifact("environment", true);
        shell.Publisher.PrepareBundle();
        Task<ProtectedArtifactBundleResult?> save = shell.Publisher.SaveBundleAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (change)
        {
            case "edit": shell.Workspace.Form!.Fields[0].Text = "corrected"; break;
            case "definition": shell.Loader.LoadDefinition(DesktopApplicationTests.Definition(2)); break;
            case "destination": shell.Publisher.OutputDirectory = directory.Path + "-synthetic-private-path"; break;
            case "cancel": shell.Cancel(); break;
            case "dispose": shell.Dispose(); break;
        }
        string status = shell.Status;
        release.SetResult();
        await save.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(Directory.GetFiles(directory.Path).Length).IsEqualTo(0);
        await Assert.That(shell.Status).IsEqualTo(status);
        await Assert.That(shell.Publisher.CanSaveBundle).IsFalse();
        await Assert.That(shell.Publisher.IsSaving).IsFalse();
    }

    [Test]
    public async Task LateConflictPreservesExistingBytesAndRequiresFreshPreparation()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TemporaryDirectory();
        using var shell = DesktopTestContext.Root(async (path, bytes, cancellation) =>
        {
            using var output = await new ProtectedArtifactWriter().PrepareAsync(
                SetupArtifactKind.Unknown, path, bytes, cancellation);
            await File.WriteAllTextAsync(path, "existing-operator-data", cancellation);
            return await output.CommitAsync(cancellation);
        }, directory.Path, protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.Publisher.PrepareAsync();
        await Assert.That(await shell.Publisher.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.TargetChanged);
        await Assert.That(await File.ReadAllTextAsync(System.IO.Path.Combine(directory.Path, "service.json")))
            .IsEqualTo("existing-operator-data");
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Publisher.PrepareBundle()).IsTrue();
        await Assert.That((await shell.Publisher.SaveBundleAsync())!.Succeeded).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(System.IO.Path.Combine(directory.Path, "service.json")))
            .IsEqualTo("existing-operator-data");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("configurator-desktop-readiness-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
