using Configurator.Artifacts;

namespace Configurator.Cli;

/// <summary>Retains established publication facts independently of later work and receipt delivery.</summary>
internal sealed class SetupCliOutcomeLedger
{
    private SetupCliCommandResult? _planned;

    internal void Plan(SetupCliInvocation invocation, SetupCliCommand command, SetupCliCommandResult result)
    {
        if (_planned is not null) throw new InvalidOperationException("publication-plan-sealed");
        SetupCliCommandResult reserved = result with
        {
            Artifacts = result.Artifacts.Select(artifact => artifact with { WriteStatus = "not-attempted" }).ToArray(),
            Cleanup = "unknown"
        };
        SetupCliResultPlan.EnsureCapacity(invocation, command, reserved);
        _planned = reserved;
    }

    internal void Record(int index, string status)
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        SetupCliMachineArtifact[] artifacts = _planned.Artifacts.ToArray();
        artifacts[index] = artifacts[index] with { WriteStatus = status };
        _planned = _planned with { Artifacts = artifacts };
    }

    internal void Record(string artifactId, SetupCliPublicationOutcome outcome)
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        int index = Array.FindIndex(_planned.Artifacts.ToArray(), artifact => artifact.Id == artifactId);
        if (index < 0) throw new InvalidOperationException("publication-artifact-unknown");
        Record(index, outcome.Unknown ? "unknown" : Status(outcome.Status));
    }

    internal void Cleanup(string status)
    {
        if (_planned is not null) _planned = _planned with { Cleanup = status };
    }

    internal void BeginPublication()
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        for (int index = 0; index < _planned.Artifacts.Count; index++) Record(index, "unknown");
    }

    internal SetupCliCommandResult Apply(SetupCliPublicationResult publication, SetupCliCommandResult result, bool blockedRejections = false)
    {
        if (_planned is null) throw new InvalidOperationException("publication-plan-missing");
        if (publication.Outcomes.Count != _planned.Artifacts.Count)
        {
            for (int index = 0; index < _planned.Artifacts.Count; index++) Record(index, "unknown");
            Cleanup("unknown");
            return Preserve(SetupCliResults.Failure(SetupCliExitCode.Internal, "publication-status-invalid"));
        }
        for (int index = 0; index < publication.Outcomes.Count; index++)
            Record(index, publication.Outcomes[index].Unknown ? "unknown" : Status(publication.Outcomes[index].Status));
        Cleanup(publication.CleanupFailed ? "failed" : "completed");
        bool failed = publication.Outcomes.Any(item => item.Unknown
            || item.Status is not null and not ProtectedArtifactStatus.Written)
            || !publication.Cancelled && publication.Outcomes.Any(item => item.Status is null);
        if (publication.CleanupFailed || failed)
            result = result with
            {
                Exit = !publication.CleanupFailed && blockedRejections
                    && publication.Outcomes.Any(item => item.Status is not null and not ProtectedArtifactStatus.Written)
                    && publication.Outcomes.All(item => !item.Unknown && item.Status is null or ProtectedArtifactStatus.Written
                        or ProtectedArtifactStatus.UnsupportedPlatform or ProtectedArtifactStatus.TargetExists or ProtectedArtifactStatus.TargetChanged
                        or ProtectedArtifactStatus.PermissionDenied or ProtectedArtifactStatus.UnsafeTarget or ProtectedArtifactStatus.InvalidRequest)
                    ? SetupCliExitCode.Blocked : SetupCliExitCode.Io,
                Diagnostics = [new(publication.CleanupFailed ? "publication-cleanup-failed" : "publication-failed", "/output", "error")]
            };
        else if (publication.Cancelled)
            result = result with
            {
                Exit = SetupCliExitCode.Cancelled,
                Completion = "cancelled",
                Diagnostics = [new("cancelled", "", "error")]
            };
        return Preserve(result);
    }

    internal SetupCliCommandResult Preserve(SetupCliCommandResult result)
    {
        if (_planned is null) return result;
        if (_planned.Cleanup == "failed" && result.Exit is SetupCliExitCode.Success or SetupCliExitCode.Cancelled)
            result = result with
            {
                Exit = SetupCliExitCode.Io,
                Completion = "failed",
                Diagnostics = [new("publication-cleanup-failed", "/output", "error")]
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

    internal static string Status(ProtectedArtifactStatus? status) => status switch
    {
        ProtectedArtifactStatus.Written => "written",
        ProtectedArtifactStatus.TargetExists or ProtectedArtifactStatus.TargetChanged => "conflict",
        ProtectedArtifactStatus.IoFailure => "failed",
        null => "not-attempted",
        ProtectedArtifactStatus.UnsupportedPlatform or ProtectedArtifactStatus.UnsafeTarget
            or ProtectedArtifactStatus.PermissionDenied or ProtectedArtifactStatus.InvalidRequest => "blocked",
        _ => "unknown"
    };
}
