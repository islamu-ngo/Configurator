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
        bool failed = publication.Outcomes.Any(item => item.Unknown || item.Status is not ProtectedArtifactStatus.Written);
        if (publication.CleanupFailed || failed)
            result = result with { Exit = !publication.CleanupFailed && publication.Outcomes.Any(item => item.Status is ProtectedArtifactStatus.UnsupportedPlatform
                    or ProtectedArtifactStatus.TargetExists or ProtectedArtifactStatus.TargetChanged or ProtectedArtifactStatus.PermissionDenied
                    or ProtectedArtifactStatus.UnsafeTarget or ProtectedArtifactStatus.InvalidRequest) && blockedRejections
                    ? SetupCliExitCode.Blocked : SetupCliExitCode.Io,
                Diagnostics = [new(publication.CleanupFailed ? "publication-cleanup-failed" : "publication-failed", "/output", "error")] };
        return Preserve(result);
    }

    internal SetupCliCommandResult Preserve(SetupCliCommandResult result) => _planned is null ? result : result with
    { Artifacts = _planned.Artifacts, Cleanup = _planned.Cleanup, DefinitionDigest = _planned.DefinitionDigest,
        Coverage = _planned.Coverage, Readiness = _planned.Readiness, Preparation = _planned.Preparation };

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
