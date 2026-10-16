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
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        await Assert.That(await DefinitionSourceFiles.CreateAsync(destination, "original"u8.ToArray(), true, work.Token)).IsTrue();
        using (DefinitionSourceFiles.PreparedArtifact identical = DefinitionSourceFiles.PrepareCreate(destination, "original"u8.ToArray(), true))
        {
            await identical.StageAsync(work.Token);
            await Assert.That(Directory.GetFiles(files.Path("releases")).Length).IsEqualTo(1);
            await Assert.That(await identical.CommitAsync(work.Token)).IsFalse();
        }
        await Assert.That(await DefinitionSourceFiles.CreateAsync(destination, "original"u8.ToArray(), true, work.Token)).IsFalse();
        await Assert.That(() => DefinitionSourceFiles.CreateAsync(destination, "replacement"u8.ToArray(), true, work.Token).AsTask())
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(destination)).IsEqualTo("original");
    }

    [Test]
    public async Task StagingDoesNotPublishAndLateConflictDoesNotOverwrite()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        using DefinitionSourceFiles.PreparedArtifact prepared = DefinitionSourceFiles.PrepareCreate(destination, "candidate"u8.ToArray(), true);
        await prepared.StageAsync(work.Token);
        await Assert.That(File.Exists(destination)).IsFalse();
        File.WriteAllText(destination, "editor-owned");
        await Assert.That(() => prepared.CommitAsync(work.Token).AsTask()).Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(destination)).IsEqualTo("editor-owned");
    }

    [Test]
    public async Task AbandonedStagingRemovesTemporaryFilesWithoutPublishing()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        using (DefinitionSourceFiles.PreparedArtifact prepared = DefinitionSourceFiles.PrepareCreate(destination, "candidate"u8.ToArray(), true))
        {
            await prepared.StageAsync(work.Token);
            await Assert.That(File.Exists(destination)).IsFalse();
        }
        await Assert.That(Directory.GetFiles(files.Path("releases"))).IsEmpty();
    }

    [Test]
    public async Task MutationRejectsChangedOriginalAndExistingLockWithoutOverwriting()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string source = files.Path("configurator.yaml");
        File.WriteAllText(source, "editor change");
        await Assert.That(() => DefinitionSourceFiles.ReplaceAsync(source, "original"u8.ToArray(), "replacement"u8.ToArray(), work.Token).AsTask())
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(source)).IsEqualTo("editor change");
        await Assert.That(File.Exists(source + ".configurator.lock")).IsFalse();
        File.WriteAllText(source + ".configurator.lock", "");
        await Assert.That(() => DefinitionSourceFiles.ReplaceAsync(source, "editor change"u8.ToArray(), "replacement"u8.ToArray(), work.Token).AsTask())
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(source)).IsEqualTo("editor change");
    }

    [Test]
    public async Task SourceAndParentSymlinksReject()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string target = files.Path("target.yaml");
        File.WriteAllText(target, "original");
        File.CreateSymbolicLink(files.Path("configurator.yaml"), target);
        await Assert.That(async () => { await DefinitionSourceFiles.ReadAsync(files.Path("configurator.yaml"), work.Token); }).Throws<DefinitionFileException>();
        Directory.CreateDirectory(files.Path("real"));
        Directory.CreateSymbolicLink(files.Path("linked"), files.Path("real"));
        await Assert.That(() => DefinitionSourceFiles.CreateAsync(files.Path("linked/new.yaml"), Encoding.UTF8.GetBytes("new"), false, work.Token).AsTask())
            .Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(target)).IsEqualTo("original");
    }

    [Test]
    public async Task CancelledWorkDoesNotReadOrStageAndStagedMutationIsCleanedWithoutCommit()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string source = files.Path("configurator.yaml");
        File.WriteAllText(source, "original");
        using (DefinitionSourceFiles.PreparedArtifact prepared = DefinitionSourceFiles.PrepareReplace(source, "original"u8.ToArray(), "replacement"u8.ToArray()))
        {
            await prepared.StageAsync(work.Token);
            await work.CancelAsync();
            await Assert.That(async () => { await DefinitionSourceFiles.ReadAsync(files.Path("missing.yaml"), work.Token); })
                .Throws<OperationCanceledException>();
            await Assert.That(() => DefinitionSourceFiles.CreateAsync(files.Path("new/source.yaml"), "new"u8.ToArray(), false, work.Token).AsTask())
                .Throws<OperationCanceledException>();
            await Assert.That(() => prepared.CommitAsync(work.Token).AsTask()).Throws<OperationCanceledException>();
        }
        await Assert.That(File.ReadAllText(source)).IsEqualTo("original");
        await Assert.That(Directory.Exists(files.Path("new"))).IsFalse();
        await Assert.That(Directory.GetFiles(files.Root)).IsEquivalentTo(new[] { source });
    }

    [Test]
    public async Task CancellationAfterFirstCommitRetainsPrefixAndCleansUncommittedStaging()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string first = files.Path("definition.json");
        string second = files.Path("id-map.json");
        using (DefinitionSourceFiles.PreparedArtifact canonical = DefinitionSourceFiles.PrepareCreate(first, "canonical"u8.ToArray(), false))
        using (DefinitionSourceFiles.PreparedArtifact mapping = DefinitionSourceFiles.PrepareCreate(second, "mapping"u8.ToArray(), false))
        {
            await canonical.StageAsync(work.Token);
            await mapping.StageAsync(work.Token);
            await Assert.That(await canonical.CommitAsync(work.Token)).IsTrue();
            await work.CancelAsync();
            await Assert.That(() => mapping.CommitAsync(work.Token).AsTask()).Throws<OperationCanceledException>();
        }
        await Assert.That(File.ReadAllText(first)).IsEqualTo("canonical");
        await Assert.That(File.Exists(second)).IsFalse();
        await Assert.That(Directory.GetFiles(files.Root)).IsEquivalentTo(new[] { first });
    }
}

internal sealed class DefinitionTestDirectory : IDisposable
{
    internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "configurator-definition-" + Guid.NewGuid().ToString("N"));
    internal DefinitionTestDirectory() => Directory.CreateDirectory(Root);
    internal string Path(string relative) => System.IO.Path.Combine(Root, relative);
    public void Dispose() => Directory.Delete(Root, true);
}
