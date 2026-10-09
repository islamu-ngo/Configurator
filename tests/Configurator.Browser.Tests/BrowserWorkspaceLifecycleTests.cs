namespace Configurator.Browser.Tests;

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserWorkspaceLifecycleTests
{
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
    public async Task DelayedOldExportCleanupCannotEraseNewOutputOrResumeStalePreparation()
    {
        using var context = BrowserFixture.Context();
        await using var policyModule = new LifecycleModule();
        await using var exportModule = new LifecycleModule();
        var javascript = new LifecycleJavascript(policyModule, exportModule);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        component.Find("#choice-mode").Change("single");
        component.Find("#choice-hostname").Input("old.test");
        component.Find("#prepare-draft").Click();
        component.Find(".configuration-editor__artifact button").Click();
        exportModule.GateNextInvalidate = true;
        Task older = component.InvokeAsync(() => component.Find("#prepare-draft").ClickAsync(new MouseEventArgs()));
        await exportModule.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        component.Find("#choice-hostname").Input("current.test");
        component.Find("#prepare-draft").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        exportModule.Release.TrySetResult();
        await older.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.FindAll(".configuration-editor__artifact")).Count().IsEqualTo(1);
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("preparation-ready");
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
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "beginWorkspace" && GateNextBegin || identifier == "invalidate" && GateNextInvalidate)
            {
                GateNextBegin = false;
                GateNextInvalidate = false;
                Started.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            }
            object? result = identifier == "initialize" ? new BrowserOfflineState(true, true, true)
                : identifier is "beginWorkspace" or "copy" ? true : null;
            return result is null ? default! : (TValue)result;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
