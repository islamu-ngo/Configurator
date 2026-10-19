namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionAnswerPresenceTests
{
    [Test]
    public async Task ImportedEmptyStringSurvivesSnapshotAndPreparation()
    {
        using var form = new DefinitionFormModel(Definition());
        DefinitionImportResult result = await form.ImportAsync("config",
            _ => Task.FromResult<ReadOnlyMemory<byte>>("""{"text":""}"""u8.ToArray()));
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("text")).IsTrue();
        using DefinitionArtifact? artifact = DefinitionArtifactComposer.Compose(form.Definition, "config", form.Snapshot()).Artifact;
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo("""{"text":""}""");
    }

    [Test]
    public async Task EmptyDefaultAndTypedEmptyEnumRemainSupplied()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.Snapshot().Values["defaultText"].GetRawText()).IsEqualTo("\"\"");
        await Assert.That(form.SetValue(["choice"], JsonSerializer.SerializeToElement(""))).IsTrue();
        await Assert.That(form.Snapshot().Values["choice"].GetRawText()).IsEqualTo("\"\"");
    }

    [Test]
    public async Task ExplicitEmptyScalarAndContainersCanBeOmittedWithoutLosingSiblings()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["text"], JsonSerializer.SerializeToElement(""));
        form.SetValue(["enabled"], JsonSerializer.SerializeToElement(false));
        form.SetValue(["count"], JsonSerializer.SerializeToElement(0));
        form.SupplyObject(["options"]);
        form.SupplyList(["items"]);
        await Assert.That(form.Snapshot().Values["text"].GetRawText()).IsEqualTo("\"\"");
        await Assert.That(form.Snapshot().Values["enabled"].GetRawText()).IsEqualTo("false");
        await Assert.That(form.Snapshot().Values["count"].GetRawText()).IsEqualTo("0");
        await Assert.That(form.Snapshot().Values["options"].GetRawText()).IsEqualTo("{}");
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[]");
        form.SetValue(["text"], null);
        form.SetValue(["options"], null);
        form.SetValue(["items"], null);
        await Assert.That(form.Snapshot().Values.ContainsKey("text")).IsFalse();
        await Assert.That(form.Snapshot().Values["enabled"].GetBoolean()).IsFalse();
        await Assert.That(form.Snapshot().Values["count"].GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task PristineEmptyListItemMatchesTypedThenErasedItem()
    {
        using var form = new DefinitionFormModel(Definition());
        form.AddListItem(["items"]);
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("""[""]""");
        form.SetValue(["items", "0"], JsonSerializer.SerializeToElement("changed"));
        form.SetValue(["items", "0"], JsonSerializer.SerializeToElement(""));
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("""[""]""");
    }

    [Test]
    public async Task SameValueDeliberateTypedEditFencesHeldImport()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["text"], JsonSerializer.SerializeToElement("current"));
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> pending = form.ImportAsync("config", _ => read.Task);
        long before = form.InputRevision;
        form.SetValue(["text"], JsonSerializer.SerializeToElement("current"));
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        read.SetResult("""{"text":"current"}"""u8.ToArray());
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(5))).Code).IsEqualTo("import-stale");
        await Assert.That(form.Snapshot().Values["text"].GetString()).IsEqualTo("current");
    }

    [Test]
    public async Task RepeatedInvalidDraftFencesHeldImportWithoutAcceptedValueChange()
    {
        using var form = new DefinitionFormModel(Definition());
        DefinitionFieldInput count = form.Fields.Single(field => field.Definition.Id == "count");
        count.Text = "invalid";
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> pending = form.ImportAsync("config", _ => read.Task);
        long before = form.InputRevision;
        count.Text = "invalid";
        await Assert.That(form.InputRevision).IsEqualTo(before + 1);
        await Assert.That(form.HasInputErrors).IsTrue();
        read.SetResult("""{"text":"late"}"""u8.ToArray());
        await Assert.That((await pending.WaitAsync(TimeSpan.FromSeconds(5))).Code).IsEqualTo("import-stale");
    }

    [Test]
    public async Task NestedRejectedEditsAreAtomicAndRemainInvalidUntilEachDraftIsCorrected()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["options", "count"], JsonSerializer.SerializeToElement(7));
        long observedRevision = -1;
        form.InputChanged += (_, _) =>
            observedRevision = Task.Run(form.Snapshot).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult().InputRevision;
        long before = form.InputRevision;
        await Assert.That(form.EditValue(["options", "count"], "invalid")).IsFalse();
        await Assert.That(observedRevision).IsEqualTo(before + 1);
        await Assert.That(form.Snapshot().Values["options"].GetRawText()).IsEqualTo("""{"count":7}""");
        form.EditValue(["options", "other"], "invalid");
        form.EditValue(["options", "count"], "7");
        await Assert.That(form.HasInputErrors).IsTrue();
        form.SetValue(["options", "other"], null);
        await Assert.That(form.HasInputErrors).IsFalse();
    }

    [Test]
    public async Task CollectionAppendAndRemovalDoNotForgetSurvivingInvalidDrafts()
    {
        using var form = new DefinitionFormModel(Definition());
        form.AddListItem(["items"]);
        form.AddListItem(["items"]);
        await Assert.That(form.EditValue(["items", "1"], new string('x', 65_537))).IsFalse();
        form.AddListItem(["items"]);
        await Assert.That(form.HasInputErrors).IsTrue();
        form.RemoveListItem(["items"], 0);
        await Assert.That(form.HasInputErrors).IsTrue();
        form.EditValue(["items", "0"], "");
        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("""["",""]""");
    }

    [Test]
    public async Task DenialClearsSecretDraftsButRetainsInvalidPublicDraftsAndTypedSiblings()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.SetSecretEntryEnabled(true);
        form.EditValue(["options", "secret"], "synthetic-secret");
        form.EditValue(["options", "count"], "invalid");
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.Snapshot().Values["options"].GetRawText()).IsEqualTo("{}");
        await Assert.That(form.EditValue(["options", "secret"], "")).IsFalse();
        form.EditValue(["options", "count"], "0");
        await Assert.That(form.HasInputErrors).IsFalse();
    }

    [Test]
    public async Task DeliberateOmissionFencesImportWithoutMaterializingAbsentAncestors()
    {
        using var form = new DefinitionFormModel(Definition());
        var read = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("config", _ => read.Task);
        form.SetValue(["options", "count"], null);
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.EditValue(["options", "count"], null)).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.ContainsKey("options")).IsFalse();
        read.SetResult("""{"text":"late"}"""u8.ToArray());
        await Assert.That((await import.WaitAsync(TimeSpan.FromSeconds(5))).Code).IsEqualTo("import-stale");
    }

    [Test]
    public async Task TypedValueOwnsCallerJsonAfterTheSourceDocumentIsDisposed()
    {
        using var form = new DefinitionFormModel(Definition());
        using (JsonDocument source = JsonDocument.Parse("\"\""))
            form.SetValue(["text"], source.RootElement);
        await Assert.That(form.Snapshot().Values["text"].GetRawText()).IsEqualTo("\"\"");
    }

    [Test]
    public async Task EmptySecretIsStillConsentGatedThroughDirectFieldText()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.Fields.Single(field => field.Definition.Id == "secretText").Text = "";
        form.Fields.Single(field => field.Definition.Id == "options").Text = """{"secret":""}""";
        await Assert.That(form.Snapshot().Values.ContainsKey("secretText")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("options")).IsFalse();
        await Assert.That(form.SetValue(["secretText"], JsonSerializer.SerializeToElement(""))).IsFalse();
    }

    [Test]
    [Arguments("{\"text\":\"ordinary\",\"secretText\":\"\"}", "secretText", "\"\"")]
    [Arguments("{\"text\":\"ordinary\",\"options\":{\"secret\":\"\"}}", "options", "{\"secret\":\"\"}")]
    public async Task EmptySecretImportRequiresConsentBeforePublishingAnyAnswer(string json, string field, string expected)
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        DefinitionImportResult rejected = await form.ImportAsync("config",
            _ => Task.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(json)));
        await Assert.That(rejected.Code).IsEqualTo("import-secret-consent-required");
        await Assert.That(form.Snapshot().Values.ContainsKey("text")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey(field)).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        form.SetSecretEntryEnabled(true);
        DefinitionImportResult accepted = await form.ImportAsync("config",
            _ => Task.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(json)));
        await Assert.That(accepted.Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values[field].GetRawText()).IsEqualTo(expected);
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":1,"familyId":"c9fcf10e-526d-4007-bd03-825d039366ed","revision":1,
     "applicationId":"example","applicationVersions":["1"],"publisher":"example","title":"Example","entryPoints":["setup"],"locales":["en"],
     "fields":[
       {"id":"text","type":"string","title":"Text","sensitivity":"public","disclosure":"guided"},
       {"id":"secretText","type":"string","title":"Secret text","sensitivity":"secret","disclosure":"guided"},
       {"id":"defaultText","type":"string","default":"","title":"Default","sensitivity":"public","disclosure":"guided"},
       {"id":"choice","type":"enum","choices":["","other"],"title":"Choice","sensitivity":"public","disclosure":"guided"},
       {"id":"enabled","type":"boolean","title":"Enabled","sensitivity":"public","disclosure":"guided"},
       {"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"guided"},
       {"id":"options","type":"object","title":"Options","sensitivity":"public","disclosure":"guided","properties":[
         {"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"guided"},
         {"id":"other","type":"integer","title":"Other","sensitivity":"public","disclosure":"guided"},
         {"id":"secret","type":"string","title":"Secret","sensitivity":"secret","disclosure":"guided"}]},
       {"id":"items","type":"list","itemType":"string","title":"Items","sensitivity":"public","disclosure":"guided"}],
     "artifacts":[{"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"process",
       "projection":{"op":"object","members":{"text":{"op":"copy","field":"text"},"secretText":{"op":"copy","field":"secretText"},"options":{"op":"copy","field":"options"}}}}]}
    """));
}
