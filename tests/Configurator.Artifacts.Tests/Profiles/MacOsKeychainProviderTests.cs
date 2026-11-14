namespace Configurator.Artifacts.Tests.Profiles;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Artifacts.Profiles;

[NotInParallel]
public sealed class MacOsKeychainProviderTests
{
    [Test]
    public async Task ScopedKeysRoundTripWithoutReplacementOrPublicSecretSerialization()
    {
        using var api = new KeychainApi();
        var provider = Provider(api);
        using var missing = await provider.LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(missing.Status).IsEqualTo(OsKeyringStatus.Missing);
        await Assert.That(api.Entries.Count).IsEqualTo(0);
        using var first = await provider.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var second = await provider.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(first.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(second.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(first.Creation).IsEqualTo(OsKeyringCreation.Created);
        await Assert.That(first.KeyId == second.KeyId).IsFalse();
        using var read = await provider.LookupAsync(first.KeyId, first.Purpose);
        await Assert.That(read.Status).IsEqualTo(OsKeyringStatus.Success);
        byte[] original = first.Lease!.CopyRecipientUtf8();
        byte[] retrieved = read.Lease!.CopyRecipientUtf8();
        try { await Assert.That(retrieved).IsEquivalentTo(original); }
        finally
        {
            CryptographicOperations.ZeroMemory(original);
            CryptographicOperations.ZeroMemory(retrieved);
        }
        using var wrongPurpose = await provider.LookupAsync(first.KeyId, second.Purpose);
        await Assert.That(wrongPurpose.Status).IsEqualTo(OsKeyringStatus.Missing);
        using var commitment = second.Lease!.CreateCommitment();
        await Assert.That(commitment.KeyId).IsEqualTo(second.KeyId);
        string safe = JsonSerializer.Serialize(first) + JsonSerializer.Serialize(first.Lease) + first;
        foreach (byte[] bytes in api.Entries.Values)
            await Assert.That(safe.Contains(Encoding.UTF8.GetString(bytes), StringComparison.Ordinal)).IsFalse();
        first.Dispose();
        await Assert.That(() => first.Lease.CopyRecipientUtf8()).Throws<ObjectDisposedException>();
        await Assert.That(api.Entries.Count).IsEqualTo(2);
        await Assert.That(api.Entries.Values.First().SequenceEqual(api.Entries.Values.Last())).IsFalse();
        await Assert.That(api.LiveObjects).IsEqualTo(0);
        await Assert.That(api.InteractionAllowed).IsEqualTo((byte)1);
    }

    [Test]
    public async Task DuplicateCreationCannotReplaceAnExistingCredential()
    {
        using var api = new KeychainApi { Collision = true };
        using var result = await Provider(api).CreateAsync(OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidRequest);
        await Assert.That(result.Creation).IsEqualTo(OsKeyringCreation.NotAttempted);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(api.Entries.Single().Value).IsEquivalentTo(new byte[44]);
        await Assert.That(api.LiveObjects).IsEqualTo(0);
    }

    [Test]
    [Arguments(-25300, OsKeyringStatus.Missing)]
    [Arguments(-25308, OsKeyringStatus.Locked)]
    [Arguments(-25315, OsKeyringStatus.Locked)]
    [Arguments(-25293, OsKeyringStatus.Denied)]
    [Arguments(-25243, OsKeyringStatus.Denied)]
    [Arguments(-128, OsKeyringStatus.Cancelled)]
    [Arguments(-25291, OsKeyringStatus.Unavailable)]
    [Arguments(-50, OsKeyringStatus.InvalidRequest)]
    [Arguments(-9999, OsKeyringStatus.Unavailable)]
    public async Task NativeFailuresStayDistinctAndReleaseReturnedObjects(int error, OsKeyringStatus expected)
    {
        using var api = new KeychainApi { Error = error, ReturnDataOnFailure = true };
        var provider = Provider(api);
        using var read = await provider.LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(read.Status).IsEqualTo(expected);
        using var created = await provider.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(created.Status).IsEqualTo(error switch
        {
            -25300 => OsKeyringStatus.Unavailable,
            -9999 => OsKeyringStatus.CreationUncertain,
            _ => expected
        });
        await Assert.That(created.Creation).IsEqualTo(error == -9999
            ? OsKeyringCreation.Uncertain : OsKeyringCreation.NotAttempted);
        await Assert.That(api.Entries.Count).IsEqualTo(0);
        await Assert.That(api.LiveObjects).IsEqualTo(0);
        await Assert.That(api.InteractionAllowed).IsEqualTo((byte)1);
    }

    [Test]
    [Arguments(-1, false, false)]
    [Arguments(0, false, false)]
    [Arguments(43, false, false)]
    [Arguments(45, false, false)]
    [Arguments(long.MaxValue, false, false)]
    [Arguments(44, true, false)]
    [Arguments(44, false, true)]
    public async Task WrongTypeLengthOrNullBytesRejectBeforeCopy(long length, bool wrongType, bool nullBytes)
    {
        using var api = new KeychainApi { ReturnedLength = length, WrongType = wrongType, NullBytes = nullBytes };
        using var result = await Provider(api).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(api.LiveObjects).IsEqualTo(0);
    }

    [Test]
    [Arguments("invalid-alphabet")]
    [Arguments("padding-bits")]
    [Arguments("wrong-decoded-length")]
    [Arguments("whitespace")]
    public async Task NonCanonicalNativeDataNeverBecomesALease(string malformed)
    {
        byte[] value = Encoding.ASCII.GetBytes(new string('A', 43) + "=");
        if (malformed == "invalid-alphabet") value[0] = 255;
        if (malformed == "padding-bits") value[42] = (byte)'B';
        if (malformed == "wrong-decoded-length") value[43] = (byte)'A';
        if (malformed == "whitespace") value[0] = (byte)' ';
        using var api = new KeychainApi { ReturnedValue = value };
        using var result = await Provider(api).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(api.LiveObjects).IsEqualTo(0);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task PartialQueryAllocationFailureReleasesEveryOwnedObject(int allocation)
    {
        foreach (bool creating in new[] { false, true })
        {
            using var api = new KeychainApi { FailAllocation = allocation };
            var provider = Provider(api);
            using var result = creating
                ? await provider.CreateAsync(OsKeyringPurpose.NativeRecipient)
                : await provider.LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
            await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.Unavailable);
            await Assert.That(result.Creation).IsEqualTo(OsKeyringCreation.NotAttempted);
            await Assert.That(api.Entries.Count).IsEqualTo(0);
            await Assert.That(api.LiveObjects).IsEqualTo(0);
            await Assert.That(api.InteractionAllowed).IsEqualTo((byte)1);
        }
    }

    [Test]
    public async Task NullSuccessfulResultIsRejected()
    {
        using var api = new KeychainApi { ReturnedLength = 44, FailAllocation = 6 };
        using var result = await Provider(api).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(api.LiveObjects).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationRetainsPhysicalWriteAndKnownOrUncertainCreation(bool throws)
    {
        using var api = new KeychainApi { HoldAdd = true, ThrowAfterAdd = throws };
        var provider = Provider(api);
        using var cancellation = new CancellationTokenSource();
        Task<OsKeyringResult> pending = provider.CreateAsync(OsKeyringPurpose.NativeRecipient, cancellation.Token).AsTask();
        await api.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        try
        {
            await Assert.That(pending.IsCompleted).IsFalse();
            using var competing = await Provider(api).CreateAsync(OsKeyringPurpose.ComparisonKey);
            await Assert.That(competing.Status).IsEqualTo(OsKeyringStatus.Busy);
            await Assert.That(api.InteractionAllowed).IsEqualTo((byte)0);
        }
        finally { api.Continue.TrySetResult(); }
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result.Status).IsEqualTo(throws ? OsKeyringStatus.CreationUncertain : OsKeyringStatus.Cancelled);
        await Assert.That(result.Creation).IsEqualTo(throws ? OsKeyringCreation.Uncertain : OsKeyringCreation.Created);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(result.KeyId != Guid.Empty).IsTrue();
        await Assert.That((JsonSerializer.Serialize(result) + result).Contains("synthetic-private-diagnostic", StringComparison.Ordinal)).IsFalse();
        await Assert.That(api.LiveObjects).IsEqualTo(0);
        await Assert.That(api.InteractionAllowed).IsEqualTo((byte)1);
        using var recovered = await provider.LookupAsync(result.KeyId, result.Purpose);
        await Assert.That(recovered.Status).IsEqualTo(OsKeyringStatus.Success);
    }

    [Test]
    public async Task CancellationDuringResultReleaseKeepsGlobalPermitUntilCleanupEnds()
    {
        using var api = new KeychainApi { ReturnedLength = 44, HoldResultRelease = true };
        using var cancellation = new CancellationTokenSource();
        Task<OsKeyringResult> pending = Provider(api).LookupAsync(Guid.NewGuid(),
            OsKeyringPurpose.NativeRecipient, cancellation.Token).AsTask();
        await api.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        try
        {
            await Assert.That(pending.IsCompleted).IsFalse();
            using var competing = await Provider(api).CreateAsync(OsKeyringPurpose.ComparisonKey);
            await Assert.That(competing.Status).IsEqualTo(OsKeyringStatus.Busy);
        }
        finally { api.Continue.TrySetResult(); }
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.Cancelled);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(api.LiveObjects).IsEqualTo(0);
        await Assert.That(api.InteractionAllowed).IsEqualTo((byte)1);
    }

    [Test]
    [Arguments((byte)0)]
    [Arguments((byte)1)]
    public async Task ExistingInteractionPolicyIsRestored(byte initial)
    {
        using var api = new KeychainApi { InteractionAllowed = initial };
        using var result = await Provider(api).CreateAsync(OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(api.InteractionAllowed).IsEqualTo(initial);
        await Assert.That(api.LiveObjects).IsEqualTo(0);
    }

    [Test]
    [Arguments("get")]
    [Arguments("disable")]
    [Arguments("keychain")]
    [Arguments("restore")]
    public async Task SetupOrRestorationFailureRefusesAndPreservesEstablishedCreation(string stage)
    {
        using var api = new KeychainApi { FailureStage = stage };
        using var result = await Provider(api).CreateAsync(OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.Unavailable);
        await Assert.That(result.Creation).IsEqualTo(stage == "restore"
            ? OsKeyringCreation.Created : OsKeyringCreation.NotAttempted);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(api.Entries.Count).IsEqualTo(stage == "restore" ? 1 : 0);
        await Assert.That(api.LiveObjects).IsEqualTo(0);
    }

    [Test]
    public async Task PreCancellationAndUnsupportedHostsNeverEnterNativeStorage()
    {
        using var api = new KeychainApi();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var cancelled = await Provider(api).CreateAsync(OsKeyringPurpose.NativeRecipient, cancellation.Token);
        await Assert.That(cancelled.Status).IsEqualTo(OsKeyringStatus.Cancelled);
        await Assert.That(api.Allocations).IsEqualTo(0);
        if (!OperatingSystem.IsMacOS())
        {
            using var unsupported = await new OsKeyringProvider(MacOsKeychainProvider.CreateNative())
                .CreateAsync(OsKeyringPurpose.NativeRecipient);
            await Assert.That(unsupported.Status).IsEqualTo(OsKeyringStatus.UnsupportedPlatform);
        }
    }

    private static OsKeyringProvider Provider(KeychainApi api) => new(new MacOsKeychainProvider(api));

    // A finite CF object store models public handles, query semantics and borrowed constants.
    // The production adapter, not this fixture, assembles queries, maps errors and admits blobs.
    private sealed class KeychainApi : MacOsKeychainProvider.IKeychainApi, IDisposable
    {
        private sealed record NativeObject(object Value, nint Bytes = 0);
        private readonly Dictionary<nint, NativeObject> _objects = [];
        private readonly Dictionary<string, nint> _constants = [];
        private nint _returned;
        internal Dictionary<(string Service, string Account), byte[]> Entries { get; } = [];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte InteractionAllowed { get; set; } = 1;
        internal int Error { get; init; }
        internal int FailAllocation { get; init; }
        internal string? FailureStage { get; init; }
        internal bool Collision { get; init; }
        internal bool ReturnDataOnFailure { get; init; }
        internal long? ReturnedLength { get; init; }
        internal byte[]? ReturnedValue { get; init; }
        internal bool WrongType { get; init; }
        internal bool NullBytes { get; init; }
        internal bool HoldAdd { get; init; }
        internal bool ThrowAfterAdd { get; init; }
        internal bool HoldResultRelease { get; init; }
        internal int Allocations { get; private set; }
        internal int LiveObjects => _objects.Count;
        public bool IsSupported => true;

        public int GetInteraction(out byte allowed) { allowed = InteractionAllowed; return FailureStage == "get" ? -25291 : 0; }
        public int SetInteraction(byte allowed)
        {
            if (FailureStage == (allowed == 0 ? "disable" : "restore")) return -25291;
            InteractionAllowed = allowed;
            return 0;
        }
        public int CopyDefaultKeychain(int domain, out nint keychain)
        {
            keychain = Own(domain == 0 ? "user-keychain" : "other-keychain");
            return FailureStage == "keychain" ? -25291 : 0;
        }
        public nint Constant(string name)
        {
            if (!_constants.TryGetValue(name, out nint handle))
                _constants.Add(name, handle = -(_constants.Count + 1));
            return handle;
        }
        public nint CreateString(string value) => Own(value);
        public nint CreateData(ReadOnlySpan<byte> value) => Own(value.ToArray());
        public nint CreateArray(ReadOnlySpan<nint> values) => Own(values.ToArray());
        public nint CreateDictionary(ReadOnlySpan<nint> keys, ReadOnlySpan<nint> values)
        {
            Dictionary<nint, nint> entries = [];
            for (int i = 0; i < keys.Length; i++) entries.Add(keys[i], values[i]);
            return Own(entries);
        }
        public int Add(nint attributes)
        {
            var (query, identity) = Query(attributes);
            if ((string)Object(query[Constant("kSecUseKeychain")]) != "user-keychain") return -25243;
            if (query.Count != 5) return -50;
            if (Error != 0) return Error;
            if (Collision) Entries.TryAdd(identity, new byte[44]);
            if (Entries.ContainsKey(identity)) return -25299;
            Entries.Add(identity, ((byte[])Object(query[Constant("kSecValueData")])).ToArray());
            if (HoldAdd) Hold();
            if (ThrowAfterAdd) throw new IOException("synthetic-private-diagnostic");
            return 0;
        }
        public int CopyMatching(nint queryHandle, out nint result)
        {
            var (query, identity) = Query(queryHandle);
            nint[] search = (nint[])Object(query[Constant("kSecMatchSearchList")]);
            if (query.Count != 6 || search.Length != 1 ||
                (string)Object(search[0]) != "user-keychain" ||
                query[Constant("kSecReturnData")] != Constant("kCFBooleanTrue") ||
                query[Constant("kSecMatchLimit")] != Constant("kSecMatchLimitOne"))
                throw new InvalidOperationException("Invalid query scope.");
            result = 0;
            if (Error != 0)
            {
                if (ReturnDataOnFailure) result = _returned = CreateData(new byte[44]);
                return Error;
            }
            byte[]? value = ReturnedValue;
            if (value is null && ReturnedLength is not null) value = Encoding.ASCII.GetBytes(new string('A', 43) + "=");
            if (value is null && !Entries.TryGetValue(identity, out value)) return -25300;
            result = _returned = CreateData(value);
            return 0;
        }
        public nuint GetTypeId(nint value) => WrongType && value == _returned ? 99u : 17u;
        public nuint GetDataTypeId() => 17;
        public nint GetDataLength(nint value) => checked((nint)(ReturnedLength ?? ((byte[])Object(value)).Length));
        public nint GetDataBytes(nint value)
        {
            if (ReturnedLength is not null and not 44)
                throw new InvalidOperationException("Unbounded native data dereference.");
            return NullBytes ? 0 : _objects[value].Bytes;
        }
        public void Release(nint value)
        {
            if (value == _returned && HoldResultRelease) Hold();
            if (!_objects.Remove(value, out NativeObject? entry))
                throw new InvalidOperationException("Released borrowed or already released object.");
            if (entry.Value is byte[] bytes)
            {
                CryptographicOperations.ZeroMemory(bytes);
                Marshal.Copy(bytes, 0, entry.Bytes, bytes.Length);
                Marshal.FreeHGlobal(entry.Bytes);
            }
        }
        private object Object(nint value) => _objects[value].Value;
        private nint Own(object value)
        {
            int handle = ++Allocations;
            if (handle == FailAllocation) return 0;
            nint bytes = 0;
            if (value is byte[] data)
            {
                bytes = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, bytes, data.Length);
            }
            _objects.Add(handle, new(value, bytes));
            return handle;
        }
        private (Dictionary<nint, nint> Query, (string Service, string Account) Identity) Query(nint handle)
        {
            if (InteractionAllowed != 0) throw new InvalidOperationException("Interactive keychain operation.");
            var query = (Dictionary<nint, nint>)Object(handle);
            if (query[Constant("kSecClass")] != Constant("kSecClassGenericPassword"))
                throw new InvalidOperationException("Wrong credential class.");
            string service = (string)Object(query[Constant("kSecAttrService")]);
            string account = (string)Object(query[Constant("kSecAttrAccount")]);
            if (service is not ("org.islamu.configurator/profile/NativeRecipient" or "org.islamu.configurator/profile/ComparisonKey") ||
                !Guid.TryParseExact(account, "N", out _))
                throw new InvalidOperationException("Invalid credential identity.");
            return (query, (service, account));
        }
        private void Hold()
        {
            Entered.TrySetResult();
            Continue.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        public void Dispose()
        {
            Continue.TrySetResult();
            foreach (nint handle in _objects.Keys.ToArray()) Release(handle);
            foreach (byte[] value in Entries.Values) CryptographicOperations.ZeroMemory(value);
        }
    }
}
