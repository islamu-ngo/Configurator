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
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition,
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()))));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "retained.test" });
        await component.Find("[data-field='settings.name'] input").InputAsync(new ChangeEventArgs { Value = "kept" });
        string field = nested ? "settings.inner.count" : "settings.count";
        await component.Find($"[data-field='{field}'] input").InputAsync(new ChangeEventArgs { Value = "invalid" });
        if (nested) await component.Find("[data-field='settings.count'] input").InputAsync(new ChangeEventArgs { Value = "also-invalid" });
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.HasInputErrors).IsTrue();
        string container = nested ? "settings.inner" : "settings";
        await BrowserFixture.OmitAsync(context, component, container);
        await Assert.That(component.Find($"[data-field='{field}'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Find($"[data-field='{field}'] input").GetAttribute("aria-invalid")).IsEqualTo("false");
        if (nested)
        {
            await Assert.That(form.HasInputErrors).IsTrue();
            await component.Find("[data-field='settings.count'] input").InputAsync(new ChangeEventArgs { Value = "2" });
            await Assert.That(form.Snapshot().Values["settings"].GetProperty("name").GetString()).IsEqualTo("kept");
        }
        else await Assert.That(form.Snapshot().Values.ContainsKey("settings")).IsFalse();
        await Assert.That(form.HasInputErrors).IsFalse();
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());
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
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition,
            SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(document.ToJsonString()))));
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "example.test" });
        await component.Find("[data-field='tags'] [data-action='add-item']").ClickAsync(new MouseEventArgs());
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values["tags"].GetRawText()).IsEqualTo("""[""]""");
        await component.Find("[data-field='tags.0'] input").InputAsync(new ChangeEventArgs { Value = "erase" });
        await component.Find("[data-field='tags.0'] input").InputAsync(new ChangeEventArgs { Value = "" });
        await Assert.That(form.Snapshot().Values["tags"].GetRawText()).IsEqualTo("""[""]""");
        await BrowserFixture.OmitAsync(context, component, "tags");
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
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        string rowsSection = form.Sections.Single(section => section.Fields.Contains("tenants")).Id;
        string generalSection = form.Sections.Single(section => section.Fields.Contains("hostname")).Id;
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "example.test" });
        await component.Find($"[data-section='{rowsSection}']").ClickAsync(new MouseEventArgs());
        string[] names = ["alpha", "beta", "alpha"];
        for (int index = 0; index < names.Length; index++)
        {
            await component.Find("[data-field='tenants'] > fieldset > button[data-action='add-item']").ClickAsync(new MouseEventArgs());
            await component.Find($"[data-field='tenants.{index}.name'] input").InputAsync(new ChangeEventArgs { Value = names[index] });
        }
        await component.Find("#prepare-draft").ClickAsync(new MouseEventArgs());

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
        await component.Find($"[data-section='{generalSection}']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll($"[data-section-panel='{rowsSection}']")).IsEmpty();
        await component.Find("[data-correction='tenants']").ClickAsync(new MouseEventArgs());
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
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        for (int index = 0; index < 3; index++)
        {
            await component.Find("[data-field='tenants'] > fieldset > button[data-action='add-item']").ClickAsync(new MouseEventArgs());
            await component.Find($"[data-field='tenants.{index}.name'] input").InputAsync(new ChangeEventArgs { Value = "tenant-" + index });
        }
        string lastIdentity = component.Find("[data-field='tenants.2.name'] input").Id!;
        await Assert.That(component.Find("[data-field='tenants'] > fieldset > button[data-action='add-item']").HasAttribute("disabled")).IsTrue();
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='tenants.1.credential'] input").InputAsync(new ChangeEventArgs { Value = "removed-row-secret-canary" });
        await component.FindAll("[data-field='tenants'] > fieldset > .configuration-field__row > button")[1].ClickAsync(new MouseEventArgs());
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
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, admitted));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "cluster");
        await BrowserFixture.NotifyChoiceAsync(component, "replicas", "true");
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "example.test" });
        await component.Find("[data-field='count'] input").InputAsync(new ChangeEventArgs { Value = "7" });
        await component.Find("[data-field='rate'] input").InputAsync(new ChangeEventArgs { Value = "1.25" });
        await Assert.That(component.Find("[data-field='count'] input").GetAttribute("type")).IsEqualTo("text");
        await Assert.That(component.Find("[data-field='count'] input").GetAttribute("inputmode")).IsEqualTo("numeric");
        await Assert.That(component.Find("[data-field='rate'] input").GetAttribute("type")).IsEqualTo("text");
        await Assert.That(component.Find("[data-field='rate'] input").GetAttribute("inputmode")).IsEqualTo("decimal");
        await component.Find("[data-field='tags'] > fieldset > button[data-action='add-item']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-field='tags.0'] input").InputAsync(new ChangeEventArgs { Value = "blue" });
        await Assert.That(component.Find("[data-field='tags.0'] input").GetAttribute("value")).IsEqualTo("blue");
        await Assert.That(component.Find("[data-field='settings.enabled'] input").GetAttribute("type")).IsEqualTo("password");
        await component.Find("[data-field='settings.enabled'] input").InputAsync(new ChangeEventArgs { Value = "false" });
        var values = component.FindComponents<ConfigurationField>()[0].Instance.Form.Snapshot().Values;
        await Assert.That(values["count"].GetInt64()).IsEqualTo(7);
        await Assert.That(values["rate"].GetDecimal()).IsEqualTo(1.25m);
        await Assert.That(values["tags"][0].GetString()).IsEqualTo("blue");
        await Assert.That(values["settings"].GetProperty("enabled").GetBoolean()).IsFalse();
        await Assert.That(component.FindAll("textarea")).IsEmpty();
    }
}
