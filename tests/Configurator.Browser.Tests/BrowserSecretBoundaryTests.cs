namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;

public sealed class BrowserSecretBoundaryTests
{
    [Test]
    public async Task NestedSecretDenialKeepsOrdinarySiblingAndStartsReplacementWithoutConsent()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"profile","type":"object","title":"Profile","sensitivity":"public","disclosure":"guided","properties":[
              {"id":"name","type":"string","title":"Name","sensitivity":"public","disclosure":"guided"},
              {"id":"token","type":"string","title":"Token","sensitivity":"secret","disclosure":"guided"}]}
            """));
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("[data-field='profile.name'] input").Input("ordinary-sibling");
        await Assert.That(component.FindAll("[data-field='profile.token'] input")).IsEmpty();
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='profile.token'] input").Input("nested-revoked-canary");
        component.Find("#secret-consent").Change(false);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["profile"].GetRawText()).IsEqualTo("{\"name\":\"ordinary-sibling\"}");
        await Assert.That(component.FindAll("[data-field='profile.token'] input")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("nested-revoked-canary");
        component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2)));
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
    }

    [Test]
    public async Task SecretEntryDefaultsOffAndConsentRevocationRetainsOrdinarySettings()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        component.Find("[data-field='hostname'] input").Input("example.test");
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='credential'] input").Input("synthetic-private-canary");
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("type")).IsEqualTo("password");
        component.Find("#secret-consent").Change(false);
        await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("example.test");
        await Assert.That(component.Markup).DoesNotContain("synthetic-private-canary");
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Arguments.Any(arg => arg?.ToString()?.Contains("synthetic-private-canary", StringComparison.Ordinal) == true))).IsFalse();
    }

    [Test]
    public async Task SecretBearingImportRejectsBeforeConsentWithoutEchoingValues()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"mode":"single","hostname":"example.test","credential":"synthetic-private-canary"}"""));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("import-secret-consent-required");
        await Assert.That(component.Markup).DoesNotContain("synthetic-private-canary");
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("");
    }

    [Test]
    [Arguments("""{"unknown":"private-canary"}""")]
    [Arguments("""{"mode":"private-canary"}""")]
    [Arguments("""{"mode":{"secret":"private-canary"}}""")]
    [Arguments("""{"mode":"single","mode":"cluster"}""")]
    [Arguments("""{"mode":"single","replicas":true}""")]
    [Arguments("""{"mode":"cluster","replicas":"private-canary"}""")]
    [Arguments("""{"mode":"cluster","replicas":null}""")]
    public async Task UnknownDuplicateInactiveAndWrongTypedImportedValuesNeverPublishAnswersOrOutput(string text)
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsNotEqualTo("imported");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("private-canary");
        await Assert.That(component.Find("[data-field='mode'] select").GetAttribute("value")).IsEqualTo("");
    }

    [Test]
    public async Task SecretContainerMasksEveryDescendantScalarAfterConsent()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        definition["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"private","type":"object","title":"Private settings","sensitivity":"secret","disclosure":"guided","properties":[
              {"id":"enabled","type":"boolean","title":"Enabled","sensitivity":"public","disclosure":"guided"},
              {"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"guided"},
              {"id":"mode","type":"enum","title":"Mode","sensitivity":"public","disclosure":"guided","choices":["single","cluster"]}]}
            """));
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        await Assert.That(component.FindAll("[data-field='private'] input")).IsEmpty();
        component.Find("#secret-consent").Change(true);
        await Assert.That(component.FindAll("[data-field='private'] input")).Count().IsEqualTo(3);
        await Assert.That(component.FindAll("[data-field='private'] input").All(input => input.GetAttribute("type") == "password")).IsTrue();
        await Assert.That(component.FindAll("[data-field='private'] select")).IsEmpty();
    }
}
