namespace Configurator.Browser.Tests.Profiles;

using System.Security.Cryptography;
using System.Buffers;
using System.Text;
using System.Text.Json;
using Configurator.Browser.Profiles;
using Configurator.Browser.Worker.Profiles;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Core.Profiles;
using Microsoft.JSInterop;

[NotInParallel]
public sealed class ProfileWorkerClientTests
{
    private const string Hidden = "synthetic-worker-retained-secret";

    [Test]
    public async Task UnlockCodecPolicyAndCryptoUseTheSameCredentialSnapshot()
    {
        using var memory = new ChangingMemory("synthetic-first-passphrase", "synthetic-other-passphrase", 1);
        var crypto = new RecordingCrypto();
        var codec = new BrowserProfileContainerCodec(crypto);
        await Assert.That(async () => await codec.OpenAsync(Shape(), 0, memory.Value)).Throws<ProfileException>();
        await Assert.That(crypto.DecryptedCredential).IsEqualTo("synthetic-first-passphrase");
    }

    [Test]
    public async Task UnlockClientPolicyAndTransferUseTheSameCredentialSnapshot()
    {
        using var memory = new ChangingMemory("synthetic-first-passphrase", "synthetic-other-passphrase", 1);
        using var module = new RecordingModule();
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, Authority);
        await client.OpenAsync(Shape(), 0, memory.Value, _ => true);
        await Assert.That(module.Credential).IsEqualTo("synthetic-first-passphrase");
    }

    [Test]
    public async Task NativeVaultPortableRecipientCanReachTheBrowserRecoveryBoundary()
    {
        byte[] container = GeneralShape();
        var trusted = new ProfileRecoveryReference(Guid.NewGuid(), Guid.NewGuid(),
            ArtifactDigest.Compute(container), Guid.NewGuid(), portableRecoveryConfigured: true);
        using var module = new RecordingModule();
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, Authority);
        await client.VerifyRecoveryAsync(trusted, container, 1, "synthetic-first-passphrase"u8.ToArray());
        await Assert.That(module.Started).IsTrue();
    }

    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    public async Task NativeVaultDeviceRecipientNeverReachesTheBrowserRecoveryBoundary(int index)
    {
        byte[] container = GeneralShape();
        var trusted = new ProfileRecoveryReference(Guid.NewGuid(), Guid.NewGuid(),
            ArtifactDigest.Compute(container), Guid.NewGuid(), portableRecoveryConfigured: true);
        using var module = new RecordingModule();
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, Authority);
        await Assert.That(async () => await client.VerifyRecoveryAsync(trusted, container, index,
            "synthetic-first-passphrase"u8.ToArray())).Throws<ProfileException>();
        await Assert.That(module.Started).IsFalse();
    }

    [Test]
    public async Task StaleUnlockReplyRetiresItsUnadoptedCandidate()
    {
        ProfileWorkerAuthority authority = Authority();
        using var module = new RecordingModule { HoldExecution = true, Succeed = true };
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, () => authority);
        Task<ProfileWorkerReply> pending = client.OpenAsync(Shape(), 0,
            "synthetic-first-passphrase"u8.ToArray(), _ => true);
        await module.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        authority = authority with { Generation = 2 };
        module.Release.TrySetResult();
        await Assert.That(async () => await pending).Throws<ProfileException>();
        await Assert.That(module.Retired).IsTrue();
        await Assert.That(module.Adopted).IsFalse();
    }

    [Test]
    public async Task ClientRejectsLateDownloadAfterAnswerRevisionChanges()
    {
        Definition definition = Definition();
        ProfileWorkerAuthority authority = Authority();
        using var module = new RecordingModule { HoldExecution = true, OutputKind = "artifact" };
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, () => authority);
        Task<ProfileWorkerReply> pending = client.DownloadArtifactAsync(Guid.NewGuid(), definition,
            Answers(), definition.Artifacts[0].Id);
        await module.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        authority = authority with { AnswerRevision = 1, Generation = 2 };
        module.Release.TrySetResult();
        await Assert.That(async () => await pending).Throws<ProfileException>();
        await Assert.That(module.Downloaded).IsFalse();
    }

    [Test]
    public async Task ClientRetiresSynchronouslyAndKeepsAdmissionUntilTheOperationExits()
    {
        using var module = new RecordingModule { HoldExecution = true };
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, Authority);
        Task<ProfileWorkerReply> pending = client.PlanAsync(Guid.NewGuid(), Definition());
        await module.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        client.Retire();
        await Assert.That(module.Retired).IsTrue();
        using var competingModule = new RecordingModule();
        using var competingRuntime = new RecordingRuntime(competingModule);
        using var competitor = new ProfileWorkerClient(competingRuntime, Authority);
        await Assert.That(async () => await competitor.PlanAsync(Guid.NewGuid(), Definition()))
            .Throws<ProfileException>();
        module.Release.TrySetResult();
        await Assert.That(async () => await pending).Throws<ProfileException>();
    }

    [Test]
    public async Task HistoricalAdmissionFailureRetiresTheCandidateWithoutAdoption()
    {
        using var module = new RecordingModule { Succeed = true };
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, Authority);
        await Assert.That(async () => await client.OpenAsync(Shape(), 0,
            "synthetic-first-passphrase"u8.ToArray(), _ => false)).Throws<ProfileException>();
        await Assert.That(module.Retired).IsTrue();
        await Assert.That(module.Adopted).IsFalse();
    }

    [Test]
    public async Task HistoricalAdmissionCannotAdoptAfterConsentChanges()
    {
        ProfileWorkerAuthority authority = Authority();
        using var module = new RecordingModule { Succeed = true };
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, () => authority);
        await Assert.That(async () => await client.OpenAsync(Shape(), 0,
            "synthetic-first-passphrase"u8.ToArray(), _ =>
            {
                authority = authority with { ConsentEpoch = 2, ConsentGranted = false };
                return true;
            })).Throws<ProfileException>();
        await Assert.That(module.Retired).IsTrue();
        await Assert.That(module.Adopted).IsFalse();
    }

    [Test]
    public async Task CryptoFailureGetsOneSelectedAttemptAndClearsItsOwnedInputs()
    {
        var crypto = new RecordingCrypto();
        var codec = new BrowserProfileContainerCodec(crypto);
        await Assert.That(async () => await codec.OpenAsync(Shape(), 0, "synthetic-passphrase"u8.ToArray()))
            .Throws<ProfileException>();
        await Assert.That(crypto.Decryptions).IsEqualTo(1);
        await Assert.That(crypto.Container!.All(value => value == 0)).IsTrue();
        await Assert.That(crypto.Password!.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task InvalidFlattenedIndexCannotReachCrypto()
    {
        var crypto = new RecordingCrypto();
        var codec = new BrowserProfileContainerCodec(crypto);
        await Assert.That(async () => await codec.OpenAsync(Shape(), 1, "synthetic-passphrase"u8.ToArray()))
            .Throws<ProfileException>();
        await Assert.That(crypto.Decryptions).IsEqualTo(0);
    }

    [Test]
    public async Task RecipientCountMutationCannotChangeTheAdmittedCryptoInput()
    {
        Definition definition = Definition();
        byte[] payload = Payload(definition);
        var crypto = new RecordingCrypto();
        var codec = new BrowserProfileContainerCodec(crypto);
        byte[] credential = "synthetic-first-passphrase"u8.ToArray();
        try
        {
            await Assert.That(async () => await codec.SealAsync(payload, new ChangingRecipients(credential)))
                .Throws<OperationCanceledException>();
            await Assert.That(crypto.Recipients.Length).IsEqualTo(1);
            await Assert.That(crypto.Recipients[0]).IsEqualTo("synthetic-first-passphrase");
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    [Test]
    public async Task ConfirmationAndTransferredCredentialUseTheSameEntrySnapshot()
    {
        Definition definition = Definition();
        using var changing = new ChangingMemory("synthetic-first-passphrase", "synthetic-other-passphrase", 2);
        using var module = new RecordingModule();
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime, Authority);
        await client.CreateAsync(definition, Answers(), ProfileProtectionMode.ImportUpgradeOnly,
            Guid.NewGuid(), Guid.NewGuid(), "synthetic-source", "synthetic-context",
            [changing.Value], ["synthetic-first-passphrase"u8.ToArray()]);
        await Assert.That(module.Credential).IsEqualTo("synthetic-first-passphrase");
    }

    [Test]
    public async Task ClientConsentRefusalDoesNotReadSensitiveBackingMemory()
    {
        using var memory = new ChangingMemory("synthetic-first-passphrase", "synthetic-other-passphrase", 0);
        using var module = new RecordingModule();
        using var runtime = new RecordingRuntime(module);
        using var client = new ProfileWorkerClient(runtime,
            () => Authority() with { ConsentGranted = false });
        await Assert.That(async () => await client.OpenAsync(memory.Value, 0, memory.Value, _ => true))
            .Throws<ProfileException>();
        await Assert.That(memory.Reads).IsEqualTo(0);
        await Assert.That(module.Started).IsFalse();
    }

    [Test]
    public async Task UnadoptedCandidateCannotDisclosePublicValuesOrCompose()
    {
        Definition definition = Definition();
        var codec = new HeldCodec();
        codec.Release.SetResult(Profile(definition));
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput();
        ProfileWorkerRequest request = Request("unlock");
        using ProfileWorkerResult opened = await worker.ExecuteAsync(request, input);
        await Assert.That(opened.Reply.PublicValues).IsNull();
        await Assert.That(async () => await worker.ExecuteAsync(request with
        {
            RequestId = 2, Operation = "plan", ProfileReference = opened.Reply.ProfileReference
        }, input)).Throws<ProfileException>();
    }

    [Test]
    public async Task MissingConsentRejectsBeforeTheCryptoBoundary()
    {
        var codec = new HeldCodec();
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput();
        ProfileWorkerRequest request = Request("unlock") with
        {
            Authority = Authority() with { ConsentGranted = false }
        };
        await Assert.That(async () => await worker.ExecuteAsync(request, input))
            .Throws<ProfileException>();
        await Assert.That(codec.Entered.Task.IsCompleted).IsFalse();
    }

    [Test]
    public async Task RetiringAWorkerDisposesALateUnlockedOwner()
    {
        Definition definition = Definition();
        var codec = new HeldCodec();
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput(definition: definition.CanonicalBytes);
        Task<ProfileWorkerResult> opening = worker.ExecuteAsync(Request("unlock"), input).AsTask();
        await codec.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        worker.Dispose();
        using UnlockedProfile profile = Profile(definition);
        codec.Release.SetResult(profile);
        await Assert.That(async () => await opening).Throws<ObjectDisposedException>();
        await Assert.That(() => CompareBaselineCodec.Create(profile)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task CompetingWorkerRequestIsRejectedRatherThanQueued()
    {
        Definition definition = Definition();
        var codec = new HeldCodec();
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput(definition: definition.CanonicalBytes);
        ProfileWorkerRequest first = Request("unlock");
        Task<ProfileWorkerResult> opening = worker.ExecuteAsync(first, input).AsTask();
        await codec.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await Assert.That(async () => await worker.ExecuteAsync(first with { RequestId = 2 }, input))
                .Throws<ProfileException>();
        }
        finally { codec.Release.TrySetResult(Profile(definition)); }
        using ProfileWorkerResult result = await opening;
        await Assert.That(result.Reply.Status).IsEqualTo("ok");
    }

    [Test]
    public async Task BlindValuesAppearOnlyInTheDeliberateDownload()
    {
        Definition definition = Definition();
        var codec = new HeldCodec();
        codec.Release.SetResult(Profile(definition));
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput(definition: definition.CanonicalBytes);
        ProfileWorkerRequest request = Request("unlock");
        using ProfileWorkerResult opened = await worker.ExecuteAsync(request, input);
        await Assert.That(JsonSerializer.Serialize(opened.Reply, ProfileWorkerJsonContext.Default.ProfileWorkerReply))
            .DoesNotContain(Hidden);
        await Assert.That(opened.Output.Length).IsEqualTo(0);
        using ProfileWorkerResult adopted = await worker.ExecuteAsync(request with
        {
            RequestId = 2, Operation = "adopt", ProfileReference = opened.Reply.ProfileReference
        }, input);
        using var compose = new ProfileWorkerInput(definition: definition.CanonicalBytes,
            answers: """{"answerContractVersion":"0.0.1","values":{"endpoint":"new"},"omitted":[]}"""u8.ToArray());
        using ProfileWorkerResult download = await worker.ExecuteAsync(request with
        {
            RequestId = 3, Operation = "download", ProfileReference = opened.Reply.ProfileReference,
            ArtifactId = definition.Artifacts[0].Id
        }, compose);
        await Assert.That(Encoding.UTF8.GetString(download.Output.Span))
            .IsEqualTo($$"""{"endpoint":"new","token":"{{Hidden}}"}""");
        ReadOnlyMemory<byte> borrowed = download.Output;
        download.Dispose();
        await Assert.That(borrowed.Span.IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
    }

    [Test]
    public async Task BlindProfileCannotUseTheInspectableDisclosureChannel()
    {
        Definition definition = Definition();
        var codec = new HeldCodec();
        codec.Release.SetResult(Profile(definition));
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput(definition: definition.CanonicalBytes);
        ProfileWorkerRequest request = Request("unlock");
        using ProfileWorkerResult opened = await worker.ExecuteAsync(request, input);
        using ProfileWorkerResult adopted = await worker.ExecuteAsync(request with
        {
            RequestId = 2, Operation = "adopt", ProfileReference = opened.Reply.ProfileReference
        }, input);
        await Assert.That(async () => await worker.ExecuteAsync(request with
        {
            RequestId = 3, Operation = "inspect", ProfileReference = opened.Reply.ProfileReference
        }, input)).Throws<ProfileException>();
    }

    [Test]
    public async Task ConfirmedWorkerRetirementConsumesTheOldOwnerBeforeReplacementOrPreparation()
    {
        Definition definition = Definition();
        var codec = new HeldCodec();
        using UnlockedProfile predecessor = Profile(definition);
        codec.Release.SetResult(predecessor);
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput();
        ProfileWorkerRequest request = Request("unlock");
        using ProfileWorkerResult opened = await worker.ExecuteAsync(request, input);
        request = request with { RequestId = 2, Operation = "adopt", ProfileReference = opened.Reply.ProfileReference };
        using ProfileWorkerResult adopted = await worker.ExecuteAsync(request, input);
        await Assert.That(adopted.Reply.RetainedPaths).IsEquivalentTo(["/token"]);
        using ProfileWorkerResult retired = await worker.ExecuteAsync(request with
        {
            RequestId = 3, Operation = "retire-field", ExcludedPaths = ["/token"]
        }, input);
        await Assert.That(retired.Reply.Status).IsEqualTo("ok");
        await Assert.That(retired.Reply.RetainedPaths).IsEmpty();
        await Assert.That(retired.Output.Length).IsEqualTo(0);
        await Assert.That(() => CompareBaselineCodec.Create(predecessor)).Throws<ObjectDisposedException>();
        using var compose = new ProfileWorkerInput(definition: definition.CanonicalBytes,
            answers: """{"answerContractVersion":"0.0.1","values":{"endpoint":"new"},"omitted":[]}"""u8.ToArray());
        using ProfileWorkerResult refused = await worker.ExecuteAsync(request with
        {
            RequestId = 4, Operation = "download", ArtifactId = definition.Artifacts[0].Id
        }, compose);
        await Assert.That(refused.Reply.Status).IsEqualTo("not-ready");
        await Assert.That(refused.Output.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ChangedConsentEpochCannotReuseAnOpaqueProfileReference()
    {
        Definition definition = Definition();
        var codec = new HeldCodec();
        codec.Release.SetResult(Profile(definition));
        using var worker = new ProfileWorkerSession(codec);
        using var input = new ProfileWorkerInput(definition: definition.CanonicalBytes);
        ProfileWorkerRequest request = Request("unlock");
        using ProfileWorkerResult opened = await worker.ExecuteAsync(request, input);
        await Assert.That(async () => await worker.ExecuteAsync(request with
        {
            RequestId = 2, Operation = "plan", ProfileReference = opened.Reply.ProfileReference,
            Authority = request.Authority with { ConsentEpoch = 2 }
        }, input)).Throws<ProfileException>();
    }

    private static ProfileWorkerAuthority Authority() => new(
        Guid.Parse("cc19d015-c482-4f3d-93da-8d5534eddede"), 1, 0, 1, true);
    private static ProfileWorkerRequest Request(string operation) => new(Authority(), 1, operation);

    // An independently specified shape-only fixture. It makes no claim of valid authentication.
    private static byte[] Shape() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        @protected = Convert.ToBase64String("""{"enc":"A256GCM","typ":"cfgvault"}"""u8).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        iv = "AAAAAAAAAAAAAAAA", ciphertext = "AA", tag = "AAAAAAAAAAAAAAAAAAAAAA",
        header = new { alg = "PBES2-HS512+A256KW", p2c = 600000, p2s = "AAAAAAAAAAAAAAAA" },
        encrypted_key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    }));

    private static byte[] GeneralShape()
    {
        using JsonDocument flat = JsonDocument.Parse(Shape());
        JsonElement source = flat.RootElement;
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            @protected = source.GetProperty("protected"), iv = source.GetProperty("iv"),
            ciphertext = source.GetProperty("ciphertext"), tag = source.GetProperty("tag"),
            recipients = new[]
            {
                new { header = source.GetProperty("header"), encrypted_key = source.GetProperty("encrypted_key") },
                new { header = source.GetProperty("header"), encrypted_key = source.GetProperty("encrypted_key") }
            }
        });
    }

    private static Definition Definition() => DefinitionAuthoringCompiler.Compile(
        DefinitionAuthoringCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"b1fc3999-8668-4d78-8a66-b6c9cd5f30da","revision":1},
         "project":{"id":"synthetic.worker","name":"Synthetic worker"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,
          "x-configurator":{"sensitivity":"public"},"required":["token","endpoint"],"properties":{
           "token":{"type":"string","x-configurator":{"sensitivity":"secret"}},
           "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
         "outputs":{"settings.json":{"format":"json"}}}
        """u8.ToArray()));

    private static UnlockedProfile Profile(Definition definition)
    {
        byte[] payload = Payload(definition);
        try { return ProfilePayloadCodec.Admit(payload); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static AnswerSnapshot Answers() => AnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes($$"""
        {"answerContractVersion":"0.0.1","values":{"token":"{{Hidden}}","endpoint":"old"},"omitted":[]}
        """));

    private static byte[] Payload(Definition definition)
    {
        byte[] answers = Encoding.UTF8.GetBytes($$"""
            {"answerContractVersion":"0.0.1","values":{"token":"{{Hidden}}","endpoint":"old"},"omitted":[]}
            """);
        byte[] payload = ProfilePayloadCodec.Serialize(definition, AnswerSnapshotCodec.ParseJson(answers),
            ProfileProtectionMode.ImportUpgradeOnly, Guid.NewGuid(), Guid.NewGuid(), "synthetic-source", "synthetic-context");
        CryptographicOperations.ZeroMemory(answers);
        return payload;
    }

    private sealed class ChangingRecipients(ReadOnlyMemory<byte> credential) : IReadOnlyList<ReadOnlyMemory<byte>>
    {
        private int _reads;
        public int Count => Interlocked.Increment(ref _reads) == 1 ? 1 : 0;
        public ReadOnlyMemory<byte> this[int index] => index == 0 ? credential : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<ReadOnlyMemory<byte>> GetEnumerator() => new[] { credential }.AsEnumerable().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ChangingMemory(string first, string second, int stableReads) : MemoryManager<byte>
    {
        private readonly byte[] _first = Encoding.UTF8.GetBytes(first);
        private readonly byte[] _second = Encoding.UTF8.GetBytes(second);
        public int Reads { get; private set; }
        public ReadOnlyMemory<byte> Value => CreateMemory(_first.Length);
        public override Span<byte> GetSpan() => ++Reads <= stableReads ? _first : _second;
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing)
        {
            CryptographicOperations.ZeroMemory(_first);
            CryptographicOperations.ZeroMemory(_second);
        }
    }

    private sealed class RecordingCrypto : IProfileCrypto
    {
        public string[] Recipients { get; private set; } = [];
        public int Decryptions { get; private set; }
        public byte[]? Container { get; private set; }
        public byte[]? Password { get; private set; }
        public string? DecryptedCredential { get; private set; }
        public ValueTask<byte[]> EncryptAsync(byte[] payload, IReadOnlyList<byte[]> recipients)
        {
            Recipients = recipients.Select(Encoding.UTF8.GetString).ToArray();
            throw new OperationCanceledException();
        }
        public ValueTask<byte[]> DecryptAsync(byte[] container, int recipientIndex, byte[] passphrase)
        {
            Decryptions++;
            Container = container;
            Password = passphrase;
            DecryptedCredential = Encoding.UTF8.GetString(passphrase);
            throw new InvalidOperationException(Hidden);
        }
        public void Clear() { }
    }

    private sealed class RecordingRuntime(RecordingModule module) : IJSInProcessRuntime, IDisposable
    {
        private readonly RecordingFactory _factory = new(module);
        public TValue Invoke<TValue>(string identifier, params object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier != "import" || args is not ["./js/profile-worker-host.js"]) throw new NotSupportedException();
            return ValueTask.FromResult((TValue)(object)_factory);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
        public void Dispose() => _factory.Dispose();
    }

    private sealed class RecordingFactory(RecordingModule firstBridge) : IJSInProcessObjectReference
    {
        private bool _created;
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            if (identifier != "createBridge" || args is { Length: > 0 }) throw new NotSupportedException();
            RecordingModule bridge = _created ? new RecordingModule() : firstBridge;
            _created = true;
            return (TValue)(object)bridge;
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingModule : IJSInProcessObjectReference
    {
        public string? Credential { get; private set; }
        public bool Started { get; private set; }
        public bool Retired { get; private set; }
        public bool Adopted { get; private set; }
        public bool Downloaded { get; private set; }
        public bool HoldExecution { get; init; }
        public bool Succeed { get; init; }
        public string? OutputKind { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            if (identifier == "retire") Retired = true;
            if (identifier == "download") Downloaded = true;
            return default!;
        }
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Started = true;
            if (identifier != "execute") return default!;
            var request = JsonSerializer.Deserialize((string)args![1]!, ProfileWorkerJsonContext.Default.ProfileWorkerRequest)!;
            byte[][] recipients = (byte[][])args[6]!;
            Credential = Encoding.UTF8.GetString(recipients.Length == 0 ? (byte[])args[3]! : recipients[0]);
            Entered.TrySetResult();
            if (HoldExecution) await Release.Task;
            Adopted |= request.Operation == "adopt";
            string reply = JsonSerializer.Serialize(new ProfileWorkerReply(request.Authority, request.RequestId,
                Succeed || OutputKind is not null ? "ok" : "invalid")
            {
                ProfileReference = Guid.Parse("00cb46cd-e3c0-401b-919b-0c9d31414be9"), OutputKind = OutputKind
            },
                ProfileWorkerJsonContext.Default.ProfileWorkerReply);
            return (TValue)(object)reply;
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class HeldCodec : IProfileContainerCodec
    {
        public Guid? DeviceRecipientId => null;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<UnlockedProfile> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
            ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            return await Release.Task;
        }
        public ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
            IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
