namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionSessionPrivacyTests
{
    [Test]
    public async Task BrowserRejectsNestedSecretButKeepsNonsecretSiblingEditable()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.Fields.Single().Text = """{"name":"safe","password":"synthetic-secret"}""";
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        form.Fields.Single().Text = """{"name":"safe"}""";
        await Assert.That(form.Snapshot().Values["settings"].GetProperty("name").GetString()).IsEqualTo("safe");
    }

    [Test]
    public async Task RevocationClearsOnlySecretLeavesAndFencesPendingImport()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.Fields.Single().Text = """{"name":"safe","password":"synthetic-secret"}""";
        var pending = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ => pending.Task);
        form.SetSecretEntryEnabled(false);
        pending.SetResult("""{"settings":{"name":"late","password":"late-secret"}}"""u8.ToArray());
        await Assert.That((await import).Succeeded).IsFalse();
        JsonElement value = form.Snapshot().Values["settings"];
        await Assert.That(value.GetProperty("name").GetString()).IsEqualTo("safe");
        await Assert.That(value.TryGetProperty("password", out _)).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(2);
    }

    [Test]
    public async Task CorrectedAdvancedInputRemainsVisibleUntilDeliberateCollapse()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"] = JsonNode.Parse("""
        {"f0":{"type":"integer","title":"Advanced number","x-configurator":{"sensitivity":"public","detail":"advanced"}}}
        """);
        source["requiredCapabilities"] = new JsonArray("projection.copy", "projection.object", "schema.integer", "schema.object");
        source["artifacts"]![0]!["projection"]!["members"]!["settings"]!["reference"]!["path"] = "/f0";
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SetAllSectionsExpanded(true);
        form.Fields.Single().Text = "not-an-integer";
        form.SetAllSectionsExpanded(false);
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.VisibleFields.Count).IsEqualTo(1);
        form.Fields.Single().Text = "42";
        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(form.VisibleFields.Count).IsEqualTo(1);
        form.SetAllSectionsExpanded(false);
        await Assert.That(form.VisibleFields.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AggregateBudgetRejectsEditsWithoutBreakingValidationAndCanBeCorrected()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"] = new JsonObject(Enumerable.Range(0, 17).Select(index =>
            new KeyValuePair<string, JsonNode?>($"f{index}", JsonNode.Parse($$"""
            {
                "type":"string",
                "title":"Field {{index}}",
                "x-configurator":{"sensitivity":"public","detail":"standard"}
            }
            """))));
        source["artifacts"]![0]!["projection"]!["members"]!["settings"]!["reference"]!["path"] = "/f0";
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())),
            DefinitionSessionPrivacy.BrowserNoSecrets);
        // Sixteen values plus their configuration-tree keys fit the aggregate bound; seventeen do not.
        foreach (DefinitionFieldInput field in form.Fields) field.Text = new string('a', 65_520);
        await Assert.That(form.HasAnswerBudgetError).IsTrue();
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.Fields[^1].HasAnswerBudgetError).IsTrue();
        await Assert.That(form.VisibleFields.Count).IsEqualTo(17);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(16);
        _ = form.Validate();
        form.Fields[15].Text = "corrected";
        form.Fields[16].Text = "corrected";
        await Assert.That(form.HasAnswerBudgetError).IsFalse();
        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(17);
    }

    [Test]
    public async Task DraftPreparationDefersSecretLeavesWhileDefaultCompletePreparationStaysStrict()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.Fields.Single().Text = """{"name":"safe"}""";
        using SetupPresentationOutcome strict = await new DefinitionPresentationOperation(Definition(), "config", form.Snapshot())
            .ExecuteAsync(CancellationToken.None);
        await Assert.That(((DefinitionPreparationMetadata)strict.CoreResult).Succeeded).IsFalse();
        using SetupPresentationOutcome draft = await new DefinitionPresentationOperation(Definition(), "config", form.Snapshot(), prepareDraft: true)
            .ExecuteAsync(CancellationToken.None);
        var metadata = (DefinitionPreparationMetadata)draft.CoreResult;
        await Assert.That(metadata.Succeeded).IsTrue();
        await Assert.That(metadata.IsDraft).IsTrue();
        await Assert.That(metadata.Completions.Count).IsEqualTo(1);
        await Assert.That(metadata.Completions.Single().FieldPath).IsEqualTo("/settings/password");
    }

    [Test]
    public async Task SecretConsentDoesNotExposePrivateValuesInPreparationMetadata()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.Fields.Single().Text = """{"name":"safe","password":"synthetic-vault-marker-729"}""";
        using SetupPresentationOutcome outcome = await new DefinitionPresentationOperation(Definition(), "config", form.Snapshot())
            .ExecuteAsync(CancellationToken.None);
        await Assert.That(JsonSerializer.Serialize(outcome.CoreResult)).DoesNotContain("synthetic-vault-marker-729");
    }

    [Test]
    public async Task NoSecretImportRejectsSecretMaterialWithoutEchoAndNativeDefaultRemainsComplete()
    {
        byte[] bytes = """{"settings":{"name":"safe","password":"synthetic-vault-marker-729"}}"""u8.ToArray();
        using var browser = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        bool consumed = false;
        DefinitionImportResult result = await browser.ImportAsync("config", _ =>
        {
            consumed = true;
            return Task.FromResult<ReadOnlyMemory<byte>>(bytes);
        });
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("import-secret-consent-required");
        await Assert.That(consumed).IsFalse();
        await Assert.That(result.ToString()).DoesNotContain("synthetic-vault-marker-729");
        await Assert.That(bytes.AsSpan().SequenceEqual(
            """{"settings":{"name":"safe","password":"synthetic-vault-marker-729"}}"""u8)).IsTrue();
        await Assert.That(browser.InputRevision).IsEqualTo(0);
        using var native = new DefinitionFormModel(Definition());
        native.Fields.Single().Text = """{"name":"safe","password":"native-secret"}""";
        await Assert.That(native.Validate().IsReady).IsTrue();
    }

    [Test]
    public async Task ConsentedImportClearsConsumedPrivateBytesAfterRejection()
    {
        byte[] bytes = """{"settings":{"synthetic-obsolete-key":"synthetic-private-value"}}"""u8.ToArray();
        using var browser = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        DefinitionImportResult result = await browser.ImportAsync("config",
            _ => Task.FromResult<ReadOnlyMemory<byte>>(bytes));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(bytes.All(item => item == 0)).IsTrue();
        await Assert.That(result.ToString()).DoesNotContain("synthetic-private");
        await Assert.That(browser.InputRevision).IsEqualTo(0);
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion":2,"profile":"bounded-v2","familyId":"eba722b7-f132-444b-8c44-21cece008ca7","revision":1,
      "project":{"id":"example","name":"Example","publisher":"example"},"applicationVersions":["1"],
      "entryPoints":["setup"],"locales":["en"],
      "requiredCapabilities":["projection.copy","projection.object","schema.object","schema.string"],
      "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
        "settings":{"type":"object","additionalProperties":false,"required":["name","password"],"title":"Settings","x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
          "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"sensitive","detail":"standard"}},
          "password":{"type":"string","title":"Password","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}}},
      "artifacts":[{"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"process",
        "projection":{"op":"object","members":{"settings":{"op":"copy","reference":{"scope":"root","path":"/settings"}}}}}]
    }
    """));
}
