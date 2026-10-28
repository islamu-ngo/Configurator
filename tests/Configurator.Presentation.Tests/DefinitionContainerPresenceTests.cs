namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;

public sealed class DefinitionContainerPresenceTests
{
    [Test]
    public async Task ClearingAbsentRequiredChildPreservesOptionalAncestorAndRevision()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SupplyObject(["empty"]);
        form.SetValue(["items"], JsonSerializer.SerializeToElement(Array.Empty<object>()));
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(form.SetValue(["optional", "nested", "name"], null)).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("optional")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(3);
        await Assert.That(form.Validate().IsReady).IsTrue();
    }

    [Test]
    public async Task ClearingAbsentNestedChildPreservesSuppliedParentAndSibling()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.SetValue(["optional", "sibling"], JsonSerializer.SerializeToElement("kept"))).IsTrue();
        await Assert.That(form.SetValue(["optional", "nested", "name"], null)).IsTrue();
        await Assert.That(form.Snapshot().Values["optional"].GetRawText()).IsEqualTo("""{"sibling":"kept"}""");
        await Assert.That(form.InputRevision).IsEqualTo(2);
    }

    [Test]
    public async Task ExplicitEmptyContainersRemainDistinctFromAbsence()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.SupplyObject(["empty"])).IsTrue();
        await Assert.That(form.SupplyList(["items"])).IsTrue();
        await Assert.That(form.Snapshot().Values["empty"].GetRawText()).IsEqualTo("{}");
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[]");
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(form.SupplyObject(["empty"])).IsTrue();
        await Assert.That(form.SupplyList(["items"])).IsTrue();
        await Assert.That(form.AddListItem(["items"])).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(2);
        await Assert.That(form.SetValue(["empty"], null)).IsTrue();
        await Assert.That(form.SetValue(["items"], null)).IsTrue();
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(4);
        await Assert.That(form.Validate().IsReady).IsFalse();
    }

    [Test]
    public async Task SupplyingListsRetainsExistingItemsAndRejectsWrongContainerTypes()
    {
        using var form = new DefinitionFormModel(Definition());
        using JsonDocument items = JsonDocument.Parse("""["retained"]""");
        form.SetValue(["private"], items.RootElement);
        await Assert.That(form.SupplyList(["private"])).IsTrue();
        await Assert.That(form.Snapshot().Values["private"].GetRawText()).IsEqualTo("""["retained"]""");
        await Assert.That(form.SupplyList(["empty"])).IsFalse();
        await Assert.That(form.SupplyObject(["items"])).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(1);
    }

    [Test]
    public async Task SupplyingSecretListRequiresConsentEvenWhenEmpty()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.SupplyList(["private"])).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        form.SetSecretEntryEnabled(true);
        await Assert.That(form.SupplyList(["private"])).IsTrue();
        await Assert.That(form.Snapshot().Values["private"].GetRawText()).IsEqualTo("[]");
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.Snapshot().Values.ContainsKey("private")).IsFalse();
        await Assert.That(form.SupplyList(["private"])).IsFalse();
    }

    private static SetupDefinition Definition() => SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {"schemaVersion":2,"profile":"bounded-v2","familyId":"03b3abf0-08e0-4e56-b94a-b78d25d28902","revision":1,
     "project":{"id":"example","name":"Example","publisher":"example"},
     "applicationVersions":["1"],"entryPoints":["setup"],"locales":["en"],
     "requiredCapabilities":["projection.copy","projection.object","schema.array","schema.bounds","schema.integer","schema.object","schema.string"],
     "configuration":{"type":"object","additionalProperties":false,"required":["empty","items"],"x-configurator":{"sensitivity":"public"},"properties":{
       "optional":{"type":"object","additionalProperties":false,"required":["nested"],"title":"Optional","x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
         "nested":{"type":"object","additionalProperties":false,"required":["name"],"title":"Nested","x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
           "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}}}},
         "sibling":{"type":"string","title":"Sibling","x-configurator":{"sensitivity":"public","detail":"standard"}}}},
       "empty":{"type":"object","additionalProperties":false,"properties":{},"title":"Empty","x-configurator":{"sensitivity":"public","detail":"standard"}},
       "items":{"type":"array","items":{"type":"integer","x-configurator":{"sensitivity":"public"}},"maxItems":0,"title":"Items","x-configurator":{"sensitivity":"public","detail":"standard"}},
       "private":{"type":"array","items":{"type":"string","x-configurator":{"sensitivity":"secret"}},"title":"Private","x-configurator":{"sensitivity":"secret","detail":"standard"}}}},
     "artifacts":[{"id":"config","format":"json","fileName":"config.json","targetUpdateMode":"create","restartRequirement":"process",
       "projection":{"op":"object","members":{
         "empty":{"op":"copy","reference":{"scope":"root","path":"/empty"}},
         "items":{"op":"copy","reference":{"scope":"root","path":"/items"}}}}}]}
    """));
}
