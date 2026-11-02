namespace Configurator.Artifacts;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core;
using Configurator.Core.Artifacts;

public sealed record ProtectedArtifactBundleOutcome(string ArtifactId, ProtectedArtifactCommitResult? Result);
