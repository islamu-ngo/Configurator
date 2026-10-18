namespace Configurator.Terminal.Tests;

using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Presentation;
using Configurator.Terminal;

public sealed class SetupTerminalPublicationReceiptTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InstalledUnverifiedReceiptSurvivesCancellationAndPrivateClear(bool cleanupFailed)
    {
        using var form = TerminalDefinitionFixture.Form();
        using var cancellation = new CancellationTokenSource();
        SetupTerminalArtifactOperation? active = null;
        using var operation = new SetupTerminalArtifactOperation(form, new UnavailableWriter(available: true),
            "/synthetic/private-output", outcomeObserver: _ =>
            {
                cancellation.Cancel();
                active!.ClearPrivateState();
            }, publishBundle: (_, _, observe, token) =>
            {
                var outcome = new ProtectedArtifactBundleOutcome("config",
                    ProtectedArtifactCommitResult.Installed(false, cleanupFailed));
                observe(outcome);
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new SetupTerminalArtifactResult([outcome]));
            });
        active = operation;
        operation.ArtifactId = "environment";
        operation.ToggleCurrentArtifact();
        await Assert.That(await operation.PrepareAsync()).IsTrue();
        await Assert.That(async () => await operation.ExecuteAsync(cancellation.Token)).Throws<OperationCanceledException>();
        SetupTerminalPublicationReceipt receipt = operation.PublicationHistory.Single();
        await Assert.That(receipt.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.Cancelled);
        await Assert.That(receipt.Outcomes[0].Result!.InstallationEstablished).IsTrue();
        await Assert.That(receipt.Outcomes[0].Result!.VerificationSucceeded).IsFalse();
        await Assert.That(receipt.CleanupFailed).IsEqualTo(cleanupFailed);
        await Assert.That(receipt.Outcomes[1].Result).IsNull();
        await Assert.That(operation.IsPrepared).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
        string status = SetupTerminalText.FormatReceipt(receipt, 0);
        await Assert.That(status).Contains("installation-established");
        await Assert.That(status).Contains("verification-failed");
        await Assert.That(status).Contains("current-integrity-not-guaranteed");
        await Assert.That(status).DoesNotContain(SetupTerminalText.Get("ProtectedDraftSaved"));
        await Assert.That(status).DoesNotContain("privateValue");
        await Assert.That(status).DoesNotContain("/synthetic/private-output");
        await Assert.That(SetupTerminalText.FormatReceipt(receipt, 1)).Contains(SetupTerminalText.Get("NotAttempted"));
        await Assert.That(SetupTerminalText.FormatClosingReceipt(receipt)).Contains("installation-established");
    }

    [Test]
    public async Task UnexpectedFailureRetainsAnUncertainValueFreeReceipt()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new SetupTerminalArtifactOperation(form, new UnavailableWriter(available: true), Path.GetTempPath(),
            beforePublication: _ => throw new InvalidOperationException("synthetic-private-path"));
        await Assert.That(await operation.PrepareAsync()).IsTrue();
        await Assert.That(async () => await operation.ExecuteAsync(CancellationToken.None)).Throws<InvalidOperationException>();
        SetupTerminalPublicationReceipt receipt = operation.PublicationHistory.Single();
        await Assert.That(receipt.Failed).IsTrue();
        await Assert.That(receipt.IsUnknown).IsTrue();
        await Assert.That(receipt.IsSettled).IsTrue();
        await Assert.That(receipt.Outcomes[0].Result).IsNull();
        string status = SetupTerminalText.FormatReceipt(receipt, 0);
        await Assert.That(status).Contains("terminal-operation-failed");
        await Assert.That(status).Contains(SetupTerminalText.Get("PublicationUnknown"));
        await Assert.That(status).DoesNotContain("synthetic-private-path");
    }

    [Test]
    public async Task HistoryIsBoundedCopiedAndClearedOnlyOnFinalDisposal()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new SetupTerminalArtifactOperation(form, new UnavailableWriter(), Path.GetTempPath());
        var attempts = new List<Guid>();
        for (int attempt = 0; attempt < 18; attempt++)
        {
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            await operation.ExecuteAsync(CancellationToken.None);
            attempts.Add(operation.PublicationHistory[^1].AttemptId);
        }
        IReadOnlyList<SetupTerminalPublicationReceipt> snapshot = operation.PublicationHistory;
        await Assert.That(operation.LatestPublication!.AttemptId).IsEqualTo(attempts[^1]);
        await Assert.That(snapshot).Count().IsEqualTo(16);
        await Assert.That(snapshot.Select(item => item.AttemptId).Distinct()).Count().IsEqualTo(16);
        await Assert.That(snapshot.Select(item => item.AttemptId).SequenceEqual(attempts.Skip(2))).IsTrue();
        await Assert.That(snapshot.All(item => item.IsSettled && item.Outcomes.Count == 1 &&
            item.Outcomes[0].Result?.Status == ProtectedArtifactStatus.UnsupportedPlatform)).IsTrue();
        operation.ClearPrivateState();
        await Assert.That(operation.PublicationHistory).Count().IsEqualTo(16);
        operation.Dispose();
        await Assert.That(operation.PublicationHistory).IsEmpty();
        await Assert.That(operation.LatestPublication).IsNull();
        await Assert.That(snapshot).Count().IsEqualTo(16);
        ProtectedArtifactBundleOutcome[] callerOwned = [new("declared", ProtectedArtifactCommitResult.Installed(true))];
        var receipt = new SetupTerminalPublicationReceipt(Guid.NewGuid(), callerOwned, true);
        var result = new SetupTerminalArtifactResult(callerOwned);
        callerOwned[0] = new("changed", null);
        await Assert.That(receipt.Outcomes[0].ArtifactId).IsEqualTo("declared");
        await Assert.That(result.Outcomes[0].Result!.Succeeded).IsTrue();
    }

    [Test]
    public async Task DrainingPublicationCannotRestartAndUnknownRemainsDistinctFromUnattempted()
    {
        using var form = TerminalDefinitionFixture.Form();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var operation = new SetupTerminalArtifactOperation(form, new UnavailableWriter(available: true), Path.GetTempPath(),
            beforePublication: async _ => { reached.TrySetResult(); await release.Task; });
        using var cancellation = new CancellationTokenSource();
        await Assert.That(await operation.PrepareAsync()).IsTrue();
        Task execution = operation.ExecuteAsync(cancellation.Token);
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();
            operation.ClearPrivateState();
            await Assert.That(operation.IsPublishing).IsTrue();
            await Assert.That(await operation.PrepareAsync()).IsFalse();
            await Assert.That(async () => { await operation.ExecuteAsync(CancellationToken.None); }).Throws<InvalidOperationException>();
            operation.MarkPublicationUnknown();
            await Assert.That(operation.CurrentPublication!.IsUnknown).IsTrue();
            await Assert.That(ReferenceEquals(operation.LatestPublication, operation.CurrentPublication)).IsTrue();
            await Assert.That(operation.CurrentPublication.IsSettled).IsFalse();
        }
        finally { release.TrySetResult(); }
        await Assert.That(async () => await execution.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
        await Assert.That(operation.CurrentPublication).IsNull();
        await Assert.That(ReferenceEquals(operation.LatestPublication, operation.PublicationHistory[0])).IsTrue();
        await Assert.That(operation.PublicationHistory[0].IsSettled).IsTrue();
        await Assert.That(operation.PublicationHistory[0].Outcomes[0].Result).IsNull();
        await Assert.That(operation.PublicationHistory[0].CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.Cancelled);
    }

    [Test]
    public async Task ObserverFailurePreservesRealCommittedFileAndStopsFollowingCommits()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("terminal-observer-").FullName;
        try
        {
            using var form = TerminalDefinitionFixture.Form();
            using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), directory,
                outcomeObserver: _ => throw new InvalidOperationException("synthetic-private-exception"));
            operation.ArtifactId = "environment";
            operation.ToggleCurrentArtifact();
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            await operation.ExecuteAsync(CancellationToken.None);
            SetupTerminalPublicationReceipt receipt = operation.PublicationHistory.Single();
            await Assert.That(receipt.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.ObserverFailed);
            await Assert.That(receipt.Outcomes[0].Result!.InstallationEstablished).IsTrue();
            await Assert.That(receipt.Outcomes[0].Result!.VerificationSucceeded).IsTrue();
            await Assert.That(receipt.Outcomes[1].Result).IsNull();
            await Assert.That(receipt.ToString()).DoesNotContain("synthetic-private-exception");
            await Assert.That(Directory.GetFiles(directory)).Count().IsEqualTo(1);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task CancelledCommittedPrefixRemainsHistoricalWithoutRestoringPrivateState(int prefix)
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("terminal-receipt-").FullName;
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var form = TerminalDefinitionFixture.Form();
            int written = 0;
            using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), directory,
                beforePublication: async token =>
                {
                    if (prefix != 0) return;
                    reached.TrySetResult();
                    await release.Task.WaitAsync(token);
                }, outcomeObserver: outcome =>
                {
                    if (outcome.Result is not { InstallationEstablished: true } || ++written != prefix) return;
                    reached.TrySetResult();
                    release.Task.GetAwaiter().GetResult();
                });
            operation.ArtifactId = "environment";
            operation.ToggleCurrentArtifact();
            using var session = new SetupPresentationSession(new StrongReferenceMessenger());
            SetupWorkspaceId.TryCreate("definition", out var id);
            using var workspace = session.CreateWorkspace(id, operation);
            workspace.Activate();
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            Task execution = Task.Run(() => workspace.ExecuteAsync(Guid.NewGuid()));
            try
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                workspace.Cancel();
                operation.ClearPrivateState();
            }
            finally { release.TrySetResult(); }
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            await workspace.WaitForPendingOperationsAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(operation.PublicationHistory).Count().IsEqualTo(1);
            SetupTerminalPublicationReceipt receipt = operation.PublicationHistory[0];
            await Assert.That(receipt.IsSettled).IsTrue();
            await Assert.That(receipt.Outcomes.Count(item => item.Result is { InstallationEstablished: true })).IsEqualTo(prefix);
            await Assert.That(workspace.Output).IsEmpty();
            await Assert.That(workspace.Result).IsNull();
            await Assert.That(operation.IsPrepared).IsFalse();
            await Assert.That(form.Snapshot().Values.ContainsKey("token")).IsFalse();
            await Assert.That(receipt.ToString()).DoesNotContain("privateValue");
            await Assert.That(receipt.ToString()).DoesNotContain(directory);
            await Assert.That(Directory.GetFiles(directory)).Count().IsEqualTo(prefix);
            if (prefix > 0)
            {
                string path = Path.Combine(directory, "config.json");
                await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("""{"name":"sample","token":"privateValue"}""");
                await Assert.That(File.GetUnixFileMode(path)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally
        {
            release.TrySetResult();
            Directory.Delete(directory, true);
        }
    }

    private sealed class UnavailableWriter(bool available = false) : IProtectedArtifactWriter
    {
        public bool IsAvailable => available;
        public Task<ProtectedArtifactPreparation> PrepareAsync(Configurator.Core.SetupArtifactKind kind, string targetPath,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
