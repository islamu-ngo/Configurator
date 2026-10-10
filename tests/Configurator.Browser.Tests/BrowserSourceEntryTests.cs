namespace Configurator.Browser.Tests;

using Bunit;
using Configurator.Browser.Pages;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserSourceEntryTests
{
    [Test]
    public async Task SourceInputsHaveConnectedLabelsAndFormatHelp()
    {
        using var context = BrowserFixture.Context();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var page = context.Render<Open>();
        foreach (string id in new[] { "definition-file", "source-url" })
        {
            var input = page.Find("#" + id);
            await Assert.That(page.Find($"label[for='{id}']").TextContent.Length > 0).IsTrue();
            string description = input.GetAttribute("aria-describedby")!;
            await Assert.That(page.Find("#" + description).TextContent.Length > 0).IsTrue();
        }
        await Assert.That(page.Find("#definition-file").GetAttribute("accept")).IsEqualTo(".json,.yaml,.yml,application/json,application/yaml,text/yaml");
        await Assert.That(page.Find("#source-url").GetAttribute("type")).IsEqualTo("url");
        await Assert.That(page.FindAll("h1")).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments("source.json", false)]
    [Arguments("source.yaml", true)]
    [Arguments("source.yml", true)]
    public async Task LocalSourcePickerAdmitsBeforeWorkspaceAndRestoresFocus(string name, bool yaml)
    {
        var registry = new BrowserDefinitionRegistry([]);
        using var context = BrowserFixture.Context(registry);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var page = context.Render<Open>();
        var input = page.FindComponent<InputFile>();
        InputFile original = input.Instance;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(yaml ? BrowserDefinitionSourceTests.AuthoringYaml : BrowserDefinitionSourceTests.AuthoringJson);
        await page.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary(bytes, name)));
        await Assert.That(page.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("source-admitted");
        await Assert.That(page.FindAll(".configuration-editor")).IsEmpty();
        await Assert.That(System.Text.Encoding.UTF8.GetString(registry.Current.Definitions.Single().CanonicalBytes.Span))
            .DoesNotContain("synthetic-local-comment-not-retained");
        await Assert.That(ReferenceEquals(original, page.FindComponent<InputFile>().Instance)).IsFalse();
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier == "Blazor._internal.domWrapper.focus")).IsTrue();
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier is "fetchDefinition" or "retainDefinition")).IsFalse();
    }

    [Test]
    [Arguments("invalid.yaml")]
    [Arguments("invalid.txt")]
    public async Task InvalidLocalSourceLeavesPickerAvailableWithValueFreeStatus(string name)
    {
        using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var page = context.Render<Open>();
        var input = page.FindComponent<InputFile>();
        await page.InvokeAsync(() => input.UploadFiles(InputFileContent.CreateFromBinary("synthetic-private-invalid-source"u8.ToArray(), name)));
        await Assert.That(page.FindAll(".configuration-editor")).IsEmpty();
        await Assert.That(page.FindAll("[data-code=source-admitted]")).IsEmpty();
        await Assert.That(page.Find("[role=status]").TextContent).DoesNotContain("synthetic-private");
        await Assert.That(page.Find("#definition-file").HasAttribute("disabled")).IsFalse();
    }

    [Test]
    public async Task PublicUrlEntryAdmitsDefinitionAndSharesOnlyPinnedMetadata()
    {
        using var context = BrowserFixture.Context();
        var pwa = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        pwa.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        pwa.Setup<string[]>("retainedDigests").SetResult([]);
        var source = context.JSInterop.SetupModule("./browser-definition-source.js");
        source.Setup<byte[]>("fetchDefinition", _ => true).SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var page = context.Render<Open>();
        page.Find("#source-url").Input("https://publisher.example/definition.json");
        await page.Find("#load-public-source").ClickAsync(new MouseEventArgs());
        string link = page.Find("#definition-share-link").GetAttribute("value")!;
        var uri = new Uri(link);
        BrowserDefinitionPointer pointer = BrowserDefinitionPointer.Parse(uri.Fragment, uri.Query);
        await Assert.That(pointer.Digest).IsEqualTo(BrowserFixture.Definition.Digest.Value);
        await Assert.That(pointer.Source.AbsoluteUri).IsEqualTo("https://publisher.example/definition.json");
        await Assert.That(page.FindAll("#choice-hostname")).IsEmpty();
        await Assert.That(uri.Query).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task PrivateUrlCanaryRejectsWithoutEchoAndKeepsLocalUploadAvailable()
    {
        using var context = BrowserFixture.Context();
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var page = context.Render<Open>();
        page.Find("#source-url").Input("https://publisher.example/definition.json?token=synthetic-private-canary");
        page.Find("#load-public-source").Click();
        await Assert.That(page.Find("[role=status]").TextContent).DoesNotContain("synthetic-private-canary");
        await Assert.That(page.Find("[role=status]").GetAttribute("data-code")).IsEqualTo("source-invalid");
        await Assert.That(page.FindAll("#definition-file")).Count().IsEqualTo(1);
        await Assert.That(page.FindAll("#definition-share-link")).IsEmpty();
    }
}
