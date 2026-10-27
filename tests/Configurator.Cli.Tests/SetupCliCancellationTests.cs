using System.Text;
using System.Text.Json;
using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;

namespace Configurator.Cli.Tests;

public sealed class SetupCliCancellationTests
{
    [Test]
    public async Task AlreadyCancelledInvocationDoesNotReadOrPublishButDeliversReceipt()
    {
        using var work = new CancellationTokenSource();
        work.Cancel();
        var input = new RecordingInput();
        var output = new ControlledWriter("unused");
        SetupCliExitCode exit = await RunAsync(input, output, work.Token).WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That((int)exit).IsEqualTo(130);
        await Assert.That(input.Reads).IsEqualTo(0);
        await Assert.That(output.Publications).IsEqualTo(0);
        await AssertCancellationReceiptAsync(output);
    }

    [Test]
    [Arguments("definition.json")]
    [Arguments("answers.json")]
    [Arguments("-")]
    public async Task CancellationInterruptsReadAndClearsPreviouslyTransferredBuffers(string blockedPath)
    {
        using var work = new CancellationTokenSource();
        var input = new RecordingInput { BlockedPath = blockedPath };
        var output = new ControlledWriter("unused");
        Task<SetupCliExitCode> running = RunAsync(input, output, work.Token,
            inputPath: blockedPath == "-" ? "-" : "answers.json");
        await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await work.CancelAsync();

        await Assert.That((int)await running.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(130);
        await Assert.That(output.Publications).IsEqualTo(0);
        await Assert.That(input.Returned.All(bytes => bytes.All(value => value == 0))).IsTrue();
        if (blockedPath != "definition.json") await Assert.That(input.Returned.Count).IsEqualTo(1);
        await AssertCancellationReceiptAsync(output);
    }

    [Test]
    [Arguments("prepare", "not-attempted", 130, "completed")]
    [Arguments("before-commit", "not-attempted", 130, "completed")]
    [Arguments("established-commit", "written", 130, "completed")]
    [Arguments("unverified-commit", "written", 74, "completed")]
    [Arguments("cleanup", "written", 74, "failed")]
    [Arguments("operational-failure", "failed", 74, "completed")]
    public async Task CancellationKeepsEstablishedFactsAndEarlierFailurePrecedence(
        string boundary, string writeStatus, int expectedExit, string cleanup)
    {
        using var work = new CancellationTokenSource();
        var input = new RecordingInput();
        var output = new ControlledWriter(boundary);
        Task<SetupCliExitCode> running = RunAsync(input, output, work.Token);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await work.CancelAsync();
        SetupCliExitCode exit = await running.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That((int)exit).IsEqualTo(expectedExit);
        await Assert.That(output.Publications).IsEqualTo(1);
        await Assert.That(output.Payload.Span.IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
        await Assert.That(input.Returned.All(bytes => bytes.All(value => value == 0))).IsTrue();
        await Assert.That(SetupCliMachineContractVerifier.Validate(output.Receipt)).IsEmpty();
        using JsonDocument result = JsonDocument.Parse(output.Receipt);
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString()).IsEqualTo(writeStatus);
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("installation").GetString())
            .IsEqualTo(boundary is "prepare" or "before-commit" ? "not-attempted"
                : boundary == "operational-failure" ? "not-established" : "established");
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("verification").GetString())
            .IsEqualTo(boundary is "prepare" or "before-commit" or "operational-failure" ? "not-attempted"
                : boundary == "unverified-commit" ? "failed" : "succeeded");
        await Assert.That(result.RootElement.GetProperty("cleanup").GetString()).IsEqualTo(cleanup);
        await Assert.That(result.RootElement.GetProperty("completion").GetString())
            .IsEqualTo(expectedExit == 130 ? "cancelled" : "failed");
    }

    [Test]
    public async Task CancellationAfterBundlePrefixDoesNotInventRollbackOrLaterWrites()
    {
        using var work = new CancellationTokenSource();
        var output = new ControlledWriter("prefix");
        Task<SetupCliExitCode> running = RunAsync(new RecordingInput(), output, work.Token, bundle: true);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await work.CancelAsync();

        await Assert.That((int)await running.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(130);
        await AssertCancellationReceiptAsync(output);
        using JsonDocument result = JsonDocument.Parse(output.Receipt);
        JsonElement artifacts = result.RootElement.GetProperty("artifacts");
        await Assert.That(artifacts.GetArrayLength()).IsEqualTo(2);
        await Assert.That(artifacts[0].GetProperty("id").GetString()).IsEqualTo("config");
        await Assert.That(artifacts[0].GetProperty("writeStatus").GetString()).IsEqualTo("written");
        await Assert.That(artifacts[1].GetProperty("id").GetString()).IsEqualTo("yaml");
        await Assert.That(artifacts[1].GetProperty("writeStatus").GetString()).IsEqualTo("not-attempted");
    }

    [Test]
    [Arguments("observed-cleanup-failure", "succeeded", "failed", "publication-cleanup-failed")]
    [Arguments("observed-cleanup-return", "succeeded", "failed", "publication-cleanup-failed")]
    [Arguments("observed-verification-failure", "failed", "unknown", "publication-failed")]
    public async Task ObservedBundleFailureSurvivesLaterCancellation(
        string boundary, string verification, string cleanup, string diagnostic)
    {
        using var work = new CancellationTokenSource();
        var output = new ControlledWriter(boundary);
        Task<SetupCliExitCode> running = RunAsync(new RecordingInput(), output, work.Token, bundle: true);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await work.CancelAsync();

        await Assert.That(await running.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(output.Publications).IsEqualTo(1);
        await Assert.That(output.Deliveries).IsEqualTo(1);
        await Assert.That(SetupCliMachineContractVerifier.Validate(output.Receipt)).IsEmpty();
        using JsonDocument result = JsonDocument.Parse(output.Receipt);
        JsonElement receipt = result.RootElement;
        await Assert.That(receipt.GetProperty("exitCode").GetInt32()).IsEqualTo(74);
        await Assert.That(receipt.GetProperty("completion").GetString()).IsEqualTo("failed");
        await Assert.That(receipt.GetProperty("cleanup").GetString()).IsEqualTo(cleanup);
        await Assert.That(receipt.GetProperty("diagnostics")[0].GetProperty("code").GetString()).IsEqualTo(diagnostic);
        JsonElement artifacts = receipt.GetProperty("artifacts");
        await Assert.That(artifacts[0].GetProperty("writeStatus").GetString()).IsEqualTo("written");
        await Assert.That(artifacts[0].GetProperty("installation").GetString()).IsEqualTo("established");
        await Assert.That(artifacts[0].GetProperty("verification").GetString()).IsEqualTo(verification);
        // A thrown writer cannot certify the unreported remainder.
        string remaining = boundary == "observed-cleanup-return" ? "not-attempted" : "unknown";
        await Assert.That(artifacts[1].GetProperty("writeStatus").GetString()).IsEqualTo(remaining);
        await Assert.That(artifacts[1].GetProperty("installation").GetString()).IsEqualTo(remaining);
        await Assert.That(artifacts[1].GetProperty("verification").GetString()).IsEqualTo(remaining);
    }

    [Test]
    public async Task CancelledWorkCannotCancelTheSeparateBoundedFinalDelivery()
    {
        using var work = new CancellationTokenSource();
        var output = new ControlledWriter("established-commit") { BlockDelivery = true };
        Task<SetupCliExitCode> running = RunAsync(new RecordingInput(), output, work.Token);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await work.CancelAsync();
        await output.DeliveryEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(output.DeliveryToken.CanBeCanceled).IsTrue();
        await Assert.That(output.DeliveryToken.IsCancellationRequested).IsFalse();
        await Assert.That(running.IsCompleted).IsFalse();
        output.ReleaseDelivery.TrySetResult();

        await Assert.That((int)await running.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(130);
        await Assert.That(output.Deliveries).IsEqualTo(1);
        await AssertCancellationReceiptAsync(output);
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(24, false)]
    [Arguments(24, true)]
    public async Task BrokenFinalDeliveryOverridesCancellationWithoutAppendingReceipt(int prefix, bool brokenError)
    {
        using var work = new CancellationTokenSource();
        var output = new ControlledWriter("established-commit") { FailedDeliveryPrefix = prefix };
        var error = new ErrorWriter(brokenError);
        Task<SetupCliExitCode> running = RunAsync(new RecordingInput(), output, work.Token, error: error);
        await output.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await work.CancelAsync();

        await Assert.That(await running.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(output.Deliveries).IsEqualTo(1);
        await Assert.That(output.Receipt.Length).IsEqualTo(prefix);
        await Assert.That(error.Attempts).IsEqualTo(1);
        if (!brokenError)
            await Assert.That(Encoding.UTF8.GetString(error.Bytes)).IsEqualTo("configurator: result delivery failed\n");
    }

    [Test]
    public async Task OwnedBufferClearsTheOriginalAndAnyPreviouslyBorrowedMemory()
    {
        byte[] original = "synthetic-private-buffer"u8.ToArray();
        var buffer = new SetupCliInputBuffer(original);
        ReadOnlyMemory<byte> borrowed = buffer.Bytes;
        buffer.Dispose();
        buffer.Dispose();

        await Assert.That(original.All(value => value == 0)).IsTrue();
        await Assert.That(borrowed.Span.IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
        await Assert.That(() => buffer.Bytes).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task InvalidAnswersStillReleaseBothOwnedInputBuffers()
    {
        var input = new RecordingInput { Answers = "not-json" };
        var output = new ControlledWriter("unused");
        await Assert.That(await RunAsync(input, output, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)))
            .IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(input.Returned.Count).IsEqualTo(2);
        await Assert.That(input.Returned.All(bytes => bytes.All(value => value == 0))).IsTrue();
        await Assert.That(output.Publications).IsEqualTo(0);
    }

    private static Task<SetupCliExitCode> RunAsync(RecordingInput input, ControlledWriter output,
        CancellationToken cancellationToken, bool bundle = false, string inputPath = "answers.json", ISetupCliWriter? error = null)
    {
        string[] arguments = bundle
            ? ["artifact", "bundle-create", "--definition", "definition.json", "--artifact", "config", "--artifact", "yaml",
                "--input", inputPath, "--output", "prepared", "--machine"]
            : ["artifact", "create", "--definition", "definition.json", "--artifact", "config", "--input", inputPath,
                "--output", "prepared.json", "--machine"];
        return new SetupCliApplication().RunAsync(new(arguments, SetupCliMode.Machine,
            new(input, output, error ?? new ErrorWriter(false), CliFixture.RejectingAuthoringFiles, 65_536, 4 * 1024 * 1024)), cancellationToken);
    }

    private static async Task AssertCancellationReceiptAsync(ControlledWriter output)
    {
        await Assert.That(output.Deliveries).IsEqualTo(1);
        await Assert.That(SetupCliMachineContractVerifier.Validate(output.Receipt)).IsEmpty();
        using JsonDocument result = JsonDocument.Parse(output.Receipt);
        await Assert.That(result.RootElement.GetProperty("exitCode").GetInt32()).IsEqualTo(130);
        await Assert.That(result.RootElement.GetProperty("completion").GetString()).IsEqualTo("cancelled");
    }

    private sealed class RecordingInput : ISetupCliInput
    {
        internal string? BlockedPath { get; init; }
        internal string Answers { get; init; } = CliFixture.Answers;
        internal int Reads { get; private set; }
        internal List<byte[]> Returned { get; } = [];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<SetupCliInputBuffer> ReadAsync(string path, int maximumBytes, CancellationToken cancellationToken)
        {
            Reads++;
            cancellationToken.ThrowIfCancellationRequested();
            if (path == BlockedPath)
            {
                Entered.TrySetResult();
                var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await never.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            byte[] bytes = Encoding.UTF8.GetBytes(path == "definition.json" ? CliFixture.Definition : Answers);
            if (bytes.Length > maximumBytes) throw new IOException("input-bound");
            Returned.Add(bytes);
            return new SetupCliInputBuffer(bytes);
        }
    }

    private sealed class ControlledWriter(string boundary) : ISetupCliWriter
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource DeliveryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseDelivery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BlockDelivery { get; init; }
        internal int? FailedDeliveryPrefix { get; init; }
        internal int Publications { get; private set; }
        internal int Deliveries { get; private set; }
        internal CancellationToken DeliveryToken { get; private set; }
        internal byte[] Receipt { get; private set; } = [];
        internal ReadOnlyMemory<byte> Payload { get; private set; }

        public async ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            Deliveries++;
            DeliveryToken = cancellationToken;
            DeliveryEntered.TrySetResult();
            if (BlockDelivery) await ReleaseDelivery.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (FailedDeliveryPrefix is { } prefix)
            {
                Receipt = bytes[..prefix].ToArray();
                throw new IOException("synthetic-transport-failure");
            }
            Receipt = bytes.ToArray();
        }

        public async ValueTask<SetupCliPublicationResult> WriteArtifactAsync(string path, SetupArtifactKind kind,
            ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            Payload = bytes;
            await AwaitCancellationAsync(cancellationToken);
            ProtectedArtifactCommitResult? result = boundary switch
            {
                "prepare" or "before-commit" => null,
                "operational-failure" => ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure),
                "unverified-commit" => ProtectedArtifactCommitResult.Installed(false),
                _ => ProtectedArtifactCommitResult.Installed(true)
            };
            return new([new(result)], CleanupFailed: boundary == "cleanup", Cancelled: true);
        }

        public async ValueTask<SetupCliPublicationResult> WriteBundleAsync(IReadOnlyList<DefinitionArtifact> artifacts,
            string directory, int maximumBytes, Action<string, SetupCliPublicationOutcome> outcomeObserver,
            CancellationToken cancellationToken)
        {
            outcomeObserver(artifacts[0].ArtifactId, new(ProtectedArtifactCommitResult.Installed(
                boundary != "observed-verification-failure",
                cleanupFailed: boundary is "observed-cleanup-failure" or "observed-cleanup-return")));
            await AwaitCancellationAsync(cancellationToken);
            if (boundary is "observed-cleanup-failure" or "observed-verification-failure")
                cancellationToken.ThrowIfCancellationRequested();
            return new([new(ProtectedArtifactCommitResult.Installed(true)), new(null)], Cancelled: true);
        }

        private async Task AwaitCancellationAsync(CancellationToken cancellationToken)
        {
            Publications++;
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration registration = cancellationToken.Register(() => cancelled.TrySetResult());
            Entered.TrySetResult();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await cancelled.Task.WaitAsync(deadline.Token);
        }
    }

    private sealed class ErrorWriter(bool broken) : ISetupCliWriter
    {
        internal int Attempts { get; private set; }
        internal byte[] Bytes { get; private set; } = [];
        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken)
        {
            Attempts++;
            cancellationToken.ThrowIfCancellationRequested();
            if (broken) throw new IOException("synthetic-error-transport-failure");
            Bytes = bytes.ToArray();
            return ValueTask.CompletedTask;
        }
        public ValueTask<SetupCliPublicationResult> WriteArtifactAsync(string path, SetupArtifactKind kind,
            ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public ValueTask<SetupCliPublicationResult> WriteBundleAsync(IReadOnlyList<DefinitionArtifact> artifacts,
            string directory, int maximumBytes, Action<string, SetupCliPublicationOutcome> outcomeObserver,
            CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
}
