using System.Text;

namespace Configurator.Cli.Tests;

public sealed class DefinitionSourceFileTests
{
    [Test]
    [Arguments(0)]
    [Arguments(4 * 1024 * 1024 + 1)]
    public async Task NativeSourceBoundsKeepClosedDiagnosticWithoutPrivatePath(int length)
    {
        using var files = new DefinitionTestDirectory();
        string path = files.Path("synthetic-private-source.yaml");
        await File.WriteAllBytesAsync(path, new byte[length]);
        var authority = new PublicDefinitionFiles();
        try
        {
            using SetupCliInputBuffer input = await authority.ReadAsync(path, 4 * 1024 * 1024, CancellationToken.None);
            throw new InvalidOperationException("out-of-bound-source-admitted");
        }
        catch (DefinitionFileException exception)
        {
            await Assert.That(exception.Code).IsEqualTo("definition-source-bound");
            await Assert.That(exception.Message).DoesNotContain(path);
        }
    }

    [Test]
    public async Task NativeSourceAtExactBoundRetainsByteOrderAndCallerOwnership()
    {
        using var files = new DefinitionTestDirectory();
        string path = files.Path("configurator.yaml");
        byte[] expected = new byte[4 * 1024 * 1024];
        expected[0] = 7;
        expected[1] = 3;
        expected[^1] = 9;
        await File.WriteAllBytesAsync(path, expected);
        var authority = new PublicDefinitionFiles();
        using SetupCliInputBuffer actual = await authority.ReadAsync(path, 4 * 1024 * 1024, CancellationToken.None);
        ReadOnlyMemory<byte> borrowed = actual.Bytes;
        await Assert.That(borrowed.Span.SequenceEqual(expected)).IsTrue();
        actual.Dispose();
        await Assert.That(borrowed.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That((await File.ReadAllBytesAsync(path))[0]).IsEqualTo((byte)7);
    }

    [Test]
    public async Task DiscoveryRejectsAmbiguityAndExplicitSelectionWins()
    {
        using var files = new DefinitionTestDirectory();
        File.WriteAllText(files.Path("configurator.yaml"), "first");
        File.WriteAllText(files.Path("configurator.json"), "second");
        var authority = new PublicDefinitionFiles();
        await Assert.That(async () =>
        {
            await authority.DiscoverSourceAsync(files.Root, null, CancellationToken.None);
        }).Throws<DefinitionFileException>();
        await Assert.That(await authority.DiscoverSourceAsync(files.Root, files.Path("configurator.json"), CancellationToken.None))
            .IsEqualTo(files.Path("configurator.json"));
    }

    [Test]
    public async Task ImmutableOutputAcceptsIdenticalBytesAndRejectsConflicts()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        var authority = new PublicDefinitionFiles();
        using (ISetupCliPublicFilePreparation first = authority.PrepareCreate(destination, "original"u8.ToArray(), 4 * 1024 * 1024, true))
        {
            await first.StageAsync(work.Token);
            await Assert.That(await first.CommitAsync(work.Token)).IsTrue();
        }
        using (ISetupCliPublicFilePreparation identical = authority.PrepareCreate(destination, "original"u8.ToArray(), 4 * 1024 * 1024, true))
        {
            await identical.StageAsync(work.Token);
            await Assert.That(File.ReadAllText(destination)).IsEqualTo("original");
            await Assert.That(await identical.CommitAsync(work.Token)).IsFalse();
        }
        await Assert.That(Directory.GetFiles(files.Path("releases")).Length).IsEqualTo(1);
        using (ISetupCliPublicFilePreparation conflicting = authority.PrepareCreate(destination, "replacement"u8.ToArray(), 4 * 1024 * 1024, true))
        {
            await conflicting.StageAsync(work.Token);
            await Assert.That(() => conflicting.CommitAsync(work.Token).AsTask()).Throws<DefinitionFileException>();
        }
        await Assert.That(File.ReadAllText(destination)).IsEqualTo("original");
        await Assert.That(Directory.GetFiles(files.Path("releases")).Length).IsEqualTo(1);
    }

