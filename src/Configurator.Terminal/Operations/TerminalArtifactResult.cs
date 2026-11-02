namespace Configurator.Terminal;

using Configurator.Artifacts;

internal sealed record TerminalArtifactResult
{
    internal TerminalArtifactResult(IEnumerable<ProtectedArtifactBundleOutcome> outcomes,
        ProtectedArtifactBundleCompletionCode completionCode = ProtectedArtifactBundleCompletionCode.Completed,
        bool cleanupFailed = false)
    {
        Outcomes = Array.AsReadOnly(outcomes.ToArray());
        CompletionCode = completionCode;
        CleanupFailed = cleanupFailed || Outcomes.Any(item => item.Result is { CleanupFailed: true });
    }
    internal IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes { get; }
    internal ProtectedArtifactBundleCompletionCode CompletionCode { get; }
    internal bool CleanupFailed { get; }
    internal bool Succeeded => CompletionCode == ProtectedArtifactBundleCompletionCode.Completed && !CleanupFailed && Outcomes.Count > 0
        && Outcomes.All(item => item.Result is { Succeeded: true });
    public override string ToString() => $"{nameof(TerminalArtifactResult)}:Succeeded={Succeeded}:Count={Outcomes.Count}";
}
