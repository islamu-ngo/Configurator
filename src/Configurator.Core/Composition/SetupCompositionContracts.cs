namespace Configurator.Core.Composition;

using System.Text.Json;

public enum CompositionSourceKind
{
    Json,
    Yaml,
    Directory
}

public enum CompositionArtifactKind
{
    Document
}

public enum CompositionFailureCode
{
    None = 0,
    InvalidSource,
    InvalidDocument,
    InvalidKey,
    DuplicateKey,
    KeyCollision,
    InvalidScalar,
    UnsupportedYamlGrammar,
    LimitExceeded,
    UnsafePath,
    PathCollision,
    UnsafeEntry,
    UnsupportedFilesystem,
    SourceChanged,
    SourceConflict,
    ForbiddenAuthority,
    ContractInvalid,
    Cancelled
}

public abstract record CompositionSource
{
    private protected CompositionSource(CompositionSourceKind kind) => Kind = kind;

    public CompositionSourceKind Kind { get; }
}

public sealed record CompositionJsonSource : CompositionSource
{
    private readonly byte[] _bytes;

    public CompositionJsonSource(ReadOnlyMemory<byte> bytes)
        : base(CompositionSourceKind.Json) => _bytes = bytes.ToArray();

    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
}

public sealed record CompositionYamlSource : CompositionSource
{
    private readonly byte[] _bytes;

    public CompositionYamlSource(ReadOnlyMemory<byte> bytes)
        : base(CompositionSourceKind.Yaml) => _bytes = bytes.ToArray();

    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
}

public sealed record CompositionDirectorySource : CompositionSource
{
    public CompositionDirectorySource(string rootDirectory)
        : base(CompositionSourceKind.Directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = rootDirectory;
    }

    internal string RootDirectory { get; }

    public override string ToString() => $"{nameof(CompositionDirectorySource)}:{Kind}";
}

public readonly record struct CompositionFailure
{
    public CompositionFailure(CompositionFailureCode code)
    {
        if (code == CompositionFailureCode.None)
            throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }

    public CompositionFailureCode Code { get; }

    public override string ToString() => Code.ToString();
}

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

public interface ICompositionPublicationCommitBarrier
{
    ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken);
}

public sealed class CompositionImmediatePublicationCommitBarrier
    : ICompositionPublicationCommitBarrier
{
    public static CompositionImmediatePublicationCommitBarrier Instance { get; } = new();

    private CompositionImmediatePublicationCommitBarrier() { }

    public ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
