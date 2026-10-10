namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserTypedPrivacyTests
{
    [Test]
    public async Task LocallyCompiledNestedSecretsRequireConsentAndRevocationClearsOutput()
    {
        using var context = BrowserFixture.Context();
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(new BrowserDefinitionRegistry([]), javascript, policy);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(BrowserDefinitionSourceTests.AuthoringJson));
        SetupDefinition definition = await source.ReadLocalAsync(input);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("[data-field='HOST'] input").Input("example.test");
        await Assert.That(component.Find("[data-field='settings.secret'] input").HasAttribute("disabled")).IsTrue();
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='settings.secret'] input").Input("synthetic-local-nested-secret");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        component.Find("#secret-consent").Change(false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["settings"].TryGetProperty("secret", out _)).IsFalse();
        await Assert.That(component.Markup).DoesNotContain("synthetic-local-nested-secret");
        await Assert.That(context.JSInterop.Invocations.Any(invocation => invocation.Identifier is "fetchDefinition" or "retainDefinition")).IsFalse();
    }

    [Test]
    public async Task SensitiveChildrenAreMaskedButEditableWithoutSecretConsent()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("""
            {"id":"settings","type":"object","title":"Settings","sensitivity":"sensitive","disclosure":"guided","properties":[
              {"id":"ordinary","type":"string","title":"Private setting","sensitivity":"public","disclosure":"guided"},
              {"id":"secret","type":"string","title":"Secret setting","sensitivity":"secret","disclosure":"guided"}]}
            """)));
        var ordinary = component.Find("[data-field='settings.ordinary'] input");
        await Assert.That(ordinary.GetAttribute("type")).IsEqualTo("password");
        await Assert.That(ordinary.HasAttribute("disabled")).IsFalse();
        ordinary.Input("synthetic-private-value");
        await Assert.That(component.Find("[data-field='settings.secret'] input").HasAttribute("disabled")).IsTrue();
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='settings.secret'] input").Input("synthetic-nested-secret");
        component.Find("#secret-consent").Change(false);
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["settings"].GetProperty("ordinary").GetString()).IsEqualTo("synthetic-private-value");
        await Assert.That(form.Snapshot().Values["settings"].TryGetProperty("secret", out _)).IsFalse();
        await Assert.That(component.Markup).DoesNotContain("synthetic-nested-secret");
    }

    [Test]
    public async Task TypedFalseAndExplicitEmptyCollectionsRemainDistinctFromAbsence()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, WithField("""
            {"id":"settings","type":"object","title":"Settings","sensitivity":"public","disclosure":"guided","properties":[
              {"id":"enabled","type":"boolean","title":"Enabled","sensitivity":"public","disclosure":"guided"},
              {"id":"names","type":"list","title":"Names","sensitivity":"public","disclosure":"guided","itemType":"string","maxItems":3}]}
            """)));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values.ContainsKey("settings")).IsFalse();
        component.Find("[data-field='settings.enabled'] select").Change("false");
        await Assert.That(form.Snapshot().Values["settings"].GetProperty("enabled").GetBoolean()).IsFalse();
        component.Find("[data-field='settings.enabled'] select").Change("");
        await Assert.That(form.Snapshot().Values["settings"].EnumerateObject().Count()).IsEqualTo(0);
        component.Find("[data-field='settings.names'] [data-action='supply-list']").Click();
        JsonElement settings = form.Snapshot().Values["settings"];
        await Assert.That(settings.GetProperty("names").ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(settings.GetProperty("names").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task SuppliedOptionalCompoundWithRequiredSecretOffersDraftOrConsentRecovery()
    {
        JsonNode document = JsonNode.Parse(WithField("""
            {"id":"settings","type":"object","title":"Settings","sensitivity":"public","disclosure":"guided","properties":[
              {"id":"ordinary","type":"string","title":"Ordinary setting","sensitivity":"public","disclosure":"guided"},
              {"id":"secret","type":"string","title":"Required secret","sensitivity":"secret","disclosure":"guided","required":true}]}
            """).CanonicalBytes.Span)!;
        var fields = document["fields"]!.AsArray();
        fields.Remove(fields.Single(item => item!["id"]!.GetValue<string>() == "credential"));
        var members = document["artifacts"]![0]!["projection"]!["members"]!.AsObject();
        members.Remove("credential");
        members["settings"] = JsonNode.Parse("""{"op":"copy","field":"settings"}""");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("[data-field='settings.ordinary'] input").Input("synthetic-ordinary-setting");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.FindAll("[data-action='enable-secret-entry']")).Count().IsEqualTo(1);
        component.Find("[data-action='enable-secret-entry']").Click();
        component.Find("[data-field='settings.secret'] input").Input("synthetic-compound-secret");
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find(".configuration-editor__artifact").GetAttribute("data-readiness")).IsEqualTo("complete");
        component.Find("#secret-consent").Change(false);
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("synthetic-compound-secret");
    }

    private static SetupDefinition WithField(string field)
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse(field));
        return SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()));
    }
}
