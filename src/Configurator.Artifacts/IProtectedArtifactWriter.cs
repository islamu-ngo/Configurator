using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Configurator.Core;


namespace Configurator.Artifacts;

public interface IProtectedArtifactWriter
{
    bool IsAvailable { get; }

    Task<ProtectedArtifactPreparation> PrepareAsync(
        ArtifactKind kind,
        string targetPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default);
}
