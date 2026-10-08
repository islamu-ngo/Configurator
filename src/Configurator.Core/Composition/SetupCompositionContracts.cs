namespace Configurator.Core.Composition;

using System.Text.Json;

public enum SetupCompositionSourceKind
{
    Json,
    Yaml,
    Directory
}

public enum SetupCompositionArtifactKind
{
    Document
}

public enum SetupCompositionFailureCode
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

public abstract record SetupCompositionSource
{
    private protected SetupCompositionSource(SetupCompositionSourceKind kind) => Kind = kind;

    public SetupCompositionSourceKind Kind { get; }
}

public sealed record SetupCompositionJsonSource : SetupCompositionSource
{
    private readonly byte[] _bytes;

    public SetupCompositionJsonSource(ReadOnlyMemory<byte> bytes)
        : base(SetupCompositionSourceKind.Json) => _bytes = bytes.ToArray();

    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
}

public sealed record SetupCompositionYamlSource : SetupCompositionSource
{
    private readonly byte[] _bytes;

    public SetupCompositionYamlSource(ReadOnlyMemory<byte> bytes)
        : base(SetupCompositionSourceKind.Yaml) => _bytes = bytes.ToArray();

    public ReadOnlyMemory<byte> Bytes => new((byte[])_bytes.Clone());
}

public sealed record SetupCompositionDirectorySource : SetupCompositionSource
{
    public SetupCompositionDirectorySource(string rootDirectory)
        : base(SetupCompositionSourceKind.Directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = rootDirectory;
    }

    internal string RootDirectory { get; }

    public override string ToString() => $"{nameof(SetupCompositionDirectorySource)}:{Kind}";
}

public readonly record struct SetupCompositionFailure
{
    public SetupCompositionFailure(SetupCompositionFailureCode code)
    {
        if (code == SetupCompositionFailureCode.None)
            throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }

    public SetupCompositionFailureCode Code { get; }

    public override string ToString() => Code.ToString();
}

public sealed class SetupCompositionResult
{
    private readonly byte[]? _serializedBytes;

    private SetupCompositionResult(
        JsonElement? artifact,
        byte[]? serializedBytes,
        ArtifactDigest digest,
        SetupCompositionFailure failure)
    {
        Artifact = artifact?.Clone();
        _serializedBytes = serializedBytes is null ? null : (byte[])serializedBytes.Clone();
        Digest = digest;
        Failure = failure;
    }

    public bool Succeeded => Artifact is not null && Failure.Code == SetupCompositionFailureCode.None;
    public SetupCompositionArtifactKind ArtifactKind => SetupCompositionArtifactKind.Document;
    public JsonElement? Artifact { get; }
    public ReadOnlyMemory<byte> SerializedBytes =>
        _serializedBytes is null ? ReadOnlyMemory<byte>.Empty : new((byte[])_serializedBytes.Clone());
    public ArtifactDigest Digest { get; }
    public SetupCompositionFailure Failure { get; }

    internal static SetupCompositionResult Success(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        return new(document.RootElement, bytes, ArtifactDigest.Compute(bytes), default);
    }

    internal static SetupCompositionResult Failed(SetupCompositionFailureCode code) =>
        new(null, null, default, new SetupCompositionFailure(code));

    public override string ToString() =>
        $"{nameof(SetupCompositionResult)}:Succeeded={Succeeded}:ArtifactKind={ArtifactKind}:Failure={Failure.Code}";
}

public interface ISetupCompositionPublicationCommitBarrier
{
    ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken);
}

public sealed class SetupCompositionImmediatePublicationCommitBarrier
    : ISetupCompositionPublicationCommitBarrier
{
    public static SetupCompositionImmediatePublicationCommitBarrier Instance { get; } = new();

    private SetupCompositionImmediatePublicationCommitBarrier() { }

    public ValueTask AwaitPublicationCommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
