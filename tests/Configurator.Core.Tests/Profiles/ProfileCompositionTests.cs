namespace Configurator.Core.Tests.Profiles;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Core.Profiles;
using Configurator.Core.Tests.Definitions;

public sealed class ProfileCompositionTests
{
    private const string Hidden = "synthetic-retained-composition-sentinel";
    private const string Properties = """
        {"token":{"type":"string","x-configurator":{"sensitivity":"secret"}},
         "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}
        """;

    [Test]
    public async Task AdmittedFixtureSupportsOrdinaryCompleteComposition()
    {
        Definition definition = Definition(Properties, required: ["token", "endpoint"]);
        AnswerSnapshot answers = Answers($$"""{"token":"{{Hidden}}","endpoint":"old"}""");
        await Assert.That(DefinitionValidator.Validate(definition, answers).IsReady).IsTrue();
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition, definition.Artifacts[0].Id, answers);
        using DefinitionArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span))
            .IsEqualTo($$"""{"endpoint":"old","token":"{{Hidden}}"}""");
    }

    [Test]
    public async Task RetainedValuesReachOnlyTheDeliberateSinkAndItsOwnedCopyClears()
    {
        Definition definition = Definition(Properties, required: ["token", "endpoint"]);
        using UnlockedProfile profile = Profile(definition, $$"""{"token":"{{Hidden}}","endpoint":"old"}""");
        AnswerSnapshot replacements = Answers("""{"endpoint":"new"}""", 17);
        using var input = new ProfileCompositionInput(profile, definition, replacements);
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(artifact!.InputRevision).IsEqualTo(17L);
        await Assert.That(artifact.Sensitivity).IsEqualTo(ArtifactSensitivity.Restricted);
        await Assert.That(JsonSerializer.Serialize(new { input, result, replacements })).DoesNotContain(Hidden);
        await Assert.That(input.ToString() + result + artifact).DoesNotContain(Hidden);

        ReadOnlyMemory<byte> borrowed = default;
        string delivered = "";
        await artifact.WriteToNativeSaveAsync((bytes, _) =>
        {
            borrowed = bytes;
            delivered = Encoding.UTF8.GetString(bytes.Span);
            return ValueTask.CompletedTask;
        });
        await Assert.That(delivered).IsEqualTo($$"""{"endpoint":"new","token":"{{Hidden}}"}""");
        await Assert.That(borrowed.Length > 0 && borrowed.Span.IndexOfAnyExcept((byte)0) == -1).IsTrue();
        await Assert.That(async () => await artifact.WriteToBrowserDownloadAsync((_, _) => ValueTask.CompletedTask))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task NestedPublicReplacementPreservesOnlyEligibleFixedSecretSiblings()
    {
        Definition definition = Definition("""
            {"connection":{"type":"object","additionalProperties":false,"required":["token","endpoint"],
             "x-configurator":{"sensitivity":"public"},"properties":{
              "token":{"type":"string","x-configurator":{"sensitivity":"secret"}},
              "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}}}
            """, required: ["connection"]);
        using UnlockedProfile profile = Profile(definition, $$$"""{"connection":{"token":"{{{Hidden}}}","endpoint":"old"}}""");
        using var input = new ProfileCompositionInput(profile, definition, Answers("""{"connection":{"endpoint":"new"}}"""));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(await Download(artifact!))
            .IsEqualTo($$$"""{"connection":{"endpoint":"new","token":"{{{Hidden}}}"}}""");
    }

    [Test]
    public async Task NewRequiredPublicAnswerDoesNotPreventUnchangedSecretCarry()
    {
        Definition source = Definition(Properties, required: ["token", "endpoint"]);
        Definition target = Definition("""
            {"token":{"type":"string","x-configurator":{"sensitivity":"secret"}},
             "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "region":{"type":"string","x-configurator":{"sensitivity":"public"}}}
            """, 2, ["token", "endpoint", "region"]);
        using UnlockedProfile profile = Profile(source, $$"""{"token":"{{Hidden}}","endpoint":"old"}""");
        using var input = new ProfileCompositionInput(profile, target, Answers("""{"region":"west"}"""));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, target.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(await Download(artifact!))
            .IsEqualTo($$"""{"endpoint":"old","region":"west","token":"{{Hidden}}"}""");
    }

    [Test]
    public async Task TightenedSecretPolicyRequiresReplacementDespiteIncidentalValidity()
    {
        Definition source = Definition(Properties, required: ["token"]);
        Definition target = Definition("""
            {"token":{"type":"string","minLength":5,"x-configurator":{"sensitivity":"secret"}},
             "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}
            """, 2, ["token"]);
        await Assert.That(DefinitionValidator.Validate(target, Answers($$"""{"token":"{{Hidden}}"}""")).IsReady).IsTrue();
        using UnlockedProfile profile = Profile(source, $$"""{"token":"{{Hidden}}"}""");
        using var input = new ProfileCompositionInput(profile, target, Answers("{}"));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, target.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Diagnostics.Any(item => item.Code == "value-required")).IsTrue();
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain(Hidden);

        using var replacement = new ProfileCompositionInput(profile, target, Answers("""{"token":"synthetic-replacement"}"""));
        ProfileArtifactResult replaced = ProfileArtifactComposer.Compose(replacement, target.Artifacts[0].Id);
        using ProfileArtifact? replacedArtifact = replaced.Artifact;
        await Assert.That(replaced.Succeeded).IsTrue();
        await Assert.That(await Download(replacedArtifact!)).IsEqualTo("""{"token":"synthetic-replacement"}""");
    }

    [Test]
    public async Task NestedSecretTighteningRejectsIncidentallyValidRetainedValue()
    {
        Definition source = Definition("""
            {"connection":{"type":"object","additionalProperties":false,"required":["token"],
             "x-configurator":{"sensitivity":"public"},"properties":{
              "token":{"type":"string","minLength":2,"x-configurator":{"sensitivity":"secret"}},
              "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}}}
            """, required: ["connection"]);
        Definition target = Definition("""
            {"connection":{"type":"object","additionalProperties":false,"required":["token"],
             "x-configurator":{"sensitivity":"public"},"properties":{
              "token":{"type":"string","minLength":5,"x-configurator":{"sensitivity":"secret"}},
              "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}}}
            """, 2, ["connection"]);
        string retained = $$$"""{"connection":{"token":"{{{Hidden}}}","endpoint":"old"}}""";
        await Assert.That(DefinitionValidator.Validate(source, Answers(retained)).IsReady).IsTrue();
        await Assert.That(DefinitionValidator.Validate(target, Answers(retained)).IsReady).IsTrue();
        using UnlockedProfile profile = Profile(source, retained);
        using var input = new ProfileCompositionInput(profile, target, Answers("""{"connection":{"endpoint":"new"}}"""));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, target.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Diagnostics.Any(item => item.Code == "value-required")).IsTrue();
        await Assert.That(ProfileUpgradePlanner.Plan(profile, target).Fields.Single().Eligibility)
            .IsEqualTo(ProfileCarryEligibility.ReplacementRequired);
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain(Hidden);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ClonedPermanentExclusionsPreventOriginalValuesReturningAfterUndo(bool required)
    {
        Definition definition = Definition(Properties, required: required ? ["token"] : []);
        using UnlockedProfile profile = Profile(definition, $$"""{"token":"{{Hidden}}","endpoint":"old"}""");
        var exclusions = new HashSet<DefinitionPath> { DefinitionPath.Parse("/token") };
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"), exclusions);
        exclusions.Clear();
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsEqualTo(!required);
        if (required)
            await Assert.That(result.Diagnostics.Any(item => item.Code == "value-required")).IsTrue();
        else
            await Assert.That(await Download(artifact!)).IsEqualTo("""{"endpoint":"old"}""");

        using var replacement = new ProfileCompositionInput(profile, definition,
            Answers("""{"token":"replacement"}"""), new HashSet<DefinitionPath> { DefinitionPath.Parse("/token") });
        ProfileArtifactResult replaced = ProfileArtifactComposer.Compose(replacement, definition.Artifacts[0].Id);
        using ProfileArtifact? replacedArtifact = replaced.Artifact;
        await Assert.That(await Download(replacedArtifact!)).IsEqualTo("""{"endpoint":"old","token":"replacement"}""");
    }

    [Test]
    public async Task ExplicitOmissionSuppressesRetainedValues()
    {
        Definition definition = Definition(Properties);
        using UnlockedProfile profile = Profile(definition, $$"""{"token":"{{Hidden}}","endpoint":"old"}""");
        using var input = new ProfileCompositionInput(profile, definition,
            Answers("{}", omitted: [DefinitionPath.Parse("/token")]));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(await Download(artifact!)).IsEqualTo("""{"endpoint":"old"}""");
    }

    [Test]
    public async Task StoredFixedOmissionSuppressesInitialValueUntilExplicitlyReplaced()
    {
        Definition definition = Definition("""
            {"endpoint":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"initial"}}}
            """);
        using UnlockedProfile profile = Profile(definition, "{}", [DefinitionPath.Parse("/endpoint")]);
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(await Download(artifact!)).IsEqualTo("{}");

        using var replacement = new ProfileCompositionInput(profile, definition, Answers("""{"endpoint":"replacement"}"""));
        ProfileArtifactResult replaced = ProfileArtifactComposer.Compose(replacement, definition.Artifacts[0].Id);
        using ProfileArtifact? replacementArtifact = replaced.Artifact;
        await Assert.That(replaced.Succeeded).IsTrue();
        await Assert.That(await Download(replacementArtifact!)).IsEqualTo("""{"endpoint":"replacement"}""");
    }

    [Test]
    public async Task CollectionRowsNeverCarryByOrdinal()
    {
        Definition definition = Definition("""
            {"rows":{"type":"array","items":{"type":"object","additionalProperties":false,
             "x-configurator":{"sensitivity":"public"},"required":["token"],"properties":{
              "name":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "token":{"type":"string","x-configurator":{"sensitivity":"secret"}}}}}}
            """, required: ["rows"]);
        using UnlockedProfile profile = Profile(definition, $$"""{"rows":[{"name":"first","token":"{{Hidden}}"}]}""");
        using var input = new ProfileCompositionInput(profile, definition, Answers("""{"rows":[{"name":"different"}]}"""));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain(Hidden);
    }

    [Test]
    public async Task TargetScopeUsesReplacementSelectorsAndRejectsOtherArtifacts()
    {
        Definition definition = DefinitionDeploymentScopeTests.Create();
        using UnlockedProfile profile = Profile(definition,
            $$"""{"scope":"all","endpoint":"old","workerSecret":"{{Hidden}}"}""");
        using var input = new ProfileCompositionInput(profile, definition, Answers("""{"scope":"dashboard","endpoint":"new"}"""));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, "dashboard");
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(await Download(artifact!)).IsEqualTo("""{"endpoint":"new"}""");
        ProfileArtifactResult forbidden = ProfileArtifactComposer.Compose(input, "worker");
        using ProfileArtifact? forbiddenArtifact = forbidden.Artifact;
        await Assert.That(forbidden.Succeeded).IsFalse();
        await Assert.That(forbidden.Diagnostics.Single().Code).IsEqualTo("artifact-context");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetiringScopeOrProfilePreventsPreparedOutputDisclosure(bool retireProfile)
    {
        Definition definition = Definition(Properties);
        using UnlockedProfile profile = Profile(definition, $$"""{"token":"{{Hidden}}"}""");
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        if (retireProfile) profile.Dispose();
        else input.Dispose();
        bool entered = false;
        await Assert.That(async () => await artifact!.WriteToNativeSaveAsync((_, _) =>
        {
            entered = true;
            return ValueTask.CompletedTask;
        })).Throws<ObjectDisposedException>();
        await Assert.That(entered).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetirementAtCompositionCompletionCannotPublishLateOutput(bool cancel)
    {
        Definition definition = Definition(Properties);
        using UnlockedProfile profile = Profile(definition, $$"""{"token":"{{Hidden}}"}""");
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"));
        using var cancellation = new CancellationTokenSource();
        using var activity = new Activity(nameof(RetirementAtCompositionCompletionCannotPublishLateOutput));
        activity.Start();
        using var measurement = new DefinitionBundleTests.PreparationMeasurement(activity, _ =>
        {
            if (cancel) cancellation.Cancel();
            else input.Dispose();
        });
        if (cancel)
            await Assert.That(() => ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id, cancellation.Token))
                .Throws<OperationCanceledException>();
        else
            await Assert.That(() => ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id))
                .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task FailingSinkClearsItsCopyAndConsumesOutput()
    {
        Definition definition = Definition(Properties);
        using UnlockedProfile profile = Profile(definition, $$"""{"token":"{{Hidden}}"}""");
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(input, definition.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        ReadOnlyMemory<byte> borrowed = default;
        await Assert.That(async () => await artifact!.WriteToBrowserDownloadAsync((bytes, _) =>
        {
            borrowed = bytes;
            throw new InvalidOperationException("synthetic-sink-failure");
        })).Throws<InvalidOperationException>();
        await Assert.That(borrowed.Length > 0 && borrowed.Span.IndexOfAnyExcept((byte)0) == -1).IsTrue();
        await Assert.That(async () => await artifact!.WriteToNativeSaveAsync((_, _) => ValueTask.CompletedTask))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ScopedSealingPreservesBlindModeAndEmbedsValidatedUpgrade()
    {
        Definition source = Definition(Properties, required: ["token"]);
        Definition target = Definition(Properties, 2, ["token"]);
        using UnlockedProfile profile = Profile(source, $$"""{"token":"{{Hidden}}","endpoint":"old"}""");
        using var input = new ProfileCompositionInput(profile, target, Answers("""{"endpoint":"new"}"""));
        using var codec = new PayloadBoundary();
        Guid snapshotId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        using ProfileSealedSnapshot sealedSnapshot = await ProfileArtifactComposer.SealAsync(input, codec,
            snapshotId, new ReadOnlyMemory<byte>[] { "synthetic-portable"u8.ToArray() });
        await Assert.That(sealedSnapshot.Reference.ProfileId).IsEqualTo(profile.ProfileId);
        await Assert.That(sealedSnapshot.Reference.SnapshotId).IsEqualTo(snapshotId);
        await Assert.That(codec.Admitted!.Mode).IsEqualTo(ProfileProtectionMode.ImportUpgradeOnly);
        await Assert.That(codec.Admitted.DefinitionDigest).IsEqualTo(target.Digest);
        await Assert.That(() => ProfilePayloadCodec.InspectAnswers(codec.Admitted)).Throws<ProfileException>();
        await Assert.That(codec.SourceLocator).IsEqualTo("synthetic-source");
        await Assert.That(codec.EnvironmentContext).IsEqualTo("synthetic-environment");
        await Assert.That(codec.BorrowedPayload.Length > 0 && codec.BorrowedPayload.Span.IndexOfAnyExcept((byte)0) == -1).IsTrue();

        using var reopened = new ProfileCompositionInput(codec.Admitted, target, Answers("{}"));
        ProfileArtifactResult result = ProfileArtifactComposer.Compose(reopened, target.Artifacts[0].Id);
        using ProfileArtifact? artifact = result.Artifact;
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(await Download(artifact!)).IsEqualTo($$"""{"endpoint":"new","token":"{{Hidden}}"}""");
        input.Dispose();
        await Assert.That(() => sealedSnapshot.CopyContainer()).Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LateSealingResultIsClearedAfterCancellationOrScopeRetirement(bool cancel)
    {
        Definition definition = Definition(Properties);
        using UnlockedProfile profile = Profile(definition, $$"""{"token":"{{Hidden}}"}""");
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"));
        using var codec = new PayloadBoundary(gated: true);
        using var cancellation = new CancellationTokenSource();
        Task<ProfileSealedSnapshot> pending = ProfileArtifactComposer.SealAsync(input, codec, Guid.NewGuid(),
            new ReadOnlyMemory<byte>[] { "synthetic-portable"u8.ToArray() }, cancellationToken: cancellation.Token).AsTask();
        await codec.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancel) cancellation.Cancel();
        else input.Dispose();
        codec.Release.SetResult();
        if (cancel) await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        else await Assert.That(async () => await pending).Throws<ObjectDisposedException>();
        await Assert.That(codec.Ciphertext.AsSpan().IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
        await Assert.That(codec.BorrowedPayload.Span.IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
    }

    [Test]
    public async Task DeviceOnlyScopedSealingRequiresAcknowledgementOfThisProfile()
    {
        Definition definition = Definition(Properties);
        using UnlockedProfile profile = Profile(definition, "{}");
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"));
        using var codec = new PayloadBoundary { DeviceRecipientId = Guid.NewGuid() };
        await Assert.That(async () => await ProfileArtifactComposer.SealAsync(input, codec, Guid.NewGuid(), [],
            Guid.NewGuid())).Throws<ProfileRecoveryException>();
        await Assert.That(codec.Entered.Task.IsCompleted).IsFalse();
        using ProfileSealedSnapshot result = await ProfileArtifactComposer.SealAsync(input, codec, Guid.NewGuid(), [],
            profile.ProfileId);
        await Assert.That(result.Reference.PortableRecoveryConfigured).IsFalse();
        await Assert.That(result.Reference.DeviceRecipientId).IsEqualTo(codec.DeviceRecipientId);
    }

    [Test]
    public async Task PortablePolicyAndCodecConsumeTheSameOwnedRecipientSnapshot()
    {
        Definition definition = Definition(Properties);
        using UnlockedProfile profile = Profile(definition, "{}");
        using var input = new ProfileCompositionInput(profile, definition, Answers("{}"));
        byte[] material = "synthetic-portable"u8.ToArray();
        var recipients = new ChangingRecipientList(material);
        using var codec = new PayloadBoundary { DeviceRecipientId = Guid.NewGuid() };
        using ProfileSealedSnapshot result = await ProfileArtifactComposer.SealAsync(
            input, codec, Guid.NewGuid(), recipients);
        await Assert.That(result.Reference.PortableRecoveryConfigured).IsTrue();
        await Assert.That(codec.PortableCount).IsEqualTo(1);
        await Assert.That(codec.PortableMaterial).IsEqualTo("synthetic-portable");
        await Assert.That(codec.BorrowedRecipient.Length > 0 &&
            codec.BorrowedRecipient.Span.IndexOfAnyExcept((byte)0) == -1).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(material)).IsEqualTo("synthetic-portable");
    }

    [Test]
    public async Task InvalidCombinedAnswersCannotReachSealingBoundary()
    {
        Definition source = Definition(Properties, required: ["token"]);
        Definition target = Definition("""
            {"token":{"type":"string","minLength":5,"x-configurator":{"sensitivity":"secret"}},
             "endpoint":{"type":"string","x-configurator":{"sensitivity":"public"}}}
            """, 2, ["token"]);
        using UnlockedProfile profile = Profile(source, $$"""{"token":"{{Hidden}}"}""");
        using var input = new ProfileCompositionInput(profile, target, Answers("{}"));
        using var codec = new PayloadBoundary();
        await Assert.That(async () => await ProfileArtifactComposer.SealAsync(input, codec, Guid.NewGuid(),
            new ReadOnlyMemory<byte>[] { "synthetic-portable"u8.ToArray() })).Throws<DefinitionException>();
        await Assert.That(codec.Entered.Task.IsCompleted).IsFalse();
    }

    [Test]
    public async Task ConflictingTargetRevisionCannotCreateCompositionScope()
    {
        Definition source = Definition(Properties);
        Definition conflicting = Definition("""{"different":{"type":"boolean"}}""");
        using UnlockedProfile profile = Profile(source, "{}");
        await Assert.That(() => new ProfileCompositionInput(profile, conflicting, Answers("{}"))).Throws<DefinitionException>();
    }

    /// <summary>Exercises payload/lifetime at the external codec boundary, not cryptography or container interoperability.</summary>
    private sealed class PayloadBoundary : IProfileContainerCodec, IDisposable
    {
        internal PayloadBoundary(bool gated = false)
        {
            if (!gated) Release.SetResult();
        }

        public Guid? DeviceRecipientId { get; init; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[] Ciphertext { get; } = [1, 2, 3, 4];
        internal ReadOnlyMemory<byte> BorrowedPayload { get; private set; }
        internal UnlockedProfile? Admitted { get; private set; }
        internal string? SourceLocator { get; private set; }
        internal string? EnvironmentContext { get; private set; }
        internal int PortableCount { get; private set; }
        internal string? PortableMaterial { get; private set; }
        internal ReadOnlyMemory<byte> BorrowedRecipient { get; private set; }

        public async ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
            IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default)
        {
            BorrowedPayload = payload;
            PortableCount = recipientMaterials.Count;
            if (PortableCount > 0)
            {
                BorrowedRecipient = recipientMaterials[0];
                PortableMaterial = Encoding.UTF8.GetString(BorrowedRecipient.Span);
            }
            Admitted = ProfilePayloadCodec.Admit(payload);
            using JsonDocument document = JsonDocument.Parse(payload);
            SourceLocator = document.RootElement.GetProperty("definitionSource").GetString();
            EnvironmentContext = document.RootElement.GetProperty("environmentContext").GetString();
            Entered.SetResult();
            await Release.Task;
            return Ciphertext;
        }

        public ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
            ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose() => Admitted?.Dispose();
    }

    private sealed class ChangingRecipientList(ReadOnlyMemory<byte> material) : IReadOnlyList<ReadOnlyMemory<byte>>
    {
        private int _countReads;
        public int Count => Interlocked.Increment(ref _countReads) == 1 ? 1 : 0;
        public ReadOnlyMemory<byte> this[int index] => index == 0 ? material : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<ReadOnlyMemory<byte>> GetEnumerator()
        {
            for (int index = 0; index < Count; index++) yield return this[index];
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static async Task<string> Download(ProfileArtifact artifact)
    {
        string result = "";
        await artifact.WriteToBrowserDownloadAsync((bytes, _) =>
        {
            result = Encoding.UTF8.GetString(bytes.Span);
            return ValueTask.CompletedTask;
        });
        return result;
    }

    private static AnswerSnapshot Answers(string json, long revision = 0, IEnumerable<DefinitionPath>? omitted = null)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(revision, document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal), omitted);
    }

    private static UnlockedProfile Profile(Definition definition, string json, IEnumerable<DefinitionPath>? omitted = null)
    {
        byte[] payload = ProfilePayloadCodec.Serialize(definition, Answers(json, omitted: omitted), ProfileProtectionMode.ImportUpgradeOnly,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.NewGuid(), "synthetic-source", "synthetic-environment");
        try { return ProfilePayloadCodec.Admit(payload); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static Definition Definition(string properties, int revision = 1, string[]? required = null)
    {
        JsonNode source = JsonNode.Parse("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"b1fc3999-8668-4d78-8a66-b6c9cd5f30da","revision":1},
             "project":{"id":"synthetic.composition","name":"Synthetic composition"},"applicationVersions":["1.0"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{}},
             "outputs":{"settings.json":{"format":"json"}}}
            """)!;
        source["identity"]!["revision"] = revision;
        source["configuration"]!["properties"] = JsonNode.Parse(properties);
        if (required is not null) source["configuration"]!["required"] = JsonSerializer.SerializeToNode(required);
        return DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
    }
}
