namespace Configurator.Browser.Profiles;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>A capture of the caller's workspace authority, never an independent mutable lease.</summary>
public sealed record ProfileWorkerAuthority(Guid WorkspaceId, long Generation, long AnswerRevision,
    long ConsentEpoch, bool ConsentGranted);

/// <summary>Bounded profile commands; profile references alone confer no authority.</summary>
public sealed record ProfileWorkerRequest(ProfileWorkerAuthority Authority, long RequestId, string Operation)
{
    public Guid ProfileReference { get; init; }
    public int RecipientIndex { get; init; }
    public Guid ProfileId { get; init; }
    public Guid SnapshotId { get; init; }
    public string? TrustedDigest { get; init; }
    public Guid? DeviceRecipientId { get; init; }
    public bool PortableRecoveryConfigured { get; init; }
    public string? ArtifactId { get; init; }
    public string[] ExcludedPaths { get; init; } = [];
    public string? Mode { get; init; }
    public string? DefinitionSource { get; init; }
    public string? EnvironmentContext { get; init; }
}

public sealed record ProfileWorkerField(string Path, string Eligibility, string Comparison);

/// <summary>No retained secret, credential, plaintext payload or restricted artifact belongs in this reply.</summary>
public sealed record ProfileWorkerReply(ProfileWorkerAuthority Authority, long RequestId, string Status)
{
    public Guid ProfileReference { get; init; }
    public Guid ProfileId { get; init; }
    public Guid SnapshotId { get; init; }
    public string? Mode { get; init; }
    public Guid FamilyId { get; init; }
    public long DefinitionRevision { get; init; }
    public string? DefinitionDigest { get; init; }
    public JsonElement? PublicValues { get; init; }
    public ProfileWorkerField[] Fields { get; init; } = [];
    public string[] RetainedPaths { get; init; } = [];
    public string[] Codes { get; init; } = [];
    public string? OutputKind { get; init; }
    public string? CiphertextDigest { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ProfileWorkerRequest))]
[JsonSerializable(typeof(ProfileWorkerReply))]
public sealed partial class ProfileWorkerJsonContext : JsonSerializerContext;
