namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserOutputGuidanceTests
{
    [Test]
    [Arguments("NativeNeutralDefinition")]
    [Arguments("OutputGuidance.Grafana")]
    [Arguments("OutputGuidance.SecretGeneration")]
    public async Task MaintainedNonTaskExamplesNeverManufactureRelatedTasks(string resource)
    {
        using Stream source = typeof(BrowserOutputGuidanceTests).Assembly.GetManifestResourceStream(resource)!;
        using var bytes = new MemoryStream();
        await source.CopyToAsync(bytes);
        var definition = DefinitionCodec.ParseJson(bytes.ToArray());
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("download", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        if (resource == "OutputGuidance.Grafana")
            await component.InvokeAsync(() =>
            {
                form.SetValue(["datasource", "name"], JsonSerializer.SerializeToElement("Synthetic metrics"));
                form.SetValue(["datasource", "url"], JsonSerializer.SerializeToElement("https://metrics.invalid"));
                form.SetValue(["server", "domain"], JsonSerializer.SerializeToElement("grafana.invalid"));
            });
        await BrowserFixture.PrepareAsync(component, true);
        await Assert.That(component.FindAll("[data-prepared-file]").Count).IsGreaterThan(0);
        await component.Find("[data-action='download-file']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-file-status='download-requested']").Count).IsGreaterThan(0);
        await Assert.That(component.FindAll("[data-next-actions]")).IsEmpty();
        await Assert.That(component.FindAll("[data-transfer-review]")).IsEmpty();
        await Assert.That(component.FindAll("#configuration-output")).IsEmpty();
    }

    [Test]
    public async Task NextTaskReviewIsExplicitAndOldConfirmationCannotOverwriteNewerAnswers()
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await component.InvokeAsync(() => form.EditValue(["source"], "First"));
        await BrowserFixture.PrepareAsync(component, false);
        await component.Find("[data-action='copy-file']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-next-option='identity']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-transfer-review]").Count).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values.ContainsKey("target")).IsFalse();
        var panel = component.FindComponent<ConfigurationNextActionsPanel>().Instance;
        var oldApply = panel.ApplyRequested;
        var oldReview = panel.Review!;
        var oldSelection = new ConfigurationNextActionsPanel.TransferSelection(oldReview, oldReview.Candidates.Select(item => item.Id));
        await component.Find("[data-transfer-cancel]").ClickAsync(new MouseEventArgs());
        await component.Find("[data-next-option='identity']").ClickAsync(new MouseEventArgs());
        await component.InvokeAsync(() => oldApply.InvokeAsync(oldSelection));
        await Assert.That(form.Snapshot().Values.ContainsKey("target")).IsFalse();
        await component.InvokeAsync(() => form.EditValue(["source"], "Newer"));
        await component.InvokeAsync(() => oldApply.InvokeAsync(oldSelection));
        await Assert.That(form.Snapshot().Values.ContainsKey("target")).IsFalse();
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var selected)).IsTrue();
        await Assert.That(selected.GetString()).IsEqualTo("settings");
    }

    [Test]
    public async Task PartialExportKeepsCoveragePartialAndTransferSwitchIsOneAcceptedChange()
    {
        var runtime = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonNode second = runtime["artifacts"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == "settings")!.DeepClone();
        second["id"] = "settings-second";
        second["fileName"] = "settings-second.json";
        runtime["artifacts"]!.AsArray().Add(second);
        runtime["presentation"]!["outputSelector"]!["options"]![0]!["artifactIds"] = new JsonArray("settings", "settings-second");
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition,
            DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString()))));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await component.InvokeAsync(() => form.EditValue(["source"], "Reviewed"));
        await BrowserFixture.PrepareAsync(component, false);
        await component.Find("[data-prepared-file='settings'] [data-action='copy-file']").ClickAsync(new MouseEventArgs());
        await Assert.That(form.OutputWorkflow.Tasks.Single(item => item.Option == "settings").Status)
            .IsEqualTo(DefinitionOutputExportStatus.Partial);
        await component.Find("[data-next-option='identity']").ClickAsync(new MouseEventArgs());
        long revision = form.InputRevision;
        await component.Find("[data-transfer-apply]").ClickAsync(new MouseEventArgs());
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.Snapshot().Values["target"].GetString()).IsEqualTo("Reviewed");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var selected)).IsTrue();
        await Assert.That(selected.GetString()).IsEqualTo("identity");
        await Assert.That(component.FindAll("[data-transfer-review]")).IsEmpty();
        await Assert.That(component.FindAll("[data-next-actions]")).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OnlyAcknowledgedExportEnablesGuidanceWithItsActualDeliveryKind(bool download)
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>(download ? "download" : "copy", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await component.InvokeAsync(() => form.EditValue(["source"], "Retained"));
        await Assert.That(component.FindAll("[data-next-actions]")).IsEmpty();
        await BrowserFixture.PrepareAsync(component, false);
        await Assert.That(component.FindAll("[data-next-actions]")).IsEmpty();
        await component.Find(download ? "[data-action='download-file']" : "[data-action='copy-file']")
            .ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(download ? "[data-file-status='download-requested']" : "[data-file-status='copied']").Count)
            .IsEqualTo(1);
        var state = form.OutputWorkflow.Tasks.Single(item => item.Option == "settings");
        await Assert.That(state.Status).IsEqualTo(DefinitionOutputExportStatus.ExportedCurrent);
        await Assert.That(state.Deliveries.Single().Kind).IsEqualTo(download
            ? DefinitionOutputDeliveryKind.DownloadRequested : DefinitionOutputDeliveryKind.ClipboardCopied);
        await Assert.That(component.FindAll("[data-next-option='identity']").Count).IsEqualTo(1);
        await Assert.That(component.Find("[data-output-workflow-announcement]").GetAttribute("aria-live")).IsEqualTo("polite");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var selected)).IsTrue();
        await Assert.That(selected.GetString()).IsEqualTo("settings");
    }

    [Test]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task ProtectedReviewStaysMaskedAndRevocationRetiresItsCapturedConfirmation(string locale)
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(true);
        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, ProtectedDefinition())
            .Add(item => item.InitialLocale, locale));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.InvokeAsync(() => form.EditValue(["source"], "synthetic-ui-secret-sentinel"));
        await BrowserFixture.PrepareAsync(component, false);
        await component.Find("[data-action='copy-file']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-next-option='identity']").ClickAsync(new MouseEventArgs());
        var panel = component.FindComponent<ConfigurationNextActionsPanel>().Instance;
        var review = panel.Review!;
        var confirm = panel.ApplyRequested;
        await Assert.That(component.Find("[data-transfer-review]").GetAttribute("dir")).IsEqualTo(locale == "ar" ? "rtl" : "ltr");
        await Assert.That(component.FindAll("[data-transfer-masked]").Count).IsEqualTo(1);
        await Assert.That(component.Markup.Contains("synthetic-ui-secret-sentinel", StringComparison.Ordinal)).IsFalse();
        await Assert.That(JsonSerializer.Serialize(review).Contains("synthetic-ui-secret-sentinel", StringComparison.Ordinal)).IsFalse();
        await component.Find("[data-action='revoke-secret-entry']").ClickAsync(new MouseEventArgs());
        await component.InvokeAsync(() => confirm.InvokeAsync(new ConfigurationNextActionsPanel.TransferSelection(review, [review.Candidates[0].Id])));
        await Assert.That(component.FindAll("[data-transfer-review]")).IsEmpty();
        await Assert.That(form.Snapshot().Values.ContainsKey("source")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("target")).IsFalse();
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
        while (form.Undo())
            await Assert.That(form.Snapshot().Values.ContainsKey("source")).IsFalse();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("clear")]
    [Arguments("replace")]
    public async Task LateJavascriptAcknowledgementCannotCreateCurrentGuidance(string retirement)
    {
        await using var context = BrowserFixture.Context();
        await using var javascript = new GatedCopyRuntime(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await BrowserFixture.PrepareAsync(component, false);
        Task copy = component.Find("[data-action='copy-file']").ClickAsync(new MouseEventArgs());
        Task? discarding = null;
        try
        {
            await javascript.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            if (retirement == "edit")
                await component.InvokeAsync(() => form.EditValue(["source"], "Newer"));
            else if (retirement == "clear")
                discarding = component.InvokeAsync(component.Instance.DiscardAsync);
            else
                component.Render(parameters => parameters.Add(item => item.Definition, Definition()));
        }
        finally { javascript.Release.TrySetResult(); }
        await copy.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        if (discarding is not null) await discarding.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await Assert.That(component.FindAll("[data-next-actions]")).IsEmpty();
        await Assert.That(component.FindAll("[data-file-status='copied']")).IsEmpty();
        if (retirement != "replace")
            await Assert.That(form.OutputWorkflow.Tasks.All(item => item.Status == DefinitionOutputExportStatus.NotExported)).IsTrue();
    }

    private static Definition ProtectedDefinition()
    {
        var runtime = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        foreach (string name in new[] { "source", "target" })
            runtime["configuration"]!["properties"]![name]!["x-configurator"]!["sensitivity"] = "secret";
        runtime["locales"] = new JsonArray("ar", "en");
        runtime["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
              "/configuration/properties/source/title":"\u0627\u0633\u0645 \u0627\u0644\u0645\u0624\u0633\u0633\u0629",
              "/configuration/properties/target/title":"\u0627\u0633\u0645 \u0627\u0644\u0645\u0624\u0633\u0633\u0629 \u0641\u064a \u0627\u0644\u0645\u0633\u062a\u0646\u062f",
              "/presentation/outputSelector/options/0/nextActions/0/title":"\u0625\u0639\u062f\u0627\u062f \u0627\u0644\u0647\u0648\u064a\u0629"
            }}}}
            """);
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString()));
    }

    private sealed class GatedCopyRuntime(IJSRuntime fallback) : IJSRuntime, IAsyncDisposable
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Module _module = new();

        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            InvokeAsync<T>(identifier, CancellationToken.None, args);

        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import" || args?.FirstOrDefault()?.ToString() != "./js/browser-artifacts.js")
                return fallback.InvokeAsync<T>(identifier, cancellationToken, args);
            _module.Owner = this;
            return ValueTask.FromResult((T)(object)_module);
        }

        public ValueTask DisposeAsync() { Release.TrySetResult(); return _module.DisposeAsync(); }

        private sealed class Module : IJSObjectReference
        {
            internal GatedCopyRuntime Owner { get; set; } = null!;
            public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
                InvokeAsync<T>(identifier, CancellationToken.None, args);

            public async ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier != "copy") return default!;
                Owner.Started.TrySetResult();
                await Owner.Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                return (T)(object)true;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task RefusedExportDoesNotManufactureGuidance()
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./js/browser-artifacts.js");
        module.Setup<bool>("copy", _ => true).SetResult(false);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, Definition()));
        var form = component.FindComponent<ConfigurationConfigurePanel>().Instance.Form;
        await BrowserFixture.PrepareAsync(component, false);
        await component.Find("[data-action='copy-file']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-next-actions]")).IsEmpty();
        await Assert.That(form.OutputWorkflow.Tasks.Single(item => item.Option == "settings").Status)
            .IsEqualTo(DefinitionOutputExportStatus.NotExported);
    }

    internal static Definition Definition() => DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {
          "$schema":"urn:islamu:configurator:authoring:v0.0.1",
          "identity":{"familyId":"c4d5c647-e568-47d9-8889-3f41564c43f1","revision":1},
          "project":{"id":"browser-workflow","name":"Browser workflow"},"applicationVersions":["1.0"],
          "configuration":{"type":"object","additionalProperties":false,"required":["task"],
            "x-configurator":{"sensitivity":"public"},"properties":{
              "task":{"type":"string","enum":["settings","identity"],
                "x-configurator":{"sensitivity":"public","initialValue":"settings"}},
              "source":{"type":"string","title":"Organization name",
                "x-configurator":{"sensitivity":"public","applicableWhen":{
                  "op":"equals","reference":{"scope":"root","path":"/task"},"value":"settings"}}},
              "target":{"type":"string","title":"Document organization name",
                "x-configurator":{"sensitivity":"public","suggestedFrom":{"pointer":"/source"},
                  "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"}}}
            }},
          "presentation":{"defaultJourney":"configure",
            "sections":[{"id":"configure","title":"Configure","pointers":["/task","/source","/target"]}],
            "journeys":[{"id":"configure","title":"Configure","sections":["configure"]}],
            "outputSelector":{"pointer":"/task","options":[
              {"value":"settings","title":"Settings file","artifactIds":["settings"],
                "nextActions":[{"targetOption":"identity","title":"Prepare identity","priority":"recommended"}]},
              {"value":"identity","title":"Identity file","artifactIds":["identity"]}
            ]}},
          "outputs":{
            "settings.json":{"id":"settings","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"settings"}}},
            "identity.json":{"id":"identity","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"}}}
          }
        }
        """));
}
