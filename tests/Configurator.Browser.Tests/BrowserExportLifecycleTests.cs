namespace Configurator.Browser.Tests;

using System.Text;
using Microsoft.JSInterop;

public sealed class BrowserExportLifecycleTests
{
    [Test]
    public async Task PreparingNeverInvokesBrowserAndInvalidationRemovesPrivateBytes()
    {
        using var context = BrowserFixture.Context();
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

    private sealed class GatedJavascript : IJSRuntime
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<IJSObjectReference> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Started.TrySetResult();
            return (TValue)(object)await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
    private sealed class RecordingModule : IJSObjectReference
    {
        internal int PrivateOpeningCount { get; private set; }
        internal bool Disposed { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "open") PrivateOpeningCount++;
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
