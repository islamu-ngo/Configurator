namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserCollectionWorkflowTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OmittingInvalidObjectClearsItsDraftAndPreservesSibling(bool nested)
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"settings","type":"object","title":"Settings","sensitivity":"public","disclosure":"guided","properties":[
              {"id":"name","type":"string","title":"Name","sensitivity":"public","disclosure":"guided"},
              {"id":"count","type":"integer","title":"Count","sensitivity":"public","disclosure":"guided"},
              {"id":"inner","type":"object","title":"Inner","sensitivity":"public","disclosure":"guided","properties":[
                {"id":"count","type":"integer","title":"Inner count","sensitivity":"public","disclosure":"guided"}]}]}
            """));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition,
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()))));
        component.Find("[data-field='mode'] select").Change("single");
        component.Find("[data-field='hostname'] input").Input("retained.test");
        component.Find("[data-field='settings.name'] input").Input("kept");
        string field = nested ? "settings.inner.count" : "settings.count";
        component.Find($"[data-field='{field}'] input").Input("invalid");
        if (nested) component.Find("[data-field='settings.count'] input").Input("also-invalid");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.HasInputErrors).IsTrue();
        string container = nested ? "settings.inner" : "settings";
        component.Find($"[data-field='{container}'] > [data-action=omit]").Click();
        await Assert.That(component.Find($"[data-field='{field}'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Find($"[data-field='{field}'] > .configuration-field__error").TextContent).IsEqualTo("");
        if (nested)
        {
            await Assert.That(form.HasInputErrors).IsTrue();
            component.Find("[data-field='settings.count'] input").Input("2");
            await Assert.That(form.Snapshot().Values["settings"].GetProperty("name").GetString()).IsEqualTo("kept");
        }
        else await Assert.That(form.Snapshot().Values.ContainsKey("settings")).IsFalse();
        await Assert.That(form.HasInputErrors).IsFalse();
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("retained.test");
    }

    [Test]
    public async Task EmptyStringRowAndContainerOmissionPreserveSiblingAnswers()
    {
        JsonNode document = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        document["fields"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"tags","type":"list","title":"Tags","itemType":"string","sensitivity":"public","disclosure":"guided"}
            """));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition,
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()))));
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "example.test" });
        await component.Find("[data-field='tags'] [data-action='add-item']").ClickAsync(new MouseEventArgs());
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["tags"].GetRawText()).IsEqualTo("""[""]""");
        await component.Find("[data-field='tags.0'] input").InputAsync(new ChangeEventArgs { Value = "erase" });
        await component.Find("[data-field='tags.0'] input").InputAsync(new ChangeEventArgs { Value = "" });
        await Assert.That(form.Snapshot().Values["tags"].GetRawText()).IsEqualTo("""[""]""");
        await component.Find("[data-field='tags'] > [data-action='omit']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.Snapshot().Values.ContainsKey("tags")).IsFalse();
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("example.test");
    }

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
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        string rowsSection = form.Sections.Single(section => section.Fields.Contains("tenants")).Id;
        string generalSection = form.Sections.Single(section => section.Fields.Contains("hostname")).Id;
        component.Find("[data-field='hostname'] input").Input("example.test");
        component.Find($"[data-section='{rowsSection}']").Click();
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
        component.Find($"[data-section='{generalSection}']").Click();
        await Assert.That(component.FindAll($"[data-section-panel='{rowsSection}']")).IsEmpty();
        component.Find("[data-correction='tenants']").Click();
        await Assert.That(component.FindAll($"[data-section-panel='{rowsSection}']").Count).IsEqualTo(1);
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
        component.Find("[data-field='mode'] select").Change("cluster");
        component.Find("[data-field='replicas'] select").Change("true");
        component.Find("[data-field='hostname'] input").Input("example.test");
        component.Find("[data-field='count'] input").Input("7");
        component.Find("[data-field='rate'] input").Input("1.25");
        await Assert.That(component.Find("[data-field='count'] input").GetAttribute("type")).IsEqualTo("text");
        await Assert.That(component.Find("[data-field='count'] input").GetAttribute("inputmode")).IsEqualTo("numeric");
        await Assert.That(component.Find("[data-field='rate'] input").GetAttribute("type")).IsEqualTo("text");
        await Assert.That(component.Find("[data-field='rate'] input").GetAttribute("inputmode")).IsEqualTo("decimal");
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
