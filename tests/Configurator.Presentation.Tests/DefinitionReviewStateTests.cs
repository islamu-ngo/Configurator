namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionReviewStateTests
{
    [Test]
    public async Task SecretImportRejectionIdentifiesAdmittedRecoveryWithoutApplyingAnswers()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["secret"] = JsonNode.Parse("""
        {"type":"string","title":"Secret","x-configurator":{"sensitivity":"secret","detail":"advanced"}}
        """);
        source["artifacts"]![0]!["projection"]!["members"]!["secret"] = JsonNode.Parse("""
        {"op":"copy","reference":{"scope":"root","path":"/secret"}}
        """);
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())),
            DefinitionSessionPrivacy.BrowserNoSecrets);
        long revision = form.InputRevision;
        DefinitionImportResult result = await Import(form, """{"secret":"synthetic-private-import"}""");
        await Assert.That(result.Code).IsEqualTo("import-secret-consent-required");
        await Assert.That(result.Diagnostic!.FieldId).IsEqualTo("secret");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values.ContainsKey("secret")).IsFalse();
        await Assert.That(result.ToString()).DoesNotContain("synthetic-private-import");
    }

    [Test]
    public async Task ResetRootInitializerKeepsEffectiveChildProvenanceWithoutMaterializingAnswers()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"] = JsonNode.Parse("""
        {"options":{"type":"object","additionalProperties":false,"required":["host"],"title":"Options",
          "x-configurator":{"sensitivity":"public","detail":"standard","initialValue":{"host":"example.test"}},
          "properties":{
            "host":{"type":"string","title":"Host","x-configurator":{"sensitivity":"public","detail":"standard"}}}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = JsonNode.Parse("""
        ["projection.literal","schema.object","schema.string","values.initial"]
        """);
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.ResetValue(["options"]);
        long revision = form.InputRevision;
        await Assert.That(form.AnswerOrigin(["options", "host"])).IsEqualTo(DefinitionAnswerOrigin.Defaulted);
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("options")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task ImportPreservesUnmentionedOriginsAndInvalidDrafts()
    {
        using var form = new DefinitionFormModel(Definition());
        form.EditValue(["note"], "retained");
        form.EditValue(["count"], "7");
        form.EditValue(["count"], "invalid-number");
        DefinitionImportResult result = await Import(form, """{"enabled":false}""");

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(form.AnswerOrigin(["name"])).IsEqualTo(DefinitionAnswerOrigin.Defaulted);
        await Assert.That(form.AnswerOrigin(["note"])).IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(form.AnswerOrigin(["enabled"])).IsEqualTo(DefinitionAnswerOrigin.Imported);
        await Assert.That(form.ReadDraftText(["count"])).IsEqualTo("invalid-number");
        await Assert.That(form.HasInputErrors).IsTrue();
    }

    [Test]
    public async Task PrefillMayReplaceOnlyPristineDefaults()
    {
        using var form = new DefinitionFormModel(Definition());
        DefinitionImportResult result = await Import(form, """{"name":"prefilled"}""");
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(form.AnswerOrigin(["name"])).IsEqualTo(DefinitionAnswerOrigin.Imported);

        using var explicitForm = new DefinitionFormModel(Definition());
        explicitForm.EditValue(["name"], "authored");
        long revision = explicitForm.InputRevision;
        DefinitionImportResult conflict = await Import(explicitForm, """{"name":"different"}""");
        await Assert.That(conflict.Code).IsEqualTo("answer-conflict");
        await Assert.That(explicitForm.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task EqualValueImportStillRecordsDeliberateImport()
    {
        using var form = new DefinitionFormModel(Definition());
        DefinitionImportResult result = await Import(form, """{"name":"authored"}""");
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(form.AnswerOrigin(["name"])).IsEqualTo(DefinitionAnswerOrigin.Imported);
        await Assert.That(form.AnswerOrigin(["enabled"])).IsEqualTo(DefinitionAnswerOrigin.Defaulted);
    }

    private static Task<DefinitionImportResult> Import(DefinitionFormModel form, string json) =>
        form.ImportAsync("config", _ => Task.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(json)));

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":2,"profile":"bounded-v2","familyId":"32d3b55d-7285-4b0f-8bac-77c9c9f32004","revision":1,
     "project":{"id":"example","publisher":"example","name":"Review example"},
     "applicationVersions":["1"],"entryPoints":["setup"],"locales":["en"],
     "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
       "properties":{
         "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"basic","initialValue":"authored"}},
         "note":{"type":"string","title":"Note","x-configurator":{"sensitivity":"public","detail":"standard"}},
         "count":{"type":"integer","title":"Count","x-configurator":{"sensitivity":"public","detail":"standard"}},
         "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public","detail":"basic","initialValue":false}}}},
     "artifacts":[{"id":"config","format":"json","fileName":"service.json","targetUpdateMode":"create","restartRequirement":"process",
       "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/name"}},
         "note":{"op":"copy","reference":{"scope":"root","path":"/note"}},
         "count":{"op":"copy","reference":{"scope":"root","path":"/count"}},
         "enabled":{"op":"copy","reference":{"scope":"root","path":"/enabled"}}}}}],
     "requiredCapabilities":["projection.copy","projection.object","schema.boolean","schema.integer","schema.object","schema.string","values.initial"]}
    """));
}
