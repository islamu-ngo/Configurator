namespace Configurator.Terminal;

using Configurator.Artifacts;

/// <summary>Copied file facts for one attempt, independent of answers and current preparation.</summary>
internal sealed record SetupTerminalPublicationReceipt
{
    internal SetupTerminalPublicationReceipt(Guid attemptId, IEnumerable<ProtectedArtifactBundleOutcome> outcomes,
        bool isSettled, ProtectedArtifactBundleCompletionCode? completionCode = null, bool cleanupFailed = false,
        bool isUnknown = false, bool failed = false)
    {
        AttemptId = attemptId;
        ProtectedArtifactBundleOutcome[] copy = outcomes.ToArray();
        if (copy.Length > 64) throw new ArgumentOutOfRangeException(nameof(outcomes));
        Outcomes = Array.AsReadOnly(copy);
        IsSettled = isSettled;
        CompletionCode = completionCode;
        CleanupFailed = cleanupFailed;
        IsUnknown = isUnknown;
        Failed = failed;
    }

    internal Guid AttemptId { get; }
    internal IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes { get; }
    internal bool IsSettled { get; }
    internal ProtectedArtifactBundleCompletionCode? CompletionCode { get; }
    internal bool CleanupFailed { get; }
    internal bool IsUnknown { get; }
    internal bool Failed { get; }
    public override string ToString() => $"{nameof(SetupTerminalPublicationReceipt)}:Settled={IsSettled}:Count={Outcomes.Count}:Unknown={IsUnknown}";
}