    [Test]
    public async Task StagingDoesNotPublishAndLateConflictDoesNotOverwrite()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string destination = files.Path("releases/1.json");
        var authority = new PublicDefinitionFiles();
        using ISetupCliPublicFilePreparation prepared = authority.PrepareCreate(destination, "candidate"u8.ToArray(), 4 * 1024 * 1024, true);
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
        var authority = new PublicDefinitionFiles();
        using (ISetupCliPublicFilePreparation prepared = authority.PrepareCreate(destination, "candidate"u8.ToArray(), 4 * 1024 * 1024, true))
        {
            await prepared.StageAsync(work.Token);
            await Assert.That(File.Exists(destination)).IsFalse();
        }
        await Assert.That(Directory.GetFiles(files.Path("releases"))).IsEmpty();
    }

    [Test]
    public async Task CandidatePublicationPreservesConcurrentSourceChangesWithoutMutationLocks()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string source = files.Path("configurator.yaml");
        string candidate = files.Path("candidate.yaml");
        File.WriteAllText(source, "original");
        var authority = new PublicDefinitionFiles();
        using ISetupCliPublicFilePreparation prepared = authority.PrepareCreate(candidate, "replacement"u8.ToArray(), 4 * 1024 * 1024, false);
        await prepared.StageAsync(work.Token);
        File.WriteAllText(source, "editor change");
        await Assert.That(await prepared.CommitAsync(work.Token)).IsTrue();
        await Assert.That(File.ReadAllText(source)).IsEqualTo("editor change");
        await Assert.That(File.ReadAllText(candidate)).IsEqualTo("replacement");
        await Assert.That(File.Exists(source + ".configurator.lock")).IsFalse();
    }

    [Test]
    public async Task SourceAndParentSymlinksReject()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string target = files.Path("target.yaml");
        File.WriteAllText(target, "original");
        File.CreateSymbolicLink(files.Path("configurator.yaml"), target);
        var authority = new PublicDefinitionFiles();
        await Assert.That(async () =>
        {
            using SetupCliInputBuffer input = await authority.ReadAsync(files.Path("configurator.yaml"), 4 * 1024 * 1024, work.Token);
        }).Throws<DefinitionFileException>();
        Directory.CreateDirectory(files.Path("real"));
        Directory.CreateSymbolicLink(files.Path("linked"), files.Path("real"));
        await Assert.That(() =>
        {
            using ISetupCliPublicFilePreparation prepared = authority.PrepareCreate(files.Path("linked/new.yaml"), Encoding.UTF8.GetBytes("new"), 4 * 1024 * 1024, false);
        }).Throws<DefinitionFileException>();
        await Assert.That(File.ReadAllText(target)).IsEqualTo("original");
    }

    [Test]
    public async Task CancelledWorkDoesNotReadOrStageAndStagedCandidateIsCleanedWithoutCommit()
    {
        using var work = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var files = new DefinitionTestDirectory();
        string source = files.Path("configurator.yaml");
        File.WriteAllText(source, "original");
        var authority = new PublicDefinitionFiles();
        using (ISetupCliPublicFilePreparation prepared = authority.PrepareCreate(files.Path("candidate.yaml"), "replacement"u8.ToArray(), 4 * 1024 * 1024, false))
        {
            await prepared.StageAsync(work.Token);
            await work.CancelAsync();
            await Assert.That(async () =>
            {
                using SetupCliInputBuffer input = await authority.ReadAsync(files.Path("missing.yaml"), 4 * 1024 * 1024, work.Token);
            })
                .Throws<OperationCanceledException>();
            using ISetupCliPublicFilePreparation cancelled = authority.PrepareCreate(files.Path("new/source.yaml"), "new"u8.ToArray(), 4 * 1024 * 1024, false);
            await Assert.That(() => cancelled.StageAsync(work.Token).AsTask()).Throws<OperationCanceledException>();
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
        var authority = new PublicDefinitionFiles();
        using (ISetupCliPublicFilePreparation canonical = authority.PrepareCreate(first, "canonical"u8.ToArray(), 4 * 1024 * 1024, false))
        using (ISetupCliPublicFilePreparation mapping = authority.PrepareCreate(second, "mapping"u8.ToArray(), 4 * 1024 * 1024, false))
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
