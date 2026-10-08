namespace Configurator.Presentation;

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
    SetupAnswerSnapshot answers) : ISetupPresentationOperation
{
    public Task<SetupPresentationOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = DefinitionArtifactComposer.Compose(definition, artifactId, answers);
        cancellationToken.ThrowIfCancellationRequested();
        var artifact = result.Artifact;
        var metadata = new DefinitionPreparationMetadata(
            result.Succeeded,
            artifactId,
            artifact?.FileName,
            artifact?.MediaType,
            artifact?.Digest,
            definition.Digest,
            answers.InputRevision,
            Array.AsReadOnly(result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray()));
        ReadOnlyMemory<byte> output = artifact is null
            ? ReadOnlyMemory<byte>.Empty
            : artifact.Bytes.ToArray();
        return Task.FromResult(new SetupPresentationOutcome(metadata, output));
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
    IReadOnlyList<string> DiagnosticCodes);
