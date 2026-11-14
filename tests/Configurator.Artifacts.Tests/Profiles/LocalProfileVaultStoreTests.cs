namespace Configurator.Artifacts.Tests.Profiles;

using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using Configurator.Artifacts.Profiles;
using Configurator.Core.Profiles;

[SupportedOSPlatform("linux")]
public sealed class LocalProfileVaultStoreTests
{
    private static readonly Guid ProfileId = Guid.Parse("ff6c108f-f794-4e32-b8a4-bc7807c6a7a4");
    private static readonly Guid SnapshotId = Guid.Parse("d923397e-b5bb-475c-bb03-4d6517ae1605");
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = PrivateFile | UnixFileMode.UserExecute;

    [Test]
    [Arguments(ProfileFileKind.Encrypted)]
    [Arguments(ProfileFileKind.Comparison)]
    public async Task InstallsExactImmutablePrivateBytesAndListsOnlyUnverifiedMetadata(ProfileFileKind kind)
    {
        Linux();
        using var directory = new TestDirectory();
        var store = new LocalProfileVaultStore(directory.Path);
        byte[] bytes = Bytes(kind);
        ProtectedArtifactBundleResult result = await store.SaveAsync(ProfileId, SnapshotId, kind, bytes);
        await Assert.That(result.Succeeded).IsTrue();
        ProfileRevisionDescriptor revision = (await store.ListAsync()).Single();
        await Assert.That(revision.IsAuthenticated).IsFalse();
        await Assert.That(revision.ProfileId).IsEqualTo(ProfileId);
        await Assert.That(revision.SnapshotId).IsEqualTo(SnapshotId);
        await Assert.That(await store.ReadAsync(revision)).IsEquivalentTo(bytes);
        await Assert.That(File.GetUnixFileMode(Path.Combine(directory.Path, revision.Name))).IsEqualTo(PrivateFile);
        await Assert.That(Directory.GetFileSystemEntries(directory.Path).Length).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentSameRevisionSavesNeverOverwriteAnInstalledFile()
    {
        Linux();
        using var directory = new TestDirectory();
        var store = new LocalProfileVaultStore(directory.Path);
        byte[] bytes = Bytes();
        ProtectedArtifactBundleResult[] results = await Task.WhenAll(
            store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, bytes),
            store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, bytes));
        await Assert.That(results.Count(result => result.Succeeded)).IsEqualTo(1);
        await Assert.That(results.Count(result => result.Outcomes.Single().Result is { InstallationEstablished: false })).IsEqualTo(1);
        await Assert.That(await File.ReadAllBytesAsync(FilePath(directory.Path))).IsEquivalentTo(bytes);
    }

    [Test]
    public async Task SaveSnapshotsCallerBytesBeforeReturningItsPendingOperation()
    {
        Linux();
        using var directory = new TestDirectory();
        var store = new LocalProfileVaultStore(directory.Path);
        byte[] bytes = Bytes();
        byte[] expected = (byte[])bytes.Clone();
        Task<ProtectedArtifactBundleResult> pending = store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, bytes);
        CryptographicOperations.ZeroMemory(bytes);
        await Assert.That((await pending).Succeeded).IsTrue();
        await Assert.That(await File.ReadAllBytesAsync(FilePath(directory.Path))).IsEquivalentTo(expected);
    }

    [Test]
    public async Task ExistingSharedRootIsRefusedForDiscoveryAndPublication()
    {
        Linux();
        using var directory = new TestDirectory();
        File.SetUnixFileMode(directory.Path, PrivateDirectory | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        var store = new LocalProfileVaultStore(directory.Path);
        await Assert.That(async () => { _ = await store.ListAsync(); }).Throws<ProfileVaultStoreException>();
        ProtectedArtifactBundleResult result = await store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, Bytes());
        await Assert.That(result.Outcomes.Single().Result?.InstallationEstablished).IsFalse();
        await Assert.That(Directory.GetFileSystemEntries(directory.Path).Length).IsEqualTo(0);
    }

    [Test]
    public async Task SymlinkAncestorCannotCreateAStoreInItsDestination()
    {
        Linux();
        using var directory = new TestDirectory();
        string actual = Path.Combine(directory.Path, "actual");
        Directory.CreateDirectory(actual, PrivateDirectory);
        string alias = Path.Combine(directory.Path, "alias");
        Directory.CreateSymbolicLink(alias, actual);
        var store = new LocalProfileVaultStore(Path.Combine(alias, "vault"));
        await Assert.That(async () => { _ = await store.ListAsync(); }).Throws<ProfileVaultStoreException>();
        await Assert.That(Directory.Exists(Path.Combine(actual, "vault"))).IsFalse();
    }

    [Test]
    [Arguments("replacement")]
    [Arguments("symlink")]
    [Arguments("permissions")]
    [Arguments("oversized")]
    public async Task ReopenCannotSubstituteChangedOrUnrestrictedFileAuthority(string attack)
    {
        Linux();
        using var directory = new TestDirectory();
        var store = new LocalProfileVaultStore(directory.Path);
        await store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, Bytes());
        ProfileRevisionDescriptor revision = (await store.ListAsync()).Single();
        string path = FilePath(directory.Path);
        if (attack is "replacement" or "symlink")
        {
            string retained = Path.Combine(directory.Path, "retained");
            File.Move(path, retained);
            if (attack == "symlink") File.CreateSymbolicLink(path, retained);
            else
            {
                await File.WriteAllBytesAsync(path, Bytes());
                File.SetUnixFileMode(path, PrivateFile);
            }
        }
        else if (attack == "permissions") File.SetUnixFileMode(path, PrivateFile | UnixFileMode.GroupRead);
        else
        {
            await using var output = new FileStream(path, FileMode.Open, FileAccess.Write);
            output.SetLength(ProfileContainerPolicy.MaximumContainerBytes + 1L);
        }
        ProfileVaultStoreException? failure = null;
        try { _ = await store.ReadAsync(revision); }
        catch (ProfileVaultStoreException exception) { failure = exception; }
        await Assert.That(failure is not null).IsTrue();
        await Assert.That(failure?.ToString().Contains(directory.Path, StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task RootReplacementCannotRedirectAnExistingDescriptor()
    {
        Linux();
        using var directory = new TestDirectory();
        string root = Path.Combine(directory.Path, "vault");
        var store = new LocalProfileVaultStore(root);
        await store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, Bytes());
        ProfileRevisionDescriptor revision = (await store.ListAsync()).Single();
        Directory.Move(root, Path.Combine(directory.Path, "retained"));
        Directory.CreateDirectory(root, PrivateDirectory);
        await File.WriteAllBytesAsync(FilePath(root), Bytes());
        File.SetUnixFileMode(FilePath(root), PrivateFile);
        await Assert.That(async () => { _ = await store.ReadAsync(revision); }).Throws<ProfileVaultStoreException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FileReplacementDuringDiscoveryOrReadRejectsBeforePublishing(bool reading)
    {
        Linux();
        using var directory = new TestDirectory();
        var original = new LocalProfileVaultStore(directory.Path);
        await original.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, Bytes());
        ProfileRevisionDescriptor revision = (await original.ListAsync()).Single();
        var store = new LocalProfileVaultStore(directory.Path, null, () =>
        {
            string path = FilePath(directory.Path);
            File.Move(path, Path.Combine(directory.Path, "retained"));
            File.WriteAllBytes(path, Bytes());
            File.SetUnixFileMode(path, PrivateFile);
        });
        await Assert.That(async () =>
        {
            if (reading) _ = await store.ReadAsync(revision);
            else _ = await store.ListAsync();
        }).Throws<ProfileVaultStoreException>();
    }

    [Test]
    public async Task InventoryIsBoundedEvenWhenNamesAreUnrecognized()
    {
        Linux();
        using var directory = new TestDirectory();
        for (int index = 0; index <= ProfileFileReader.MaximumEntries; index++)
            await File.WriteAllBytesAsync(Path.Combine(directory.Path, "ignored-" + index), []);
        await Assert.That(async () => { _ = await new LocalProfileVaultStore(directory.Path).ListAsync(); })
            .Throws<ProfileVaultStoreException>();
    }

    [Test]
    public async Task CancellationBeforePublicationCreatesNoRootOrFile()
    {
        Linux();
        using var directory = new TestDirectory();
        string root = Path.Combine(directory.Path, "vault");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.That(async () =>
        {
            _ = await new LocalProfileVaultStore(root).SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted,
                Bytes(), cancellation.Token);
        }).Throws<OperationCanceledException>();
        await Assert.That(Directory.Exists(root)).IsFalse();
    }

    [Test]
    public async Task CancellationAfterInstallationRetainsTheFileAndItsEstablishedReceipt()
    {
        Linux();
        using var directory = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        var store = new LocalProfileVaultStore(directory.Path, new ProtectedArtifactWriter(() => cancellation.Cancel()));
        ProtectedArtifactBundleResult result = await store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted,
            Bytes(), cancellation.Token);
        await Assert.That(result.Outcomes.Single().Result?.InstallationEstablished).IsTrue();
        await Assert.That(result.CompletionCode).IsEqualTo(ProtectedArtifactBundleCompletionCode.Cancelled);
        await Assert.That(await File.ReadAllBytesAsync(FilePath(directory.Path))).IsEquivalentTo(Bytes());
    }

    [Test]
    public async Task PostInstallationFailureAndCleanupUncertaintyNeverBecomeNotInstalled()
    {
        Linux();
        using var directory = new TestDirectory();
        var store = new LocalProfileVaultStore(directory.Path, new CleanupFaultWriter());
        ProtectedArtifactBundleResult result = await store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, Bytes());
        await Assert.That(result.Outcomes.Single().Result?.InstallationEstablished).IsTrue();
        await Assert.That(result.CleanupFailed).IsTrue();
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(await File.ReadAllBytesAsync(FilePath(directory.Path))).IsEquivalentTo(Bytes());
    }

    [Test]
    public async Task UnavailableWriterCreatesNoOrdinaryFileOrDirectoryFallback()
    {
        using var directory = new TestDirectory();
        string root = Path.Combine(directory.Path, "vault");
        var store = new LocalProfileVaultStore(root, new UnavailableWriter());
        ProtectedArtifactBundleResult result = await store.SaveAsync(ProfileId, SnapshotId, ProfileFileKind.Encrypted, Bytes());
        await Assert.That(result.Outcomes.Single().Result?.Status).IsEqualTo(ProtectedArtifactStatus.UnsupportedPlatform);
        await Assert.That(Directory.Exists(root)).IsFalse();
    }

    private static string FilePath(string root) => Path.Combine(root,
        ProfileRevisionDescriptor.NameOf(ProfileId, SnapshotId, ProfileFileKind.Encrypted));

    private static byte[] Bytes(ProfileFileKind kind = ProfileFileKind.Encrypted)
    {
        if (kind == ProfileFileKind.Encrypted) return Encoding.UTF8.GetBytes(ProfileVaultCodecTests.GeneralOne);
        byte[] payload = ProfileVaultCodecTests.Payload();
        try
        {
            using UnlockedProfile profile = ProfilePayloadCodec.Admit(payload);
            return CompareBaselineCodec.Serialize(CompareBaselineCodec.Create(profile));
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static void Linux()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("actual-linux-required");
    }

    private sealed class UnavailableWriter : IProtectedArtifactWriter
    {
        public bool IsAvailable => false;
        public Task<ProtectedArtifactPreparation> PrepareAsync(Configurator.Core.ArtifactKind kind, string targetPath,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unavailable-writer-invoked");
    }

    private sealed class CleanupFaultWriter : IProtectedArtifactWriter
    {
        public bool IsAvailable => true;
        public async Task<ProtectedArtifactPreparation> PrepareAsync(Configurator.Core.ArtifactKind kind, string targetPath,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            ProtectedArtifactPreparation native = await new ProtectedArtifactWriter().PrepareAsync(kind, targetPath, bytes, cancellationToken);
            return new ProtectedArtifactPreparation(async (token, establish) =>
            {
                ProtectedArtifactCommitResult result = await native.CommitAsync(token);
                if (result.InstallationEstablished) establish();
                return result;
            }, () =>
            {
                native.Dispose();
                throw new IOException("synthetic-private-cleanup-sentinel");
            });
        }
    }
}
