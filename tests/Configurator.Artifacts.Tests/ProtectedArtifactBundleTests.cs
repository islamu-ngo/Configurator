using System.Text;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

namespace Configurator.Artifacts.Tests;

public sealed class ProtectedArtifactBundleTests
{
    [Test]
    public async Task SuccessfulBundlePublishesOnlyOwnerPrivateDeclaredFiles()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TestDirectory();
        DefinitionArtifact[] artifacts = Artifacts();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            new ProtectedArtifactWriter(), artifacts, directory.Path);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(string.Join(',', result.Outcomes.Select(item => item.ArtifactId))).IsEqualTo("one,two");
        foreach (DefinitionArtifact artifact in artifacts)
        {
            string target = Path.Combine(directory.Path, artifact.FileName);
            await Assert.That(await File.ReadAllBytesAsync(target)).IsEquivalentTo(artifact.Bytes.ToArray());
            await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        await Assert.That(Directory.GetFileSystemEntries(directory.Path).Length).IsEqualTo(2);
    }

    [Test]
    public async Task PublisherKeepsPayloadUntilCommitThenClearsEveryClone()
    {
        var writer = new ControlledWriter();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused");
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(writer.NonzeroAtCommit).IsEqualTo(2);
        await Assert.That(writer.Payloads.Count).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    public async Task CleanupFailureStillClearsEveryPayloadClone()
    {
        var writer = new ControlledWriter { ThrowOnCleanupIndex = 0 };
        await Assert.That(async () => await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused")).Throws<InvalidOperationException>();
        await Assert.That(writer.Payloads.Count).IsEqualTo(2);
        await Assert.That(writer.Disposed).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    public async Task ThrowingFirstPreparationStillDisposesSecondAndClearsPayloads()
    {
        using var cancellation = new CancellationTokenSource();
        var writer = new ControlledWriter
        {
            ThrowOnCleanupIndex = 0,
            AfterPrepare = index =>
            {
                if (index == 1) cancellation.Cancel();
            }
        };
        await Assert.That(async () => await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused", cancellation.Token)).Throws<InvalidOperationException>();
        await Assert.That(writer.Committed).IsEqualTo(0);
        await Assert.That(writer.Disposed).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    public async Task DisposedLaterArtifactPreventsAnyCommitAndClearsEarlierClone()
    {
        DefinitionArtifact[] artifacts = Artifacts();
        var writer = new ControlledWriter
        {
            AfterPrepare = index =>
        {
            if (index == 0) artifacts[1].Dispose();
        }
        };
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            artifacts, "/unused");
        await Assert.That(writer.Committed).IsEqualTo(0);
        await Assert.That(result.Outcomes[0].Status).IsNull();
        await Assert.That(result.Outcomes[1].Status).IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    public async Task ExistingLaterDestinationPreventsEveryCommit()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TestDirectory();
        DefinitionArtifact[] artifacts = Artifacts();
        string existing = Path.Combine(directory.Path, artifacts[1].FileName);
        await File.WriteAllTextAsync(existing, "original");

        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            new ProtectedArtifactWriter(), artifacts, directory.Path);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Outcomes[0].Status).IsNull();
        await Assert.That(result.Outcomes[1].Status).IsEqualTo(ProtectedArtifactStatus.TargetExists);
        await Assert.That(File.Exists(Path.Combine(directory.Path, artifacts[0].FileName))).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(existing)).IsEqualTo("original");
    }

    [Test]
    public async Task DuplicateDestinationIsRejectedBeforePreparingAnything()
    {
        DefinitionArtifact[] artifacts = Artifacts();
        var writer = new ControlledWriter();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            [artifacts[0], artifacts[0]], "/unused");
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(writer.Prepared).IsEqualTo(0);
        await Assert.That(result.Outcomes.All(item => item.Status == ProtectedArtifactStatus.InvalidRequest)).IsTrue();
    }

    [Test]
    public async Task MixedInputRevisionsAreRejectedBeforePreparingAnything()
    {
        DefinitionArtifact[] artifacts = Artifacts();
        DefinitionArtifact second = Artifacts(inputRevision: 2)[1];
        var writer = new ControlledWriter();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            [artifacts[0], second], "/unused");
        await Assert.That(writer.Prepared).IsEqualTo(0);
        await Assert.That(result.Outcomes.All(item => item.Status == ProtectedArtifactStatus.InvalidRequest)).IsTrue();
    }

    [Test]
    public async Task UnsafeLaterDestinationPreventsFirstPublication()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TestDirectory();
        DefinitionArtifact[] artifacts = Artifacts();
        string unsafeTarget = Path.Combine(directory.Path, artifacts[1].FileName);
        File.CreateSymbolicLink(unsafeTarget, Path.Combine(directory.Path, "outside"));
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            new ProtectedArtifactWriter(), artifacts, directory.Path);
        await Assert.That(result.Outcomes[1].Status).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
        await Assert.That(File.Exists(Path.Combine(directory.Path, artifacts[0].FileName))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(directory.Path, "outside"))).IsFalse();
    }

    [Test]
    public async Task PreflightRejectionCleansPriorPreparationsAndCommitsNothing()
    {
        var writer = new ControlledWriter { RejectIndex = 1 };
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused");
        await Assert.That(writer.Prepared).IsEqualTo(2);
        await Assert.That(writer.Disposed).IsEqualTo(1);
        await Assert.That(writer.Committed).IsEqualTo(0);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
        await Assert.That(result.Outcomes[0].Status).IsNull();
        await Assert.That(result.Outcomes[1].Status).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
    }

    [Test]
    public async Task LaterRaceReportsCommittedPrefixWithoutClaimingRollback()
    {
        var writer = new ControlledWriter { FailCommitIndex = 1 };
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused");
        await Assert.That(writer.Prepared).IsEqualTo(2);
        await Assert.That(writer.Committed).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
        await Assert.That(result.Outcomes[0].Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(result.Outcomes[1].Status).IsEqualTo(ProtectedArtifactStatus.TargetChanged);
        await Assert.That(result.ToString()!.Contains("/unused", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task CancellationAfterCommittedPrefixReportsPrefix()
    {
        using var cancellation = new CancellationTokenSource();
        var writer = new ControlledWriter { CancelAfterFirstCommit = cancellation };
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused", cancellation.Token);
        await Assert.That(writer.Committed).IsEqualTo(1);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
        await Assert.That(result.Outcomes[0].Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(result.Outcomes[1].Status).IsNull();
    }

    private static DefinitionArtifact[] Artifacts(long inputRevision = 1)
    {
        const string json = """
            {"schemaVersion":1,"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1,
             "applicationId":"example.service","applicationVersions":["1.0"],"publisher":"example",
             "title":"Example","entryPoints":["setup"],"locales":["en"],
             "fields":[{"id":"service.name","type":"string","required":true,"sensitivity":"public",
             "disclosure":"guided","title":"Service name"}],
             "artifacts":[
              {"id":"one","format":"json","fileName":"one.json","applySemantics":"create",
               "restart":"none","projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"}}}},
              {"id":"two","format":"yaml","fileName":"two.yaml","applySemantics":"create",
               "restart":"none","projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"}}}}]}
            """;
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(json));
        using var document = System.Text.Json.JsonDocument.Parse("""{"service.name":"sample"}""");
        var answers = new SetupAnswerSnapshot(inputRevision, document.RootElement.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.Clone()));
        return definition.Artifacts.Take(2).Select(item => DefinitionArtifactComposer.Compose(definition, item.Id, answers).Artifact!).ToArray();
    }

    private sealed class ControlledWriter : IProtectedArtifactWriter
    {
        public bool IsAvailable => true;
        public int Prepared { get; private set; }
        public int Disposed { get; private set; }
        public int Committed { get; private set; }
        public int NonzeroAtCommit { get; private set; }
        public List<ReadOnlyMemory<byte>> Payloads { get; } = [];
        public int RejectIndex { get; init; } = -1;
        public int FailCommitIndex { get; init; } = -1;
        public int ThrowOnCleanupIndex { get; init; } = -1;
        public CancellationTokenSource? CancelAfterFirstCommit { get; init; }
        public Action<int>? AfterPrepare { get; init; }

        public Task<ProtectedArtifactPreparation> PrepareAsync(SetupArtifactKind kind, string targetPath,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            int index = Prepared++;
            Payloads.Add(bytes);
            AfterPrepare?.Invoke(index);
            if (index == RejectIndex)
                return Task.FromResult(ProtectedArtifactPreparation.Rejected(ProtectedArtifactStatus.UnsafeTarget));
            return Task.FromResult(new ProtectedArtifactPreparation(_ =>
            {
                Committed++;
                if (bytes.Span.ContainsAnyExcept((byte)0)) NonzeroAtCommit++;
                if (Committed == 1) CancelAfterFirstCommit?.Cancel();
                return Task.FromResult(index == FailCommitIndex
                    ? ProtectedArtifactStatus.TargetChanged : ProtectedArtifactStatus.Written);
            }, () =>
            {
                Disposed++;
                if (index == ThrowOnCleanupIndex) throw new InvalidOperationException("synthetic-cleanup-failure");
            }));
        }
    }
}
