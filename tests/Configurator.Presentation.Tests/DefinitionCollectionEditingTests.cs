namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;

public sealed class DefinitionCollectionEditingTests
{
    [Test]
    public async Task TypedRowsMaterializeOnlyDeclaredContainersAndEveryEditAdvancesRevision()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.AddListItem(["rows"])).IsTrue();
        JsonElement row = form.Snapshot().Values["rows"][0];
        await Assert.That(row.GetProperty("metadata").EnumerateObject().Count()).IsEqualTo(0);
        await Assert.That(row.GetProperty("settings").GetProperty("enabled").GetBoolean()).IsTrue();
        await Assert.That(form.SetValue(["rows", "0", "metadata", "name"], JsonSerializer.SerializeToElement("first"))).IsTrue();
        await Assert.That(form.AddListItem(["rows"])).IsTrue();
        await Assert.That(form.AddListItem(["rows"])).IsFalse();
        await Assert.That(form.SetValue(["rows", "1", "metadata", "name"], JsonSerializer.SerializeToElement("second"))).IsTrue();
        await Assert.That(form.RemoveListItem(["rows"], 0)).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(5);
        using DefinitionArtifact? artifact = DefinitionArtifactComposer.Compose(Definition(), "config", form.Snapshot()).Artifact;
        await Assert.That(Encoding.UTF8.GetString(artifact!.Bytes.Span))
            .IsEqualTo("""{"rows":[{"metadata":{"name":"second"},"settings":{"enabled":true}}]}""");
    }

    [Test]
    public async Task PerLeafConsentRetainsSafeRowsAndClearsSecretsWithoutRestoringRemovedRows()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        form.AddListItem(["rows"]);
        await Assert.That(form.CanEdit(["rows", "0", "metadata", "name"])).IsTrue();
        await Assert.That(form.CanEdit(["rows", "0", "password"])).IsFalse();
        await Assert.That(form.SetValue(["rows", "0", "password"], JsonSerializer.SerializeToElement("synthetic-private-row"))).IsFalse();
        form.SetSecretEntryEnabled(true);
        form.SetValue(["rows", "0", "metadata", "name"], JsonSerializer.SerializeToElement("safe"));
        form.SetValue(["rows", "0", "password"], JsonSerializer.SerializeToElement("synthetic-private-row"));
        form.AddListItem(["rows"]);
        form.SetValue(["rows", "1", "metadata", "name"], JsonSerializer.SerializeToElement("removed"));
        form.RemoveListItem(["rows"], 1);
        form.SetSecretEntryEnabled(false);
        JsonElement rows = form.Snapshot().Values["rows"];
        await Assert.That(rows.GetArrayLength()).IsEqualTo(1);
        await Assert.That(rows[0].GetProperty("metadata").GetProperty("name").GetString()).IsEqualTo("safe");
        await Assert.That(rows[0].TryGetProperty("password", out _)).IsFalse();
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":1,"familyId":"03b3abf0-08e0-4e56-b94a-b78d25d28901","revision":1,
     "applicationId":"example","applicationVersions":["1"],"publisher":"example","title":"Example","entryPoints":["setup"],"locales":["en"],
     "fields":[{"id":"rows","type":"list","itemType":"object","maxItems":2,"title":"Rows","sensitivity":"public","disclosure":"guided","properties":[
       {"id":"metadata","type":"object","required":true,"title":"Metadata","sensitivity":"public","disclosure":"guided","properties":[
         {"id":"name","type":"string","required":true,"title":"Name","sensitivity":"public","disclosure":"guided"}]},
       {"id":"settings","type":"object","required":true,"title":"Settings","sensitivity":"public","disclosure":"guided","properties":[
         {"id":"enabled","type":"boolean","default":true,"title":"Enabled","sensitivity":"public","disclosure":"guided"}]},
       {"id":"password","type":"string","title":"Password","sensitivity":"secret","disclosure":"guided"}]}],
     "artifacts":[{"id":"config","format":"json","fileName":"config.json","applySemantics":"create","restart":"process",
       "projection":{"op":"object","members":{"rows":{"op":"copy","field":"rows"}}}}]}
    """));
}
