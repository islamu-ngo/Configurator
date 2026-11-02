namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserOutputSelectionTests
{
    [Test]
    public async Task ReuseFollowsOutputMutationsButPreservesExplicitFormatDuringOrdinaryEdits()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition("file.kind")));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await Assert.That(component.Find("#import-artifact").GetAttribute("value")).IsEqualTo("settings");
        await component.InvokeAsync(() => form.SelectOutput("identity"));
        await Assert.That(component.Find("#import-artifact").GetAttribute("value")).IsEqualTo("identity");
        await component.Find("#import-artifact").ChangeAsync(new ChangeEventArgs { Value = "settings" });
        await component.InvokeAsync(() => form.EditValue(["hostname"], "synthetic.test"));
        await Assert.That(component.Find("#import-artifact").GetAttribute("value")).IsEqualTo("settings");
        await component.Find("#import-purpose").ChangeAsync(new ChangeEventArgs { Value = "compare" });
        await component.InvokeAsync(() => component.Instance.DiscardAsync());
        await component.InvokeAsync(() => component.Instance.BeginAsync());
        await Assert.That(component.Find("#import-purpose").GetAttribute("value")).IsEqualTo("fill");
        await Assert.That(component.Find("#import-artifact").GetAttribute("value")).IsEqualTo("settings");
    }

    [Test]
    [Arguments("deployment/target")]
    [Arguments("file.kind")]
    public async Task ImplicitEntryPreservesCoreInitializationWithoutSupplyingAnAnswer(string fieldName)
    {
        Definition definition = Definition(fieldName);
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([definition]));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{definition.FamilyId:D}");
        var component = context.Render<App>();
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.FromSegments([fieldName]), out JsonElement value)).IsTrue();
        await Assert.That(value.GetString()).IsEqualTo("settings");
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(form.InputRevision).IsEqualTo(0L);
    }

    [Test]
    [Arguments("deployment/target")]
    [Arguments("file.kind")]
    public async Task ExplicitEntrySuppliesItsBoundValueOnceAndNeverOverridesLaterEdits(string fieldName)
    {
        Definition definition = Definition(fieldName);
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([definition]));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{definition.FamilyId:D}?entry=identity");
        var component = context.Render<App>();
        var editor = component.FindComponent<ConfigurationEditor>();
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.FromSegments([fieldName]), out JsonElement value)).IsTrue();
        await Assert.That(value.GetString()).IsEqualTo("identity");
        await Assert.That(form.Snapshot().Values[fieldName].GetString()).IsEqualTo("identity");
        await Assert.That(form.InputRevision).IsEqualTo(1L);
        await component.InvokeAsync(() => form.SelectOutput("settings"));
        long revision = form.InputRevision;
        await component.InvokeAsync(() => form.SelectOutput("unknown"));
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        editor.Render(parameters => parameters.Add(item => item.Definition, definition));
        await Assert.That(form.Snapshot().Values[fieldName].GetString()).IsEqualTo("settings");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(component.FindComponent<ConfigurationConfigurePanel>().Instance.Form).IsSameReferenceAs(form);
    }

    [Test]
    public async Task EventStartsAtEnvironmentAndBasicsWithoutSelectingAnOutput()
    {
        BrowserDefinitionRegistry registry = BrowserDefinitionRegistry.LoadBundled();
        Definition definition = registry.Current.Resolve(Guid.Parse("ea55cccf-dce5-4f88-8ea1-6dd211714edc"));
        await using var context = BrowserFixture.Context(registry);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/launch/{definition.FamilyId:D}");
        var component = context.Render<App>();
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/operation"), out JsonElement value)).IsTrue();
        await Assert.That(value.GetString()).IsEqualTo("environment");
        await Assert.That(form.InputRevision).IsEqualTo(0L);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
        await Assert.That(component.Find("[data-section-panel]").GetAttribute("data-section-panel")).IsEqualTo("basics");
        await Assert.That(component.FindAll("[data-field='env.API_ENDPOINT']").Count).IsEqualTo(1);
    }

    private static Definition Definition(string fieldName)
    {
        string pointer = DefinitionPath.FromSegments([fieldName]).ToPointer();
        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"d66d02af-6a66-4396-9717-452e8eb9541b","revision":1},
             "project":{"id":"example.output","name":"Output selection"},"applicationVersions":["1.0"],
             "entryPoints":["identity","settings"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
              "properties":{
               "OUTPUT_FIELD":{"type":"string","enum":["identity","settings"],
                 "x-configurator":{"sensitivity":"public","initialValue":"settings"}},
               "hostname":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
             "presentation":{"defaultJourney":"configure",
              "sections":[{"id":"configure","title":"Configure","pointers":["OUTPUT_PATH","/hostname"]}],
              "journeys":[{"id":"configure","title":"Configure","sections":["configure"]}],
              "outputSelector":{"pointer":"OUTPUT_PATH","options":[
               {"value":"settings","title":"Settings file","artifactIds":["settings"]},
               {"value":"identity","title":"Identity file","artifactIds":["identity"]}]}},
             "outputs":{
              "settings.json":{"id":"settings","format":"json","projection":{"op":"object","members":{
                "hostname":{"op":"copy","reference":{"scope":"root","path":"/hostname"}}},
                "when":{"op":"equals","reference":{"scope":"root","path":"OUTPUT_PATH"},"value":"settings"}}},
              "identity.json":{"id":"identity","format":"json","projection":{"op":"object","members":{
                "hostname":{"op":"copy","reference":{"scope":"root","path":"/hostname"}}},
                "when":{"op":"equals","reference":{"scope":"root","path":"OUTPUT_PATH"},"value":"identity"}}}}}
            """.Replace("OUTPUT_FIELD", fieldName, StringComparison.Ordinal)
            .Replace("OUTPUT_PATH", pointer, StringComparison.Ordinal)));
    }
}
