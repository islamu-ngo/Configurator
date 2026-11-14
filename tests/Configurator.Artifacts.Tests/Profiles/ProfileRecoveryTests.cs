namespace Configurator.Artifacts.Tests.Profiles;

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Artifacts.Profiles;
using Configurator.Core;
using Configurator.Core.Profiles;

[NotInParallel]
[SupportedOSPlatform("linux")]
public sealed class ProfileRecoveryTests
{
    private static readonly byte[] Recovery = Encoding.UTF8.GetBytes("synthetic-portable-recovery-material");
    private static readonly byte[] Replacement = Encoding.UTF8.GetBytes("synthetic-replacement-recovery-material");
    private static readonly Guid NewSnapshot = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Test]
    public async Task ExactCopiedSnapshotRecoversWithoutTheNativeRecipientOrAnswerDisclosure()
    {
        using var native = new NativeStore();
        var keyring = new OsKeyringProvider(native);
        using var key = await keyring.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var profile = Profile();
        using var sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var original = new StoreDirectory();
        using var copied = new StoreDirectory();
        ProfileRevisionDescriptor revision = await Save(original.Store, sealedProfile);
        byte[] bytes = await original.Store.ReadAsync(revision);
        try
        {
            await Assert.That((await copied.Store.SaveAsync(profile.ProfileId, profile.SnapshotId,
                ProfileFileKind.Encrypted, bytes)).Succeeded).IsTrue();
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        native.LoseAll();
        using var fresh = new NativeProfileContainerCodec();
        var copiedRevision = (await copied.Store.ListAsync()).Single();
        OsKeyringException? unavailable = null;
        try { using var opened = await fresh.OpenNativeAsync(copied.Store, copiedRevision, 0, keyring); }
        catch (OsKeyringException exception) { unavailable = exception; }
        await Assert.That(unavailable?.Status).IsEqualTo(OsKeyringStatus.Missing);
        await Assert.That(native.Creations).IsEqualTo(1);

        ProfileRecoveryResult result = await ProfileRecoveryVerifier.VerifyAsync(copied.Store, copiedRevision,
            sealedProfile.Reference, 1, Recovery);
        await Assert.That(result.State).IsEqualTo(ProfileRecoveryState.VerifiedForSnapshot);
        await Assert.That(result.Failure).IsEqualTo(ProfileRecoveryFailure.None);
        await Assert.That(result.Reference).IsEqualTo(sealedProfile.Reference);
        await Assert.That(result.RecipientIndex).IsEqualTo(1);
        string facts = JsonSerializer.Serialize(result) + result + JsonSerializer.Serialize(sealedProfile);
        await Assert.That(facts.Contains("synthetic-private-answer", StringComparison.Ordinal)).IsFalse();
        await Assert.That(facts.Contains("synthetic-portable", StringComparison.Ordinal)).IsFalse();
        await Assert.That(facts.Contains(copied.Path, StringComparison.Ordinal)).IsFalse();
        await Assert.That(facts.Contains("ciphertext", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task WrongOrMissingPortableCredentialCannotVerifyAfterAllNativeKeysAreLost(bool missing)
    {
        using var native = new NativeStore();
        var keyring = new OsKeyringProvider(native);
        using var key = await keyring.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var profile = Profile();
        using var sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var directory = new StoreDirectory();
        var revision = await Save(directory.Store, sealedProfile);
        native.LoseAll();
        var result = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision,
            sealedProfile.Reference, 1, missing ? ReadOnlyMemory<byte>.Empty : Replacement);
        await Assert.That(result.State).IsEqualTo(ProfileRecoveryState.ConfiguredUnverified);
        await Assert.That(result.Failure).IsEqualTo(missing
            ? ProfileRecoveryFailure.MissingPassphrase : ProfileRecoveryFailure.AuthenticationFailed);
        await Assert.That(native.Creations).IsEqualTo(1);
    }

    [Test]
    public async Task NativeUnlockAndRecipientCountCannotVerifyAnUnusedPortableRecipient()
    {
        using var native = new NativeStore();
        var keyring = new OsKeyringProvider(native);
        using var key = await keyring.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var profile = Profile();
        using var sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var directory = new StoreDirectory();
        var revision = await Save(directory.Store, sealedProfile);
        using var opened = await codec.OpenNativeAsync(directory.Store, revision, 0, keyring);
        await Assert.That(opened.ProfileId).IsEqualTo(profile.ProfileId);
        await Assert.That(sealedProfile.Reference.State).IsEqualTo(ProfileRecoveryState.ConfiguredUnverified);
        var nativeSelection = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision,
            sealedProfile.Reference, 0, Recovery);
        await Assert.That(nativeSelection.Failure).IsEqualTo(ProfileRecoveryFailure.NotPortableRecipient);
        var absentSelection = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision,
            sealedProfile.Reference, 2, Recovery);
        await Assert.That(absentSelection.Failure).IsEqualTo(ProfileRecoveryFailure.MissingRecipient);
        var wrongSelection = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision,
            sealedProfile.Reference, 1, Replacement);
        await Assert.That(wrongSelection.State).IsEqualTo(ProfileRecoveryState.ConfiguredUnverified);
    }

    [Test]
    public async Task SameAuthenticatedIdentityWithDifferentCiphertextDoesNotMatchTheTrustedCopy()
    {
        using var profile = Profile();
        using var codec = new NativeProfileContainerCodec();
        using var trusted = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var substituted = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var directory = new StoreDirectory();
        var revision = await Save(directory.Store, substituted);
        var result = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision, trusted.Reference, 0, Recovery);
        await Assert.That(result.Failure).IsEqualTo(ProfileRecoveryFailure.CiphertextMismatch);
        await Assert.That(result.State).IsEqualTo(ProfileRecoveryState.ConfiguredUnverified);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AuthenticatedIdentityMustMatchIndependentlyExpectedStoreNames(bool wrongProfile, bool useNative)
    {
        using var native = new NativeStore();
        var keyring = new OsKeyringProvider(native);
        using var key = await keyring.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var profile = Profile();
        using var codec = new NativeProfileContainerCodec(useNative ? key.Lease : null);
        using var sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var directory = new StoreDirectory();
        Guid profileId = wrongProfile ? NewSnapshot : profile.ProfileId;
        Guid snapshotId = wrongProfile ? profile.SnapshotId : NewSnapshot;
        byte[] bytes = sealedProfile.CopyContainer();
        try
        {
            await Assert.That((await directory.Store.SaveAsync(profileId, snapshotId,
                ProfileFileKind.Encrypted, bytes)).Succeeded).IsTrue();
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        var revision = (await directory.Store.ListAsync()).Single();
        var expected = new ProfileRecoveryReference(profileId, snapshotId, sealedProfile.Reference.Digest,
            codec.DeviceRecipientId, true);
        int portableIndex = useNative ? 1 : 0;
        var result = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision, expected, portableIndex, Recovery);
        await Assert.That(result.Failure).IsEqualTo(ProfileRecoveryFailure.IdentityMismatch);
        await Assert.That(result.State).IsEqualTo(ProfileRecoveryState.ConfiguredUnverified);
        await Assert.That(async () =>
        {
            using var opened = useNative
                ? await codec.OpenNativeAsync(directory.Store, revision, 0, keyring)
                : await codec.OpenStoredAsync(directory.Store, revision, 0, Recovery);
        }).Throws<ProfileRecoveryException>();
    }

    [Test]
    public async Task UnrelatedRevisionCannotUseAnEarlierRecoveryReference()
    {
        using var profile = Profile();
        using var codec = new NativeProfileContainerCodec();
        using var original = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var later = await ProfileRecipientLifecycle.ResealAsync(profile, NewSnapshot, codec, [Recovery]);
        using var directory = new StoreDirectory();
        var revision = await Save(directory.Store, later);
        var result = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision, original.Reference, 0, Recovery);
        await Assert.That(result.Failure).IsEqualTo(ProfileRecoveryFailure.IdentityMismatch);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnauthenticatedRecoveryLabelOrRemovalCannotBorrowTheOriginalDigest(bool remove)
    {
        using var native = new NativeStore();
        var keyring = new OsKeyringProvider(native);
        using var key = await keyring.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var profile = Profile();
        using var sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        byte[] bytes = sealedProfile.CopyContainer();
        var node = JsonNode.Parse(bytes)!;
        CryptographicOperations.ZeroMemory(bytes);
        if (remove) node["recipients"]!.AsArray().RemoveAt(1);
        else node["recipients"]![1]!["header"]!["kid"] = NewSnapshot.ToString("N");
        bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        using var directory = new StoreDirectory();
        try
        {
            await Assert.That((await directory.Store.SaveAsync(profile.ProfileId, profile.SnapshotId,
                ProfileFileKind.Encrypted, bytes)).Succeeded).IsTrue();
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        var revision = (await directory.Store.ListAsync()).Single();
        using var opened = await codec.OpenNativeAsync(directory.Store, revision, 0, keyring);
        await Assert.That(opened.SnapshotId).IsEqualTo(profile.SnapshotId);
        var result = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision,
            sealedProfile.Reference, 1, Recovery);
        await Assert.That(result.Failure).IsEqualTo(ProfileRecoveryFailure.CiphertextMismatch);
    }

    [Test]
    public async Task DeviceOnlyCreationRequiresAcknowledgementForThisProfile()
    {
        using var native = new NativeStore();
        using var key = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var profile = Profile();
        foreach (Guid? acknowledgement in new Guid?[] { null, NewSnapshot })
        {
            ProfileRecoveryException? failure = null;
            try { using var rejected = await ProfileRecipientLifecycle.SealAsync(profile, codec, [], acknowledgement); }
            catch (ProfileRecoveryException exception) { failure = exception; }
            await Assert.That(failure?.Failure).IsEqualTo(ProfileRecoveryFailure.DeviceBoundAcknowledgementRequired);
        }
        using var accepted = await ProfileRecipientLifecycle.SealAsync(profile, codec, [], profile.ProfileId);
        await Assert.That(accepted.Reference.State).IsEqualTo(ProfileRecoveryState.DeviceBoundOnly);
        await Assert.That(accepted.Reference.DeviceRecipientId).IsEqualTo(key.KeyId);
        using var directory = new StoreDirectory();
        var revision = await Save(directory.Store, accepted);
        var recovery = await ProfileRecoveryVerifier.VerifyAsync(directory.Store, revision, accepted.Reference, 0, Recovery);
        await Assert.That(recovery.State).IsEqualTo(ProfileRecoveryState.DeviceBoundOnly);
        await Assert.That(recovery.Failure).IsEqualTo(ProfileRecoveryFailure.NotPortableRecipient);
    }

    [Test]
    public async Task AdmissionCountsTheConfiguredDeviceRecipientAndRejectsEmptyRecipientSets()
    {
        using var native = new NativeStore();
        using var key = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var portable = new NativeProfileContainerCodec();
        using var profile = Profile();
        await Assert.That(async () =>
        {
            using var rejected = await ProfileRecipientLifecycle.SealAsync(profile, codec,
                Enumerable.Repeat<ReadOnlyMemory<byte>>(Recovery, 8).ToArray());
        }).Throws<ProfileException>();
        await Assert.That(async () =>
        {
            using var rejected = await ProfileRecipientLifecycle.SealAsync(profile, portable, [], profile.ProfileId);
        }).Throws<ProfileException>();
        using var comparison = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(() => new NativeProfileContainerCodec(comparison.Lease!)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SnapshotIdentityAdmissionRejectsEmptyOrReusedHostIdentifiers(bool empty)
    {
        using var profile = Profile();
        using var codec = new NativeProfileContainerCodec();
        ProfileRecoveryException? failure = null;
        try
        {
            using var rejected = await ProfileRecipientLifecycle.ResealAsync(profile,
                empty ? Guid.Empty : profile.SnapshotId, codec, [Recovery]);
        }
        catch (ProfileRecoveryException exception) { failure = exception; }
        await Assert.That(failure?.Failure).IsEqualTo(ProfileRecoveryFailure.InvalidSnapshotIdentity);
    }

    [Test]
    [Arguments(ProfileProtectionMode.ImportUpgradeOnly)]
    [Arguments(ProfileProtectionMode.InspectableEncrypted)]
    public async Task ResealingRetainsCustodyAndOldRecipientsInSeparateImmutableFiles(ProfileProtectionMode mode)
    {
        using var native = new NativeStore();
        var keyring = new OsKeyringProvider(native);
        using var key = await keyring.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var oldCodec = new NativeProfileContainerCodec(key.Lease!);
        using var portableCodec = new NativeProfileContainerCodec();
        using var commitment = new ProfileSecretCommitment(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        using var profile = Profile(mode, commitment);
        var before = CompareBaselineCodec.Create(profile);
        using var original = await ProfileRecipientLifecycle.SealAsync(profile, oldCodec, [Recovery]);
        using var changed = await ProfileRecipientLifecycle.ResealAsync(profile, NewSnapshot, portableCodec, [Replacement]);
        using var directory = new StoreDirectory();
        var originalRevision = await Save(directory.Store, original);
        var changedRevision = await Save(directory.Store, changed);
        using var oldNative = await oldCodec.OpenNativeAsync(directory.Store, originalRevision, 0, keyring);
        using var oldPortable = await portableCodec.OpenStoredAsync(directory.Store, originalRevision, 1, Recovery);
        using var newPortable = await portableCodec.OpenStoredAsync(directory.Store, changedRevision, 0, Replacement);
        await Assert.That(newPortable.ProfileId).IsEqualTo(profile.ProfileId);
        await Assert.That(newPortable.SnapshotId).IsEqualTo(NewSnapshot);
        await Assert.That(newPortable.Mode).IsEqualTo(mode);
        await Assert.That(newPortable.DefinitionDigest).IsEqualTo(profile.DefinitionDigest);
        var after = CompareBaselineCodec.Create(newPortable);
        await Assert.That(after.KeyId).IsEqualTo(before.KeyId);
        await Assert.That(after.ContextTag).IsEqualTo(before.ContextTag);
        await Assert.That(after.Commitments["/token"]).IsEqualTo(before.Commitments["/token"]);
        if (mode == ProfileProtectionMode.ImportUpgradeOnly)
            await Assert.That(() => ProfilePayloadCodec.InspectAnswers(newPortable)).Throws<ProfileException>();
        else
            await Assert.That(ProfilePayloadCodec.InspectAnswers(newPortable).Values["token"].GetString())
                .IsEqualTo("synthetic-private-answer");
        await Assert.That(async () =>
        {
            using var wrong = await portableCodec.OpenStoredAsync(directory.Store, changedRevision, 0, Recovery);
        }).Throws<ProfileException>();
        await Assert.That((await directory.Store.ListAsync()).Count).IsEqualTo(2);
        await Assert.That(native.Entries.Count).IsEqualTo(1);
        byte[] first = original.CopyContainer();
        byte[] second = changed.CopyContainer();
        try
        {
            using var left = JsonDocument.Parse(first);
            using var right = JsonDocument.Parse(second);
            await Assert.That(left.RootElement.GetProperty("iv").GetString() ==
                right.RootElement.GetProperty("iv").GetString()).IsFalse();
            await Assert.That(left.RootElement.GetProperty("ciphertext").GetString() ==
                right.RootElement.GetProperty("ciphertext").GetString()).IsFalse();
            await Assert.That((await directory.Store.SaveAsync(profile.ProfileId, profile.SnapshotId,
                ProfileFileKind.Encrypted, second)).Succeeded).IsFalse();
            await Assert.That(await directory.Store.ReadAsync(originalRevision)).IsEquivalentTo(first);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationOrOwnerDisposalBeforeSealPublicationClearsTheLosingOutput(bool dispose)
    {
        using var profile = Profile();
        using var real = new NativeProfileContainerCodec();
        var gate = new GatedCodec(real);
        using var cancellation = new CancellationTokenSource();
        Task<ProfileSealedSnapshot> pending = ProfileRecipientLifecycle.ResealAsync(
            profile, NewSnapshot, gate, [Recovery], cancellationToken: cancellation.Token).AsTask();
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        if (dispose) profile.Dispose();
        else cancellation.Cancel();
        gate.Release.TrySetResult();
        if (dispose)
            await Assert.That(async () => { using var rejected = await pending; }).Throws<ObjectDisposedException>();
        else
            await Assert.That(async () => { using var rejected = await pending; }).Throws<OperationCanceledException>();
        await Assert.That(gate.Output!.All(value => value == 0)).IsTrue();
        await Assert.That(gate.Payload.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    [Arguments(OsKeyringStatus.Missing)]
    [Arguments(OsKeyringStatus.Locked)]
    [Arguments(OsKeyringStatus.Denied)]
    [Arguments(OsKeyringStatus.ServiceMissing)]
    [Arguments(OsKeyringStatus.Unavailable)]
    public async Task NativeLookupFailuresRemainDistinctAndNeverCreateRecipients(OsKeyringStatus failure)
    {
        using var native = new NativeStore();
        var keyring = new OsKeyringProvider(native);
        using var key = await keyring.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var profile = Profile();
        using var sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var directory = new StoreDirectory();
        var revision = await Save(directory.Store, sealedProfile);
        native.Failure = failure;
        OsKeyringException? refused = null;
        try { using var opened = await codec.OpenNativeAsync(directory.Store, revision, 0, keyring); }
        catch (OsKeyringException exception) { refused = exception; }
        await Assert.That(refused?.Status).IsEqualTo(failure);
        await Assert.That(native.Creations).IsEqualTo(1);
        await Assert.That(refused?.ToString().Contains(directory.Path, StringComparison.Ordinal)).IsFalse();
        await Assert.That(refused?.ToString().Contains("synthetic", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RecoveryOwnsTheCredentialAcrossReadAndHonorsCancellationBeforeAdoption(bool cancel)
    {
        using var profile = Profile();
        using var codec = new NativeProfileContainerCodec();
        using var sealedProfile = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var directory = new StoreDirectory();
        var revision = await Save(directory.Store, sealedProfile);
        var gate = new GatedStore(directory.Store);
        using var cancellation = new CancellationTokenSource();
        byte[] material = Recovery.ToArray();
        Task<ProfileRecoveryResult> pending = ProfileRecoveryVerifier.VerifyAsync(gate, revision,
            sealedProfile.Reference, 0, material, cancellation.Token).AsTask();
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        CryptographicOperations.ZeroMemory(material);
        if (cancel) cancellation.Cancel();
        gate.Release.TrySetResult();
        if (cancel)
            await Assert.That(async () => { _ = await pending; }).Throws<OperationCanceledException>();
        else
            await Assert.That((await pending).State).IsEqualTo(ProfileRecoveryState.VerifiedForSnapshot);
        await Assert.That(gate.ReadBytes!.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task RecoveryReferenceAdmissionRequiresIndependentlySuppliedIdentityAndDigest()
    {
        using var profile = Profile();
        await Assert.That(() => new ProfileRecoveryReference(profile.ProfileId, profile.SnapshotId,
            default, null, true)).Throws<ProfileRecoveryException>();
        await Assert.That(() => new ProfileRecoveryReference(Guid.Empty, profile.SnapshotId,
            ArtifactDigest.Compute("synthetic-ciphertext"u8), null, true)).Throws<ProfileRecoveryException>();
    }

    [Test]
    public async Task CallerMutationAtCodecEntryCannotRemovePortableRecoveryAfterPolicyAdmission()
    {
        using var native = new NativeStore();
        using var key = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var codec = new NativeProfileContainerCodec(key.Lease!);
        using var profile = Profile();
        using var control = await ProfileRecipientLifecycle.SealAsync(profile, codec, [Recovery]);
        using var controlDirectory = new StoreDirectory();
        var controlRevision = await Save(controlDirectory.Store, control);
        var admitted = await ProfileRecoveryVerifier.VerifyAsync(controlDirectory.Store, controlRevision,
            control.Reference, 1, Recovery);
        await Assert.That(admitted.State).IsEqualTo(ProfileRecoveryState.VerifiedForSnapshot);

        var materials = new List<ReadOnlyMemory<byte>> { Recovery };
        var mutation = new MutatingCodec(codec, materials.Clear);
        using var raced = await ProfileRecipientLifecycle.SealAsync(profile, mutation, materials);
        using var racedDirectory = new StoreDirectory();
        var racedRevision = await Save(racedDirectory.Store, raced);
        var recovered = await ProfileRecoveryVerifier.VerifyAsync(racedDirectory.Store, racedRevision,
            raced.Reference, 1, Recovery);
        await Assert.That(recovered.State).IsEqualTo(ProfileRecoveryState.VerifiedForSnapshot);
        await Assert.That(recovered.Failure).IsEqualTo(ProfileRecoveryFailure.None);
    }

    private static UnlockedProfile Profile(ProfileProtectionMode mode = ProfileProtectionMode.ImportUpgradeOnly,
        ProfileSecretCommitment? commitment = null)
    {
        byte[] payload = ProfileVaultCodecTests.Payload(commitment, mode);
        try { return ProfilePayloadCodec.Admit(payload); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static async Task<ProfileRevisionDescriptor> Save(LocalProfileVaultStore store, ProfileSealedSnapshot snapshot)
    {
        byte[] bytes = snapshot.CopyContainer();
        try
        {
            await Assert.That((await store.SaveAsync(snapshot.Reference.ProfileId, snapshot.Reference.SnapshotId,
                ProfileFileKind.Encrypted, bytes)).Succeeded).IsTrue();
            return (await store.ListAsync()).Single(revision => revision.SnapshotId == snapshot.Reference.SnapshotId);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private sealed class StoreDirectory : IDisposable
    {
        internal StoreDirectory()
        {
            if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "synthetic-profile-recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Store = new LocalProfileVaultStore(Path);
        }
        internal string Path { get; }
        internal LocalProfileVaultStore Store { get; }
        public void Dispose() => Directory.Delete(Path, true);
    }

    private sealed class NativeStore : IOsKeyringNative, IDisposable
    {
        internal Dictionary<(Guid Id, OsKeyringPurpose Purpose), byte[]> Entries { get; } = [];
        internal int Creations { get; private set; }
        internal OsKeyringStatus? Failure { get; set; }
        public async ValueTask<OsKeyringNativeResult> LookupAsync(Guid id, OsKeyringPurpose purpose, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (Failure is { } failure) return new(failure);
            return Entries.TryGetValue((id, purpose), out byte[]? value)
                ? new OsKeyringNativeResult(OsKeyringStatus.Success, value.ToArray())
                : new OsKeyringNativeResult(OsKeyringStatus.Missing);
        }
        public async ValueTask<OsKeyringNativeResult> CreateAsync(Guid id, OsKeyringPurpose purpose,
            ReadOnlyMemory<byte> canonicalUtf8, CancellationToken cancellationToken)
        {
            Creations++;
            Entries.Add((id, purpose), canonicalUtf8.ToArray());
            await Task.Yield();
            return new(OsKeyringStatus.Success, creation: OsKeyringCreation.Created);
        }
        internal void LoseAll()
        {
            foreach (byte[] value in Entries.Values) CryptographicOperations.ZeroMemory(value);
            Entries.Clear();
        }
        public void Dispose() => LoseAll();
    }

    private sealed class GatedCodec(IProfileContainerCodec inner) : IProfileContainerCodec
    {
        public Guid? DeviceRecipientId => inner.DeviceRecipientId;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[]? Output { get; private set; }
        internal ReadOnlyMemory<byte> Payload { get; private set; }
        public async ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
            IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default)
        {
            Payload = payload;
            Output = await inner.SealAsync(payload, recipientMaterials, cancellationToken);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
            return Output;
        }
        public ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
            ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default) =>
            inner.OpenAsync(container, recipientIndex, passphraseUtf8, cancellationToken);
    }

    private sealed class GatedStore(LocalProfileVaultStore inner) : IProfileVaultStore
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[]? ReadBytes { get; private set; }
        public async ValueTask<byte[]> ReadAsync(ProfileRevisionDescriptor revision, CancellationToken cancellationToken = default)
        {
            byte[] bytes = await inner.ReadAsync(revision, cancellationToken);
            try
            {
                ReadBytes = bytes;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
                return bytes;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw;
            }
        }
        public ValueTask<IReadOnlyList<ProfileRevisionDescriptor>> ListAsync(CancellationToken cancellationToken = default) =>
            inner.ListAsync(cancellationToken);
        public Task<ProtectedArtifactBundleResult> SaveAsync(Guid profileId, Guid snapshotId, ProfileFileKind kind,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
            inner.SaveAsync(profileId, snapshotId, kind, bytes, cancellationToken);
    }

    private sealed class MutatingCodec(IProfileContainerCodec inner, Action beforeConsumption) : IProfileContainerCodec
    {
        public Guid? DeviceRecipientId => inner.DeviceRecipientId;
        public ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
            IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default)
        {
            beforeConsumption();
            return inner.SealAsync(payload, recipientMaterials, cancellationToken);
        }
        public ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
            ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default) =>
            inner.OpenAsync(container, recipientIndex, passphraseUtf8, cancellationToken);
    }
}
