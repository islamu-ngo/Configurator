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
        component.Find("[data-section='credentials']").Click();
        component.Find("#choice-credential").Input("synthetic-navigation-secret");
        component.Find("[data-section='connection']").Click();
        await Assert.That(component.Find("#choice-hostname").GetAttribute("value")).IsEqualTo("example.test");
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsTrue();
        await Assert.That(component.Find("[data-section-panel='credentials']").HasAttribute("hidden")).IsTrue();
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
        await Assert.That(component.Find("[data-section-panel='credentials']").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.Find("#choice-credential").HasAttribute("disabled")).IsTrue();
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
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
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"optional_note","type":"string","title":"Optional note","sensitivity":"public","disclosure":"advanced"}
            """));
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await Assert.That(component.Find("label[for='choice-HTTPServer_URL']").TextContent).Contains("HTTP Server URL");
        await Assert.That(component.Find("[data-field='HTTPServer_URL'] code").TextContent).IsEqualTo("HTTPServer_URL");
        await Assert.That(component.Find("label[for='choice-authored_key']").TextContent).Contains("Publisher's exact wording");
        await Assert.That(component.FindAll("#configuration-experience option")).Count().IsEqualTo(1);
        await Assert.That(component.Find("#configuration-experience option").GetAttribute("value")).IsEqualTo("configure");
        await Assert.That(component.Find("#configuration-experience option").TextContent).IsEqualTo("Configure");
        await Assert.That(component.FindAll("[data-field='optional_note']")).IsEmpty();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SetAllSectionsExpanded(true));
        await Assert.That(component.Find("#choice-optional_note").HasAttribute("disabled")).IsFalse();
        await component.InvokeAsync(() => form.SetAllSectionsExpanded(false));
        await Assert.That(component.Find("[data-field='optional_note']").ParentElement!.HasAttribute("hidden")).IsTrue();
        await Assert.That(component.FindComponents<ConfigurationField>()[0].Instance.Form.Definition.Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    public async Task AuthoredExperienceAndDisclosureChangesPreservePreparedOutput()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, GroupedDefinition()));
        await Assert.That(component.Find("#configuration-experience").GetAttribute("value")).IsEqualTo("quick-start");
        await Assert.That(component.FindAll("#configuration-experience option")).Count().IsEqualTo(2);
        await Assert.That(component.Find("#configuration-experience option[value='quick-start']").TextContent).IsEqualTo("Connect service");
        await Assert.That(component.Find("#configuration-experience option[value='inspect-settings']").TextContent).IsEqualTo("Inspect configuration");
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#secret-consent").Change(true);
        component.Find("[data-section='credentials']").Click();
        component.Find("#choice-credential").Input("synthetic-experience-secret");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        long revision = form.InputRevision;

        component.Find("#configuration-experience").Change("inspect-settings");
        await Assert.That(form.SelectedExperienceId).IsEqualTo("inspect-settings");
        await Assert.That(component.Find("[data-navigation]").GetAttribute("data-navigation")).IsEqualTo("sections");
        await component.InvokeAsync(() => form.SetAllSectionsExpanded(true));
        await Assert.That(component.Find("button[aria-expanded]").GetAttribute("aria-expanded")).IsEqualTo("true");
        component.Find("button[aria-expanded]").Click();
        await Assert.That(form.OptionalEditorsExpanded).IsFalse();
        component.Find("#configuration-experience").Change("quick-start");

        await Assert.That(component.Find("[data-navigation]").GetAttribute("data-navigation")).IsEqualTo("guided");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("preparation-ready");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["credential"].GetString()).IsEqualTo("synthetic-experience-secret");
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsTrue();
    }

    [Test]
    public async Task InvalidNestedAdvancedInputRemainsReachableAndBlocksPreparationAfterSwitchingViews()
    {
        JsonNode document = JsonNode.Parse(GroupedDefinition().CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"options","type":"object","title":"Options","group":"Advanced settings","sensitivity":"public","disclosure":"advanced","properties":[
              {"id":"dotted.count","type":"integer","title":"Count","sensitivity":"public","disclosure":"advanced"}]}
            """));
        document["presentation"]!["sections"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"tuning","title":"Advanced settings","disclosure":"advanced","fields":["options"]}
            """));
        foreach (JsonNode? experience in document["presentation"]!["experiences"]!.AsArray())
            experience!["sections"]!.AsArray().Add("tuning");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        await Assert.That(component.FindAll("[data-section='tuning']")).IsEmpty();
        component.Find("#configuration-experience").Change("inspect-settings");
        component.Find("[data-section='tuning']").Click();
        component.Find("[data-field='options.dotted.count'] input").Input("invalid-number");
        component.Find("[data-section='connection']").Click();
        component.Find("#configuration-experience").Change("quick-start");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("input-type-invalid");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await component.InvokeAsync(() => form.SetDiscovery("hostname"));
        component.Find("[data-correction='options']").Click();
        await Assert.That(component.Find("[data-section-panel='tuning']").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.Find("[data-field='options.dotted.count'] input").GetAttribute("aria-invalid")).IsEqualTo("true");
        await Assert.That(form.InputDiagnostics.Single().RelativePath.SequenceEqual(["dotted.count"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["options", "dotted.count"])).IsTrue();
        await Assert.That(form.DiscoveryQuery).IsEqualTo(string.Empty);
        await Assert.That(component.FindComponents<ConfigurationField>().Single(field => field.Instance.Path.SequenceEqual(["options"]))
            .Instance.FocusPath.SequenceEqual(["options", "dotted.count"])).IsTrue();
        component.Find("[data-field='options.dotted.count'] input").Input("2");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
    }

    private static SetupDefinition GroupedDefinition()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        foreach (JsonNode? field in document["fields"]!.AsArray())
            field!["group"] = field["id"]!.GetValue<string>() == "credential" ? "Security" : "General";
        document["presentation"] = JsonNode.Parse("""
            {"defaultExperience":"quick-start","sections":[
              {"id":"connection","title":"General","fields":["mode","replicas","hostname"]},
              {"id":"credentials","title":"Security","fields":["credential"]}],
             "experiences":[
              {"id":"quick-start","title":"Connect service","initialDisclosure":"guided","navigation":"guided",
               "sections":["connection","credentials"]},
              {"id":"inspect-settings","title":"Inspect configuration","initialDisclosure":"advanced","navigation":"sections",
               "sections":["credentials","connection"]}]}
            """);
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
    }
}
