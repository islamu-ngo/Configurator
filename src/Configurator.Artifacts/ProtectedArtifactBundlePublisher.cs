namespace Configurator.Artifacts;

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core;
using Configurator.Core.Artifacts;

public sealed record ProtectedArtifactBundleOutcome(string ArtifactId, ProtectedArtifactStatus? Status);

public sealed class ProtectedArtifactBundleResult
{
    internal ProtectedArtifactBundleResult(IReadOnlyList<ProtectedArtifactBundleOutcome> outcomes) =>
        Outcomes = Array.AsReadOnly(outcomes.ToArray());
    public IReadOnlyList<ProtectedArtifactBundleOutcome> Outcomes { get; }
    public bool Succeeded => Outcomes.Count > 0 && Outcomes.All(outcome => outcome.Status == ProtectedArtifactStatus.Written);
    public override string ToString() => $"{nameof(ProtectedArtifactBundleResult)}:Succeeded={Succeeded}:Count={Outcomes.Count}";
}

public static class ProtectedArtifactBundlePublisher
{
    /// <summary>Prepares every destination before create-only commits; a later failure preserves the committed prefix.</summary>
    public static async Task<ProtectedArtifactBundleResult> PublishAsync(IProtectedArtifactWriter writer,
        IReadOnlyList<DefinitionArtifact> artifacts, string directory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(directory);
        if (artifacts.Count is < 1 or > 64) return new([]);
        DefinitionArtifact[] selected = artifacts.ToArray();
        if (selected.Any(item => item is null)) return new([]);

        ProtectedArtifactBundleOutcome[] outcomes = selected
            .Select(item => new ProtectedArtifactBundleOutcome(item.ArtifactId, null)).ToArray();
        ProtectedArtifactBundleResult Result() => new(outcomes);
        void FailAll(ProtectedArtifactStatus status)
        {
            for (int index = 0; index < outcomes.Length; index++)
                outcomes[index] = outcomes[index] with { Status = status };
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
            for (int index = 0; index < selected.Length; index++)
            {
                if (cancellationToken.IsCancellationRequested) return Result();
                DefinitionArtifact artifact = selected[index];
                string target;
                try { target = Path.Combine(directory, artifact.FileName); }
                catch (ArgumentException)
                {
                    outcomes[index] = outcomes[index] with { Status = ProtectedArtifactStatus.InvalidRequest };
                    return Result();
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
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Result(); }
                catch (ObjectDisposedException)
                {
                    outcomes[index] = outcomes[index] with { Status = ProtectedArtifactStatus.InvalidRequest };
                    return Result();
                }
                catch (UnauthorizedAccessException)
                {
                    outcomes[index] = outcomes[index] with { Status = ProtectedArtifactStatus.PermissionDenied };
                    return Result();
                }
                catch (IOException)
                {
                    outcomes[index] = outcomes[index] with { Status = ProtectedArtifactStatus.IoFailure };
                    return Result();
                }
                prepared.Add(preparation);
                if (preparation.RejectionStatus is { } rejection)
                {
                    outcomes[index] = outcomes[index] with { Status = rejection };
                    return Result();
                }
            }

            for (int index = 0; index < prepared.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    ProtectedArtifactStatus status = await prepared[index].CommitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    outcomes[index] = outcomes[index] with { Status = status };
                    if (status != ProtectedArtifactStatus.Written) break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (UnauthorizedAccessException)
                {
                    outcomes[index] = outcomes[index] with { Status = ProtectedArtifactStatus.PermissionDenied };
                    break;
                }
                catch (IOException)
                {
                    outcomes[index] = outcomes[index] with { Status = ProtectedArtifactStatus.IoFailure };
                    break;
                }
            }
            return Result();
        }
        finally
        {
            Exception? cleanupFailure = null;
            try
            {
                foreach (ProtectedArtifactPreparation preparation in prepared)
                    try { preparation.Dispose(); }
                    catch (Exception exception) { cleanupFailure ??= exception; }
            }
            finally
            {
                foreach (byte[] payload in payloads) CryptographicOperations.ZeroMemory(payload);
            }
            if (cleanupFailure is not null) ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }
}
