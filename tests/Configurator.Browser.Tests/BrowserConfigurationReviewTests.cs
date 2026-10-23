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
        source["fields"] = new JsonArray(Enumerable.Range(0, 61).Select(index => (JsonNode)new JsonObject
        {
            ["id"] = $"setting{index:D2}",
            ["title"] = $"Setting {index:D2}",
            ["type"] = "string",
            ["required"] = true,
            ["sensitivity"] = "public",
            ["disclosure"] = "advanced"
        }).ToArray());
        source["fields"]!.AsArray().Add(JsonNode.Parse("""
        {"id":"inactive","title":"Inactive","type":"string","sensitivity":"public","disclosure":"advanced",
         "activeWhen":{"op":"equals","field":"setting00","value":"enabled"}}
        """));
        source["artifacts"]![0]!["projection"] = JsonNode.Parse("""{"op":"literal","value":{}}""");
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
        source["fields"]!.AsArray().Add(JsonNode.Parse("""
        {"id":"enabled","type":"boolean","title":"Enable integration","default":false,"sensitivity":"public","disclosure":"essential"}
        """));
        source["fields"]!.AsArray().Add(JsonNode.Parse("""
        {"id":"dependent","type":"string","title":"Dependent setting","sensitivity":"public","disclosure":"advanced",
         "activeWhen":{"op":"equals","field":"enabled","value":true}}
        """));
        source["artifacts"]![0]!["projection"]!["members"]!["dependent"] = JsonNode.Parse("""{"op":"copy","field":"dependent"}""");
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
        await Assert.That(form.Snapshot().Values["enabled"].GetBoolean()).IsFalse();
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
        await Assert.That(form.Snapshot().Values["name"].GetString()).IsEqualTo(conflict ? "manual-answer" : "authored");
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
        var definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await Assert.That(component.FindAll("#artifact-import")).IsEmpty();
    }

    private static SetupDefinition Definition()
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"name","type":"string","default":"authored","title":"Name","sensitivity":"public","disclosure":"essential"},
         {"id":"note","type":"string","title":"Note","sensitivity":"public","disclosure":"advanced"},
         {"id":"secret","type":"string","title":"Secret","sensitivity":"secret","disclosure":"advanced"}]
        """);
        source.Remove("presentation");
        source["artifacts"] = JsonNode.Parse("""
        [{"id":"config","format":"json","fileName":"service.json","applySemantics":"create","restart":"process",
          "projection":{"op":"object","members":{"name":{"op":"copy","field":"name"},"note":{"op":"copy","field":"note"},"secret":{"op":"copy","field":"secret"}}}}]
        """);
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }
}
