using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Configurator.Core;


namespace Configurator.Artifacts;

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
