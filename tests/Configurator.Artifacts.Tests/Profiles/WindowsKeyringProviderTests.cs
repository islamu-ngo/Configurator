namespace Configurator.Artifacts.Tests.Profiles;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Artifacts.Profiles;

[NotInParallel]
public sealed class WindowsKeyringProviderTests
{
    [Test]
    public async Task MissingLookupDoesNotCreateAndIndependentKeysRoundTripByPurpose()
    {
        using var api = new CredentialApi();
        var provider = Provider(api);
        using var missing = await provider.LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(missing.Status).IsEqualTo(OsKeyringStatus.Missing);
        await Assert.That(api.Entries.Count).IsEqualTo(0);
        using var first = await provider.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var second = await provider.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(first.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(second.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(first.KeyId == second.KeyId).IsFalse();
        await Assert.That(api.Entries.Count).IsEqualTo(2);
        using var read = await provider.LookupAsync(first.KeyId, first.Purpose);
        byte[] expected = first.Lease!.CopyRecipientUtf8();
        byte[] actual = read.Lease!.CopyRecipientUtf8();
        try { await Assert.That(actual).IsEquivalentTo(expected); }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
        using var wrongPurpose = await provider.LookupAsync(first.KeyId, OsKeyringPurpose.ComparisonKey);
        await Assert.That(wrongPurpose.Status).IsEqualTo(OsKeyringStatus.Missing);
        await Assert.That(api.Freed).IsEqualTo(1);
        await Assert.That(api.WireValid).IsTrue();
        await Assert.That(api.Entries.Values.First().SequenceEqual(api.Entries.Values.Last())).IsFalse();
        using var commitment = second.Lease!.CreateCommitment();
        await Assert.That(commitment.KeyId).IsEqualTo(second.KeyId);
        string safe = JsonSerializer.Serialize(first) + JsonSerializer.Serialize(first.Lease) + first;
        foreach (byte[] blob in api.Entries.Values)
            await Assert.That(safe.Contains(System.Text.Encoding.UTF8.GetString(blob), StringComparison.Ordinal)).IsFalse();
        first.Dispose();
        await Assert.That(() => first.Lease.CopyRecipientUtf8()).Throws<ObjectDisposedException>();
        await Assert.That(api.Entries.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(43, false)]
    [Arguments(45, false)]
    [Arguments(2560, false)]
    [Arguments(2561, false)]
    [Arguments(-1, false)]
    [Arguments(44, true)]
    public async Task InvalidNativeBlobBoundsRejectBeforeDereferenceAndStillFree(int length, bool nullBlob)
    {
        using var api = new CredentialApi { BlobLength = unchecked((uint)length), NullBlob = nullBlob };
        using var result = await Provider(api).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(api.Freed).IsEqualTo(1);
    }

    [Test]
    public async Task MalformedCanonicalBlobIsRejectedAfterNativeAllocationIsFreed()
    {
        using var api = new CredentialApi { BlobLength = 44 };
        using var result = await Provider(api).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(api.Freed).IsEqualTo(1);
    }

    [Test]
    public async Task SuccessfulReadWithNullAllocationRejectsWithoutFreeingNull()
    {
        using var api = new CredentialApi { NullAllocation = true };
        using var result = await Provider(api).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(api.Freed).IsEqualTo(0);
    }

    [Test]
    [Arguments(1168, OsKeyringStatus.Missing)]
    [Arguments(1312, OsKeyringStatus.Unavailable)]
    [Arguments(5, OsKeyringStatus.Denied)]
    [Arguments(170, OsKeyringStatus.Busy)]
    [Arguments(1223, OsKeyringStatus.Cancelled)]
    [Arguments(87, OsKeyringStatus.InvalidRequest)]
    [Arguments(1004, OsKeyringStatus.InvalidRequest)]
    [Arguments(9999, OsKeyringStatus.Unavailable)]
    public async Task NativeErrorCodesMapWithoutExposingDiagnostics(int error, OsKeyringStatus expected)
    {
        using var api = new CredentialApi { Error = error };
        var provider = Provider(api);
        using var read = await provider.LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        using var write = await provider.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(read.Status).IsEqualTo(expected);
        await Assert.That(write.Status).IsEqualTo(error == 1168 ? OsKeyringStatus.Unavailable : expected);
        await Assert.That(write.Creation).IsEqualTo(OsKeyringCreation.NotAttempted);
        await Assert.That(api.Entries.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationWaitsForPhysicalWriteAndRetainsCreatedOrUncertainIdentity(bool throws)
    {
        using var api = new CredentialApi { HoldWrite = true, ThrowAfterWrite = throws };
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
            await Assert.That(api.Entries.Count).IsEqualTo(1);
        }
        finally { api.Release.TrySetResult(); }
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result.Status).IsEqualTo(throws ? OsKeyringStatus.CreationUncertain : OsKeyringStatus.Cancelled);
        await Assert.That(result.Creation).IsEqualTo(throws ? OsKeyringCreation.Uncertain : OsKeyringCreation.Created);
        await Assert.That(result.Lease is null).IsTrue();
        using var recovered = await provider.LookupAsync(result.KeyId, result.Purpose);
        await Assert.That(recovered.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That((JsonSerializer.Serialize(result) + result).Contains("synthetic-private-diagnostic", StringComparison.Ordinal)).IsFalse();
        using var subsequent = await provider.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(subsequent.KeyId == result.KeyId).IsFalse();
        await Assert.That(api.Entries.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CancellationDuringReadCleanupRetainsPermitUntilFreeCompletes()
    {
        using var api = new CredentialApi();
        var provider = Provider(api);
        using var created = await provider.CreateAsync(OsKeyringPurpose.NativeRecipient);
        api.HoldFree = true;
        using var cancellation = new CancellationTokenSource();
        Task<OsKeyringResult> pending = provider.LookupAsync(created.KeyId, created.Purpose, cancellation.Token).AsTask();
        await api.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        try
        {
            await Assert.That(pending.IsCompleted).IsFalse();
            using var competing = await Provider(api).CreateAsync(OsKeyringPurpose.ComparisonKey);
            await Assert.That(competing.Status).IsEqualTo(OsKeyringStatus.Busy);
        }
        finally { api.Release.TrySetResult(); }
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.Cancelled);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(api.Freed).IsEqualTo(1);
    }

    [Test]
    public async Task PreCancelledWorkNeverEntersNativeStore()
    {
        using var api = new CredentialApi();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var result = await Provider(api).CreateAsync(OsKeyringPurpose.NativeRecipient, cancellation.Token);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.Cancelled);
        await Assert.That(result.Creation).IsEqualTo(OsKeyringCreation.NotAttempted);
        await Assert.That(api.Entries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task UnsupportedPlatformRefusesWithoutEnteringApi()
    {
        using var api = new CredentialApi { IsSupported = false };
        using var result = await Provider(api).CreateAsync(OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.UnsupportedPlatform);
        await Assert.That(api.Entries.Count).IsEqualTo(0);
        if (!OperatingSystem.IsWindows())
        {
            using var actual = await new OsKeyringProvider(WindowsCredentialKeyringProvider.CreateNative())
                .LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
            await Assert.That(actual.Status).IsEqualTo(OsKeyringStatus.UnsupportedPlatform);
        }
    }

    private static OsKeyringProvider Provider(CredentialApi api) =>
        new(new WindowsCredentialKeyringProvider(api));

    // Models the public CREDENTIALW memory layout independently, not a managed-result mock.
    private sealed class CredentialApi : WindowsCredentialKeyringProvider.ICredentialApi, IDisposable
    {
        internal Dictionary<string, byte[]> Entries { get; } = [];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal uint? BlobLength { get; init; }
        internal bool NullBlob { get; init; }
        internal bool NullAllocation { get; init; }
        internal bool HoldWrite { get; init; }
        internal bool HoldFree { get; set; }
        internal bool ThrowAfterWrite { get; init; }
        internal int Error { get; init; }
        internal int Freed { get; private set; }
        internal bool WireValid { get; private set; } = true;
        public bool IsSupported { get; init; } = true;
        private static int HeaderSize => IntPtr.Size == 8 ? 80 : 52;
        private static int SizeOffset => IntPtr.Size == 8 ? 32 : 24;
        private static int BlobOffset => IntPtr.Size == 8 ? 40 : 28;

        public bool Read(string target, out nint credential, out int error)
        {
            error = Error;
            credential = 0;
            if (error != 0) return false;
            if (NullAllocation) return true;
            if (BlobLength is null && !Entries.ContainsKey(target)) { error = 1168; return false; }
            credential = Marshal.AllocHGlobal(HeaderSize + 44);
            Marshal.Copy(new byte[HeaderSize + 44], 0, credential, HeaderSize + 44);
            Marshal.WriteInt32(credential, 4, 1);
            Marshal.WriteInt32(credential, SizeOffset, unchecked((int)(BlobLength ?? 44)));
            // An invalid non-null address makes premature dereference fail, even for large sizes.
            nint blob = NullBlob ? 0 : BlobLength is null or 44 ? credential + HeaderSize : 1;
            Marshal.WriteIntPtr(credential, BlobOffset, blob);
            if (BlobLength is null) Marshal.Copy(Entries[target], 0, blob, 44);
            return true;
        }

        public bool Write(ref WindowsCredentialKeyringProvider.Credential credential, out int error)
        {
            error = Error;
            if (error != 0) return false;
            nint header = Marshal.AllocHGlobal(HeaderSize);
            try
            {
                Marshal.StructureToPtr(credential, header, false);
                WireValid &= Marshal.SizeOf<WindowsCredentialKeyringProvider.Credential>() == HeaderSize &&
                    Marshal.ReadInt32(header, 0) == 0 && Marshal.ReadInt32(header, 4) == 1 &&
                    Marshal.ReadInt32(header, SizeOffset) == 44 &&
                    Marshal.ReadInt32(header, BlobOffset + IntPtr.Size) == 2 &&
                    Marshal.ReadInt32(header, BlobOffset + IntPtr.Size + 4) == 0;
                string target = Marshal.PtrToStringUni(Marshal.ReadIntPtr(header, 8))!;
                byte[] value = new byte[44];
                Marshal.Copy(Marshal.ReadIntPtr(header, BlobOffset), value, 0, value.Length);
                // CredWrite replaces: the test must catch any accidental shared target.
                if (Entries.Remove(target, out byte[]? old)) CryptographicOperations.ZeroMemory(old);
                Entries[target] = value;
                if (HoldWrite && Entries.Count == 1)
                {
                    Entered.TrySetResult();
                    Release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                }
                if (ThrowAfterWrite && Entries.Count == 1) throw new IOException("synthetic-private-diagnostic");
                return true;
            }
            finally { Marshal.FreeHGlobal(header); }
        }

        public void Free(nint credential)
        {
            if (HoldFree)
            {
                Entered.TrySetResult();
                Release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
            // Poison before freeing: the returned lease must own a copy.
            Marshal.Copy(new byte[HeaderSize + 44], 0, credential, HeaderSize + 44);
            Marshal.FreeHGlobal(credential);
            Freed++;
        }

        public void Dispose()
        {
            Release.TrySetResult();
            foreach (byte[] value in Entries.Values) CryptographicOperations.ZeroMemory(value);
        }
    }
}
