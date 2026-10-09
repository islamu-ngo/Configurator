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
    public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using DefinitionDraftArtifactResult? draft = prepareDraft
            ? DefinitionDraftArtifactComposer.Compose(definition, artifactId, answers) : null;
        DefinitionArtifactResult? complete = prepareDraft ? null
            : DefinitionArtifactComposer.Compose(definition, artifactId, answers);
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
