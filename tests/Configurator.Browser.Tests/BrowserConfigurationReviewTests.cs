namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserConfigurationReviewTests
{
    [Test]
    public async Task SupportedImportPrecedesManualEntry()
    {
        using var context = BrowserFixture.Context();
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
        using var context = BrowserFixture.Context();
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
        await Assert.That(review.QuerySelector("[data-review-path='note']")!.GetAttribute("data-origin")).IsEqualTo("Omitted");
        await Assert.That(review.QuerySelector("[data-review-path='secret']")!.GetAttribute("data-origin")).IsEqualTo("Explicit");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
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
        using var context = BrowserFixture.Context();
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
