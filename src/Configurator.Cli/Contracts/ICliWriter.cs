using Configurator.Artifacts;
using Configurator.Core;
using Configurator.Core.Artifacts;

namespace Configurator.Cli;

public interface ICliWriter
{
    ValueTask<ProtectedArtifactStatus?> CheckArtifactDestinationAsync(string path, CancellationToken cancellationToken);

    ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken);

    ValueTask<CliPublicationResult> WriteArtifactAsync(string path, ArtifactKind kind,
        ReadOnlyMemory<byte> bytes, int maximumBytes, CancellationToken cancellationToken);

    /// <summary>Reports established artifact outcomes synchronously before later work can fail; the result supplies final cleanup and cancellation facts.</summary>
    ValueTask<CliPublicationResult> WriteBundleAsync(
        IReadOnlyList<DefinitionArtifact> artifacts, string directory, int maximumBytes,
        Action<string, CliPublicationOutcome> outcomeObserver, CancellationToken cancellationToken);
}

