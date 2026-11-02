namespace Configurator.Presentation.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionMapBoundsTests
{
    [Test]
    public async Task MaterializingAnAncestorCannotBypassTheMapBound()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["map"]!["additionalProperties"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"properties":{"value":{"type":"integer"}}}
            """);
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(
            System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())));
        await Assert.That(form.SetValue(["map", "first", "value"], JsonSerializer.SerializeToElement(1))).IsTrue();
        long revision = form.InputRevision;

        await Assert.That(form.SetValue(["map", "second", "value"], JsonSerializer.SerializeToElement(2))).IsFalse();

        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["map"].GetRawText()).IsEqualTo("{\"first\":{\"value\":1}}");
    }

    [Test]
    public async Task MapAddPreservesLiteralKeysAndRejectsDuplicatesBoundsAndInvalidKeys()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(form.AddMapEntry(["map"], "\ud800")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.AddMapEntry(["map"], "literal/~key")).IsTrue();
        long revision = form.InputRevision;
        await Assert.That(form.AddMapEntry(["map"], "literal/~key")).IsFalse();
        await Assert.That(form.AddMapEntry(["map"], "second")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["map"].GetProperty("literal/~key").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task AddingPastTheDeclaredMapBoundPreservesAcceptedState()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["map"], JsonSerializer.SerializeToElement(new { first = 1 }));
        long revision = form.InputRevision;

        bool accepted = form.SetValue(["map", "second"], JsonSerializer.SerializeToElement(2));

        await Assert.That(accepted).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["map"].GetRawText()).IsEqualTo("{\"first\":1}");
    }

    private static Definition Definition() => DefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"a4eb9877-03ad-46fd-9d55-2d5e68bc885b","revision":1},
         "project":{"id":"map-bounds","name":"Map bounds"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"properties":{
           "map":{"type":"object","maxProperties":1,"additionalProperties":{"type":"integer"}}
         }},"outputs":{"config.json":{"id":"config","format":"json"}}}
        """u8.ToArray());
}
