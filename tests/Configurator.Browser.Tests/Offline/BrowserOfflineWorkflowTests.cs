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
}
