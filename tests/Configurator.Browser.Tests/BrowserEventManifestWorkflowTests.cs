namespace Configurator.Browser.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;

public sealed class BrowserEventManifestWorkflowTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task CompleteEventManifestContainsExactlyTypedTenantRowsAndNoInactiveArtifacts(int count)
    {
        var definition = BrowserDefinitionRegistry.LoadBundled().Current.Resolve(Guid.Parse("a4bcfaa6-9979-43e5-b1f5-00d552618cdf"));
        using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("#choice-operation").Change("configuration-manifest");
        component.Find("#choice-configuration-manifest\\.f0000").Input("qa-config");
        component.Find("[data-field='configuration-manifest.f0001'] > fieldset > button").Click();
        component.Find("[data-field='configuration-manifest.f0001.modules.tech_enabled'] select").Change("true");
        var tenants = new JsonArray();
        if (count == 0) component.Find("[data-field='configuration-manifest.tenants'] > fieldset > button[data-action='supply-list']").Click();
        for (int index = 0; index < count; index++)
        {
            component.Find("[data-field='configuration-manifest.tenants'] > fieldset > button[data-action='add-item']").Click();
            component.Find($"[data-field='configuration-manifest.tenants.{index}.metadata.name'] input").Input("tenant-" + index);
            component.Find($"[data-field='configuration-manifest.tenants.{index}.spec.displayName'] input").Input("Tenant " + index);
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
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find(".configuration-editor__artifact h3").TextContent).IsEqualTo("configuration-manifest.json");
        component.Find(".configuration-editor__artifact button").Click();
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
