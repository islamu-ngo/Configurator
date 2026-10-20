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
        source["fields"]!.AsArray().Add(JsonNode.Parse("""
        {"id":"secret","type":"string","title":"Secret","sensitivity":"secret","disclosure":"advanced"}
        """));
        source["artifacts"]![0]!["projection"]!["members"]!["secret"] = JsonNode.Parse("""{"op":"copy","field":"secret"}""");
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
    public async Task OmittedRootDefaultKeepsEffectiveChildProvenanceWithoutMaterializingAnswers()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"options","type":"object","default":{"host":"example.test"},"title":"Options",
          "sensitivity":"public","disclosure":"guided","properties":[
            {"id":"host","type":"string","title":"Host","required":true,"sensitivity":"public","disclosure":"guided"}]}]
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SetValue(["options"], null);
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
    {"schemaVersion":1,"familyId":"32d3b55d-7285-4b0f-8bac-77c9c9f32004","revision":1,
     "applicationId":"example","applicationVersions":["1"],"publisher":"example",
     "title":"Review example","entryPoints":["setup"],"locales":["en"],
     "fields":[
       {"id":"name","type":"string","default":"authored","title":"Name","sensitivity":"public","disclosure":"essential"},
       {"id":"note","type":"string","title":"Note","sensitivity":"public","disclosure":"guided"},
       {"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"guided"},
       {"id":"enabled","type":"boolean","default":false,"title":"Enabled","sensitivity":"public","disclosure":"essential"}],
     "artifacts":[{"id":"config","format":"json","fileName":"service.json","applySemantics":"create","restart":"process",
       "projection":{"op":"object","members":{"name":{"op":"copy","field":"name"},
         "note":{"op":"copy","field":"note"},"count":{"op":"copy","field":"count"},"enabled":{"op":"copy","field":"enabled"}}}}]}
    """));
}
