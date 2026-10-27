namespace Configurator.Presentation;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Configurator.Core;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

/// <summary>
/// Captures admitted definition and answer snapshots for a generation-fenced
/// workspace. Published metadata never retains the composed artifact bytes.
/// </summary>
public sealed class DefinitionPresentationOperation(
    SetupDefinition definition,
    string artifactId,
    SetupAnswerSnapshot answers,
    bool prepareDraft = false) : ISetupPresentationOperation
{
    /// <summary>Copies an admitted artifact; the caller owns this operation even when execution never starts.</summary>
    public static DefinitionPreparedAdoption Adopt(DefinitionArtifact artifact,
        IEnumerable<DefinitionDraftCompletion>? completions = null) => new(artifact, completions);

    public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using DefinitionDraftArtifactResult? draft = prepareDraft
            ? DefinitionDraftArtifactComposer.Compose(definition, artifactId, answers, cancellationToken) : null;
        DefinitionArtifactResult? complete = prepareDraft ? null
            : DefinitionArtifactComposer.Compose(definition, artifactId, answers, cancellationToken);
        using DefinitionArtifact? artifact = draft?.Artifact ?? complete?.Artifact;
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = new DefinitionPreparationMetadata(
            draft?.Succeeded ?? complete!.Succeeded,
            artifactId,
            artifact?.FileName,
            artifact?.MediaType,
            artifact?.Digest,
            definition.Digest,
            answers.InputRevision,
            Array.AsReadOnly((draft?.Diagnostics ?? complete!.Diagnostics).Select(diagnostic => diagnostic.Code).ToArray()))
        {
            IsDraft = draft?.IsDraft ?? false,
            Completions = draft?.Completions ?? []
        };
        ReadOnlyMemory<byte> source = artifact is null
            ? ReadOnlyMemory<byte>.Empty
            : artifact.Bytes;
        try { return Task.FromResult(new SetupPresentationOutcome(metadata, source)); }
        finally
        {
            if (MemoryMarshal.TryGetArray(source, out ArraySegment<byte> segment))
                CryptographicOperations.ZeroMemory(segment.AsSpan());
        }
    }
}

/// <summary>Owns staged output until execution transfers it to a generation-fenced workspace.</summary>
public sealed class DefinitionPreparedAdoption : ISetupPresentationOperation, IDisposable
{
    private SetupPresentationOutcome? _prepared;

    internal DefinitionPreparedAdoption(DefinitionArtifact artifact,
        IEnumerable<DefinitionDraftCompletion>? completions = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        DefinitionDraftCompletion[] locations = completions?.Where(item => item.ArtifactId == artifact.ArtifactId).ToArray() ?? [];
        var metadata = new DefinitionPreparationMetadata(true, artifact.ArtifactId, artifact.FileName,
            artifact.MediaType, artifact.Digest, artifact.DefinitionDigest, artifact.InputRevision, [])
        {
            IsDraft = locations.Length > 0,
            Completions = Array.AsReadOnly(locations)
        };
        ReadOnlyMemory<byte> source = artifact.Bytes;
        try
        {
            _prepared = new(metadata, source);
        }
        finally
        {
            if (MemoryMarshal.TryGetArray(source, out ArraySegment<byte> segment))
                CryptographicOperations.ZeroMemory(segment.AsSpan());
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _prepared, null)?.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        SetupPresentationOutcome? outcome = Interlocked.Exchange(ref _prepared, null);
        ObjectDisposedException.ThrowIf(outcome is null, this);
        return Task.FromResult(outcome);
    }
}

public sealed record DefinitionPreparationMetadata(
    bool Succeeded,
    string ArtifactId,
    string? FileName,
    string? MediaType,
    ArtifactDigest? Digest,
    ArtifactDigest DefinitionDigest,
    long InputRevision,
    IReadOnlyList<string> DiagnosticCodes)
{
    public bool IsDraft { get; init; }
    public IReadOnlyList<DefinitionDraftCompletion> Completions { get; init; } = [];
}
