namespace Configurator.Core.Tests.Profiles;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Configurator.Core.Composition;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Core.Profiles;

public sealed class RetainedProfileRetirementTests
{
    private const string First = "synthetic-first-retained";
    private const string Second = "synthetic-second-retained";

    [Test]
    public async Task RetirementRevokesOriginalOwnerBorrowedInputAndPreparedOutput()
    {
        Definition definition = Definition();
        using UnlockedProfile original = Profile(definition);
        using var oldInput = new ProfileCompositionInput(original, definition, Answers("{}"));
        ProfileArtifactResult prepared = ProfileArtifactComposer.Compose(oldInput, definition.Artifacts[0].Id);
        using ProfileArtifact? oldOutput = prepared.Artifact;
        await Assert.That(prepared.Succeeded).IsTrue();

        using UnlockedProfile successor = ProfilePayloadCodec.RetireRetainedFields(
            original, [DefinitionPath.Parse("/first/nested")]);

        await Assert.That(() => ProfilePayloadCodec.InspectAnswers(original)).Throws<ObjectDisposedException>();
        await Assert.That(() => ProfileArtifactComposer.Compose(oldInput, definition.Artifacts[0].Id))
            .Throws<ObjectDisposedException>();
        await Assert.That(async () => await oldOutput!.WriteToNativeSaveAsync((_, _) => ValueTask.CompletedTask))
            .Throws<ObjectDisposedException>();
        using var current = new ProfileCompositionInput(successor, definition, Answers("{}"));
        await Assert.That(await Compose(current, definition)).IsEqualTo($$"""{"second":"{{Second}}"}""");
    }

    [Test]
    public async Task RejectedReplacementCannotRestoreRetiredFieldOrDiscardOtherRetainedField()
    {
        Definition definition = Definition();
        using UnlockedProfile original = Profile(definition);
        using UnlockedProfile successor = ProfilePayloadCodec.RetireRetainedFields(
            original, [DefinitionPath.Parse("/first")]);
        using var invalid = new ProfileCompositionInput(successor, definition, Answers("""{"first":42}"""));
        ProfileArtifactResult rejected = ProfileArtifactComposer.Compose(invalid, definition.Artifacts[0].Id);
        using ProfileArtifact? rejectedOutput = rejected.Artifact;
        await Assert.That(rejected.Succeeded).IsFalse();
        using var cleared = new ProfileCompositionInput(successor, definition, Answers("{}"));
        await Assert.That(await Compose(cleared, definition)).IsEqualTo($$"""{"second":"{{Second}}"}""");
        using var replacement = new ProfileCompositionInput(successor, definition, Answers("""{"first":"synthetic-new"}"""));
        await Assert.That(await Compose(replacement, definition))
            .IsEqualTo($$"""{"first":"synthetic-new","second":"{{Second}}"}""");
    }

