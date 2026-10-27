namespace Configurator.Artifacts;

/// <summary>Copied file facts for one attempt, independent of answers and current preparation.</summary>
public sealed record ProtectedArtifactPublicationReceipt
{
    public ProtectedArtifactPublicationReceipt(Guid attemptId, IEnumerable<ProtectedArtifactBundleOutcome> outcomes,
        bool isSettled, ProtectedArtifactBundleCompletionCode? completionCode = null, bool cleanupFailed = false,
        bool isUnknown = false, bool failed = false)
    {
        AttemptId = attemptId;
        ProtectedArtifactBundleOutcome[] copy = outcomes.ToArray();
        if (copy.Length > 64) throw new ArgumentOutOfRangeException(nameof(outcomes));
        Outcomes = Array.AsReadOnly(copy);
        IsSettled = isSettled;
        CompletionCode = completionCode;
        CleanupFailed = cleanupFailed || copy.Any(item => item.Result is { CleanupFailed: true });
        IsUnknown = isUnknown;
        Failed = failed;
    }

    public Guid AttemptId { get; }
    public IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes { get; }
    public bool IsSettled { get; }
    public ProtectedArtifactBundleCompletionCode? CompletionCode { get; }
    public bool CleanupFailed { get; }
    public bool IsUnknown { get; }
    public bool Failed { get; }
    public override string ToString() => $"{nameof(ProtectedArtifactPublicationReceipt)}:Settled={IsSettled}:Count={Outcomes.Count}:Unknown={IsUnknown}";
}
