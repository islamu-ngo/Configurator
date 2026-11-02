namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionDirectEditingTests
{
    [Test]
    public async Task DirectVisibilityExposesOrdinaryAdvancedFieldsWithoutSupplyingAnswers()
    {
        Definition definition = Definition();
        using var authored = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserNoSecrets);
        using var direct = new DefinitionFormModel(definition, DefinitionSessionPrivacy.BrowserNoSecrets,
            editorVisibility: DefinitionEditorVisibilityPolicy.Direct);
        int notifications = 0;
        direct.InputChanged += (_, _) => notifications++;

        await Assert.That(authored.IsEditorVisible(["text"])).IsFalse();
        await Assert.That(direct.IsEditorVisible(["text"])).IsTrue();
        await Assert.That(direct.IsEditorVisible(["flag"])).IsTrue();
        await Assert.That(direct.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(direct.InputRevision).IsEqualTo(0);
        await Assert.That(notifications).IsEqualTo(0);
    }

    [Test]
    public async Task DirectVisibilityDoesNotBroadenTheSelectedExperience()
    {
        JsonNode source = JsonNode.Parse(Definition().CanonicalBytes.Span)!;
        source["presentation"] = JsonNode.Parse("""
            {"defaultJourney":"simple","navigation":"sections","sections":[
              {"id":"basic","title":"Basic","pointers":["/flag"]},
              {"id":"other","title":"Other","pointers":["/text","/token"]}],
             "journeys":[{"id":"simple","title":"Simple","sections":["basic"]}]}
            """);
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())),
            DefinitionSessionPrivacy.BrowserNoSecrets, editorVisibility: DefinitionEditorVisibilityPolicy.Direct);

        await Assert.That(form.IsEditorVisible(["flag"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["text"])).IsFalse();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(0);
    }

    [Test]
    public async Task DirectVisibilityNeverGrantsSecretEditingAuthority()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets,
            editorVisibility: DefinitionEditorVisibilityPolicy.Direct);

        await Assert.That(form.CanEdit(["token"])).IsFalse();
        await Assert.That(form.EditValue(["token"], "synthetic-denied-value")).IsFalse();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.SessionPrivacy).IsEqualTo(DefinitionSessionPrivacy.BrowserNoSecrets);
    }

    [Test]
    public async Task DirectEditsKeepEmptyFalseOmissionAndResetDistinct()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets,
            editorVisibility: DefinitionEditorVisibilityPolicy.Direct);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.SetValue(["text"], JsonSerializer.SerializeToElement(string.Empty))).IsTrue();
        await Assert.That(form.SetValue(["flag"], JsonSerializer.SerializeToElement(false))).IsTrue();
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo(string.Empty);
        await Assert.That(form.Snapshot().Values["flag"].GetBoolean()).IsFalse();
        await Assert.That(form.AnswerOrigin(["text"])).IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(form.AnswerOrigin(["flag"])).IsEqualTo(DefinitionAnswerOrigin.Explicit);

        await Assert.That(form.EditValue(["text"], null)).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.FromSegments(["text"]), out _)).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("text")).IsFalse();
        await Assert.That(form.ResetValue(["text"])).IsTrue();
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.FromSegments(["text"]), out JsonElement initialized)).IsTrue();
        await Assert.That(initialized.GetString()).IsEqualTo("seed");
        await Assert.That(form.Snapshot().Values.ContainsKey("text")).IsFalse();
        await Assert.That(form.AnswerOrigin(["text"])).IsEqualTo(DefinitionAnswerOrigin.Defaulted);
        await Assert.That(form.Snapshot().Values["flag"].GetBoolean()).IsFalse();
    }

    private static Definition Definition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":"0.0.1","profile":"bounded-v0.0.1","familyId":"f87bc505-b134-46e6-ae10-60458baf32b6","revision":1,
     "project":{"id":"direct-edit","name":"Direct edit fixture","publisher":"example"},
     "applicationVersions":["1"],"entryPoints":["setup"],"locales":["en"],
     "configuration":{"type":"object","additionalProperties":false,
       "x-configurator":{"sensitivity":"public"},"properties":{
         "text":{"type":"string","title":"Text","x-configurator":{"sensitivity":"public","detail":"advanced","initialValue":"seed"}},
         "flag":{"type":"boolean","title":"Flag","x-configurator":{"sensitivity":"public","detail":"advanced"}},
         "token":{"type":"string","title":"Credential","x-configurator":{"sensitivity":"secret","detail":"advanced"}}}},
     "requiredCapabilities":["projection.copy","projection.object","schema.boolean","schema.object","schema.string","values.initial"],
     "artifacts":[{"id":"config","format":"json","fileName":"service.json","targetUpdateMode":"create","restartRequirement":"process",
       "projection":{"op":"object","members":{"text":{"op":"copy","reference":{"scope":"root","path":"/text"}}}}}]}
    """));
}
