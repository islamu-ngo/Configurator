using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Configurator.Core;

[assembly: InternalsVisibleTo("Configurator.Artifacts.Tests")]

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

public sealed class ProtectedArtifactPreparation : IDisposable
{
    private readonly Func<CancellationToken, Task<ProtectedArtifactStatus>> _commit;
    private readonly Action _cleanup;
    private readonly ProtectedArtifactStatus? _rejectionStatus;
    private int _completed;

    internal ProtectedArtifactPreparation(
        Func<CancellationToken, Task<ProtectedArtifactStatus>> commit,
        Action cleanup, ProtectedArtifactStatus? rejectionStatus = null)
    {
        _commit = commit;
        _cleanup = cleanup;
        _rejectionStatus = rejectionStatus;
    }

    internal static ProtectedArtifactPreparation Rejected(ProtectedArtifactStatus status) =>
        new(_ => Task.FromResult(status), static () => { }, status);

    /// <summary>A value-free preflight refusal; null means the preparation is ready to commit.</summary>
    public ProtectedArtifactStatus? RejectionStatus => _rejectionStatus;

    /// <summary>The established commit result, retained even when subsequent cleanup throws.</summary>
    public ProtectedArtifactStatus? EstablishedStatus { get; private set; }
    /// <summary>Cleanup failed independently of any established commit result.</summary>
    public bool CleanupFailed { get; private set; }

    public async Task<ProtectedArtifactStatus> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return ProtectedArtifactStatus.InvalidRequest;
        ExceptionDispatchInfo? commitFailure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            EstablishedStatus = await _commit(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            commitFailure = ExceptionDispatchInfo.Capture(exception);
        }
        try { _cleanup(); }
        catch
        {
            CleanupFailed = true;
            if (commitFailure is null) throw;
        }
        commitFailure?.Throw();
        return EstablishedStatus!.Value;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
            _cleanup();
    }
}

public interface IProtectedArtifactWriter
{
    bool IsAvailable { get; }

    Task<ProtectedArtifactPreparation> PrepareAsync(
        SetupArtifactKind kind,
        string targetPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default);
}

public sealed class ProtectedArtifactWriter : IProtectedArtifactWriter
{
    public const int MaximumBytes = 4 * 1024 * 1024;

    public bool IsAvailable => OperatingSystem.IsLinux();

    public static void WritePublic(Stream destination, SetupArtifactKind kind, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (bytes.Length > MaximumBytes || !SetupArtifactPolicy.TryProjectPublic(kind, bytes, out var projection))
            throw new IOException("protected-output-required");
        destination.Write(projection.Span);
        destination.Flush();
    }

    public async Task<ProtectedArtifactPreparation> PrepareAsync(
        SetupArtifactKind kind,
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
        return await UnixProtectedArtifactWriter.PrepareAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
    }
}
