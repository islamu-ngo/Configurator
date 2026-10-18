namespace Configurator.Artifacts;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core;
using Configurator.Core.Artifacts;

public sealed record ProtectedArtifactBundleOutcome(string ArtifactId, ProtectedArtifactCommitResult? Result);

public enum ProtectedArtifactBundleCompletionCode { Completed, Cancelled, ObserverFailed }

public sealed class ProtectedArtifactBundleResult
{
    internal ProtectedArtifactBundleResult(IReadOnlyList<ProtectedArtifactBundleOutcome> outcomes,
        ProtectedArtifactBundleCompletionCode completionCode = ProtectedArtifactBundleCompletionCode.Completed,
        bool cleanupFailed = false)
    {
        Outcomes = Array.AsReadOnly(outcomes.ToArray());
        CompletionCode = completionCode;
        CleanupFailed = cleanupFailed;
    }
    public IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes { get; }
    public ProtectedArtifactBundleCompletionCode CompletionCode { get; }
    /// <summary>Cleanup failed independently of the established file outcomes and completion reason.</summary>
    public bool CleanupFailed { get; }
    public bool Succeeded => CompletionCode == ProtectedArtifactBundleCompletionCode.Completed && !CleanupFailed
        && Outcomes.Count > 0 && Outcomes.All(outcome => outcome.Result is { Succeeded: true });
    public override string ToString() =>
        $"{nameof(ProtectedArtifactBundleResult)}:Completion={CompletionCode}:CleanupFailed={CleanupFailed}:Succeeded={Succeeded}:Count={Outcomes.Count}";
}

public static class ProtectedArtifactBundlePublisher
{
    /// <summary>Prepares every destination before create-only commits; observes established outcomes before cancellation and retains them across observer or cleanup failure.</summary>
    public static async Task<ProtectedArtifactBundleResult> PublishAsync(IProtectedArtifactWriter writer,
        IReadOnlyList<DefinitionArtifact> artifacts, string directory,
        Action<ProtectedArtifactBundleOutcome>? outcomeObserver = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(directory);
        if (artifacts.Count is < 1 or > 64) return new([]);
        DefinitionArtifact[] selected = artifacts.ToArray();
        if (selected.Any(item => item is null)) return new([]);

        ProtectedArtifactBundleOutcome[] outcomes = selected
            .Select(item => new ProtectedArtifactBundleOutcome(item.ArtifactId, null)).ToArray();
        ProtectedArtifactBundleCompletionCode completionCode = ProtectedArtifactBundleCompletionCode.Completed;
        bool cleanupFailed = false;
        ProtectedArtifactBundleResult Result() => new(outcomes, completionCode, cleanupFailed);
        void RecordOutcome(int index, ProtectedArtifactCommitResult result)
        {
            outcomes[index] = outcomes[index] with { Result = result };
            if (completionCode == ProtectedArtifactBundleCompletionCode.ObserverFailed) return;
            try { outcomeObserver?.Invoke(outcomes[index]); }
            catch (Exception) { completionCode = ProtectedArtifactBundleCompletionCode.ObserverFailed; }
        }
        void FailAll(ProtectedArtifactStatus status)
        {
            for (int index = 0; index < outcomes.Length; index++)
                RecordOutcome(index, ProtectedArtifactCommitResult.NotInstalled(status));
        }

        DefinitionArtifact first = selected[0];
        if (string.IsNullOrWhiteSpace(directory)
            || selected.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() != selected.Length
            || selected.Select(item => item.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length
            || selected.Any(item => item.Sensitivity != SetupArtifactSensitivity.Restricted
                || item.DefinitionDigest != first.DefinitionDigest
                || item.DefinitionRevision != first.DefinitionRevision
                || item.InputRevision != first.InputRevision))
        {
            FailAll(ProtectedArtifactStatus.InvalidRequest);
            return Result();
        }
        if (!writer.IsAvailable)
        {
            FailAll(ProtectedArtifactStatus.UnsupportedPlatform);
            return Result();
        }

        var prepared = new List<ProtectedArtifactPreparation>(selected.Length);
        var payloads = new List<byte[]>(selected.Length);
        try
        {
            await PublishPreparedAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                foreach (ProtectedArtifactPreparation preparation in prepared)
                    try { preparation.Dispose(); }
                    catch (Exception) { cleanupFailed = true; }
            }
            finally
            {
                foreach (byte[] payload in payloads) CryptographicOperations.ZeroMemory(payload);
            }
        }
        return Result();

        async Task PublishPreparedAsync()
        {
            for (int index = 0; index < selected.Length; index++)
            {
                if (Cancelled()) return;
                DefinitionArtifact artifact = selected[index];
                string target;
                try { target = Path.Combine(directory, artifact.FileName); }
                catch (ArgumentException)
                {
                    RecordOutcome(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest));
                    return;
                }
                ProtectedArtifactPreparation preparation;
                try
                {
                    ReadOnlyMemory<byte> payload = artifact.Bytes;
                    if (!MemoryMarshal.TryGetArray(payload, out ArraySegment<byte> segment) || segment.Array is null)
                        throw new InvalidOperationException("artifact-payload-not-array");
                    payloads.Add(segment.Array);
                    preparation = await writer.PrepareAsync(SetupArtifactKind.Configuration,
                        target, payload, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completionCode = ProtectedArtifactBundleCompletionCode.Cancelled;
                    return;
                }
                catch (ObjectDisposedException)
                {
                    RecordOutcome(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.InvalidRequest));
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    RecordOutcome(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.PermissionDenied));
                    return;
                }
                catch (IOException)
                {
                    RecordOutcome(index, ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure));
                    return;
                }
                prepared.Add(preparation);
                if (preparation.RejectionStatus is { } rejection)
                {
                    RecordOutcome(index, ProtectedArtifactCommitResult.NotInstalled(rejection));
                    return;
                }
            }

            for (int index = 0; index < prepared.Count; index++)
            {
                if (Cancelled()) return;
                ProtectedArtifactPreparation preparation = prepared[index];
                ProtectedArtifactCommitResult result;
                try
                {
                    result = await preparation.CommitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception) when (preparation.EstablishedResult is not null)
                {
                    result = preparation.EstablishedResult;
                    if (cancellationToken.IsCancellationRequested)
                        completionCode = ProtectedArtifactBundleCompletionCode.Cancelled;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completionCode = ProtectedArtifactBundleCompletionCode.Cancelled;
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    result = ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.PermissionDenied);
                }
                catch (IOException)
                {
                    result = ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure);
                }
                finally { cleanupFailed |= preparation.CleanupFailed; }
                cleanupFailed |= result.CleanupFailed;
                RecordOutcome(index, result);
                if (!result.Succeeded || cleanupFailed || completionCode == ProtectedArtifactBundleCompletionCode.Cancelled
                    || completionCode == ProtectedArtifactBundleCompletionCode.ObserverFailed) return;
            }
        }

        bool Cancelled()
        {
            if (!cancellationToken.IsCancellationRequested) return false;
            completionCode = ProtectedArtifactBundleCompletionCode.Cancelled;
            return true;
        }
    }
}
