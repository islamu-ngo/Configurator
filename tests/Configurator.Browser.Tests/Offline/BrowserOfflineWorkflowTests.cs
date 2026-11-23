namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Components.Configuration;
using Configurator.Browser.Pages;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserOfflineWorkflowTests
{
    [Test]
    public async Task DisconnectedRetainedDefinitionReopensWithPinnedIdentityAndEmptyAnswers()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        module.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        module.Setup<long>("subscribeConnectivity", _ => true).SetResult(1L);
        module.SetupVoid("unsubscribeConnectivity", 1L).SetVoidResult();
        module.Setup<byte[]?>("readDefinition", BrowserFixture.Definition.Digest.Value)
            .SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        Definition? retained = await policy.ReadRetainedDefinitionAsync(BrowserFixture.Definition.Digest.Value);
        await Assert.That(retained).IsNotNull();
        await Assert.That(retained!.Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        using var form = new DefinitionFormModel(retained, DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
    }

    [Test]
    public async Task CatalogueLinksEachCompleteAdmittedDefinitionByItsExactDigest()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<Catalogue>();
        await Assert.That(component.FindAll("h1")).Count().IsEqualTo(1);
        await Assert.That(component.Find("a[href^='launch/']").GetAttribute("href"))
            .IsEqualTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        await Assert.That(BrowserFixture.Definition.Fields.Any(field => field.Sensitivity == FieldSensitivity.Secret)).IsTrue();
    }

    [Test]
    public async Task WorkspaceStartupGateTransitionsAcrossPreparingFailedReadyAndBeforeEntryWithZeroFieldInputs()
    {
        await using var context = BrowserFixture.Context();
        await using var policyModule = new GatedOfflineModule();
        var javascript = new GatedOfflineJavascript(policyModule);
        await using var policy = new BrowserOfflinePolicy(javascript);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        context.Services.AddSingleton(policy);

        var component = context.Render<ConfigurationEditor>(parameters => parameters
            .Add(item => item.Definition, BrowserFixture.Definition));

        // 1. Preparing: zero inputs/controls mounted; public catalogue escape hatch available.
        await policyModule.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-startup-state]").GetAttribute("data-startup-state")).IsEqualTo("preparing");
        await Assert.That(component.FindAll("input, select, textarea, .configuration-editor__controls")).IsEmpty();
        await Assert.That(component.Find("a[href='']")).IsNotNull();

        // 2. PreparationFailed (retryable): custody rejected -> zero inputs; retry button available.
        policyModule.AcceptWorkspace = false;
        policyModule.Release.TrySetResult();
        await policyModule.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        component.WaitForState(
            () => component.FindAll("[data-startup-state]") is { Count: 1 } nodes && nodes[0].GetAttribute("data-startup-state") == "failed",
            TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-startup-state]").GetAttribute("data-startup-state")).IsEqualTo("failed");
        await Assert.That(component.FindAll("input, select, textarea, .configuration-editor__controls")).IsEmpty();
        await Assert.That(component.Find("[data-action='start-workspace']")).IsNotNull();

        // 3. Ready: retrying with accepted custody unblocks interactive workspace controls.
        policyModule.AcceptWorkspace = true;
        await component.Find("[data-action='start-workspace']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-startup-state]")).IsEmpty();
        await Assert.That(component.Find(".configuration-editor__controls").HasAttribute("disabled")).IsFalse();
        await Assert.That(component.FindAll("[data-field='hostname'] input")).Count().IsEqualTo(1);

        // 4. BeforeEntry: clearing clean workspace returns to BeforeEntry with zero inputs.
        await BrowserFixture.SelectPanelAsync(component, "prepare");
        await component.Find("#clear-workspace").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-startup-state]").GetAttribute("data-startup-state")).IsEqualTo("before-entry");
        await Assert.That(component.FindAll("input, select, textarea, .configuration-editor__controls")).IsEmpty();
        await Assert.That(component.Find("[data-action='start-workspace']")).IsNotNull();
        await Assert.That(component.Find("a[href='']")).IsNotNull();
    }

    [Test]
    public async Task EmptySectionDisplaysThreeDistinctExplanationsAndRevealsHiddenOptionalFields()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        BrowserFixture.WithField(source, "cluster_nodes", """
            {"type":"integer","title":"Cluster nodes","x-configurator":{"sensitivity":"public","detail":"basic",
              "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"cluster"}}}
            """, "schema.integer");
        BrowserFixture.WithField(source, "advanced_timeout", """
            {"type":"integer","title":"Timeout","x-configurator":{"sensitivity":"public","detail":"advanced"}}
            """, "schema.integer");
        source["presentation"] = JsonNode.Parse("""
            {"defaultJourney":"default","sections":[
              {"id":"general","title":"General","pointers":["/mode","/hostname","/credential"]},
              {"id":"cluster_only","title":"Cluster condition","pointers":["/replicas"],
               "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"cluster"}},
              {"id":"advanced_only","title":"Advanced options","pointers":["/advanced_timeout"]},
              {"id":"prerequisite_only","title":"Conditional prerequisite","pointers":["/cluster_nodes"]}],
             "journeys":[
              {"id":"default","title":"Setup","sections":["general","cluster_only","advanced_only","prerequisite_only"]}]}
            """);
        Definition definition = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));

        await using var context = BrowserFixture.Context();
        var component = BrowserFixture.RenderWorkspace(context, definition);
        var navigation = BrowserFixture.SectionNavigation(context, component);

        // State 3: Prerequisite unresolved -> explicit empty/filtered explanation before selection.
        await navigation.Find("[data-section='prerequisite_only']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-empty-section-state]").GetAttribute("data-empty-section-state"))
            .IsEqualTo("no-fields");

        // State 2: Hidden optional fields -> explicit explanation + one-click reveal action.
        await navigation.Find("[data-section='advanced_only']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-empty-section-state]").GetAttribute("data-empty-section-state"))
            .IsEqualTo("hidden-optional");
        await component.Find("[data-action='show-section-options']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-field='advanced_timeout'] input")).Count().IsEqualTo(1);

        // State 1: Outside scope / hidden by condition -> explicit condition explanation.
        await navigation.Find("[data-section='general']").ClickAsync(new MouseEventArgs());
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        await navigation.Find("[data-section='cluster_only']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-empty-section-state]").GetAttribute("data-empty-section-state"))
            .IsEqualTo("outside-scope");
    }

    [Test]
    public async Task OpenPageOffersCleanEntryOfflineRetryAndRecoversReadiness()
    {
        await using var context = BrowserFixture.Context();
        var offline = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        offline.Setup<BrowserOfflineState>("initialize", _ => true).SetResult(new BrowserOfflineState(false, false, true));
        offline.Setup<BrowserOfflineState>("retryInitialize", _ => true).SetResult(new BrowserOfflineState(true, true, true));
        offline.Setup<string[]>("retainedDigests", _ => true).SetResult([]);
        offline.Setup<long>("subscribeConnectivity", _ => true).SetResult(1L);
        offline.SetupVoid("unsubscribeConnectivity", _ => true).SetVoidResult();

        var component = context.Render<Open>();
        await Assert.That(component.FindAll("[data-offline-recovery]")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("[data-action='retry-offline']")).Count().IsEqualTo(1);

        await component.Find("[data-action='retry-offline']").ClickAsync(new MouseEventArgs());

        await Assert.That(component.FindAll("[data-offline-recovery]")).IsEmpty();
        await Assert.That(component.Find("[role='status']").GetAttribute("data-code")).IsEqualTo("offline-ready");
        await Assert.That(context.Services.GetRequiredService<BrowserOfflinePolicy>().OfflineReady).IsTrue();
    }

    [Test]
    [Arguments("en", "ltr")]
    [Arguments("ar", "rtl")]
    public async Task OpenPageRendersRetainedListWithNamedRemoveLabelsAndApgFocusRestoration(string locale, string direction)
    {
        Definition alpha = BrowserDefinitionStoreTests.WithTitle(BrowserFixture.Revision(1), "Alpha Service");
        Definition beta = BrowserDefinitionStoreTests.WithTitle(BrowserFixture.Revision(2), "Beta Service");
        Definition gamma = BrowserDefinitionStoreTests.WithTitle(BrowserFixture.Revision(3), "Gamma Service");

        var retained = new List<Definition> { alpha, beta, gamma };
        List<string> focusedIds = [];
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        await using var offlineModule = new DynamicOfflineModule(retained);
        await using var focusModule = new RecordingFocusModule(focusedIds);
        var javascript = new DynamicOfflineJavascript(offlineModule, focusModule);
        await using var policy = new BrowserOfflinePolicy(javascript);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        context.Services.AddSingleton(policy);
        context.Services.AddScoped(_ => new Configurator.Browser.Services.Accessibility.AccessibilityFocusService(javascript));

        var component = context.Render<Open>(parameters => parameters
            .AddCascadingValue((locale, direction)));

        await Assert.That(component.Find("[data-retained-capacity]").TextContent).Contains("3 / 8");
        var removeButtons = component.FindAll("[data-action='remove-retained']");
        await Assert.That(removeButtons).Count().IsEqualTo(3);
        await Assert.That(removeButtons[0].GetAttribute("aria-label")).Contains("Alpha Service");
        await Assert.That(removeButtons[1].GetAttribute("aria-label")).Contains("Beta Service");
        await Assert.That(removeButtons[2].GetAttribute("aria-label")).Contains("Gamma Service");
        await Assert.That(component.FindAll("[data-retained-definitions] bdi").Count).IsGreaterThanOrEqualTo(3);

        // Removing first item focuses next item's remove button (Beta).
        await component.Find($"#remove-retained-{alpha.Digest.Value}").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-retained-capacity]").TextContent).Contains("2 / 8");
        await Assert.That(component.Find("[role='status']").GetAttribute("data-code")).IsEqualTo("retained-definition-removed");
        await Assert.That(component.Find("[role='status']").TextContent).Contains("Alpha Service");
        await Assert.That(focusedIds.LastOrDefault()).IsEqualTo($"remove-retained-{beta.Digest.Value}");

        // Removing last item (Gamma) focuses previous item's remove button (Beta).
        await component.Find($"#remove-retained-{gamma.Digest.Value}").ClickAsync(new MouseEventArgs());
        await Assert.That(component.Find("[data-retained-capacity]").TextContent).Contains("1 / 8");
        await Assert.That(focusedIds.LastOrDefault()).IsEqualTo($"remove-retained-{beta.Digest.Value}");

        // Removing sole remaining item focuses fallback #definition-file.
        await component.Find($"#remove-retained-{beta.Digest.Value}").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-retained-definitions]")).IsEmpty();
        await Assert.That(focusedIds.LastOrDefault()).IsEqualTo("definition-file");
    }

    [Test]
    public async Task OpenPageDistinguishesRetentionCapacityReachedFromStorageUnavailable()
    {
        Definition candidate = BrowserFixture.Definition;
        var retained = new List<Definition> { candidate };
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        await using var offlineModule = new DynamicOfflineModule(retained) { NextRetainResult = "capacity-reached" };
        await using var focusModule = new RecordingFocusModule([]);
        var javascript = new DynamicOfflineJavascript(offlineModule, focusModule);
        await using var policy = new BrowserOfflinePolicy(javascript);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        context.Services.AddSingleton(policy);
        context.Services.AddScoped(_ => new Configurator.Browser.Services.Accessibility.AccessibilityFocusService(javascript));

        var component = context.Render<Open>();
        await component.Find("[data-action='select-retained']").ClickAsync(new MouseEventArgs());
        await component.Find(".open-page__retention input[type='checkbox']").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });
        await component.Find("[data-action='retain-definition']").ClickAsync(new MouseEventArgs());

        string capacityCode = component.Find("[role='status']").GetAttribute("data-code") ?? string.Empty;
        string capacityMessage = component.Find("[role='status']").TextContent;
        await Assert.That(capacityCode).IsEqualTo("offline-retention-capacity");

        offlineModule.NextRetainResult = "storage-unavailable";
        await component.Find("[data-action='retain-definition']").ClickAsync(new MouseEventArgs());
        string unavailableCode = component.Find("[role='status']").GetAttribute("data-code") ?? string.Empty;
        string unavailableMessage = component.Find("[role='status']").TextContent;
        await Assert.That(unavailableCode).IsEqualTo("offline-preparation-unavailable");
        await Assert.That(capacityMessage).IsNotEqualTo(unavailableMessage);
    }

    private sealed class GatedOfflineJavascript(GatedOfflineModule policy) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object? result = identifier == "import"
                ? args?[0]?.ToString() == "./pwa-lifecycle.js" ? policy : NoOpModule.Instance
                : null;
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }

    private sealed class GatedOfflineModule : IJSObjectReference
    {
        internal bool AcceptWorkspace { get; set; } = true;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _gatedBegin = true;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "initialize")
                return (TValue)(object)new BrowserOfflineState(true, true, true);
            if (identifier == "beginWorkspace")
            {
                if (_gatedBegin)
                {
                    _gatedBegin = false;
                    Started.TrySetResult();
                    await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    Completed.TrySetResult();
                }
                return (TValue)(object)AcceptWorkspace;
            }
            return default!;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoOpModule : IJSObjectReference
    {
        internal static NoOpModule Instance { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult<TValue>(default!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult<TValue>(default!);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DynamicOfflineJavascript(DynamicOfflineModule offline, RecordingFocusModule focus) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                string? target = args?[0]?.ToString();
                object module = target switch
                {
                    "./pwa-lifecycle.js" => offline,
                    "./js/accessibility.js" => focus,
                    _ => NoOpModule.Instance
                };
                return ValueTask.FromResult((TValue)module);
            }

            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed class DynamicOfflineModule(List<Definition> retained) : IJSObjectReference
    {
        internal string NextRetainResult { get; set; } = "retained";

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object? result = identifier switch
            {
                "initialize" => new BrowserOfflineState(true, true, true),
                "retryInitialize" => new BrowserOfflineState(true, true, true),
                "subscribeConnectivity" => 1L,
                "retainedDigests" => retained.Select(item => item.Digest.Value).ToArray(),
                "readDefinition" => retained.FirstOrDefault(item => item.Digest.Value == args?[0]?.ToString())?.CanonicalBytes.ToArray(),
                "deleteDefinition" => retained.RemoveAll(item => item.Digest.Value == args?[0]?.ToString()) > 0,
                "retainDefinition" => NextRetainResult,
                _ => default(TValue)
            };
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingFocusModule(List<string> focusedIds) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "setFocusById" && args?.Length > 0 && args[0] is string id)
                focusedIds.Add(id);

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
