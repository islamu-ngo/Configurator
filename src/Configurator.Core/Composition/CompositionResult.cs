namespace Configurator.Core.Composition;

using System.Text.Json;

public sealed class CompositionResult
{
    private readonly byte[]? _serializedBytes;

    private CompositionResult(
        JsonElement? artifact,
        byte[]? serializedBytes,
        ArtifactDigest digest,
        CompositionFailure failure)
    {
        Artifact = artifact?.Clone();
        _serializedBytes = serializedBytes is null ? null : (byte[])serializedBytes.Clone();
        Digest = digest;
        Failure = failure;
    }

    public bool Succeeded => Artifact is not null && Failure.Code == CompositionFailureCode.None;
    public CompositionArtifactKind ArtifactKind => CompositionArtifactKind.Document;
    public JsonElement? Artifact { get; }
    public ReadOnlyMemory<byte> SerializedBytes =>
        _serializedBytes is null ? ReadOnlyMemory<byte>.Empty : new((byte[])_serializedBytes.Clone());
    public ArtifactDigest Digest { get; }
    public CompositionFailure Failure { get; }

    internal static CompositionResult Success(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        return new(document.RootElement, bytes, ArtifactDigest.Compute(bytes), default);
    }

    internal static CompositionResult Failed(CompositionFailureCode code) =>
        new(null, null, default, new CompositionFailure(code));

    public override string ToString() =>
        $"{nameof(CompositionResult)}:Succeeded={Succeeded}:ArtifactKind={ArtifactKind}:Failure={Failure.Code}";
}
