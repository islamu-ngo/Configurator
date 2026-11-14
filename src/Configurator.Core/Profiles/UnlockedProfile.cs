namespace Configurator.Core.Profiles;

using System.Security.Cryptography;
using Configurator.Core.Definitions;

/// <summary>Owns retained plaintext. Values are available only to scoped Core operations, never ordinary UI snapshots.</summary>
public sealed class UnlockedProfile : IDisposable
{
    private byte[]? _payload;
    private readonly object _access = new();
    internal UnlockedProfile(byte[] payload, Definition definition, Guid profileId, Guid snapshotId,
        ProfileProtectionMode mode, bool isSourceSubset = false)
    {
        _payload = payload;
        Definition = definition;
        ProfileId = profileId;
        SnapshotId = snapshotId;
        Mode = mode;
        IsSourceSubset = isSourceSubset;
    }

    public Guid ProfileId { get; }
    public Guid SnapshotId { get; }
    public ProfileProtectionMode Mode { get; }
    public Guid FamilyId => Definition.FamilyId;
    public long DefinitionRevision => Definition.Revision;
    public ArtifactDigest DefinitionDigest => Definition.Digest;
    internal Definition Definition { get; }
    internal bool IsSourceSubset { get; }

    /// <summary>Retires all borrowed authority and transfers the buffer to the consuming Core operation.</summary>
    internal byte[] TakePayload()
    {
        lock (_access)
            return Interlocked.Exchange(ref _payload, null) ??
                throw new ObjectDisposedException(nameof(UnlockedProfile));
    }

    internal byte[] CopyPayload()
    {
        lock (_access)
        {
            byte[] source = Volatile.Read(ref _payload) ??
                throw new ObjectDisposedException(nameof(UnlockedProfile));
            byte[] copy = source.ToArray();
            if (Volatile.Read(ref _payload) is null)
            {
                CryptographicOperations.ZeroMemory(copy);
                throw new ObjectDisposedException(nameof(UnlockedProfile));
            }
            return copy;
        }
    }
    internal void EnsureAlive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _payload) is null, this);

    public void Dispose()
    {
        byte[]? payload = Interlocked.Exchange(ref _payload, null);
        if (payload is not null)
            lock (_access) CryptographicOperations.ZeroMemory(payload);
    }

    public override string ToString() => "UnlockedProfile:Redacted";
}
