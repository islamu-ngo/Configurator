using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Configurator.Core;

[assembly: InternalsVisibleTo("Configurator.Artifacts.Tests")]
[assembly: InternalsVisibleTo("Configurator.Cli.Tests")]

namespace Configurator.Artifacts;

public sealed class ProtectedArtifactWriter : IProtectedArtifactWriter
{
    private readonly Action? _beforeVerification;

    public ProtectedArtifactWriter() { }

    internal ProtectedArtifactWriter(Action beforeVerification) => _beforeVerification = beforeVerification;

    public const int MaximumBytes = 4 * 1024 * 1024;

    public bool IsAvailable => OperatingSystem.IsLinux();

    public static void WritePublic(Stream destination, ArtifactKind kind, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (bytes.Length > MaximumBytes || !ArtifactPolicy.TryProjectPublic(kind, bytes, out var projection))
            throw new IOException("protected-output-required");
        destination.Write(projection.Span);
        destination.Flush();
    }

    public async Task<ProtectedArtifactPreparation> PrepareAsync(
        ArtifactKind kind,
        string targetPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
            return WindowsProtectedArtifactWriter.Unavailable();
        if (!OperatingSystem.IsLinux())
            return ProtectedArtifactPreparation.Rejected(ProtectedArtifactStatus.UnsupportedPlatform);
        if (targetPath == "-")
            return ProtectedArtifactPreparation.Rejected(ProtectedArtifactStatus.UnsafeTarget);
        if (string.IsNullOrWhiteSpace(targetPath) || bytes.IsEmpty || bytes.Length > MaximumBytes)
            return ProtectedArtifactPreparation.Rejected(ProtectedArtifactStatus.InvalidRequest);

        // Every file, including an unknown kind, uses the same owner-only boundary.
        return await UnixProtectedArtifactWriter.PrepareAsync(targetPath, bytes, cancellationToken, _beforeVerification).ConfigureAwait(false);
    }
}
