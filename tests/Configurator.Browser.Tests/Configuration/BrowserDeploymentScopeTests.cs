namespace Configurator.Browser.Tests;

using System.Text;
using System.IO.Compression;
using System.Text.Json;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserDeploymentScopeTests
{
    [Test]
    public async Task PreparedBundleDownloadPreservesDeclaredNamesAndRecipientContents()
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("download", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Deployment()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await component.InvokeAsync(() => form.SetSecretEntryEnabled(true));
        await component.InvokeAsync(() => form.EditValue(["worker"], "synthetic-api-admin"));
        await BrowserFixture.PrepareAsync(component, false);
        await component.Find("[data-action='download-all-files']").ClickAsync(new MouseEventArgs());
        byte[] bytes = (byte[])module.Invocations.Single(call => call.Identifier == "open").Arguments[2]!;
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        await Assert.That(string.Join('|', zip.Entries.Select(entry => entry.FullName))).IsEqualTo("dashboard.json|worker.json");
        using Stream workerSource = zip.GetEntry("worker.json")!.Open();
        using Stream dashboardSource = zip.GetEntry("dashboard.json")!.Open();
        using JsonDocument worker = JsonDocument.Parse(workerSource);
        using JsonDocument dashboard = JsonDocument.Parse(dashboardSource);
        await Assert.That(worker.RootElement.GetProperty("VALUE").GetString()).IsEqualTo("synthetic-api-admin");
        await Assert.That(dashboard.RootElement.GetProperty("VALUE").GetString()).IsEqualTo("synthetic-dashboard");
        await Assert.That(dashboard.RootElement.GetRawText()).DoesNotContain("synthetic-api-admin");
    }

    [Test]
    public async Task DeveloperDestinationIsAnExplicitEligibleViewChoice()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Deployment(environment: true)));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await component.Find("[data-editing-mode='developer']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindComponent<ConfigurationDeveloper>().Instance.EnvironmentArtifactId).IsNull();
        long revision = form.InputRevision;
        await component.Find("#developer-destination").ChangeAsync(new ChangeEventArgs { Value = "dashboard" });
        await Assert.That(component.FindComponent<ConfigurationDeveloper>().Instance.EnvironmentArtifactId).IsEqualTo("dashboard");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await component.InvokeAsync(() => form.SelectConfigurationContext("split", "dashboard"));
        await Assert.That(component.FindAll("#developer-destination")).IsEmpty();
        await Assert.That(component.FindComponent<ConfigurationDeveloper>().Instance.EnvironmentArtifactId).IsEqualTo("dashboard");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NarrowingOrRevocationFencesHeldCombinedClipboardExposure(bool revoke)
    {
        await using var context = BrowserFixture.Context();
        var javascript = new BrowserExportLifecycleTests.GatedJavascript(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Deployment()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await component.InvokeAsync(() => form.SetSecretEntryEnabled(true));
        await component.InvokeAsync(() => form.EditValue(["worker"], "synthetic-api-admin"));
        await BrowserFixture.PrepareAsync(component, false);
        Task copy = component.Find("[data-action='copy-all-files']").ClickAsync(new MouseEventArgs());
        await javascript.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await component.InvokeAsync(() =>
        {
            if (revoke) form.SetSecretEntryEnabled(false);
            else form.SelectConfigurationContext("split", "dashboard");
        });
        await using var module = new BrowserExportLifecycleTests.RecordingModule();
        javascript.Release.TrySetResult(module);
        await copy.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(module.PrivateOpeningCount).IsEqualTo(0);
        await Assert.That(component.FindAll("[data-prepared-file]")).IsEmpty();
        if (!revoke)
        {
            await BrowserFixture.PrepareAsync(component, false);
            await component.Find("[data-action='copy-file']").ClickAsync(new MouseEventArgs());
            await Assert.That(Encoding.UTF8.GetString(module.OpeningBytes!)).DoesNotContain("synthetic-api-admin");
            await Assert.That(Encoding.UTF8.GetString(module.OpeningBytes!)).Contains("synthetic-dashboard");
        }
    }

    [Test]
    public async Task ContextControlsPrecedeFieldsAndNarrowDeveloperAndImportDestinations()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Deployment()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await Assert.That(component.FindAll("#configuration-context, [data-section-panel]")[0].Id).IsEqualTo("configuration-context");
        await Assert.That(component.Find("#configuration-recipe").GetAttribute("value")).IsEqualTo("standalone");
        await Assert.That(component.Find("#configuration-scope").GetAttribute("value")).IsEqualTo("all");
        await component.Find("#configuration-recipe").ChangeAsync(new ChangeEventArgs { Value = "split" });
        await component.Find("#configuration-scope").ChangeAsync(new ChangeEventArgs { Value = "dashboard" });
        await Assert.That(form.ConfigurationContext!.ScopeId).IsEqualTo("dashboard");
        await Assert.That(component.FindAll("#import-artifact option[value='worker']")).IsEmpty();
        await Assert.That(component.FindAll("#import-artifact option[value='dashboard']").Count).IsEqualTo(1);
        long revision = form.InputRevision;
        await component.Find("#configuration-detail").ChangeAsync(new ChangeEventArgs { Value = "advanced" });
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.ConfigurationContext.ScopeId).IsEqualTo("dashboard");
        await component.Find("#configuration-recipe").ChangeAsync(new ChangeEventArgs { Value = "standalone" });
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.ConfigurationContext.RecipeId).IsEqualTo("split");
        await Assert.That(form.ConfigurationContext.ScopeId).IsEqualTo("dashboard");
        await Assert.That(component.Find("#configuration-recipe").GetAttribute("value")).IsEqualTo("split");
        await component.Find("[data-context-pair='standalone:all']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.ConfigurationContext.RecipeId).IsEqualTo("standalone");
        await Assert.That(form.ConfigurationContext.ScopeId).IsEqualTo("all");
    }

    internal static Definition Deployment(bool environment = false) => DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"d66d02af-6a66-4396-9717-452e8eb9541b","revision":1},
         "project":{"id":"example.scope","name":"Worker and dashboard"},"applicationVersions":["1.0"],
         "entryPoints":["worker","dashboard"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
          "properties":{
           "recipe":{"type":"string","enum":["standalone","split"],"x-configurator":{"sensitivity":"public","initialValue":"standalone"}},
           "scope":{"type":"string","enum":["all","worker","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
           "worker":{"type":"string","x-configurator":{"sensitivity":"secret"}},
           "dashboard":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"synthetic-dashboard"}}}},
         "deployment":{"recipePointer":"/recipe","scopePointer":"/scope",
          "components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
          "recipes":[{"id":"standalone","title":"Standalone","scopes":[
           {"id":"all","title":"All components","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]}]},
           {"id":"split","title":"Separate processes","scopes":[
            {"id":"all","title":"All components","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
            {"id":"worker","title":"Worker only","componentIds":["worker"],"artifactIds":["worker"]},
            {"id":"dashboard","title":"Dashboard only","componentIds":["dashboard"],"artifactIds":["dashboard"]}]}]},
         "outputs":{
          "worker.json":{"id":"worker","format":"json","consumerIds":["worker"],"projection":{"op":"object","members":{
           "value":{"op":"copy","reference":{"scope":"root","path":"/worker"}}}}},
          "dashboard.json":{"id":"dashboard","format":"json","consumerIds":["dashboard"],"projection":{"op":"object","members":{
           "value":{"op":"copy","reference":{"scope":"root","path":"/dashboard"}}}}}}}
        """.Replace(".json", environment ? ".env" : ".json", StringComparison.Ordinal)
            .Replace("\"format\":\"json\"", environment
                ? "\"format\":\"dotenv\",\"surface\":\"process-environment\",\"environmentDialect\":\"docker-run\""
                : "\"format\":\"json\"", StringComparison.Ordinal)
            .Replace("\"value\":{\"op\"", "\"VALUE\":{\"op\"", StringComparison.Ordinal)));
}
