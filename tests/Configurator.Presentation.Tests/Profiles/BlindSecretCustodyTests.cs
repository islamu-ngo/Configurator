namespace Configurator.Presentation.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Profiles;

public sealed class BlindSecretCustodyTests
{
    [Test]
    public async Task BlindOpenKeepsRetainedValuesOutsideObservableAnswersAndOrdinaryOutput()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await Assert.That(await form.OpenProfileAsync(_ => Task.FromResult(profile))).IsTrue();

        string observable = JsonSerializer.Serialize(new
        {
            answers = form.Snapshot(),
            projection = form.Projection,
            profile = form.Profile,
            fields = form.Fields.Select(field => field.Text),
            undo = form.LastUndoLabel
        });
        await Assert.That(observable.Contains("synthetic-retained-password", StringComparison.Ordinal)).IsFalse();
        await Assert.That(form.Snapshot().Values["name"].GetString()).IsEqualTo("public-name");
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(form.CanUndo).IsFalse();
        await Assert.That(form.CanEdit(["password"])).IsFalse();
        await Assert.That(form.SetValue(["password"], JsonSerializer.SerializeToElement("unconfirmed"))).IsFalse();
        form.Fields.Single(field => field.Definition.Name == "password").Text = "unconfirmed";
        await Assert.That(form.Fields.Single(field => field.Definition.Name == "password").Text).IsEqualTo("");
    }

    [Test]
    public async Task DeliberateRestrictedOutputContainsRetainedSecretAndClearsCallbackCopy()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        await Assert.That((await form.PrepareProfileOutputAsync(definition.Artifacts[0].Id)).Count).IsEqualTo(0);
        ReadOnlyMemory<byte> delivered = default;
        bool containedSecret = false;

        await Assert.That(await form.WriteProfileOutputToNativeSaveAsync((bytes, _) =>
        {
            delivered = bytes;
            using JsonDocument document = JsonDocument.Parse(bytes);
            containedSecret = document.RootElement.GetProperty("password").GetString() == "synthetic-retained-password";
            return ValueTask.CompletedTask;
        })).IsTrue();

        await Assert.That(containedSecret).IsTrue();
        await Assert.That(delivered.ToArray().All(value => value == 0)).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(await form.WriteProfileOutputToNativeSaveAsync((_, _) =>
            throw new InvalidOperationException("output-is-one-shot"))).IsFalse();
    }

    [Test]
    public async Task PublicEditRetiresPreparedOutputButKeepsRetainedSecretAvailableForNewPreparation()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        await form.PrepareProfileOutputAsync(definition.Artifacts[0].Id);
        form.EditValue(["name"], "edited-public");
        bool invoked = false;
        await Assert.That(await form.WriteProfileOutputToNativeSaveAsync((_, _) =>
        {
            invoked = true;
            return ValueTask.CompletedTask;
        })).IsFalse();
        await Assert.That(invoked).IsFalse();
        await Assert.That((await form.PrepareProfileOutputAsync(definition.Artifacts[0].Id)).Count).IsEqualTo(0);
        await Assert.That(await form.WriteProfileOutputToNativeSaveAsync((bytes, _) =>
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (document.RootElement.GetProperty("name").GetString() != "edited-public" ||
                document.RootElement.GetProperty("password").GetString() != "synthetic-retained-password")
                throw new InvalidOperationException("restricted-output-mismatch");
            return ValueTask.CompletedTask;
        })).IsTrue();
    }

    [Test]
    public async Task ConfirmedReplacementAndUndoCannotRestoreRetainedSlot()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        await Assert.That(form.ReplaceRetainedProfileValue(DefinitionPath.Parse("/password"),
            JsonSerializer.SerializeToElement("synthetic-replacement"), confirmed: false)).IsFalse();
        await Assert.That(form.ReplaceRetainedProfileValue(DefinitionPath.Parse("/password"),
            JsonSerializer.SerializeToElement("synthetic-replacement"), confirmed: true)).IsTrue();
        await Assert.That(() => ProfileUpgradePlanner.Plan(profile, definition)).Throws<ObjectDisposedException>();
        await Assert.That(form.Profile!.Slots.Single(slot => slot.Path == "/password").IsRetained).IsFalse();
        form.Undo();
        await Assert.That(form.Profile!.Slots.Single(slot => slot.Path == "/password").IsRetained).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That((await form.PrepareProfileOutputAsync(definition.Artifacts[0].Id)).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task RetirementAfterSinkEntryDoesNotClaimToUndoItsPhysicalAction()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        await form.PrepareProfileOutputAsync(definition.Artifacts[0].Id);
        bool sinkEntered = false;
        ReadOnlyMemory<byte> delivered = default;
        bool current = await form.WriteProfileOutputToNativeSaveAsync((bytes, _) =>
        {
            sinkEntered = true;
            delivered = bytes;
            form.EditValue(["name"], "input-changed-after-deliberate-transfer");
            return ValueTask.CompletedTask;
        });
        await Assert.That(sinkEntered).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(delivered.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EditOrDisposeWhileUnlockIsPendingRejectsAndDisposesLateOwner(bool dispose)
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        var completion = new TaskCompletionSource<UnlockedProfile>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> opening = form.OpenProfileAsync(_ => completion.Task);
        if (dispose) form.Dispose();
        else form.EditValue(["name"], "newer-edit");
        UnlockedProfile late = Profile(definition);
        completion.SetResult(late);
        await Assert.That(await opening.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(() => ProfileUpgradePlanner.Plan(late, definition)).Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments(DefinitionSessionPrivacy.BrowserNoSecrets)]
    [Arguments(DefinitionSessionPrivacy.BrowserSecretsEnabled)]
    public async Task BrowserPresentationNeverObtainsNativeRetainedOwner(DefinitionSessionPrivacy privacy)
    {
        using var form = new DefinitionFormModel(Definition(), privacy);
        bool invoked = false;
        await Assert.That(await form.OpenProfileAsync(_ =>
        {
            invoked = true;
            throw new InvalidOperationException("native-owner-must-not-enter-browser-presentation");
        })).IsFalse();
        await Assert.That(invoked).IsFalse();
    }

    [Test]
    public async Task ClearingPrivateStateRetiresBlindOwnerAndPendingOutput()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        await form.PrepareProfileOutputAsync(definition.Artifacts[0].Id);
        form.ClearPrivateState();
        await Assert.That(form.Profile).IsNull();
        await Assert.That(() => ProfileUpgradePlanner.Plan(profile, definition)).Throws<ObjectDisposedException>();
        await Assert.That(await form.WriteProfileOutputToNativeSaveAsync((_, _) =>
            throw new InvalidOperationException("retired-output-must-not-be-delivered"))).IsFalse();
    }

    [Test]
    public async Task OrdinaryImportCannotReplaceRetainedSecretWithoutConfirmation()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        byte[] input = """{"name":"public-name","password":"synthetic-unconfirmed-import"}"""u8.ToArray();
        DefinitionImportResult result = await form.ImportAsync(definition.Artifacts[0].Id,
            _ => Task.FromResult<ReadOnlyMemory<byte>>(input));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(form.Profile!.Slots.Single(slot => slot.Path == "/password").IsRetained).IsTrue();
        await Assert.That(input.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task OrdinaryDocumentCannotReplaceRetainedSecretWithoutConfirmation()
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        using DefinitionDocumentSession document = form.OpenDocument();
        byte[] input = AnswerSnapshotCodec.Serialize(new(0, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("public-name"),
            ["password"] = JsonSerializer.SerializeToElement("synthetic-unconfirmed-document")
        }));
        using DefinitionDocumentParseResult parsed = document.Parse(input);
        CryptographicOperations.ZeroMemory(input);
        bool applied = parsed.Succeeded && document.Apply(parsed.Candidate!).Succeeded;
        await Assert.That(applied).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("password")).IsFalse();
        await Assert.That(form.Profile!.Slots.Single(slot => slot.Path == "/password").IsRetained).IsTrue();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("dispose")]
    [Arguments("cancel")]
    public async Task InvalidationDuringSealingDiscardsAndClearsTheLateCiphertext(string action)
    {
        Definition definition = Definition();
        using var form = new DefinitionFormModel(definition);
        using UnlockedProfile profile = Profile(definition);
        await form.OpenProfileAsync(_ => Task.FromResult(profile));
        var codec = new GatedCodec();
        using var cancellation = new CancellationTokenSource();
        Task<ProfileSealedSnapshot?> sealing = form.SealProfileAsync(codec,
            Guid.Parse("9f154c16-5e7c-4664-baa5-0d5c6136faee"), ["synthetic-portable-recovery"u8.ToArray()],
            cancellationToken: cancellation.Token).AsTask();
        await codec.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (action == "dispose") form.Dispose();
        else if (action == "cancel") await cancellation.CancelAsync();
        else form.EditValue(["name"], "newer-public-input");
        byte[] late = [1, 2, 3];
        codec.Completion.SetResult(late);

        await Assert.That(await sealing.WaitAsync(TimeSpan.FromSeconds(5))).IsNull();
        await Assert.That(late.All(value => value == 0)).IsTrue();
    }

    private sealed class GatedCodec : IProfileContainerCodec
    {
        public Guid? DeviceRecipientId => null;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<byte[]> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<byte[]> SealAsync(ReadOnlyMemory<byte> payload,
            IReadOnlyList<ReadOnlyMemory<byte>> recipientMaterials, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return new(Completion.Task);
        }

        public ValueTask<UnlockedProfile> OpenAsync(ReadOnlyMemory<byte> container, int recipientIndex,
            ReadOnlyMemory<byte> passphraseUtf8, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static UnlockedProfile Profile(Definition definition)
    {
        byte[] payload = ProfilePayloadCodec.Serialize(definition, new(0, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("public-name"),
            ["password"] = JsonSerializer.SerializeToElement("synthetic-retained-password")
        }), ProfileProtectionMode.ImportUpgradeOnly, Guid.Parse("697e7dd1-098b-415e-b6e5-b4a76c6757db"),
            Guid.Parse("e9e799a9-72ed-4991-91ac-d244d0f9be43"), "synthetic-definition", "synthetic-environment");
        try { return ProfilePayloadCodec.Admit(payload); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    private static Definition Definition() => DefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"b7db78d4-fc97-451f-81ea-9b56b91243cb","revision":1},
         "project":{"id":"profile-custody","name":"Profile custody"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"required":["name","password"],
           "x-configurator":{"sensitivity":"public"},"properties":{
             "name":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "password":{"type":"string","writeOnly":true}}},
         "outputs":{"config.json":{"format":"json"}}}
        """u8.ToArray());
}
