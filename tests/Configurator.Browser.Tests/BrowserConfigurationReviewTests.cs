namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserConfigurationReviewTests
{
    [Test]
    public async Task ReviewPagesActiveObligationsAndInspectsInactiveMetadataDeliberately()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"] = new JsonObject(Enumerable.Range(0, 61).Select(index =>
            new KeyValuePair<string, JsonNode?>($"setting{index:D2}", new JsonObject
            {
                ["title"] = $"Setting {index:D2}",
                ["type"] = "string",
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public", ["detail"] = "advanced" }
            })));
        source["configuration"]!["required"] = new JsonArray(Enumerable.Range(0, 61)
            .Select(index => (JsonNode?)JsonValue.Create($"setting{index:D2}")).ToArray());
        source["configuration"]!["properties"]!["inactive"] = JsonNode.Parse("""
        {"title":"Inactive","type":"string","x-configurator":{"sensitivity":"public","detail":"advanced",
         "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/setting00"},"value":"enabled"}}}
        """);
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = new JsonArray("predicate.root", "projection.literal", "schema.object", "schema.string");
        var definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-review-count]").GetAttribute("data-review-count")).IsEqualTo("61");
        await Assert.That(component.FindAll("[data-review-path]").Count).IsEqualTo(50);
        await Assert.That(component.Find("[data-correction='setting60']")).IsNotNull();
        await component.Find("[data-action='review-next']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-review-path]").Count).IsEqualTo(11);
        await component.Find("#review-include-inactive").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-review-count]").GetAttribute("data-review-count")).IsEqualTo("62");
        await component.Find("[data-action='review-next']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-review-path='inactive'] button")).IsEmpty();
        await Assert.That(form.Validate().Fields.Single(field => field.Id == "inactive").IsActive).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task InactiveImportRecoveryOpensControllerWithoutActivatingTheImportedField()
    {
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["configuration"]!["properties"]!["enabled"] = JsonNode.Parse("""
        {"type":"boolean","title":"Enable integration","x-configurator":{"initialValue":false,"sensitivity":"public","detail":"basic"}}
        """);
        source["configuration"]!["properties"]!["dependent"] = JsonNode.Parse("""
        {"type":"string","title":"Dependent setting","x-configurator":{"sensitivity":"public","detail":"advanced",
         "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}}
        """);
        source["artifacts"]![0]!["projection"]!["members"]!["dependent"] =
            JsonNode.Parse("""{"op":"copy","reference":{"scope":"root","path":"/dependent"}}""");
        source["requiredCapabilities"] = new JsonArray("predicate.root", "projection.copy", "projection.object",
            "schema.boolean", "schema.object", "schema.string", "values.initial");
        await using var context = BrowserFixture.Context();
        var definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SetDiscovery("obstructing-query"));
        long revision = form.InputRevision;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"dependent":"synthetic-file-canary"}"""));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await component.Find("[data-import-correction='dependent']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.DiscoveryQuery).IsEqualTo("obstructing-query");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/enabled"), out var effectiveEnabled)).IsTrue();
        await Assert.That(effectiveEnabled.GetBoolean()).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("dependent")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.Markup).DoesNotContain("synthetic-file-canary");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RejectedKnownFieldImportOffersValueFreeContextualRecovery(bool conflict)
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        if (conflict) await component.InvokeAsync(() => form.EditValue(["name"], "manual-answer"));
        long revision = form.InputRevision;
        string json = conflict ? """{"name":"synthetic-file-canary"}""" : """{"name":42}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code"))
            .IsEqualTo(conflict ? "answer-conflict" : "import-field-invalid");
        await Assert.That(component.Markup).DoesNotContain("synthetic-file-canary");
        await component.Find("[data-import-correction='name']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.IsEditorVisible(["name"])).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/name"), out var effectiveName)).IsTrue();
        await Assert.That(effectiveName.GetString()).IsEqualTo(conflict ? "manual-answer" : "authored");
        await Assert.That(form.Snapshot().Values.ContainsKey("name")).IsEqualTo(conflict);
        await component.Find("[data-field='name'] input").InputAsync(new ChangeEventArgs { Value = "corrected" });
        await Assert.That(component.FindAll("[data-import-correction]")).IsEmpty();
    }

    [Test]
    public async Task DeniedSecretImportOffersConsentRecoveryWithoutEnablingOrApplyingIt()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"secret":"synthetic-file-canary"}"""));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("import-secret-consent-required");
        await component.Find("[data-import-correction='secret']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values.ContainsKey("secret")).IsFalse();
        await Assert.That(component.FindAll("[data-field='secret'] input")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("synthetic-file-canary");
    }

    [Test]
    public async Task SupportedImportPrecedesManualEntry()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var controls = component.FindAll("input").Select(control => control.Id).ToList();
        int importIndex = controls.IndexOf("artifact-import");
        int manualIndex = controls.IndexOf(component.Find("[data-field='name'] input").Id);
        await Assert.That(importIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(manualIndex).IsGreaterThan(importIndex);
    }

    [Test]
    public async Task ReviewExplainsOriginsWithoutExposingAnswersOrChangingRevision()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() =>
        {
            form.SetSecretEntryEnabled(true);
            form.EditValue(["secret"], "synthetic-review-canary");
        });
        long revision = form.InputRevision;
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());

        var review = component.Find("[data-review-effects]");
        await Assert.That(review.OuterHtml).DoesNotContain("synthetic-review-canary");
        await Assert.That(review.QuerySelector("[data-review-path='name']")!.GetAttribute("data-origin")).IsEqualTo("Defaulted");
        await Assert.That(review.QuerySelector("[data-review-path='note']")).IsNull();
        await Assert.That(review.QuerySelector("[data-review-path='secret']")!.GetAttribute("data-origin")).IsEqualTo("Explicit");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await component.Find("#review-include-omitted").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-review-path='note']").GetAttribute("data-origin")).IsEqualTo("Omitted");
        await component.Find("[data-review-path='note'] button").ClickAsync(new MouseEventArgs());
        await Assert.That(form.IsEditorVisible(["note"])).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await component.Find("[data-action='return-to-review']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-workflow-step='review']").GetAttribute("aria-current")).IsEqualTo("step");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task UnsupportedInverseDoesNotOfferImport()
    {
        await using var context = BrowserFixture.Context();
        JsonObject source = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
        source["requiredCapabilities"] = new JsonArray("projection.literal", "schema.object", "schema.string", "values.initial");
        var definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await Assert.That(component.FindAll("#artifact-import")).IsEmpty();
    }

    private static SetupDefinition Definition()
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["configuration"] = JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
          "name":{"type":"string","title":"Name","x-configurator":{"initialValue":"authored","sensitivity":"public","detail":"basic"}},
          "note":{"type":"string","title":"Note","x-configurator":{"sensitivity":"public","detail":"advanced"}},
          "secret":{"type":"string","title":"Secret","x-configurator":{"sensitivity":"secret","detail":"advanced"}}}}
        """);
        source.Remove("presentation");
        source["artifacts"] = JsonNode.Parse("""
        [{"id":"config","format":"json","fileName":"service.json","targetUpdateMode":"create","restartRequirement":"process",
          "projection":{"op":"object","members":{
            "name":{"op":"copy","reference":{"scope":"root","path":"/name"}},
            "note":{"op":"copy","reference":{"scope":"root","path":"/note"}},
            "secret":{"op":"copy","reference":{"scope":"root","path":"/secret"}}}}}]
        """);
        source["requiredCapabilities"] = new JsonArray("projection.copy", "projection.object",
            "schema.object", "schema.string", "values.initial");
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }
}
