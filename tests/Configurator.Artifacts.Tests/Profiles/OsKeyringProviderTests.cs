namespace Configurator.Artifacts.Tests.Profiles;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Artifacts.Profiles;

[NotInParallel]
public sealed class OsKeyringProviderTests
{
    [Test]
    public async Task MissingLookupNeverCreatesOrReplacesAnotherCredential()
    {
        using var native = new NativeStore();
        var provider = new OsKeyringProvider(native);
        using var installed = await provider.CreateAsync(OsKeyringPurpose.NativeRecipient);
        byte[] original = native.Entries.Single().Value.ToArray();
        using var absent = await provider.LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(absent.Status).IsEqualTo(OsKeyringStatus.Missing);
        await Assert.That(absent.Lease is null).IsTrue();
        await Assert.That(native.Entries.Count).IsEqualTo(1);
        await Assert.That(native.Entries.Single().Value).IsEquivalentTo(original);
        using var existing = await provider.LookupAsync(installed.KeyId, OsKeyringPurpose.NativeRecipient);
        await Assert.That(existing.Status).IsEqualTo(OsKeyringStatus.Success);
        CryptographicOperations.ZeroMemory(original);
    }

    [Test]
    public async Task CreationOwnsIndependentIdentifiersAndCanonicalPurposeSeparatedKeys()
    {
        using var native = new NativeStore();
        var provider = new OsKeyringProvider(native);
        using var recipient = await provider.CreateAsync(OsKeyringPurpose.NativeRecipient);
        using var comparison = await provider.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(recipient.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(comparison.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(recipient.Creation).IsEqualTo(OsKeyringCreation.Created);
        await Assert.That(recipient.KeyId == comparison.KeyId).IsFalse();
        await Assert.That(recipient.KeyId == Guid.Empty).IsFalse();
        byte[] first = native.Entries[(recipient.KeyId, recipient.Purpose)];
        byte[] second = native.Entries[(comparison.KeyId, comparison.Purpose)];
        await Assert.That(first.Length).IsEqualTo(44);
        await Assert.That(second.Length).IsEqualTo(44);
        await Assert.That(first.SequenceEqual(second)).IsFalse();
        byte[] decoded = Convert.FromBase64String(Encoding.UTF8.GetString(first));
        await Assert.That(decoded.Length).IsEqualTo(32);
        CryptographicOperations.ZeroMemory(decoded);
        using var wrongPurpose = await provider.LookupAsync(recipient.KeyId, OsKeyringPurpose.ComparisonKey);
        await Assert.That(wrongPurpose.Status).IsEqualTo(OsKeyringStatus.Missing);
    }

    [Test]
    public async Task ConcurrentProvidersRejectWorkUntilPhysicalNativeCleanupCompletes()
    {
        using var native = new NativeStore { Hold = true };
        var first = new OsKeyringProvider(native);
        var second = new OsKeyringProvider(native);
        Task<OsKeyringResult> pending = first.CreateAsync(OsKeyringPurpose.NativeRecipient).AsTask();
        await native.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var concurrent = await second.CreateAsync(OsKeyringPurpose.ComparisonKey);
            await Assert.That(concurrent.Status).IsEqualTo(OsKeyringStatus.Busy);
            await Assert.That(native.Entries.Count).IsEqualTo(1);
        }
        finally
        {
            native.Cleanup.TrySetResult();
            using var drained = await pending;
            await native.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        using var completed = await pending;
        using var subsequent = await second.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(completed.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(subsequent.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(native.Entries.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationRetainsPermitAndKnownIdentifierThroughPhysicalCreation(bool established)
    {
        using var native = new NativeStore { Hold = true, EstablishedOnCancellation = established };
        var provider = new OsKeyringProvider(native);
        using var cancellation = new CancellationTokenSource();
        Task<OsKeyringResult> pending = provider.CreateAsync(OsKeyringPurpose.NativeRecipient, cancellation.Token).AsTask();
        await native.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await native.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await Assert.That(pending.IsCompleted).IsFalse();
            await Assert.That(native.CreateInput.ToArray()).IsEquivalentTo(native.Entries.Single().Value);
            using var competing = await new OsKeyringProvider(native).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
            await Assert.That(competing.Status).IsEqualTo(OsKeyringStatus.Busy);
        }
        finally
        {
            native.Cleanup.TrySetResult();
            using var drained = await pending;
            await native.Finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        using var cancelled = await pending;
        await Assert.That(cancelled.Status).IsEqualTo(established ? OsKeyringStatus.Cancelled : OsKeyringStatus.CreationUncertain);
        await Assert.That(cancelled.Creation).IsEqualTo(established ? OsKeyringCreation.Created : OsKeyringCreation.Uncertain);
        await Assert.That(cancelled.KeyId).IsEqualTo(native.Entries.Single().Key.Id);
        await Assert.That(cancelled.Lease is null).IsTrue();
        await Assert.That(native.CreateInput.ToArray().All(value => value == 0)).IsTrue();
        using var recovered = await provider.LookupAsync(cancelled.KeyId, cancelled.Purpose);
        await Assert.That(recovered.Status).IsEqualTo(OsKeyringStatus.Success);
        await Assert.That(native.Entries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CancellationBeforeEntryDoesNotGenerateOrPublishAKey()
    {
        using var native = new NativeStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var result = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.NativeRecipient, cancellation.Token);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.Cancelled);
        await Assert.That(result.Creation).IsEqualTo(OsKeyringCreation.NotAttempted);
        await Assert.That(result.KeyId).IsEqualTo(Guid.Empty);
        await Assert.That(native.Entries.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(OsKeyringStatus.ServiceMissing)]
    [Arguments(OsKeyringStatus.Locked)]
    [Arguments(OsKeyringStatus.Denied)]
    [Arguments(OsKeyringStatus.Unavailable)]
    [Arguments(OsKeyringStatus.Cancelled)]
    public async Task NativeRefusalsRemainDistinctAndNeverBecomeMissingOrCreated(OsKeyringStatus status)
    {
        using var native = new NativeStore { Failure = status };
        var provider = new OsKeyringProvider(native);
        using var lookup = await provider.LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        using var creation = await provider.CreateAsync(OsKeyringPurpose.ComparisonKey);
        await Assert.That(lookup.Status).IsEqualTo(status);
        await Assert.That(creation.Status).IsEqualTo(status);
        await Assert.That(creation.Creation).IsEqualTo(OsKeyringCreation.NotAttempted);
        await Assert.That(native.Entries.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(43)]
    [Arguments(45)]
    [Arguments(44)]
    public async Task MalformedNativeValuesAreRejectedAndTheirOwnedBuffersCleared(int length)
    {
        using var native = new NativeStore { InvalidValue = Enumerable.Repeat((byte)'!', length).ToArray() };
        using var result = await new OsKeyringProvider(native).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(native.ReturnedValue!.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task NoncanonicalBase64CannotBecomeARecipientCredential()
    {
        using var native = new NativeStore { InvalidValue = Encoding.ASCII.GetBytes(new string('A', 42) + "B=") };
        using var result = await new OsKeyringProvider(native).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.InvalidValue);
        await Assert.That(result.Lease is null).IsTrue();
        await Assert.That(native.ReturnedValue!.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task LeaseSerializationAndDisposalPreserveOnlySafeMetadata()
    {
        using var native = new NativeStore();
        using var result = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.ComparisonKey);
        var lease = result.Lease!;
        string secret = Encoding.UTF8.GetString(native.Entries.Single().Value);
        string sinks = JsonSerializer.Serialize(result) + JsonSerializer.Serialize(lease) + result + lease;
        await Assert.That(sinks.Contains(secret, StringComparison.Ordinal)).IsFalse();
        using (JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(lease)))
        {
            await Assert.That(json.RootElement.EnumerateObject().Count()).IsEqualTo(2);
            await Assert.That(json.RootElement.GetProperty("KeyId").GetGuid()).IsEqualTo(result.KeyId);
            await Assert.That(json.RootElement.TryGetProperty("Purpose", out _)).IsTrue();
        }
        using var commitment = lease.CreateCommitment();
        await Assert.That(commitment.KeyId).IsEqualTo(result.KeyId);
        result.Dispose();
        await Assert.That(() => lease.CreateCommitment()).Throws<ObjectDisposedException>();
        await Assert.That(JsonSerializer.Serialize(lease).Contains(secret, StringComparison.Ordinal)).IsFalse();
        await Assert.That(native.Entries.Count).IsEqualTo(1);
        using var recipient = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.NativeRecipient);
        await Assert.That(() => recipient.Lease!.CreateCommitment()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BoundaryExceptionsCannotExposeRawNativeDiagnostics()
    {
        using var native = new NativeStore { Throw = true };
        using var lookup = await new OsKeyringProvider(native).LookupAsync(Guid.NewGuid(), OsKeyringPurpose.NativeRecipient);
        using var create = await new OsKeyringProvider(native).CreateAsync(OsKeyringPurpose.NativeRecipient);
        await Assert.That(lookup.Status).IsEqualTo(OsKeyringStatus.Unavailable);
        await Assert.That(create.Status).IsEqualTo(OsKeyringStatus.CreationUncertain);
        await Assert.That(create.KeyId == Guid.Empty).IsFalse();
        await Assert.That((JsonSerializer.Serialize(create) + create).Contains("native-private-sentinel", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task UnsupportedHostHasNoExecutableAdapter()
    {
        using var result = await new OsKeyringProvider(null).CreateAsync(OsKeyringPurpose.NativeRecipient);
        await Assert.That(result.Status).IsEqualTo(OsKeyringStatus.UnsupportedPlatform);
        await Assert.That(result.Creation).IsEqualTo(OsKeyringCreation.NotAttempted);
        await Assert.That(result.Lease is null).IsTrue();
    }

    private sealed class NativeStore : IOsKeyringNative, IDisposable
    {
        internal Dictionary<(Guid Id, OsKeyringPurpose Purpose), byte[]> Entries { get; } = [];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Hold { get; init; }
        internal bool EstablishedOnCancellation { get; init; }
        internal bool Throw { get; init; }
        internal OsKeyringStatus? Failure { get; init; }
        internal byte[]? InvalidValue { get; init; }
        internal byte[]? ReturnedValue { get; private set; }
        internal ReadOnlyMemory<byte> CreateInput { get; private set; }

        public async ValueTask<OsKeyringNativeResult> LookupAsync(Guid id, OsKeyringPurpose purpose, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (Throw) throw new IOException("native-private-sentinel");
            if (Failure is { } failure) return new(failure);
            if (InvalidValue is not null)
            {
                ReturnedValue = InvalidValue.ToArray();
                return new(OsKeyringStatus.Success, ReturnedValue);
            }
            return Entries.TryGetValue((id, purpose), out byte[]? value)
                ? new OsKeyringNativeResult(OsKeyringStatus.Success, value.ToArray())
                : new OsKeyringNativeResult(OsKeyringStatus.Missing);
        }

        public async ValueTask<OsKeyringNativeResult> CreateAsync(Guid id, OsKeyringPurpose purpose,
            ReadOnlyMemory<byte> canonicalUtf8, CancellationToken cancellationToken)
        {
            if (Throw) throw new IOException("native-private-sentinel");
            if (Failure is { } failure) return new(failure);
            CreateInput = canonicalUtf8;
            Entries.Add((id, purpose), canonicalUtf8.ToArray());
            if (Hold && Entries.Count == 1)
            {
                using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
                Entered.TrySetResult();
                await Cleanup.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            }
            Finished.TrySetResult();
            return cancellationToken.IsCancellationRequested && !EstablishedOnCancellation
                ? new(OsKeyringStatus.Cancelled, creation: OsKeyringCreation.Uncertain)
                : new(OsKeyringStatus.Success, creation: OsKeyringCreation.Created);
        }

        public void Dispose()
        {
            foreach (byte[] value in Entries.Values) CryptographicOperations.ZeroMemory(value);
            if (InvalidValue is not null) CryptographicOperations.ZeroMemory(InvalidValue);
        }
    }
}
