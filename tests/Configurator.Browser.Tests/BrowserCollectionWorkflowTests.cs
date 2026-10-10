namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;

public sealed class BrowserCollectionWorkflowTests
{
    [Test]
    public async Task DuplicateRowDiagnosticConnectsOnlyAffectedRowAndCollectionControls()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        definition["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"tenants","type":"list","title":"Tenants","group":"Rows","sensitivity":"public","disclosure":"guided","itemType":"object","maxItems":3,
             "uniqueBy":["name"],"properties":[
              {"id":"name","type":"string","title":"Tenant name","sensitivity":"public","disclosure":"guided","required":true}]}
            """));
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        component.Find("#choice-hostname").Input("example.test");
        component.Find("[data-section='Rows']").Click();
        string[] names = ["alpha", "beta", "alpha"];
        for (int index = 0; index < names.Length; index++)
        {
            component.Find("[data-field='tenants'] > fieldset > button[data-action='add-item']").Click();
            component.Find($"[data-field='tenants.{index}.name'] input").Input(names[index]);
        }
        component.Find("#prepare-draft").Click();

        foreach (string selector in new[]
        {
            "[data-field='tenants'] > fieldset",
            "[data-field='tenants.2'] > fieldset",
            "[data-field='tenants.2.name'] input"
        })
        {
            var control = component.Find(selector);
            await Assert.That(control.GetAttribute("aria-invalid")).IsEqualTo("true");
            string errorId = control.GetAttribute("aria-describedby")!.Split(' ').Single(id => id.EndsWith("-error", StringComparison.Ordinal));
            await Assert.That(component.Find("#" + errorId).TextContent).Contains("value-duplicate");
        }
        for (int index = 0; index < 2; index++)
        {
            await Assert.That(component.Find($"[data-field='tenants.{index}'] > fieldset").GetAttribute("aria-invalid")).IsEqualTo("false");
            await Assert.That(component.Find($"[data-field='tenants.{index}.name'] input").GetAttribute("aria-invalid")).IsEqualTo("false");
        }
        await Assert.That(component.FindAll(".configuration-output")).IsEmpty();
        component.Find("[data-section='Other']").Click();
        await Assert.That(component.Find("[data-section-panel='Rows']").HasAttribute("hidden")).IsTrue();
        component.Find("[data-correction='tenants/2']").Click();
        await Assert.That(component.Find("[data-section-panel='Rows']").HasAttribute("hidden")).IsFalse();
        await Assert.That(component.Find("[data-field='tenants.2.name'] input").GetAttribute("value")).IsEqualTo("alpha");
    }

    [Test]
    public async Task TypedObjectRowsPreserveSurvivorIdentityAndRemovePrivateValues()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        definition["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"tenants","type":"list","title":"Tenants","sensitivity":"sensitive","disclosure":"guided","itemType":"object","maxItems":3,
             "uniqueBy":["name"],"properties":[
              {"id":"name","type":"string","title":"Tenant name","sensitivity":"sensitive","disclosure":"guided","required":true},
              {"id":"credential","type":"string","title":"Tenant secret","sensitivity":"secret","disclosure":"guided"}]}
            """));
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        for (int index = 0; index < 3; index++)
        {
            component.Find("[data-field='tenants'] > fieldset > button[data-action='add-item']").Click();
            component.Find($"[data-field='tenants.{index}.name'] input").Input("tenant-" + index);
        }
        string lastIdentity = component.Find("[data-field='tenants.2.name'] input").Id!;
        await Assert.That(component.Find("[data-field='tenants'] > fieldset > button[data-action='add-item']").HasAttribute("disabled")).IsTrue();
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='tenants.1.credential'] input").Input("removed-row-secret-canary");
        component.FindAll("[data-field='tenants'] > fieldset > .configuration-field__row > button")[1].Click();
        await Assert.That(component.Find("[data-field='tenants.1.name'] input").Id).IsEqualTo(lastIdentity);
        await Assert.That(component.Find("[data-field='tenants.1.name'] input").GetAttribute("value")).IsEqualTo("tenant-2");
        await Assert.That(component.Markup).DoesNotContain("removed-row-secret-canary");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        JsonElement rows = form.Snapshot().Values["tenants"];
        await Assert.That(rows.GetArrayLength()).IsEqualTo(2);
        await Assert.That(rows[1].GetProperty("name").GetString()).IsEqualTo("tenant-2");
        await Assert.That(rows.GetRawText()).DoesNotContain("removed-row-secret-canary");
    }

    [Test]
    public async Task SevenFieldTypesUseTypedControlsWithoutCompoundJsonTextareas()
    {
        JsonNode definition = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        foreach (string field in new[]
        {
            """{"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"guided"}""",
            """{"id":"rate","type":"number","title":"Rate","sensitivity":"public","disclosure":"guided"}""",
            """{"id":"tags","type":"list","title":"Tags","sensitivity":"public","disclosure":"guided","itemType":"string","maxItems":3}""",
            """{"id":"settings","type":"object","title":"Settings","sensitivity":"sensitive","disclosure":"guided","properties":[{"id":"enabled","type":"boolean","title":"Enabled","sensitivity":"public","disclosure":"guided"}]}"""
        }) definition["fields"]!.AsArray().Add(JsonNode.Parse(field));
        SetupDefinition admitted = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(definition.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        component.Find("#choice-mode").Change("cluster");
        component.Find("#choice-replicas").Change("true");
        component.Find("#choice-hostname").Input("example.test");
        component.Find("#choice-count").Input("7");
        component.Find("#choice-rate").Input("1.25");
        component.Find("[data-field='tags'] > fieldset > button[data-action='add-item']").Click();
        component.Find("[data-field='tags.0'] input").Input("blue");
        await Assert.That(component.Find("[data-field='settings.enabled'] input").GetAttribute("type")).IsEqualTo("password");
        component.Find("[data-field='settings.enabled'] input").Input("false");
        var values = component.FindComponents<ConfigurationField>()[0].Instance.Form.Snapshot().Values;
        await Assert.That(values["count"].GetInt64()).IsEqualTo(7);
        await Assert.That(values["rate"].GetDecimal()).IsEqualTo(1.25m);
        await Assert.That(values["tags"][0].GetString()).IsEqualTo("blue");
        await Assert.That(values["settings"].GetProperty("enabled").GetBoolean()).IsFalse();
        await Assert.That(component.FindAll("textarea")).IsEmpty();
    }
}
