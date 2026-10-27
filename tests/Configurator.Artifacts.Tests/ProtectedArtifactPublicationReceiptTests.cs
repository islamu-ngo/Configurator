namespace Configurator.Artifacts.Tests;

using Configurator.Artifacts;

public sealed class ProtectedArtifactPublicationReceiptTests
{
    [Test]
    public async Task SixtyFourOutcomesRetainInstalledAndUnattemptedFactsInOrder()
    {
        ProtectedArtifactBundleOutcome[] outcomes = Enumerable.Range(0, 64)
            .Select(index => new ProtectedArtifactBundleOutcome($"artifact-{index}", null)).ToArray();
        outcomes[0] = new("first", ProtectedArtifactCommitResult.Installed(true));
        outcomes[63] = new("last", ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true));
        var receipt = new ProtectedArtifactPublicationReceipt(Guid.NewGuid(), outcomes, true,
            ProtectedArtifactBundleCompletionCode.Cancelled);

        await Assert.That(receipt.Outcomes).Count().IsEqualTo(64);
        await Assert.That(receipt.Outcomes[0].ArtifactId).IsEqualTo("first");
        await Assert.That(receipt.Outcomes[0].Result!.Succeeded).IsTrue();
        await Assert.That(receipt.Outcomes[1].ArtifactId).IsEqualTo("artifact-1");
        await Assert.That(receipt.Outcomes[1].Result).IsNull();
        await Assert.That(receipt.Outcomes[62].ArtifactId).IsEqualTo("artifact-62");
        await Assert.That(receipt.Outcomes[63].ArtifactId).IsEqualTo("last");
        await Assert.That(receipt.Outcomes[63].Result!.InstallationEstablished).IsTrue();
        await Assert.That(receipt.Outcomes[63].Result!.VerificationSucceeded).IsFalse();
        await Assert.That(receipt.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.Cancelled);
        await Assert.That(receipt.CleanupFailed).IsTrue();
    }

    [Test]
    public async Task SixtyFiveOutcomesAreRejected()
    {
        ProtectedArtifactBundleOutcome[] outcomes = Enumerable.Range(0, 65)
            .Select(index => new ProtectedArtifactBundleOutcome($"artifact-{index}", null)).ToArray();

        await Assert.That(() => new ProtectedArtifactPublicationReceipt(Guid.NewGuid(), outcomes, true))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CallerMutationAndCollectionWritesCannotChangeSnapshot(bool useList)
    {
        ProtectedArtifactBundleOutcome[] outcomes =
        [
            new("installed", ProtectedArtifactCommitResult.Installed(false, cleanupFailed: true)),
            new("pending", null)
        ];
        IList<ProtectedArtifactBundleOutcome> callerOwned = useList ? outcomes.ToList() : outcomes;
        Guid attemptId = Guid.NewGuid();
        var receipt = new ProtectedArtifactPublicationReceipt(attemptId, callerOwned, false,
            isUnknown: true, failed: true);
        callerOwned[0] = new("replacement", ProtectedArtifactCommitResult.Installed(true));
        callerOwned[1] = new("changed", ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.TargetExists));
        if (useList) callerOwned.Clear();

        await Assert.That(receipt.AttemptId).IsEqualTo(attemptId);
        await Assert.That(receipt.Outcomes).Count().IsEqualTo(2);
        await Assert.That(receipt.Outcomes[0].ArtifactId).IsEqualTo("installed");
        await Assert.That(receipt.Outcomes[0].Result!.InstallationEstablished).IsTrue();
        await Assert.That(receipt.Outcomes[0].Result!.VerificationSucceeded).IsFalse();
        await Assert.That(receipt.Outcomes[1].ArtifactId).IsEqualTo("pending");
        await Assert.That(receipt.Outcomes[1].Result).IsNull();
        await Assert.That(receipt.CleanupFailed).IsTrue();
        await Assert.That(receipt.IsSettled).IsFalse();
        await Assert.That(receipt.IsUnknown).IsTrue();
        await Assert.That(receipt.Failed).IsTrue();
        await Assert.That(receipt.CompletionCode).IsNull();
        await Assert.That(() => ((IList<ProtectedArtifactBundleOutcome>)receipt.Outcomes)[0] = new("changed", null))
            .Throws<NotSupportedException>();
        await Assert.That(receipt.Outcomes[0].ArtifactId).IsEqualTo("installed");
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, true)]
    [Arguments(true, true, true)]
    public async Task CleanupFailureRetainsIndependentAttemptAndOutcomeFacts(
        bool attemptCleanupFailed, bool outcomeCleanupFailed, bool expectedCleanupFailed)
    {
        var receipt = new ProtectedArtifactPublicationReceipt(Guid.NewGuid(),
            [new("installed", ProtectedArtifactCommitResult.Installed(true, outcomeCleanupFailed))],
            true, ProtectedArtifactBundleCompletionCode.ObserverFailed, attemptCleanupFailed);

        await Assert.That(receipt.CleanupFailed).IsEqualTo(expectedCleanupFailed);
        await Assert.That(receipt.Outcomes[0].Result!.CleanupFailed).IsEqualTo(outcomeCleanupFailed);
        await Assert.That(receipt.Outcomes[0].Result!.InstallationEstablished).IsTrue();
        await Assert.That(receipt.Outcomes[0].Result!.VerificationSucceeded).IsTrue();
        await Assert.That(receipt.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.ObserverFailed);
        await Assert.That(receipt.IsSettled).IsTrue();
    }
}
