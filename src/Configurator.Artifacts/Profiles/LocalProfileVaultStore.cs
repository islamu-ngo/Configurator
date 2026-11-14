namespace Configurator.Artifacts.Profiles;

using System.Security.Cryptography;
using Configurator.Artifacts.Composition;
using Configurator.Core;
using Configurator.Core.Profiles;

/// <summary>Immutable opaque revision files, each published through the protected writer's commit barrier.</summary>
public sealed class LocalProfileVaultStore : IProfileVaultStore
{
    private readonly string _root;
    private readonly IProtectedArtifactWriter _writer;
    private readonly Action? _afterFileObservation;

    public LocalProfileVaultStore(string root, IProtectedArtifactWriter? writer = null) : this(root, writer, null) { }

    internal LocalProfileVaultStore(string root, IProtectedArtifactWriter? writer, Action? afterFileObservation)
    {
        try
        {
            if (root is not { Length: > 0 and <= 4096 } || !Path.IsPathFullyQualified(root))
                throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
            _root = Path.GetFullPath(root);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
        }
        _writer = writer ?? new ProtectedArtifactWriter();
        _afterFileObservation = afterFileObservation;
    }

    public async ValueTask<IReadOnlyList<ProfileRevisionDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() || !_writer.IsAvailable)
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsupportedPlatform);
        return await Task.Run(() =>
        {
            try
            {
                ProfileFileReader.EnsureRoot(_root);
                return ProfileFileReader.List(_root, cancellationToken, _afterFileObservation);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CompositionException)
            {
                throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsafeTarget);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<byte[]> ReadAsync(ProfileRevisionDescriptor revision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (!OperatingSystem.IsLinux() || !_writer.IsAvailable)
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsupportedPlatform);
        try
        {
            return await Task.Run(async () =>
                await ProfileFileReader.ReadAsync(_root, revision, cancellationToken, _afterFileObservation).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CompositionException)
        {
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.UnsafeTarget);
        }
    }

    public async Task<ProtectedArtifactBundleResult> SaveAsync(Guid profileId, Guid snapshotId, ProfileFileKind kind,
        ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        string name = ProfileRevisionDescriptor.NameOf(profileId, snapshotId, kind);
        ProtectedArtifactCommitResult? installed = null;
        var completion = ProtectedArtifactBundleCompletionCode.Completed;
        bool cleanupFailed = false;
        ProtectedArtifactBundleResult Result() => new([new(name, installed)], completion, cleanupFailed);
        if (!OperatingSystem.IsLinux() || !_writer.IsAvailable)
        {
            installed = ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsupportedPlatform);
            return Result();
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length is 0 or > ProfileContainerPolicy.MaximumContainerBytes)
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
        byte[] owned = bytes.ToArray();
        ProtectedArtifactPreparation? preparation = null;
        try
        {
            if (kind == ProfileFileKind.Encrypted) _ = ProfileContainerPolicy.Admit(owned);
            else _ = CompareBaselineCodec.ParseJson(owned);
            var rootIdentity = await Task.Run(() => ProfileFileReader.EnsureRoot(_root), cancellationToken).ConfigureAwait(false);
            preparation = await _writer.PrepareAsync(ArtifactKind.Configuration, Path.Combine(_root, name),
                owned, cancellationToken).ConfigureAwait(false);
            if (!await Task.Run(() => ProfileFileReader.RootMatches(_root, rootIdentity), cancellationToken).ConfigureAwait(false))
                throw new ProfileVaultStoreException(ProtectedArtifactStatus.TargetChanged);
            installed = await preparation.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (installed.InstallationEstablished &&
                !await Task.Run(() => ProfileFileReader.RootMatches(_root, rootIdentity)).ConfigureAwait(false))
                installed = ProtectedArtifactCommitResult.Installed(false, installed.CleanupFailed);
            if (cancellationToken.IsCancellationRequested) completion = ProtectedArtifactBundleCompletionCode.Cancelled;
        }
        catch (Exception) when (preparation?.EstablishedResult is not null)
        {
            installed = preparation.EstablishedResult;
            if (cancellationToken.IsCancellationRequested) completion = ProtectedArtifactBundleCompletionCode.Cancelled;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            completion = ProtectedArtifactBundleCompletionCode.Cancelled;
        }
        catch (ProfileVaultStoreException exception)
        {
            installed = ProtectedArtifactCommitResult.NotInstalled(exception.Status);
        }
        catch (UnauthorizedAccessException)
        {
            installed = ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.PermissionDenied);
        }
        catch (Exception exception) when (exception is IOException or CompositionException)
        {
            installed = ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure);
        }
        finally
        {
            try { preparation?.Dispose(); }
            catch (Exception) { cleanupFailed = true; }
            cleanupFailed |= preparation?.CleanupFailed == true;
            CryptographicOperations.ZeroMemory(owned);
        }
        return Result();
    }

}
