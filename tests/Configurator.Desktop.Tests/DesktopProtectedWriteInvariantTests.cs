namespace Configurator.Desktop.Tests;

using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Desktop.ViewModels;

public sealed class DesktopProtectedWriteInvariantTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task InstalledSaveRetainsVerificationAndCleanupWithoutClaimingCompleteSuccess(
        bool verificationSucceeded, bool cleanupFailed)
    {
        ProtectedArtifactCommitResult established = ProtectedArtifactCommitResult.Installed(verificationSucceeded, cleanupFailed);
        using var shell = DesktopTestContext.Root((_, _, _) => Task.FromResult(established),
            "/synthetic/private-output", protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
        ProtectedArtifactCommitResult result = await shell.Publisher.SaveAsync();
        await Assert.That(result).IsEqualTo(established);
        await Assert.That(result.InstallationEstablished).IsTrue();
        await Assert.That(result.VerificationSucceeded).IsEqualTo(verificationSucceeded);
        await Assert.That(result.CleanupFailed).IsEqualTo(cleanupFailed);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(shell.Status).Contains(verificationSucceeded ? "installed-verified" : "installed-unverified");
        await Assert.That(shell.Status).Contains("current-integrity-not-guaranteed");
        if (cleanupFailed) await Assert.That(shell.Status).Contains("cleanup-failed");
        await Assert.That(shell.Status).DoesNotContain("save-complete");
        await Assert.That(shell.Status).DoesNotContain("/synthetic/private-output");
        await Assert.That(shell.Status).DoesNotContain("Example service");
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Publisher.Preparation).IsNull();
    }

    [Test]
    public async Task DesktopWorkflowCreatesOwnerOnlyArtifactAndRefusesOverwrite()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-native-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var shell = DesktopTestContext.Root(outputDirectory: directory);
            await Assert.That(shell.Loader.LoadDefinition(DesktopApplicationTests.Definition())).IsTrue();
            await Assert.That(await shell.Publisher.PrepareAsync()).IsTrue();
            await Assert.That((await shell.Publisher.SaveAsync()).Succeeded).IsTrue();
            string target = Path.Combine(directory, shell.Publisher.FileName);
            byte[] original = await File.ReadAllBytesAsync(target);
            await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            shell.Workspace.Form!.Fields.Single(field => field.Definition.Id == "name").Text = "Replacement";
            await shell.Publisher.PrepareAsync();
            ProtectedArtifactCommitResult conflict = await shell.Publisher.SaveAsync();
            await Assert.That(conflict.Status).IsEqualTo(ProtectedArtifactStatus.TargetExists);
            await Assert.That(conflict.InstallationEstablished).IsFalse();
            await Assert.That(await File.ReadAllBytesAsync(target)).IsEquivalentTo(original);
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            await Assert.That(Directory.GetFileSystemEntries(directory)).IsEquivalentTo([target]);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationCannotRetractAnInstalledUnverifiedSaveResult(bool cleanupFailed)
    {
        using var cancellation = new CancellationTokenSource();
        using var shell = DesktopTestContext.Root((_, _, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(ProtectedArtifactCommitResult.Installed(false, cleanupFailed));
        }, protectedOutputAvailable: true);
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        await shell.Publisher.PrepareAsync();
        ProtectedArtifactCommitResult result = await shell.Publisher.SaveAsync(cancellation.Token);
        await Assert.That(result.InstallationEstablished).IsTrue();
        await Assert.That(result.VerificationSucceeded).IsFalse();
        await Assert.That(result.CleanupFailed).IsEqualTo(cleanupFailed);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(shell.Publisher.CanSave).IsFalse();
        await Assert.That(shell.Status).DoesNotContain("save-complete");
    }

    [Test]
    public async Task DesktopDependencyUsesSharedCreateOnlyWriter()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-desktop-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "output");
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        try
        {
            var writer = new ProtectedArtifactWriter();
            using var preparation = await writer.PrepareAsync(SetupArtifactKind.Unknown, target, bytes);
            await Assert.That((await preparation.CommitAsync()).Succeeded).IsTrue();
            using var overwrite = await writer.PrepareAsync(
                SetupArtifactKind.Unknown, target, RandomNumberGenerator.GetBytes(64));
            ProtectedArtifactCommitResult conflict = await overwrite.CommitAsync();
            await Assert.That(conflict.Status).IsEqualTo(ProtectedArtifactStatus.TargetExists);
            await Assert.That(conflict.InstallationEstablished).IsFalse();
            await Assert.That(await File.ReadAllBytesAsync(target)).IsEquivalentTo(bytes);
            await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await Assert.That(Directory.GetFileSystemEntries(directory)).IsEquivalentTo([target]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task UnprovedHostsCannotEnableProtectedSave()
    {
        await Assert.That(new ProtectedArtifactWriter().IsAvailable).IsEqualTo(OperatingSystem.IsLinux());
    }
}
