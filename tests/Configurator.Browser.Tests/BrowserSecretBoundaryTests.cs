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
        BrowserFixture.WithField(source, "profile", """
            {"type":"object","title":"Profile","additionalProperties":false,"x-configurator":{"sensitivity":"public","detail":"standard"},"properties":{
              "name":{"type":"string","title":"Name","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "token":{"type":"string","title":"Token","x-configurator":{"sensitivity":"secret","detail":"standard"}}}}
            """);
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        await using var context = BrowserFixture.Context();
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
        await using var context = BrowserFixture.Context();
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
        await using var context = BrowserFixture.Context();
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
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsNotEqualTo("imported");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("private-canary");
        var mode = component.FindComponents<ConfigurationField>().Single(item => item.Instance.Path.SequenceEqual(["mode"]));
        await Assert.That(mode.FindComponent<MudBlazor.MudSelect<string>>().Instance.Value).IsEqualTo("");
    }

    [Test]
    public async Task SecretContainerMasksEveryDescendantScalarAfterConsent()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(definition, "private", """
            {"type":"object","title":"Private settings","additionalProperties":false,"x-configurator":{"sensitivity":"secret","detail":"standard"},"properties":{
              "enabled":{"type":"boolean","title":"Enabled","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "count":{"type":"integer","title":"Count","x-configurator":{"sensitivity":"public","detail":"standard"}},
              "mode":{"type":"string","title":"Mode","enum":["single","cluster"],"x-configurator":{"sensitivity":"public","detail":"standard"}}}}
            """, "schema.integer");
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        await Assert.That(component.FindAll("[data-field='private'] input")).IsEmpty();
        component.Find("#secret-consent").Change(true);
        await Assert.That(component.FindAll("[data-field='private'] input")).Count().IsEqualTo(3);
        await Assert.That(component.FindAll("[data-field='private'] input").All(input => input.GetAttribute("type") == "password")).IsTrue();
        await Assert.That(component.FindAll("[data-field='private'] .configuration-field__choice")).IsEmpty();
    }
}
