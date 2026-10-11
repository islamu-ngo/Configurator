namespace Configurator.Desktop.Tests;

using System.Security.Cryptography;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Desktop.ViewModels;

public sealed class DesktopProtectedWriteInvariantTests
{
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
            await Assert.That(await shell.Publisher.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.Written);
            string target = Path.Combine(directory, shell.Publisher.FileName);
            byte[] original = await File.ReadAllBytesAsync(target);
            await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            shell.Workspace.Form!.Fields.Single(field => field.Definition.Id == "name").Text = "Replacement";
            await shell.Publisher.PrepareAsync();
            await Assert.That(await shell.Publisher.SaveAsync()).IsEqualTo(ProtectedArtifactStatus.TargetExists);
            await Assert.That(await File.ReadAllBytesAsync(target)).IsEquivalentTo(original);
            await Assert.That(shell.Publisher.CanSave).IsFalse();
            await Assert.That(Directory.GetFileSystemEntries(directory)).IsEquivalentTo([target]);
        }
        finally { Directory.Delete(directory, recursive: true); }
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
            await Assert.That(await preparation.CommitAsync()).IsEqualTo(ProtectedArtifactStatus.Written);
            using var overwrite = await writer.PrepareAsync(
                SetupArtifactKind.Unknown, target, RandomNumberGenerator.GetBytes(64));
            await Assert.That(await overwrite.CommitAsync()).IsEqualTo(ProtectedArtifactStatus.TargetExists);
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
