namespace Configurator.Browser.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserTemplateReadLifecycleTests
{
    [Test]
    public async Task RejectedUploadReleasesSelectedFileControl()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var input = component.FindComponent<InputFile>();
        InputFile selected = input.Instance;
        await component.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary(
            "invalid"u8.ToArray(), "invalid.json", contentType: "application/json")));
        await Assert.That(ReferenceEquals(selected, component.FindComponent<InputFile>().Instance)).IsFalse();
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
    }

    [Test]
    public async Task FailedReadClearsPreviouslyAcceptedDownload()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var valid = new MemoryStream(BrowserFixture.Template());
        await component.InvokeAsync(() => component.Instance.LoadAsync(valid));
        await Assert.That(component.FindAll("a[download]")).Count().IsEqualTo(1);
        using var failed = new FailedReadStream();
        await component.InvokeAsync(() => component.Instance.LoadAsync(failed));
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await Assert.That(component.FindAll("pre")).IsEmpty();
        await Assert.That(component.FindAll("[role=alert]")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task OlderReadCannotRestoreDownloadAfterNewerRejectionAndBufferIsCleared()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var older = new GatedReadStream(BrowserFixture.Template());
        Task pending = component.InvokeAsync(() => component.Instance.LoadAsync(older));
        await older.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            using var invalid = new MemoryStream("invalid"u8.ToArray());
            await component.InvokeAsync(() => component.Instance.LoadAsync(invalid));
        }
        finally { older.Release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await Assert.That(component.FindAll("pre")).IsEmpty();
        await Assert.That(older.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task NavigationFencesPendingUploadBeforeItCanRestoreOldRoute()
    {
        using var context = BrowserFixture.Context();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/launch/{BrowserFixture.Family}");
        var app = context.Render<App>();
        var editor = app.FindComponent<PublicTemplateEditor>();
        using var older = new GatedReadStream(BrowserFixture.Template());
        Task pending = editor.InvokeAsync(() => editor.Instance.LoadAsync(older));
        await older.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { await app.InvokeAsync(() => navigation.NavigateTo("/")); }
        finally { older.Release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(app.FindAll("a[download]")).IsEmpty();
        await Assert.That(app.FindAll("pre")).IsEmpty();
        await Assert.That(older.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task DefinitionChangeFencesPendingReadAndClearsOldOutput()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var older = new GatedReadStream(BrowserFixture.Template());
        Task pending = component.InvokeAsync(() => component.Instance.LoadAsync(older));
        await older.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await component.InvokeAsync(() => component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2))));
        }
        finally { older.Release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.FindAll("a[download]")).IsEmpty();
        await Assert.That(component.FindAll("pre")).IsEmpty();
        await Assert.That(older.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task CompletedReadClearsBufferEvenWhenImportSucceeded()
    {
        using var context = BrowserFixture.Context();
        var component = context.Render<PublicTemplateEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        using var input = new GatedReadStream(BrowserFixture.Template());
        input.Release.TrySetResult();
        await component.InvokeAsync(() => component.Instance.LoadAsync(input));
        await Assert.That(component.FindAll("a[download]")).Count().IsEqualTo(1);
        await Assert.That(input.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
    }

    private sealed class FailedReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("read-failed"));
    }

    // Deliberately ignores cancellation: the component's generation fence must still reject completion.
    private sealed class GatedReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Memory<byte> CapturedBuffer { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (CapturedBuffer.IsEmpty) CapturedBuffer = buffer;
            Started.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            return await base.ReadAsync(buffer, CancellationToken.None);
        }
    }
}
