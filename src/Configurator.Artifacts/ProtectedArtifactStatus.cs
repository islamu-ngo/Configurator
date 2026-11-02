using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Configurator.Core;


namespace Configurator.Artifacts;

public enum ProtectedArtifactStatus
{
    Written,
    InvalidRequest,
    TargetExists,
    UnsafeTarget,
    TargetChanged,
    PermissionDenied,
    IoFailure,
    UnsupportedPlatform
}
