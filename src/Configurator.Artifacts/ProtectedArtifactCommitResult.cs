using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Configurator.Core;


namespace Configurator.Artifacts;

/// <summary>Historical installation and immediate verification facts, not a guarantee of continued destination integrity.</summary>
public sealed record ProtectedArtifactCommitResult
{
    private ProtectedArtifactCommitResult(ProtectedArtifactStatus status, bool installationEstablished,
        bool verificationSucceeded, bool cleanupFailed)
    {
        Status = status;
        InstallationEstablished = installationEstablished;
        VerificationSucceeded = verificationSucceeded;
        CleanupFailed = cleanupFailed;
    }

    public ProtectedArtifactStatus Status { get; }
    public bool InstallationEstablished { get; }
    public bool VerificationSucceeded { get; }
    public bool CleanupFailed { get; }
    public bool Succeeded => InstallationEstablished && VerificationSucceeded && !CleanupFailed;

    public static ProtectedArtifactCommitResult NotInstalled(ProtectedArtifactStatus status, bool cleanupFailed = false)
    {
        if (!Enum.IsDefined(status) || status == ProtectedArtifactStatus.Written)
            throw new ArgumentOutOfRangeException(nameof(status));
        return new(status, false, false, cleanupFailed);
    }

    public static ProtectedArtifactCommitResult Installed(bool verificationSucceeded, bool cleanupFailed = false) =>
        new(verificationSucceeded ? ProtectedArtifactStatus.Written : ProtectedArtifactStatus.TargetChanged,
            true, verificationSucceeded, cleanupFailed);

    internal ProtectedArtifactCommitResult WithCleanupFailure() =>
        new(Status, InstallationEstablished, VerificationSucceeded, true);
}
