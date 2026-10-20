namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserConfigurationDiscoveryTests
{
    [Test]
    public async Task DiscoveryPreservesPreparedOutputAndAnswerRevision()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.EditValue(["host.name"], "example.test"));
        await component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(1);
        long revision = form.InputRevision;
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "host" });
        await component.Find("[data-facet='Sensitivity'] [data-option='public'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(1);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task ExactBindingSearchNavigatesLiteralFieldWithoutEditingAnswers()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "HOST_NAME" });
        await Assert.That(component.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
        await Assert.That(component.Find("[data-discovery-result]").GetAttribute("data-discovery-result")).IsEqualTo("host.name");
        await Assert.That(component.Find("[data-discovery-result]").GetAttribute("data-match")).IsEqualTo("binding-key");
        await component.Find("[data-discovery-result] button").ClickAsync(new MouseEventArgs());
        await Assert.That(form.DiscoveryQuery).IsEqualTo("");
        await Assert.That(form.IsEditorVisible(["host.name"])).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task FacetsCombineOrWithinAndAcrossWithIndependentResetsAndCounts()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await Assert.That(component.Find("[data-facet='Sensitivity'] [data-option='secret']").GetAttribute("data-count")).IsEqualTo("1");
        await component.Find("[data-facet='Sensitivity'] [data-option='secret'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
        await component.Find("[data-facet='Sensitivity'] [data-option='public'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.FindAll("[data-discovery-result]").Count).IsEqualTo(4);
        await component.Find("[data-facet='Requirement'] [data-option='required'] input").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "unmatched" });
        await Assert.That(component.FindAll("[data-discovery-result]")).IsEmpty();
        await component.Find("[data-action='clear-discovery-search']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "server address" });
        await component.Find("[data-action='clear-discovery-filters']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("#configuration-search").GetAttribute("value")).IsEqualTo("server address");
        await Assert.That(component.FindAll("[data-discovery-result]").Count).IsEqualTo(1);
    }

    [Test]
    public async Task InactiveResultRoutesToControllerWithoutActivatingFeatureOrConsent()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "port" });
        await Assert.That(component.FindAll("[data-discovery-result]")).IsEmpty();
        await component.Find("#discovery-include-inactive").ChangeAsync(new ChangeEventArgs { Value = true });
        await Assert.That(component.Find("[data-discovery-result='port']").GetAttribute("data-active")).IsEqualTo("false");
        await component.Find("[data-discovery-result='port'] button").ClickAsync(new MouseEventArgs());
        await Assert.That(form.IsEditorVisible(["enabled"])).IsTrue();
        await Assert.That(form.Validate().Fields.Single(field => field.Id == "port").IsActive).IsFalse();
        await Assert.That(form.Snapshot().Values["enabled"].GetBoolean()).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
    }

    [Test]
    public async Task SearchNeverIndexesPrivateAnswersAndCorrectionClearsObstructingSelections()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        await Assert.That(component.Find("#configuration-search").GetAttribute("spellcheck")).IsEqualTo("false");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() =>
        {
            form.SetSecretEntryEnabled(true);
            form.EditValue(["token"], "synthetic-discovery-private-value");
        });
        long revision = form.InputRevision;
        await component.Find("#configuration-search").InputAsync(new ChangeEventArgs { Value = "synthetic-discovery-private-value" });
        await Assert.That(component.FindAll("[data-discovery-result]")).IsEmpty();
        await component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());
        await component.Find("[data-correction='host.name']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.DiscoveryQuery).IsEqualTo("");
        await Assert.That(form.DiscoverySelection.Facets.Count).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Arguments.Any(argument =>
            argument?.ToString()?.Contains("synthetic-discovery-private-value", StringComparison.Ordinal) == true))).IsFalse();
    }

    private static SetupDefinition Definition()
    {
        JsonObject source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!.AsObject();
        source["fields"] = JsonNode.Parse("""
        [{"id":"enabled","type":"boolean","title":"Enable relay","default":false,"sensitivity":"public","disclosure":"essential"},
         {"id":"host.name","type":"string","title":"Service host","required":true,"sensitivity":"public","disclosure":"guided",
          "guidance":{"searchTerms":["server address"]}},
         {"id":"note","type":"string","title":"Operator note","sensitivity":"public","disclosure":"advanced"},
         {"id":"token","type":"string","title":"Issuer token","sensitivity":"secret","disclosure":"advanced"},
         {"id":"port","type":"integer","title":"Relay port","sensitivity":"public","disclosure":"advanced",
          "activeWhen":{"op":"equals","field":"enabled","value":true}}]
        """);
        source.Remove("presentation");
        source["artifacts"] = JsonNode.Parse("""
        [{"id":"config","format":"json","fileName":"service.json","applySemantics":"create","restart":"process",
          "projection":{"op":"object","members":{"HOST_NAME":{"op":"copy","field":"host.name"}}}}]
        """);
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
    }
}
