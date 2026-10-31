namespace Configurator.Browser.Tests;

using System.Text;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserTemplateReadLifecycleTests
{
    private static readonly byte[] OrdinaryArtifact = Encoding.UTF8.GetBytes("""{"mode":"single","hostname":"example.test"}""");

    [Test]
    public async Task RejectedUploadReleasesSelectedFileControl()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        var input = component.FindComponent<InputFile>();
        InputFile selected = input.Instance;
        await component.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary("invalid"u8.ToArray(), "invalid.json", contentType: "application/json")));
        await Assert.That(ReferenceEquals(selected, component.FindComponent<InputFile>().Instance)).IsFalse();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
    }

    [Test]
    public async Task RevocationFencesPendingImportIntoLocallyCompiledDefinition()
    {
        await using var context = BrowserFixture.Context();
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(new BrowserDefinitionRegistry([]), javascript, policy);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(BrowserDefinitionSourceTests.AuthoringJson));
        SetupDefinition definition = await source.ReadLocalAsync(input);
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, definition));
        component.Find("#secret-consent").Change(true);
        using var older = new GatedReadStream("""{"HOST":"example.test","settings":{"secret":"synthetic-local-import-secret"}}"""u8.ToArray());
        Task pending = component.InvokeAsync(() => component.Instance.LoadAsync(older));
        await older.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { component.Find("#secret-consent").Change(false); }
        finally { older.Release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        var form = component.FindComponents<ConfigurationField>()[0].Instance.Form;
        await Assert.That(form.Snapshot().Values).IsEmpty();
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Markup).DoesNotContain("synthetic-local-import-secret");
        await Assert.That(older.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("clear")]
    [Arguments("replacement")]
    [Arguments("revoke")]
    public async Task TransitionFencesLateReadAndClearsOwnedBuffer(string transition)
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        using var older = new GatedReadStream(OrdinaryArtifact);
        Task pending = component.InvokeAsync(() => component.Instance.LoadAsync(older));
        await older.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            if (transition == "edit")
                await component.Find("[data-field='hostname'] input").InputAsync(new ChangeEventArgs { Value = "retained.test" });
            else if (transition == "clear")
                await component.Find("#clear-workspace").ClickAsync(new MouseEventArgs());
            else if (transition == "replacement")
                await component.InvokeAsync(() => component.Render(parameters => parameters.Add(item => item.Definition, BrowserFixture.Revision(2))));
            else await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = false });
        }
        finally { older.Release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo(transition == "edit" ? "retained.test" : "");
        await Assert.That(older.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task NavigationFencesPendingRead()
    {
        await using var context = BrowserFixture.Context();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/launch/{BrowserFixture.Family}");
        var app = context.Render<App>();
        var editor = app.FindComponent<ConfigurationEditor>();
        await editor.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        using var older = new GatedReadStream(OrdinaryArtifact);
        Task pending = editor.InvokeAsync(() => editor.Instance.LoadAsync(older));
        await older.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { await app.InvokeAsync(() => navigation.NavigateTo("/")); }
        finally { older.Release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(app.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(older.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task ImportSuccessAndFailureBothClearReadBuffer()
    {
        await using var context = BrowserFixture.Context();
        var component = context.Render<ConfigurationEditor>(parameters => parameters.Add(item => item.Definition, BrowserFixture.Definition));
        await component.Find("#secret-consent").ChangeAsync(new ChangeEventArgs { Value = true });
        await component.Find("#import-purpose").ChangeAsync(new ChangeEventArgs { Value = "fill" });
        using var stream = new GatedReadStream(OrdinaryArtifact);
        stream.Release.TrySetResult();
        await component.InvokeAsync(() => component.Instance.LoadAsync(stream));
        await Assert.That(component.Find("[data-field='hostname'] input").GetAttribute("value")).IsEqualTo("example.test");
        await Assert.That(stream.CapturedBuffer.ToArray().All(value => value == 0)).IsTrue();
        using var failed = new FailedReadStream();
        await component.InvokeAsync(() => component.Instance.LoadAsync(failed));
        await Assert.That(component.FindAll(".configuration-editor__artifact")).IsEmpty();
        await Assert.That(component.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("import-read-failed");
    }

    private sealed class FailedReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(new IOException("read-failed"));
    }
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
