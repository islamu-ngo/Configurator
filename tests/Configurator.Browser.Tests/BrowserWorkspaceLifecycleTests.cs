namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Layout;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserWorkspaceLifecycleTests
{
    [Test]
    public async Task WarmedDiscoveryAndReviewCannotRestoreRevokedOutputOrHeldImport()
    {
        await using var context = BrowserFixture.Context();
        var layout = context.Render<MainLayout>(parameters => parameters.Add(item => item.Body, builder =>
        {
            builder.OpenComponent<ConfigurationEditor>(0);
            builder.AddAttribute(1, nameof(ConfigurationEditor.Definition), BrowserFixture.Definition);
            builder.CloseComponent();
        }));
        var component = layout.FindComponent<ConfigurationEditor>();
        await component.Find("[data-field='mode'] select").ChangeAsync(new ChangeEventArgs { Value = "single" });
        await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "retained.test" });
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("[data-field='credential'] input").InputAsync(new ChangeEventArgs { Value = "synthetic-warmed-secret" });
        await layout.Find("#configuration-search").ClickAsync(new MouseEventArgs());
        await component.Find("[data-workflow-step='review']").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll("[data-review-path]").Count).IsGreaterThan(0);
        await component.Find("#prepare-complete").ClickAsync(new MouseEventArgs());
        await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(1);
        using var stream = new HeldReadStream("{\"mode\":\"single\",\"hostname\":\"retained.test\",\"credential\":\"synthetic-held-secret\"}"u8.ToArray());
        Task reading = component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
            await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
            await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
            await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "current.test" });
        }
        finally { stream.Release.TrySetResult(); }
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values.ContainsKey("credential")).IsFalse();
        await Assert.That(form.Snapshot().Values["hostname"].GetString()).IsEqualTo("current.test");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("synthetic-held-secret");
        await Assert.That(component.Markup).DoesNotContain("synthetic-warmed-secret");
    }

    [Test]
    public async Task RepeatedEmptyEnumSelectionFencesHeldSecretImport()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        source["fields"]![0]!["choices"]!.AsArray().Insert(0, JsonValue.Create(""));
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("[data-field='mode'] select").Change("0");
        component.Find("[data-field='hostname'] input").Input("retained.test");
        component.Find("#secret-consent").Change(true);
        using var stream = new HeldReadStream("{\"mode\":\"\",\"hostname\":\"retained.test\",\"credential\":\"repeated-selection-canary\"}"u8.ToArray());
        Task reading = component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { component.Find("[data-field='mode'] select").Change("0"); }
        finally { stream.Release.TrySetResult(); }
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Markup).DoesNotContain("repeated-selection-canary");
    }

    [Test]
    [Arguments("invalid")]
    [Arguments("same-value")]
    [Arguments("denial")]
    public async Task EditOrDenialFencesSecretBearingReadCompletion(string transition)
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("[data-field='hostname'] input").Input("retained.test");
        component.Find("#secret-consent").Change(true);
        using var stream = new HeldReadStream("{\"mode\":\"single\",\"hostname\":\"retained.test\",\"credential\":\"late-import-canary\"}"u8.ToArray());
        Task reading = component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            if (transition == "denial") component.Find("#secret-consent").Change(false);
            else component.Find("[data-field='hostname'] input").Input(transition == "invalid" ? "invalid host" : "retained.test");
        }
        finally { stream.Release.TrySetResult(); }
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        if (transition == "denial") await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
        else await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo(transition == "invalid" ? "invalid host" : "retained.test");
        await Assert.That(component.Markup).DoesNotContain("late-import-canary");
        await Assert.That(stream.Captured.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task DenialRetiresSecondPendingExportBeforeFirstCleanupCompletes()
    {
        JsonNode source = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        JsonNode second = source["artifacts"]![0]!.DeepClone();
        second["id"] = "second";
        second["fileName"] = "second.json";
        source["artifacts"]!.AsArray().Add(second);
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString()));
        using var context = BrowserFixture.Context();
        await using var policyModule = new LifecycleModule();
        await using var exportModule = new LifecycleModule();
        context.Services.AddSingleton<IJSRuntime>(new LifecycleJavascript(policyModule, exportModule));
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("[data-field='mode'] select").Change("single");
        component.Find("[data-field='hostname'] input").Input("old.test");
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='credential'] input").Input("second-artifact-canary");
        component.Find("#prepare-complete").Click();
        component.FindAll(".configuration-editor__artifact")[0].QuerySelector("button")!.Click();
        exportModule.GateNextOpen = true;
        Task opening = component.InvokeAsync(() => component.FindAll(".configuration-editor__artifact")[1].QuerySelector("button")!.ClickAsync(new MouseEventArgs()));
        await exportModule.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        exportModule.GateNextInvalidate = true;
        Task denied = component.InvokeAsync(() => component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false }));
        await exportModule.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            exportModule.OpenRelease.TrySetResult();
            await opening.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(exportModule.Exposures.Count).IsEqualTo(1);
            await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        }
        finally
        {
            exportModule.OpenRelease.TrySetResult();
            exportModule.Release.TrySetResult();
            await denied.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Test]
    public async Task DenialClearsSecretsBeforeHeldCleanupAndPreservesCompetingPublicEdit()
    {
        using var context = BrowserFixture.Context();
        await using var policyModule = new LifecycleModule();
        await using var exportModule = new LifecycleModule();
        context.Services.AddSingleton<IJSRuntime>(new LifecycleJavascript(policyModule, exportModule));
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("[data-field='mode'] select").Change("single");
        component.Find("[data-field='hostname'] input").Input("old.test");
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='credential'] input").Input("revoked-lifecycle-canary");
        component.Find("#prepare-complete").Click();
        component.Find(".configuration-editor__artifact button").Click();
        await Assert.That(exportModule.Exposures.Single()).Contains("revoked-lifecycle-canary");
        exportModule.GateNextInvalidate = true;
        Task denied = component.InvokeAsync(() => component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false }));
        await exportModule.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            component.Find("[data-field='hostname'] input").Input("current.test");
            await Assert.That(component.FindAll("[data-field='credential'] input")).IsEmpty();
            var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
            await Assert.That(form.Snapshot().Values.ContainsKey("credential")).IsFalse();
            await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
            await Assert.That(exportModule.HasOpenOutput).IsFalse();
        }
        finally
        {
            exportModule.Release.TrySetResult();
            await denied.WaitAsync(TimeSpan.FromSeconds(5));
        }
        component.Find("#prepare-draft").Click();
        component.Find(".configuration-editor__artifact button").Click();
        await Assert.That(exportModule.Exposures.Last()).Contains("current.test");
        await Assert.That(exportModule.Exposures.Last()).DoesNotContain("revoked-lifecycle-canary");
        await Assert.That(component.Find("#secret-consent").HasAttribute("checked")).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OlderWorkspaceStartCannotUnlockOrDisableCurrentWorkspace(bool replaceDefinition)
    {
        using var context = BrowserFixture.Context();
        await using var policyModule = new LifecycleModule();
        await using var exportModule = new LifecycleModule();
        var javascript = new LifecycleJavascript(policyModule, exportModule);
        await using var policy = new BrowserOfflinePolicy(javascript);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        context.Services.AddSingleton(policy);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        policyModule.GateNextBegin = true;
        Task older = component.InvokeAsync(component.Instance.BeginAsync);
        await policyModule.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (replaceDefinition) component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2)));
        else await component.InvokeAsync(component.Instance.BeginAsync);
        await Assert.That(policy.IsWorkspaceActive).IsTrue();
        policyModule.Release.TrySetResult();
        await older.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(policy.IsWorkspaceActive).IsTrue();
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(component.Find(".configuration-editor__controls").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DelayedOldExportCleanupCannotEraseNewOutputOrResumeStalePreparation(bool failCleanup)
    {
        using var context = BrowserFixture.Context();
        await using var policyModule = new LifecycleModule();
        await using var exportModule = new LifecycleModule();
        var javascript = new LifecycleJavascript(policyModule, exportModule);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("[data-field='mode'] select").Change("single");
        component.Find("[data-field='hostname'] input").Input("old.test");
        component.Find("#prepare-draft").Click();
        component.Find(".configuration-editor__artifact button").Click();
        exportModule.GateNextInvalidate = true;
        exportModule.FailNextInvalidate = failCleanup;
        Task older = component.InvokeAsync(() => component.Find("#prepare-draft").ClickAsync(new MouseEventArgs()));
        await exportModule.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        component.Find("[data-field='hostname'] input").Input("current.test");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        exportModule.Release.TrySetResult();
        await older.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("preparation-ready");
        component.Find(".configuration-editor__artifact button").Click();
        await Assert.That(exportModule.Exposures.Last()).Contains("current.test");
        await Assert.That(exportModule.Exposures.Last()).DoesNotContain("old.test");
        if (failCleanup) await Assert.That(component.Markup).Contains("output-cleanup-failed");
        await Assert.That(component.Markup).DoesNotContain("cleanup-fault-canary");
    }

    private sealed class HeldReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Memory<byte> Captured { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Captured.IsEmpty) Captured = buffer;
            Started.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            return await base.ReadAsync(buffer, CancellationToken.None);
        }
    }

    private sealed class LifecycleJavascript(LifecycleModule policy, LifecycleModule export) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object? result = identifier == "import" ? args![0]?.ToString() == "./pwa-lifecycle.js" ? policy : export : null;
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }

    private sealed class LifecycleModule : IJSObjectReference
    {
        internal bool GateNextBegin { get; set; }
        internal bool GateNextInvalidate { get; set; }
        internal bool GateNextOpen { get; set; }
        internal bool FailNextInvalidate { get; set; }
        internal TaskCompletionSource OpenStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource OpenRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<string, (long Generation, byte[]? Bytes)> _sessions = new(StringComparer.Ordinal);
        internal List<string> Exposures { get; } = [];
        internal bool HasOpenOutput => _sessions.Values.Any(session => session.Bytes is not null);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "open" && GateNextOpen)
            {
                GateNextOpen = false;
                args = args!.ToArray();
                args[2] = ((byte[])args[2]!).ToArray();
                OpenStarted.TrySetResult();
                await OpenRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            if (identifier is "open" or "invalidate")
            {
                string id = (string)args![0]!;
                long generation = (long)args[1]!;
                _sessions.TryGetValue(id, out var previous);
                if (generation >= previous.Generation &&
                    !(identifier == "open" && generation == previous.Generation && previous.Bytes is not null))
                {
                    previous.Bytes?.AsSpan().Clear();
                    _sessions[id] = (generation, identifier == "open" ? ((byte[])args[2]!).ToArray() : null);
                }
            }
            if (identifier == "beginWorkspace" && GateNextBegin || identifier == "invalidate" && GateNextInvalidate)
            {
                GateNextBegin = false;
                GateNextInvalidate = false;
                Started.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                if (identifier == "invalidate" && FailNextInvalidate)
                {
                    FailNextInvalidate = false;
                    throw new InvalidOperationException("cleanup-fault-canary");
                }
            }
            bool exposed = false;
            if (identifier is "copy" or "download" or "reveal")
            {
                if (_sessions.TryGetValue((string)args![0]!, out var session)
                    && session.Generation == (long)args[1]! && session.Bytes is not null)
                {
                    Exposures.Add(Encoding.UTF8.GetString(session.Bytes));
                    exposed = true;
                }
            }
            object? result = identifier == "initialize" ? new BrowserOfflineState(true, true, true)
                : identifier == "beginWorkspace" ? true
                : identifier is "copy" or "download" or "reveal" ? exposed : null;
            return result is null ? default! : (TValue)result;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
