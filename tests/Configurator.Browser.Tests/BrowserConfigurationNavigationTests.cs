namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;

public sealed class BrowserConfigurationNavigationTests
{
    [Test]
    public async Task SectionSwitchingPreservesConsentAnswersAndPinnedIdentity()
    {
        SetupDefinition definition = GroupedDefinition();
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#secret-consent").Change(true);
        component.Find("[data-section='Security']").Click();
        component.Find("#choice-credential").Input("synthetic-navigation-secret");
        component.Find("[data-section='General']").Click();
        await Assert.That(component.Find("#choice-hostname").GetAttribute("value")).IsEqualTo("example.test");
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsTrue();
        await Assert.That(component.Find("[data-section-panel='Security']").HasAttribute("hidden")).IsTrue();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo("synthetic-navigation-secret");
        await Assert.That(form.Definition.Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    public async Task HiddenCorrectionOpensItsSectionAndDisabledSecretOffersConsentRecovery()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, GroupedDefinition()));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        component.Find("[data-correction='credential']").Click();
        await Assert.That(component.Find("[data-section-panel='Security']").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.Find("#choice-credential").HasAttribute("disabled")).IsTrue();
        await Assert.That(component.FindAll("[data-action='enable-secret-entry']")).Count().IsEqualTo(1);
        component.Find("[data-action='enable-secret-entry']").Click();
        await Assert.That(component.Find("#choice-credential").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task MechanicalFallbackPreservesAuthoredTitlesAndExactKeys()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"HTTPServer_URL","type":"string","title":"HTTPServer_URL","sensitivity":"public","disclosure":"guided"}
            """));
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"authored_key","type":"string","title":"Publisher's exact wording","sensitivity":"public","disclosure":"guided"}
            """));
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await Assert.That(component.Find("label[for='choice-HTTPServer_URL']").TextContent).Contains("HTTP Server URL");
        await Assert.That(component.Find("[data-field='HTTPServer_URL'] code").TextContent).IsEqualTo("HTTPServer_URL");
        await Assert.That(component.Find("label[for='choice-authored_key']").TextContent).Contains("Publisher's exact wording");
        await Assert.That(component.FindComponents<ConfigurationField>()[0].Instance.Form.Definition.Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    public async Task InvalidNestedAdvancedInputRemainsReachableAndBlocksPreparationAfterSwitchingViews()
    {
        JsonNode document = JsonNode.Parse(GroupedDefinition().CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"options","type":"object","title":"Options","group":"Advanced settings","sensitivity":"public","disclosure":"advanced","properties":[
              {"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"advanced"}]}
            """));
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#display-mode").Change("Advanced");
        component.Find("[data-section='Advanced settings']").Click();
        component.Find("[data-field='options.count'] input").Input("invalid-number");
        component.Find("[data-section='General']").Click();
        component.Find("#display-mode").Change("Guided");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("input-type-invalid");
        component.Find("[data-correction='options']").Click();
        await Assert.That(component.Find("[data-section-panel='Advanced settings']").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.Find("[data-field='options.count'] input").GetAttribute("aria-invalid")).IsEqualTo("true");
        component.Find("[data-field='options.count'] input").Input("2");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
    }

    private static SetupDefinition GroupedDefinition()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        foreach (JsonNode? field in document["fields"]!.AsArray())
            field!["group"] = field["id"]!.GetValue<string>() == "credential" ? "Security" : "General";
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
    }
}
