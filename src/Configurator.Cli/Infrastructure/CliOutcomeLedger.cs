using Configurator.Artifacts;

namespace Configurator.Cli;

/// <summary>Retains established publication facts independently of later work and receipt delivery.</summary>
internal sealed class CliOutcomeLedger
{
    private CliCommandResult? _planned;

    internal void Plan(CliInvocation invocation, CliCommand command, CliCommandResult result)
    {
        if (_planned is not null) throw new InvalidOperationException("publication-plan-sealed");
        CliCommandResult reserved = result with
        {
            Artifacts = result.Artifacts.Select(artifact => artifact with { WriteStatus = "not-attempted" }).ToArray(),
            Cleanup = "unknown"
        };
        CliResultPlan.EnsureCapacity(invocation, command, reserved);
        _planned = reserved;
    }

    internal void Record(int index, string status)
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        CliMachineArtifact[] artifacts = _planned.Artifacts.ToArray();
        CliMachineArtifact artifact = artifacts[index];
        if (status == "unknown" && artifact.Installation == "established") return;
        artifacts[index] = artifact with
        {
            WriteStatus = status,
            Installation = status == "unknown" && artifact.Installation != "not-applicable" ? "unknown" : artifact.Installation,
            Verification = status == "unknown" && artifact.Verification != "not-applicable" ? "unknown" : artifact.Verification
        };
        _planned = _planned with { Artifacts = artifacts };
    }

    internal void Record(string artifactId, CliPublicationOutcome outcome)
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        int index = Array.FindIndex(_planned.Artifacts.ToArray(), artifact => artifact.Id == artifactId);
        if (index < 0) throw new InvalidOperationException("publication-artifact-unknown");
        Record(index, outcome);
    }

    private void Record(int index, CliPublicationOutcome outcome)
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        if (outcome.Result?.CleanupFailed == true) Cleanup("failed");
        CliMachineArtifact[] artifacts = _planned.Artifacts.ToArray();
        CliMachineArtifact artifact = artifacts[index];
        if (artifact.Installation == "established" && outcome.Result is not { InstallationEstablished: true }) return;
        ProtectedArtifactCommitResult? result = outcome.Result;
        artifacts[index] = artifact with
        {
            WriteStatus = outcome.DeliveredToStandardOutput || result?.InstallationEstablished == true ? "written"
                : outcome.Unknown ? "unknown" : result?.Status switch
                {
                    ProtectedArtifactStatus.TargetExists or ProtectedArtifactStatus.TargetChanged => "conflict",
                    ProtectedArtifactStatus.IoFailure => "failed",
                    null => "not-attempted",
                    ProtectedArtifactStatus.UnsupportedPlatform or ProtectedArtifactStatus.UnsafeTarget
                        or ProtectedArtifactStatus.PermissionDenied or ProtectedArtifactStatus.InvalidRequest => "blocked",
                    _ => "unknown"
                },
            Installation = outcome.DeliveredToStandardOutput ? "not-applicable"
                : result is not null ? result.InstallationEstablished ? "established" : "not-established"
                : outcome.Unknown ? "unknown" : "not-attempted",
            Verification = outcome.DeliveredToStandardOutput ? "not-applicable"
                : result is not null ? result.InstallationEstablished ? result.VerificationSucceeded ? "succeeded" : "failed" : "not-attempted"
                : outcome.Unknown ? "unknown" : "not-attempted"
        };
        _planned = _planned with { Artifacts = artifacts };
    }

    internal void Cleanup(string status)
    {
        if (_planned is not null && _planned.Cleanup != "failed") _planned = _planned with { Cleanup = status };
    }

    internal void BeginPublication()
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        for (int index = 0; index < _planned.Artifacts.Count; index++) Record(index, "unknown");
    }

    internal CliCommandResult Apply(CliPublicationResult publication, CliCommandResult result, bool blockedRejections = false)
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        if (publication.Outcomes.Count != _planned.Artifacts.Count)
        {
            for (int index = 0; index < _planned.Artifacts.Count; index++) Record(index, "unknown");
            Cleanup("unknown");
            return Preserve(CliResults.Failure(CliExitCode.Internal, "publication-status-invalid"));
        }
        for (int index = 0; index < publication.Outcomes.Count; index++)
            Record(index, publication.Outcomes[index]);
        bool cleanupFailed = publication.CleanupFailed || publication.Outcomes.Any(item => item.Result?.CleanupFailed == true);
        Cleanup(cleanupFailed ? "failed" : "completed");
        bool failed = publication.Outcomes.Any(item => item.Unknown || item.Result is { Succeeded: false })
            || !publication.Cancelled && publication.Outcomes.Any(item => item.Result is null && !item.DeliveredToStandardOutput);
        if (cleanupFailed || failed)
            result = result with
            {
                Exit = !cleanupFailed && blockedRejections
                    && publication.Outcomes.Any(item => item.Result is { InstallationEstablished: false })
                    && publication.Outcomes.All(item => !item.Unknown && (item.Result is null or { Succeeded: true }
                        or
                    {
                        InstallationEstablished: false, Status: ProtectedArtifactStatus.UnsupportedPlatform or ProtectedArtifactStatus.TargetExists
                            or ProtectedArtifactStatus.TargetChanged or ProtectedArtifactStatus.PermissionDenied
                            or ProtectedArtifactStatus.UnsafeTarget or ProtectedArtifactStatus.InvalidRequest
                    }))
                    ? CliExitCode.Blocked : CliExitCode.Io,
                Completion = "failed",
                Diagnostics = [new(cleanupFailed ? "publication-cleanup-failed" : "publication-failed", "/output", "error")]
            };
        else if (publication.Cancelled)
            result = result with
            {
                Exit = CliExitCode.Cancelled,
                Completion = "cancelled",
                Diagnostics = [new("cancelled", "", "error")]
            };
        return Preserve(result);
    }

    internal CliCommandResult Preserve(CliCommandResult result)
    {
        if (_planned is null) return result;
        bool cleanupFailed = _planned.Cleanup == "failed";
        bool verificationFailed = _planned.Artifacts.Any(artifact => artifact.Verification == "failed");
        if ((cleanupFailed || verificationFailed) && result.Exit is CliExitCode.Success or CliExitCode.Cancelled)
            result = result with
            {
                Exit = CliExitCode.Io,
                Completion = "failed",
                Diagnostics = [new(cleanupFailed ? "publication-cleanup-failed" : "publication-failed", "/output", "error")]
            };
        return result with
        {
            Artifacts = _planned.Artifacts,
            Cleanup = _planned.Cleanup,
            DefinitionDigest = _planned.DefinitionDigest,
            Coverage = _planned.Coverage,
            Readiness = _planned.Readiness,
            Preparation = _planned.Preparation
        };
    }

}
