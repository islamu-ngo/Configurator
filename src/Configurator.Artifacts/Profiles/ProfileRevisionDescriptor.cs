namespace Configurator.Artifacts.Profiles;

using Configurator.Artifacts.Composition;

/// <summary>Unverified discovery metadata. Only authenticated opening can establish profile facts.</summary>
public sealed class ProfileRevisionDescriptor
{
    internal ProfileRevisionDescriptor(Guid profileId, Guid snapshotId, ProfileFileKind kind,
        LinuxDirectoryInterop.NativeIdentity root, LinuxDirectoryInterop.NativeIdentity file)
    {
        ProfileId = profileId;
        SnapshotId = snapshotId;
        Kind = kind;
        Root = root;
        File = file;
    }

    public Guid ProfileId { get; }
    public Guid SnapshotId { get; }
    public ProfileFileKind Kind { get; }
    public long Length => File.Size;
    public bool IsAuthenticated => false;
    internal LinuxDirectoryInterop.NativeIdentity Root { get; }
    internal LinuxDirectoryInterop.NativeIdentity File { get; }
    internal string Name => NameOf(ProfileId, SnapshotId, Kind);

    internal static string NameOf(Guid profileId, Guid snapshotId, ProfileFileKind kind)
    {
        if (profileId == Guid.Empty || snapshotId == Guid.Empty || !Enum.IsDefined(kind))
            throw new ProfileVaultStoreException(ProtectedArtifactStatus.InvalidRequest);
        return $"{profileId:N}.{snapshotId:N}" + (kind == ProfileFileKind.Encrypted ? ".cfgvault" : ".cfgcompare.json");
    }

    internal static bool TryParse(string name, out Guid profileId, out Guid snapshotId, out ProfileFileKind kind)
    {
        profileId = snapshotId = Guid.Empty;
        kind = name.EndsWith(".cfgvault", StringComparison.Ordinal) ? ProfileFileKind.Encrypted : ProfileFileKind.Comparison;
        return name.Length >= 66 && name[32] == '.' &&
            Guid.TryParseExact(name.AsSpan(0, 32), "N", out profileId) &&
            Guid.TryParseExact(name.AsSpan(33, 32), "N", out snapshotId) &&
            profileId != Guid.Empty && snapshotId != Guid.Empty && NameOf(profileId, snapshotId, kind) == name;
    }

    public override string ToString() => $"{nameof(ProfileRevisionDescriptor)}:{Kind}:Unverified";
}