    [Test]
    [Arguments("null")]
    [Arguments("empty")]
    [Arguments("null-path")]
    [Arguments("unknown-root")]
    [Arguments("over-limit")]
    public async Task InvalidRetirementStillConsumesOriginalOwner(string request)
    {
        Definition definition = Definition();
        using UnlockedProfile original = Profile(definition);
        using var oldInput = new ProfileCompositionInput(original, definition, Answers("{}"));
        IReadOnlyCollection<DefinitionPath> paths = request switch
        {
            "null" => null!,
            "empty" => [],
            "null-path" => [null!],
            "unknown-root" => [DefinitionPath.Parse("/undeclared/secret")],
            "over-limit" => Enumerable.Repeat(DefinitionPath.Parse("/first"),
                CompositionLimits.DefaultSequenceEntries + 1).ToArray(),
            _ => throw new InvalidOperationException("Unknown synthetic request.")
        };
        void Retire()
        {
            using UnlockedProfile unexpected = ProfilePayloadCodec.RetireRetainedFields(original, paths);
        }
        if (request == "null")
            await Assert.That(Retire).Throws<ArgumentNullException>();
        else
            await Assert.That(Retire).Throws<ProfileException>();
        await Assert.That(() => ProfileArtifactComposer.Compose(oldInput, definition.Artifacts[0].Id))
            .Throws<ObjectDisposedException>();
        await Assert.That(() => ProfilePayloadCodec.InspectAnswers(original)).Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments(ProfileProtectionMode.ImportUpgradeOnly)]
    [Arguments(ProfileProtectionMode.InspectableEncrypted)]
    public async Task FreshResealingPreservesProvenanceModeAndComparisonKeyWithNormalizedOmissions(ProfileProtectionMode mode)
    {
        Definition definition = Definition();
        using UnlockedProfile original = Profile(definition, mode, [DefinitionPath.Parse("/first/previous")]);
        CompareBaselineSnapshot baseline = CompareBaselineCodec.Create(original);
        using UnlockedProfile successor = ProfilePayloadCodec.RetireRetainedFields(
            original, [DefinitionPath.Parse("/first/nested"), DefinitionPath.Parse("/first/another")]);
        await Assert.That(successor.ProfileId).IsEqualTo(original.ProfileId);
        await Assert.That(successor.SnapshotId).IsEqualTo(original.SnapshotId);
        await Assert.That(successor.DefinitionDigest).IsEqualTo(definition.Digest);
        await Assert.That(successor.DefinitionRevision).IsEqualTo(definition.Revision);
        await Assert.That(successor.FamilyId).IsEqualTo(definition.FamilyId);
        await Assert.That(successor.Mode).IsEqualTo(mode);
        CompareBaselineSnapshot remaining = CompareBaselineCodec.Create(successor);
        await Assert.That(remaining.KeyId).IsEqualTo(baseline.KeyId);
        await Assert.That(remaining.ContextTag).IsEqualTo(baseline.ContextTag);
        await Assert.That(remaining.Commitments["/second"]).IsEqualTo(baseline.Commitments["/second"]);
        await Assert.That(JsonSerializer.Serialize(successor)).DoesNotContain(Second);

        using var codec = new PayloadBoundary();
        Guid freshId = Guid.NewGuid();
        using ProfileSealedSnapshot sealedProfile = await ProfileRecipientLifecycle.ResealAsync(
            successor, freshId, codec, [], successor.ProfileId);
        using JsonDocument payload = JsonDocument.Parse(codec.Payload);
        JsonElement root = payload.RootElement;
        await Assert.That(root.GetProperty("snapshotId").GetGuid()).IsEqualTo(freshId);
        await Assert.That(root.GetProperty("profileId").GetGuid()).IsEqualTo(original.ProfileId);
        await Assert.That(root.GetProperty("mode").GetString()).IsEqualTo(mode.ToString());
        await Assert.That(root.GetProperty("definitionSource").GetString()).IsEqualTo("synthetic-source");
        await Assert.That(root.GetProperty("environmentContext").GetString()).IsEqualTo("synthetic-environment");
        await Assert.That(root.GetProperty("comparisonKey").GetProperty("keyId").GetGuid()).IsEqualTo(baseline.KeyId);
        await Assert.That(root.GetProperty("comparisonKey").GetProperty("material").GetString())
            .IsEqualTo(Convert.ToBase64String(new byte[32]));
        await Assert.That(root.GetProperty("answers").GetProperty("values").GetRawText())
            .IsEqualTo($$"""{"second":"{{Second}}"}""");
        await Assert.That(root.GetProperty("answers").GetProperty("omitted").EnumerateArray()
            .Select(item => item.GetString()!).ToArray()).IsEquivalentTo(["/first"]);
        await Assert.That(codec.BorrowedPayload.Span.IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
    }

    [Test]
    public async Task FilteredSuccessorRequiresFreshIdentityForBothSealingPaths()
    {
        Definition definition = Definition();
        using UnlockedProfile original = Profile(definition);
        using UnlockedProfile successor = ProfilePayloadCodec.RetireRetainedFields(
            original, [DefinitionPath.Parse("/first")]);
        using var codec = new PayloadBoundary();
        await Assert.That(async () => await ProfileRecipientLifecycle.SealAsync(
            successor, codec, [], successor.ProfileId)).Throws<ProfileRecoveryException>();
        await Assert.That(codec.Payload.Length).IsEqualTo(0);
        await Assert.That(async () => await ProfileRecipientLifecycle.ResealAsync(
            successor, successor.SnapshotId, codec, [], successor.ProfileId)).Throws<ProfileRecoveryException>();
        using var input = new ProfileCompositionInput(successor, definition, Answers("""{"first":"synthetic-new"}"""));
        await Assert.That(async () => await ProfileArtifactComposer.SealAsync(
            input, codec, successor.SnapshotId, [], successor.ProfileId)).Throws<ProfileRecoveryException>();
        Guid freshId = Guid.NewGuid();
        using ProfileSealedSnapshot result = await ProfileArtifactComposer.SealAsync(
            input, codec, freshId, [], successor.ProfileId);
        using JsonDocument payload = JsonDocument.Parse(codec.Payload);
        await Assert.That(result.Reference.SnapshotId).IsEqualTo(freshId);
        await Assert.That(payload.RootElement.GetProperty("answers").GetProperty("values").GetRawText())
            .IsEqualTo($$"""{"first":"synthetic-new","second":"{{Second}}"}""");
    }

    [Test]
    public async Task RootRetirementConsumesAllFieldsAndSuppressesTheirDefaults()
    {
        Definition definition = Definition();
        using UnlockedProfile original = Profile(definition, omitted: []);
        using UnlockedProfile successor = ProfilePayloadCodec.RetireRetainedFields(original, [DefinitionPath.Parse("")]);
        using var input = new ProfileCompositionInput(successor, definition, Answers("{}"));
        await Assert.That(await Compose(input, definition)).IsEqualTo("{}");
    }

    private static Definition Definition() => DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"b1fc3999-8668-4d78-8a66-b6c9cd5f30da","revision":1},
         "project":{"id":"synthetic.retirement","name":"Synthetic retirement"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
          "first":{"type":"string","x-configurator":{"sensitivity":"secret"}},
          "second":{"type":"string","x-configurator":{"sensitivity":"secret"}},
          "defaulted":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"synthetic-default"}}}},
         "outputs":{"settings.json":{"format":"json"}}}
        """u8.ToArray()));

    private static AnswerSnapshot Answers(string json, IEnumerable<DefinitionPath>? omitted = null)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(0, document.RootElement.EnumerateObject().ToDictionary(
            property => property.Name, property => property.Value, StringComparer.Ordinal),
            omitted);
    }

    private static UnlockedProfile Profile(Definition definition,
        ProfileProtectionMode mode = ProfileProtectionMode.ImportUpgradeOnly, IEnumerable<DefinitionPath>? omitted = null)
    {
        using var key = new ProfileSecretCommitment(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), new byte[32]);
        byte[] payload = ProfilePayloadCodec.Serialize(definition,
            Answers($$"""{"first":"{{First}}","second":"{{Second}}"}""",
                omitted ?? [DefinitionPath.Parse("/defaulted")]), mode,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "synthetic-source", "synthetic-environment", key);
        try { return ProfilePayloadCodec.Admit(payload); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static async Task<string> Compose(ProfileCompositionInput input, Definition definition)
    {
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        string delivered = "";
        await artifact!.WriteToNativeSaveAsync((bytes, _) =>
        {
            delivered = Encoding.UTF8.GetString(bytes.Span);
            return ValueTask.CompletedTask;
        });
        return delivered;
    }

    /// <summary>Observes the codec boundary using synthetic payloads; makes no encryption claim.</summary>
    private sealed class PayloadBoundary : IProfileContainerCodec, IDisposable
    {
        public Guid? DeviceRecipientId { get; } = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        internal byte[] Payload { get; private set; } = [];
        internal ReadOnlyMemory<byte> BorrowedPayload { get; private set; }

        public ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
            IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default)
        {
            CryptographicOperations.ZeroMemory(Payload);
            BorrowedPayload = payload;
            Payload = payload.ToArray();
            return ValueTask.FromResult<byte[]>([1, 2, 3, 4]);
        }

        public ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
            ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose() => CryptographicOperations.ZeroMemory(Payload);
    }
}
