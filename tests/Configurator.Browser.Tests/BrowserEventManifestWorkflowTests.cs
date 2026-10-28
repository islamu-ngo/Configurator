namespace Configurator.Browser.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public sealed class BrowserEventManifestWorkflowTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task CompleteEventManifestContainsExactlyTypedTenantRowsAndNoInactiveArtifacts(int count)
    {
        var definition = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(Guid.Parse("c0d02619-0134-411b-b495-ef302f592669"));
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        await BrowserFixture.NotifyChoiceAsync(component, "operation", "configuration-manifest");
        await component.Find("[data-field='configuration-manifest.metadata.name'] input").InputAsync(new ChangeEventArgs { Value = "qa-config" });
        await component.Find("[data-field='configuration-manifest.spec.instance.settings'] > fieldset > button").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-field='configuration-manifest.spec.instance.settings.modules.tech_enabled'] input").GetAttribute("type")).IsEqualTo("password");
        await component.Find("[data-field='configuration-manifest.spec.instance.settings.modules.tech_enabled'] input").InputAsync(new ChangeEventArgs { Value = "true" });
        var tenants = new JsonArray();
        if (count == 0) await component.Find("[data-field='configuration-manifest.tenants'] > fieldset > button[data-action='supply-list']").ClickAsync(new MouseEventArgs());
        for (int index = 0; index < count; index++)
        {
            await component.Find("[data-field='configuration-manifest.tenants'] > fieldset > button[data-action='add-item']").ClickAsync(new MouseEventArgs());
            await component.Find($"[data-field='configuration-manifest.tenants.{index}.metadata.name'] input").InputAsync(new ChangeEventArgs { Value = "tenant-" + index });
            await component.Find($"[data-field='configuration-manifest.tenants.{index}.spec.displayName'] input").InputAsync(new ChangeEventArgs { Value = "Tenant " + index });
            foreach (string name in new[] { "settings", "documents", "legalDocuments" })
                await component.Find($"[data-field='configuration-manifest.tenants.{index}.spec.{name}'] > fieldset > button[data-action='supply-object']").ClickAsync(new MouseEventArgs());
            tenants.Add(new JsonObject
            {
                ["metadata"] = new JsonObject { ["name"] = "tenant-" + index },
                ["spec"] = new JsonObject
                {
                    ["displayName"] = "Tenant " + index,
                    ["settings"] = new JsonObject(),
                    ["documents"] = new JsonObject(),
                    ["legalDocuments"] = new JsonObject()
                }
            });
        }
        await component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find(".configuration-editor__artifact h3").TextContent).IsEqualTo("configuration-manifest.json");
        await component.Find(".configuration-editor__artifact button").ClickAsync(new MouseEventArgs());
        byte[] bytes = (byte[])module.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;
        using JsonDocument actual = JsonDocument.Parse(bytes);
        JsonNode expected = JsonNode.Parse("""
            {"$schema":"https://schemas.islamu.org/event/configuration-manifest/v1alpha2/schema.json",
             "apiVersion":"configuration.islamu.org/v1alpha2","kind":"ConfigurationManifest","metadata":{"name":"qa-config"},
             "spec":{"instance":{"settings":{"modules.tech_enabled":true},"documents":{},"legalDocuments":{}},"tenants":[]}}
            """)!;
        expected["spec"]!["tenants"] = tenants;
        await Assert.That(JsonElement.DeepEquals(actual.RootElement, JsonSerializer.SerializeToElement(expected))).IsTrue();
    }
}
