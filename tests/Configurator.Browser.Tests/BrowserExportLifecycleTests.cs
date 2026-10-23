namespace Configurator.Browser.Tests;

using System.Text;
using Bunit;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserExportLifecycleTests
{
    [Test]
    public async Task RemountRevocationAndReplacementFenceHeldExportAndRenderedSecretInput()
    {
        await using var context = BrowserFixture.Context();
        var javascript = new GatedJavascript(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(javascript);
        var component = context.Render<ConfigurationEditor>(parameters =>
            parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("retained.test");
        component.Find("#secret-consent").Change(true);
        component.Find("[data-field='credential'] input").Input("synthetic-remount-secret");
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        var captured = context.Render<BrowserCredentialEntryTests.EmittedInputField>(parameters => parameters
            .Add(item => item.Form, form).Add(item => item.Path, ["credential"])
            .Add(item => item.Schema, form.Definition.Fields.Single(field => field.Id == "credential").Constraints));
        object callback = captured.Instance.CaptureInput();
        component.Find("#prepare-complete").Click();
        await Assert.That(component.FindAll(".configuration-editor__artifact").Count).IsEqualTo(1);
        Task copying = component.Find(".configuration-editor__artifact button").ClickAsync(new MouseEventArgs());
        await javascript.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using var module = new RecordingModule();
        DefinitionFormModel? replacement = null;
        try
        {
            await component.InvokeAsync(() =>
            {
                form.SetDisclosureOverride(SetupFieldDisclosure.Essential);
                form.SetDisclosureOverride(SetupFieldDisclosure.Advanced);
                form.SetSecretEntryEnabled(false);
            });
            component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2)));
            replacement = component.FindComponents<ConfigurationField>()[0].Instance.Form;
            component.Find("[data-field='hostname'] input").Input("replacement.test");
            await captured.InvokeAsync(() => callback switch
            {
                EventCallback<ChangeEventArgs> typed => typed.InvokeAsync(new ChangeEventArgs { Value = "synthetic-stale-remount-input" }),
                EventCallback untyped => untyped.InvokeAsync(new ChangeEventArgs { Value = "synthetic-stale-remount-input" }),
                MulticastDelegate handler => new EventCallbackWorkItem(handler).InvokeAsync(new ChangeEventArgs { Value = "synthetic-stale-remount-input" }),
                _ => throw new InvalidOperationException("input-handler-missing")
            });
            component.Find("#secret-consent").Change(true);
        }
        finally { javascript.Release.TrySetResult(module); }
        await copying.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(module.PrivateOpeningCount).IsEqualTo(0);
        await Assert.That(replacement!.Snapshot().Values.ContainsKey("credential")).IsFalse();
        await Assert.That(replacement.Snapshot().Values["hostname"].GetString()).IsEqualTo("replacement.test");
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[data-field='credential'] input").GetAttribute("value")).IsEqualTo("");
        await Assert.That(component.Markup).DoesNotContain("synthetic-remount-secret");
        await Assert.That(component.Markup).DoesNotContain("synthetic-stale-remount-input");
    }

    [Test]
    public async Task CleanupFaultStillDisposesModuleAndCannotExposeRetiredBytes()
    {
        var javascript = new GatedJavascript();
        await using var module = new RecordingModule();
        javascript.Release.TrySetResult(module);
        await using var export = new BrowserArtifactExport(javascript);
        export.SetOutput("cleanup-export-canary"u8.ToArray());
        _ = await export.CopyAsync();
        module.FailInvalidation = true;
        await Assert.That(() => export.DisposeAsync().AsTask()).Throws<InvalidOperationException>();
        await Assert.That(module.Disposed).IsTrue();
        await Assert.That(export.HasOutput).IsFalse();
        await Assert.That(await export.CopyAsync()).IsFalse();
        await Assert.That(module.PrivateOpeningCount).IsEqualTo(1);
    }

    [Test]
    public async Task PreparingNeverInvokesBrowserAndInvalidationRemovesPrivateBytes()
    {
        await using var context = BrowserFixture.Context();
        await using var export = new BrowserArtifactExport(context.JSInterop.JSRuntime);
        byte[] source = Encoding.UTF8.GetBytes("synthetic-secret-canary");
        export.SetOutput(source);
        source.AsSpan().Clear();
        await Assert.That(export.HasOutput).IsTrue();
        await Assert.That(context.JSInterop.Invocations.Count).IsEqualTo(0);
        await export.InvalidateAsync();
        await Assert.That(export.HasOutput).IsFalse();
        await Assert.That(context.JSInterop.Invocations.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InvalidationOrDisposalFencesLateJavascriptImportBeforePrivateExposure(bool dispose)
    {
        var javascript = new GatedJavascript();
        await using var export = new BrowserArtifactExport(javascript);
        export.SetOutput(Encoding.UTF8.GetBytes("synthetic-late-export-canary"));
        Task<bool> pending = export.CopyAsync();
        await javascript.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (dispose) await export.DisposeAsync();
        else await export.InvalidateAsync();
        await using var module = new RecordingModule();
        javascript.Release.TrySetResult(module);
        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(5))).IsFalse();
        await Assert.That(module.PrivateOpeningCount).IsEqualTo(0);
        await Assert.That(export.HasOutput).IsFalse();
        if (dispose) await Assert.That(module.Disposed).IsTrue();
    }

    private sealed class GatedJavascript(IJSRuntime? fallback = null) : IJSRuntime
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<IJSObjectReference> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (fallback is not null && (identifier != "import" || args?.FirstOrDefault()?.ToString() != "./browser-artifacts.js"))
                return await fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
            Started.TrySetResult();
            return (TValue)(object)await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
    private sealed class RecordingModule : IJSObjectReference
    {
        internal int PrivateOpeningCount { get; private set; }
        internal bool Disposed { get; private set; }
        internal bool FailInvalidation { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "invalidate" && FailInvalidation) return ValueTask.FromException<TValue>(new InvalidOperationException("synthetic-cleanup-fault"));
            if (identifier == "open") PrivateOpeningCount++;
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
