using System.Text;

namespace Configurator.Cli.Tests;

public sealed class DefinitionSourceFileTests
{
    [Test]
    public async Task DiscoveryRejectsAmbiguityAndExplicitSelectionWins()
    {
        using var files = new DefinitionTestDirectory();
        File.WriteAllText(files.Path("configurator.yaml"), "first");
        File.WriteAllText(files.Path("configurator.json"), "second");
        await Assert.That(() => DefinitionSourceFiles.Discover(files.Root, null)).Throws<DefinitionFileException>();
        await Assert.That(DefinitionSourceFiles.Discover(files.Root, files.Path("configurator.json")))
            .IsEqualTo(files.Path("configurator.json"));
    }

    [Test]
    public async Task ImmutableOutputAcceptsIdenticalBytesAndRejectsConflicts()
    {
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        await Assert.That(DefinitionSourceFiles.Create(destination, "original"u8.ToArray(), true)).IsTrue();
        using (DefinitionSourceFiles.PreparedArtifact identical = DefinitionSourceFiles.PrepareCreate(destination, "original"u8.ToArray(), true))
        {
            identical.Stage();
            await Assert.That(Directory.GetFiles(files.Path("releases")).Length).IsEqualTo(1);
            await Assert.That(identical.Commit()).IsFalse();
        }
        await Assert.That(DefinitionSourceFiles.Create(destination, "original"u8.ToArray(), true)).IsFalse();
        await Assert.That(() => DefinitionSourceFiles.Create(destination, "replacement"u8.ToArray(), true))
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(destination)).IsEqualTo("original");
    }

    [Test]
    public async Task StagingDoesNotPublishAndLateConflictDoesNotOverwrite()
    {
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        using DefinitionSourceFiles.PreparedArtifact prepared = DefinitionSourceFiles.PrepareCreate(destination, "candidate"u8.ToArray(), true);
        prepared.Stage();
        await Assert.That(File.Exists(destination)).IsFalse();
        File.WriteAllText(destination, "editor-owned");
        await Assert.That(() => prepared.Commit()).Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(destination)).IsEqualTo("editor-owned");
    }

    [Test]
    public async Task AbandonedStagingRemovesTemporaryFilesWithoutPublishing()
    {
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        using (DefinitionSourceFiles.PreparedArtifact prepared = DefinitionSourceFiles.PrepareCreate(destination, "candidate"u8.ToArray(), true))
        {
            prepared.Stage();
            await Assert.That(File.Exists(destination)).IsFalse();
        }
        await Assert.That(Directory.GetFiles(files.Path("releases"))).IsEmpty();
    }

    [Test]
    public async Task MutationRejectsChangedOriginalAndExistingLockWithoutOverwriting()
    {
        using var files = new DefinitionTestDirectory();
        string source = files.Path("configurator.yaml");
        File.WriteAllText(source, "editor change");
        await Assert.That(() => DefinitionSourceFiles.Replace(source, "original"u8.ToArray(), "replacement"u8.ToArray()))
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(source)).IsEqualTo("editor change");
        await Assert.That(File.Exists(source + ".configurator.lock")).IsFalse();
        File.WriteAllText(source + ".configurator.lock", "");
        await Assert.That(() => DefinitionSourceFiles.Replace(source, "editor change"u8.ToArray(), "replacement"u8.ToArray()))
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(source)).IsEqualTo("editor change");
    }

    [Test]
    public async Task SourceAndParentSymlinksReject()
    {
        using var files = new DefinitionTestDirectory();
        string target = files.Path("target.yaml");
        File.WriteAllText(target, "original");
        File.CreateSymbolicLink(files.Path("configurator.yaml"), target);
        await Assert.That(() => DefinitionSourceFiles.Read(files.Path("configurator.yaml"))).Throws<DefinitionFileException>();
        Directory.CreateDirectory(files.Path("real"));
        Directory.CreateSymbolicLink(files.Path("linked"), files.Path("real"));
        await Assert.That(() => DefinitionSourceFiles.Create(files.Path("linked/new.yaml"), Encoding.UTF8.GetBytes("new"), false))
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(target)).IsEqualTo("original");
    }
}

internal sealed class DefinitionTestDirectory : IDisposable
{
    internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "configurator-definition-" + Guid.NewGuid().ToString("N"));
    internal DefinitionTestDirectory() => Directory.CreateDirectory(Root);
    internal string Path(string relative) => System.IO.Path.Combine(Root, relative);
    public void Dispose() => Directory.Delete(Root, true);
}
