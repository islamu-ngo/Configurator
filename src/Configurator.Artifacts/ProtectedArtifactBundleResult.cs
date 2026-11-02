namespace Configurator.Artifacts;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core;
using Configurator.Core.Artifacts;

public sealed class ProtectedArtifactBundleResult
{
    internal ProtectedArtifactBundleResult(IReadOnlyList<ProtectedArtifactBundleOutcome> outcomes,
        ProtectedArtifactBundleCompletionCode completionCode = ProtectedArtifactBundleCompletionCode.Completed,
        bool cleanupFailed = false)
    {
        Outcomes = Array.AsReadOnly(outcomes.ToArray());
        CompletionCode = completionCode;
        CleanupFailed = cleanupFailed;
    }
    public IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes { get; }
    public ProtectedArtifactBundleCompletionCode CompletionCode { get; }
    /// <summary>Cleanup failed independently of the established file outcomes and completion reason.</summary>
    public bool CleanupFailed { get; }
    public bool Succeeded => CompletionCode == ProtectedArtifactBundleCompletionCode.Completed && !CleanupFailed
        && Outcomes.Count > 0 && Outcomes.All(outcome => outcome.Result is { Succeeded: true });
    public override string ToString() =>
        $"{nameof(ProtectedArtifactBundleResult)}:Completion={CompletionCode}:CleanupFailed={CleanupFailed}:Succeeded={Succeeded}:Count={Outcomes.Count}";
}
