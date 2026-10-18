using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Configurator.Core;

[assembly: InternalsVisibleTo("Configurator.Artifacts.Tests")]
[assembly: InternalsVisibleTo("Configurator.Cli.Tests")]

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

public sealed class ProtectedArtifactPreparation : IDisposable
{
    private readonly Func<CancellationToken, Action, Task<ProtectedArtifactCommitResult>> _commit;
    private readonly Action _cleanup;
    private readonly ProtectedArtifactStatus? _rejectionStatus;
    private int _completed;

    internal ProtectedArtifactPreparation(
        Func<CancellationToken, Action, Task<ProtectedArtifactCommitResult>> commit,
        Action cleanup, ProtectedArtifactStatus? rejectionStatus = null)
    {
        _commit = commit;
        _cleanup = cleanup;
        _rejectionStatus = rejectionStatus;
    }

    internal static ProtectedArtifactPreparation Rejected(ProtectedArtifactStatus status) =>
        new((_, _) => Task.FromResult(ProtectedArtifactCommitResult.NotInstalled(status)), static () => { }, status);

    /// <summary>A value-free preflight refusal; null means the preparation is ready to commit.</summary>
    public ProtectedArtifactStatus? RejectionStatus => _rejectionStatus;

    /// <summary>The established commit result, retained even when subsequent cleanup throws.</summary>
    public ProtectedArtifactCommitResult? EstablishedResult { get; private set; }
    /// <summary>Cleanup failed independently of any established commit result.</summary>
    public bool CleanupFailed { get; private set; }

    public async Task<ProtectedArtifactCommitResult> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest);
        ExceptionDispatchInfo? commitFailure = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProtectedArtifactCommitResult result = await _commit(cancellationToken,
                () => EstablishedResult = ProtectedArtifactCommitResult.Installed(false)).ConfigureAwait(false);
            if (EstablishedResult is { InstallationEstablished: true } && !result.InstallationEstablished)
                throw new InvalidOperationException("installation-fact-cannot-be-retracted");
            EstablishedResult = result;
        }
        catch (Exception exception)
        {
            commitFailure = ExceptionDispatchInfo.Capture(exception);
        }
        try { _cleanup(); }
        catch
        {
            CleanupFailed = true;
            EstablishedResult = EstablishedResult?.WithCleanupFailure();
            if (commitFailure is null) throw;
        }
        commitFailure?.Throw();
        return EstablishedResult!;
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
    private readonly Action? _beforeVerification;

    public ProtectedArtifactWriter() { }

    internal ProtectedArtifactWriter(Action beforeVerification) => _beforeVerification = beforeVerification;

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
        return await UnixProtectedArtifactWriter.PrepareAsync(targetPath, bytes, cancellationToken, _beforeVerification).ConfigureAwait(false);
    }
}
