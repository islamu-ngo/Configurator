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
    public async Task CleanupFailurePreservesWrittenAndStillClearsEveryPayloadClone()
    {
        var writer = new ControlledWriter { ThrowOnCleanupIndex = 0 };
        var observed = new List<ProtectedArtifactBundleOutcome>();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused", outcomeObserver: observed.Add);
        await Assert.That(result.CleanupFailed).IsTrue();
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Outcomes[0].Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(result.Outcomes[1].Status).IsNull();
        await Assert.That(observed.Single().Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(writer.Committed).IsEqualTo(1);
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
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused", cancellationToken: cancellation.Token);
        await Assert.That(result.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.Cancelled);
        await Assert.That(result.CleanupFailed).IsTrue();
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
            Artifacts(), "/unused", cancellationToken: cancellation.Token);
        await Assert.That(writer.Committed).IsEqualTo(1);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
        await Assert.That(result.Outcomes[0].Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(result.Outcomes[1].Status).IsNull();
    }

    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    public async Task ObserverFailureStopsLaterCommitsWithoutLosingWrittenOrCleanup(int cleanupIndex)
    {
        var writer = new ControlledWriter { ThrowOnCleanupIndex = cleanupIndex };
        var observed = new List<ProtectedArtifactBundleOutcome>();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(writer,
            Artifacts(), "/unused", outcomeObserver: outcome =>
            {
                observed.Add(outcome);
                throw new InvalidOperationException("synthetic-private-observer-detail");
            });
        await Assert.That(result.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.ObserverFailed);
        await Assert.That(result.CleanupFailed).IsEqualTo(cleanupIndex >= 0);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Outcomes[0].Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(result.Outcomes[1].Status).IsNull();
        await Assert.That(observed.Single().Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(writer.Committed).IsEqualTo(1);
        await Assert.That(writer.Disposed).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
        await Assert.That(result.ToString().Contains("synthetic-private", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task RealWriterCancellationRetainsEveryEstablishedPrefix(int prefix)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        DefinitionArtifact[] artifacts = Artifacts();
        var observed = new List<ProtectedArtifactBundleOutcome>();
        if (prefix == 0) cancellation.Cancel();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            new ProtectedArtifactWriter(), artifacts, directory.Path, outcome =>
            {
                observed.Add(outcome);
                if (observed.Count == prefix) cancellation.Cancel();
            }, cancellation.Token);
        await Assert.That(observed.Count).IsEqualTo(prefix);
        await Assert.That(result.Outcomes.Count(outcome => outcome.Status == ProtectedArtifactStatus.Written))
            .IsEqualTo(prefix);
        for (int index = 0; index < artifacts.Length; index++)
        {
            string target = Path.Combine(directory.Path, artifacts[index].FileName);
            await Assert.That(File.Exists(target)).IsEqualTo(index < prefix);
            if (index >= prefix) continue;
            await Assert.That(await File.ReadAllBytesAsync(target)).IsEquivalentTo(artifacts[index].Bytes.ToArray());
            await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        await Assert.That(Directory.GetFileSystemEntries(directory.Path).Length).IsEqualTo(prefix);
    }

    [Test]
    public async Task RealWriterCleanupFailureRetainsProtectedFileAndObservedWritten()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var directory = new TestDirectory();
        DefinitionArtifact[] artifacts = Artifacts();
        var observed = new List<ProtectedArtifactBundleOutcome>();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            new CleanupFailingWriter(), artifacts, directory.Path, observed.Add);
        await Assert.That(result.CleanupFailed).IsTrue();
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(observed.Single().Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(result.Outcomes[0].Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(result.Outcomes[1].Status).IsNull();
        string target = Path.Combine(directory.Path, artifacts[0].FileName);
        await Assert.That(await File.ReadAllBytesAsync(target)).IsEquivalentTo(artifacts[0].Bytes.ToArray());
        await Assert.That(File.GetUnixFileMode(target)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await Assert.That(Directory.GetFileSystemEntries(directory.Path).Length).IsEqualTo(1);
    }

    [Test]
    public async Task CleanupAfterFinalCommitPreservesEveryWrittenWithoutClaimingSuccess()
    {
        var writer = new ControlledWriter { ThrowOnCleanupIndex = 1 };
        var observed = new List<ProtectedArtifactBundleOutcome>();
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            writer, Artifacts(), "/unused", observed.Add);
        await Assert.That(result.CleanupFailed).IsTrue();
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Outcomes.All(outcome => outcome.Status == ProtectedArtifactStatus.Written)).IsTrue();
        await Assert.That(observed.Count).IsEqualTo(2);
        await Assert.That(writer.Disposed).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnexpectedCommitFaultPreservesPrimaryExceptionAndObservedPrefixAfterCleanup(bool cleanupFails)
    {
        var expected = new InvalidOperationException("synthetic-programming-fault");
        var writer = new ControlledWriter
        {
            ThrowOnCommitIndex = 1,
            CommitException = expected,
            ThrowOnCleanupIndex = cleanupFails ? 1 : -1
        };
        var observed = new List<ProtectedArtifactBundleOutcome>();
        Exception? actual = null;
        try
        {
            await ProtectedArtifactBundlePublisher.PublishAsync(writer, Artifacts(), "/unused", observed.Add);
        }
        catch (InvalidOperationException exception) { actual = exception; }
        await Assert.That(ReferenceEquals(actual, expected)).IsTrue();
        await Assert.That(observed.Single().Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(writer.Disposed).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    public async Task UnrequestedCancellationRemainsAnObservedFaultAfterCleanup()
    {
        var writer = new ControlledWriter { ThrowOnCommitIndex = 1, CommitException = new OperationCanceledException() };
        var observed = new List<ProtectedArtifactBundleOutcome>();
        await Assert.That(async () => await ProtectedArtifactBundlePublisher.PublishAsync(
            writer, Artifacts(), "/unused", observed.Add)).Throws<OperationCanceledException>();
        await Assert.That(observed.Single().Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(writer.Disposed).IsEqualTo(2);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    public async Task ObserverFailureDuringPreflightRetainsEstablishedRejectionWithoutCommitting()
    {
        var writer = new ControlledWriter { RejectIndex = 1 };
        ProtectedArtifactBundleResult result = await ProtectedArtifactBundlePublisher.PublishAsync(
            writer, Artifacts(), "/unused", _ => throw new IOException("synthetic-observer-io-fault"));
        await Assert.That(result.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.ObserverFailed);
        await Assert.That(result.Outcomes[0].Status).IsNull();
        await Assert.That(result.Outcomes[1].Status).IsEqualTo(ProtectedArtifactStatus.UnsafeTarget);
        await Assert.That(writer.Committed).IsEqualTo(0);
        await Assert.That(writer.Disposed).IsEqualTo(1);
        await Assert.That(writer.Payloads.All(memory => memory.Span.ToArray().All(value => value == 0))).IsTrue();
    }

    [Test]
    public async Task BundleResultCopiesKnownOutcomesBeforeExposingThem()
    {
        ProtectedArtifactBundleOutcome[] outcomes = [new("one", ProtectedArtifactStatus.Written)];
        var result = new ProtectedArtifactBundleResult(outcomes);
        outcomes[0] = new("one", null);
        await Assert.That(result.Outcomes.Single().Status).IsEqualTo(ProtectedArtifactStatus.Written);
        await Assert.That(() => ((IList<ProtectedArtifactBundleOutcome>)result.Outcomes)[0] = outcomes[0])
            .Throws<NotSupportedException>();
        await Assert.That(result.Succeeded).IsTrue();
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
        public int ThrowOnCommitIndex { get; init; } = -1;
        public Exception? CommitException { get; init; }
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
                if (index == ThrowOnCommitIndex) throw CommitException!;
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

    private sealed class CleanupFailingWriter : IProtectedArtifactWriter
    {
        private readonly ProtectedArtifactWriter _writer = new();
        public bool IsAvailable => _writer.IsAvailable;

        public async Task<ProtectedArtifactPreparation> PrepareAsync(SetupArtifactKind kind, string targetPath,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            ProtectedArtifactPreparation preparation = await _writer.PrepareAsync(kind, targetPath, bytes, cancellationToken);
            return new ProtectedArtifactPreparation(preparation.CommitAsync, () =>
            {
                preparation.Dispose();
                throw new IOException("synthetic-cleanup-failure");
            }, preparation.RejectionStatus);
        }
    }
}
