namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

public sealed class BrowserDefinitionSourceTests
{
    internal const string AuthoringJson = """
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"f93a6b9f-c210-47a9-b90b-434954040282","revision":1},
         "project":{"id":"local.service","name":"Local Service"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,"required":["HOST"],
           "x-configurator":{"sensitivity":"public"},"properties":{
             "HOST":{"type":"string","x-configurator":{"sensitivity":"public"}},
             "settings":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
               "properties":{"secret":{"type":"string","x-configurator":{"sensitivity":"secret"}}}}}},
         "outputs":{"settings.json":{"format":"json"}}}
        """;
    internal const string AuthoringYaml = """
        # synthetic-local-comment-not-retained
        $schema: urn:islamu:configurator:authoring:v2
        identity:
          familyId: f93a6b9f-c210-47a9-b90b-434954040282
          revision: 1
        project:
          id: local.service
          name: Local Service
        applicationVersions: ["1.0"]
        configuration:
          type: object
          additionalProperties: false
          required: [HOST]
          x-configurator: {sensitivity: public}
          properties:
            HOST:
              type: string
              x-configurator: {sensitivity: public}
            settings:
              type: object
              additionalProperties: false
              x-configurator: {sensitivity: public}
              properties:
                secret:
                  type: string
                  x-configurator: {sensitivity: secret}
        outputs:
          settings.json:
            format: json
        """;

    [Test]
    public async Task LocalAuthoringAdmissionIsPreAnswerAndNeverInvokesNetworkOrStorage()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(AuthoringJson));
        SetupDefinition definition = await source.ReadLocalAsync(input);
        await Assert.That(definition.FamilyId).IsEqualTo(Guid.Parse("f93a6b9f-c210-47a9-b90b-434954040282"));
        await Assert.That(definition.Revision).IsEqualTo(1);
        await Assert.That(definition.ApplicationVersions).IsEquivalentTo(["1.0"]);
        await Assert.That(definition.EntryPoints[0]).IsEqualTo("setup");
        await Assert.That(definition.Locales[0]).IsEqualTo("en");
        using var yaml = new MemoryStream(Encoding.UTF8.GetBytes(AuthoringYaml));
        SetupDefinition paired = await source.ReadLocalAsync(yaml, yaml: true);
        await Assert.That(paired.Digest).IsEqualTo(definition.Digest);
        await Assert.That(paired.CanonicalBytes.Span.SequenceEqual(definition.CanonicalBytes.Span)).IsTrue();
        await Assert.That(context.JSInterop.Invocations).IsEmpty();
    }

    [Test]
    public async Task RemoteAuthoringRejectsBeforeAdmissionAndClearsSourceBytes()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        byte[] bytes = Encoding.UTF8.GetBytes(AuthoringJson);
        var module = context.JSInterop.SetupModule("./browser-definition-source.js");
        module.Setup<byte[]>("fetchDefinition", _ => true).SetResult(bytes);
        var request = BrowserDefinitionPointer.Parse("#src=https%3A%2F%2Fpublisher.example%2Fdefinition.json");
        await Assert.That(async () => { await source.FetchAsync(request); }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Definitions).IsEmpty();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task PublicSourceAndSelectorsAreDecodedWithoutAnswerTransfer()
    {
        BrowserDefinitionPointer request = BrowserDefinitionPointer.Parse(
            "#src=https%3A%2F%2Fpublisher.example%2Fdefinition.json&entry=setup&locale=en&appVersion=1.0");
        await Assert.That(request.Source.AbsoluteUri).IsEqualTo("https://publisher.example/definition.json");
        await Assert.That(request.Entry).IsEqualTo("setup");
        await Assert.That(request.AppVersion).IsEqualTo("1.0");
    }

    [Test]
    public async Task UnsafeSourcesAndAmbiguousMetadataRejectWithoutEcho()
    {
        string[] fragments =
        [
            "#src=http%3A%2F%2Fpublisher.example%2Fdefinition.json",
            "#src=https%3A%2F%2Fsecret%40publisher.example%2Fa.json",
            "#src=https%3A%2F%2Flocalhost%2Fa.json",
            "#src=https%3A%2F%2F127.0.0.1%2Fa.json",
            "#src=https%3A%2F%2F10.0.0.1%2Fa.json",
            "#src=https%3A%2F%2F169.254.169.254%2Fa.json",
            "#src=https%3A%2F%2F%5B%3A%3A1%5D%2Fa.json",
            "#src=https%3A%2F%2Fpublisher.local%2Fa.json",
            "#src=https%3A%2F%2Fpublisher.example%2Fa.json%3Ftoken%3Dprivate-canary",
            "#src=https%3A%2F%2Fpublisher.example%2Fa.json%23private-canary",
            "#src=https%3A%2F%2Fpublisher.example%2Fa.json&%73rc=https%3A%2F%2Fother.example%2Fa.json",
            "#src=https%3A%2F%2Fpublisher.example%2Fa.json&answer=private-canary",
            "#src=%xy", "#src=", "#digest=" + new string('a', 64),
            "#src=https%3A%2F%2Fpublisher.example%2Fa.json&digest=bad",
            "#src=" + new string('a', 4096)
        ];
        foreach (string fragment in fragments)
        {
            BrowserLaunchException? failure = null;
            try { _ = BrowserDefinitionPointer.Parse(fragment); }
            catch (BrowserLaunchException exception) { failure = exception; }
            await Assert.That(failure).IsNotNull();
            await Assert.That(failure!.Message).DoesNotContain("private-canary");
            await Assert.That(failure.Message).DoesNotContain("publisher");
        }
        await Assert.That(() => BrowserDefinitionPointer.Parse(
            "#src=https%3A%2F%2Fpublisher.example%2Fa.json", "?answer=private-canary"))
            .Throws<BrowserLaunchException>();
    }

    [Test]
    public async Task LocalAdmissionKeepsCanonicalIdentityAndRejectsRevisionConflicts()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        using var input = new MemoryStream(BrowserFixture.Definition.CanonicalBytes.ToArray());
        SetupDefinition admitted = await source.ReadLocalAsync(input);
        await Assert.That(admitted.Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        JsonNode conflict = JsonNode.Parse(admitted.CanonicalBytes.Span)!;
        conflict["project"]!["name"] = "Different content at the same revision";
        using var invalid = new MemoryStream(Encoding.UTF8.GetBytes(conflict.ToJsonString()));
        await Assert.That(async () => { await source.ReadLocalAsync(invalid); }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Resolve(admitted.FamilyId).Digest).IsEqualTo(admitted.Digest);
    }

    [Test]
    public async Task LocalReadRejectsOverflowAndHostileBytesWithoutPublication()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        using var oversized = new MemoryStream(new byte[4 * 1024 * 1024 + 1]);
        await Assert.That(async () => { await source.ReadLocalAsync(oversized); }).Throws<BrowserLaunchException>();
        using var hostile = new MemoryStream(Encoding.UTF8.GetBytes("{\"answer\":\"private-canary\"}"));
        await Assert.That(async () => { await source.ReadLocalAsync(hostile); }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Definitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task RemoteSelectionPinsCanonicalDigestAndClearsTransportBytes()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        byte[] bytes = BrowserFixture.Definition.CanonicalBytes.ToArray();
        var module = context.JSInterop.SetupModule("./browser-definition-source.js");
        module.Setup<byte[]>("fetchDefinition", "https://publisher.example/definition.json", BrowserDefinitionSource.MaximumDocumentBytes).SetResult(bytes);
        var request = BrowserDefinitionPointer.Parse("#src=https%3A%2F%2Fpublisher.example%2Fdefinition.json&digest=" + BrowserFixture.Definition.Digest.Value);
        BrowserLaunchSelection selected = await source.FetchAsync(request);
        await Assert.That(selected.Definition.Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        await Assert.That(bytes.All(item => item == 0)).IsTrue();
        await Assert.That(registry.Current.Definitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DigestAndSelectorsRejectBeforeRegistryPublication()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        var module = context.JSInterop.SetupModule("./browser-definition-source.js");
        foreach (string selector in new[] { "digest=" + new string('f', 64), "appVersion=unknown", "entry=unknown", "locale=unknown" })
        {
            module.Setup<byte[]>("fetchDefinition", "https://publisher.example/definition.json", BrowserDefinitionSource.MaximumDocumentBytes)
                .SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
            var request = BrowserDefinitionPointer.Parse("#src=https%3A%2F%2Fpublisher.example%2Fdefinition.json&" + selector);
            await Assert.That(async () => { await source.FetchAsync(request); }).Throws<BrowserLaunchException>();
            await Assert.That(registry.Current.Definitions).IsEmpty();
        }
    }

    [Test]
    public async Task LocalReadIgnoringCancellationCannotAdmitAfterInvalidation()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        using var input = new GatedDefinitionStream(Encoding.UTF8.GetBytes(AuthoringJson));
        Task<SetupDefinition> read = source.ReadLocalAsync(input);
        await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.Invalidate();
        input.Release.SetResult();
        await Assert.That(async () => { await read; }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Definitions).IsEmpty();
        await Assert.That(input.Buffer!.All(item => item == 0)).IsTrue();
    }

    [Test]
    public async Task WorkspaceStartingDuringLocalReadRejectsLateDefinitionAdmission()
    {
        await using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var javascript = context.Services.GetRequiredService<IJSRuntime>();
        var lifecycle = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        lifecycle.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        lifecycle.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        await using var policy = new BrowserOfflinePolicy(javascript);
        await using var source = new BrowserDefinitionSource(registry, javascript, policy);
        using var input = new GatedDefinitionStream(BrowserFixture.Definition.CanonicalBytes.ToArray());
        Task<SetupDefinition> pending = source.ReadLocalAsync(input);
        await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { await Assert.That(await policy.BeginWorkspaceAsync()).IsTrue(); }
        finally { input.Release.TrySetResult(); }
        await Assert.That(async () => { await pending; }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Definitions).IsEmpty();
        await Assert.That(input.Buffer!.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task CleanRouteInvalidatesLateFetchAndRestoresLocalDefinitionPicker()
    {
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        var lifecycle = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        lifecycle.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        lifecycle.Setup<string[]>("retainedDigests").SetResult([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var transport = context.JSInterop.SetupModule("./browser-definition-source.js");
        var pending = transport.Setup<byte[]>("fetchDefinition", "https://publisher.example/definition.json", BrowserDefinitionSource.MaximumDocumentBytes);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/open#src=https%3A%2F%2Fpublisher.example%2Fdefinition.json");
        var component = context.Render<App>();
        await Assert.That(component.Find("#definition-file").HasAttribute("disabled")).IsTrue();
        await component.InvokeAsync(() => navigation.NavigateTo("/open"));
        await Assert.That(component.Find("#definition-file").HasAttribute("disabled")).IsFalse();
        pending.SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        await component.InvokeAsync(() => { });
        await Assert.That(component.FindAll(".configuration-editor")).IsEmpty();
        await Assert.That(component.FindAll("[data-code=source-admitted]")).IsEmpty();
    }

    [Test]
    public async Task LateWorkspaceDiscardCannotEraseNewSourceSelection()
    {
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        context.JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var lifecycle = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        lifecycle.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        lifecycle.Setup<string[]>("retainedDigests").SetResult([]);
        lifecycle.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        context.Services.AddSingleton(policy);
        var transport = context.JSInterop.SetupModule("./browser-definition-source.js");
        transport.Setup<byte[]>("fetchDefinition", "https://publisher.example/definition.json", BrowserDefinitionSource.MaximumDocumentBytes)
            .SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        var exports = context.JSInterop.SetupModule("./browser-artifacts.js");
        exports.SetupVoid("open", _ => true).SetVoidResult();
        exports.Setup<bool>("copy", _ => true).SetResult(true);
        var cleanup = exports.SetupVoid("invalidate", _ => true);
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/open#src=https%3A%2F%2Fpublisher.example%2Fdefinition.json");
        var component = context.Render<App>();
        component.FindAll("button").Single(button => button.TextContent == "Open configuration workspace").Click();
        await BrowserFixture.NotifyChoiceAsync(component, "mode", "single");
        component.Find("[data-field='hostname'] input").Input("example.test");
        component.Find("#prepare-draft").Click();
        component.Find(".configuration-editor__artifact button").Click();
        Task discard = component.FindAll("button").Single(button => button.TextContent == "Discard this workspace to choose another definition")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await Assert.That(cleanup.Invocations.Count).IsEqualTo(1);
        transport.Setup<byte[]>("fetchDefinition", "https://other.example/definition.json", BrowserDefinitionSource.MaximumDocumentBytes)
            .SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        await component.InvokeAsync(() => navigation.NavigateTo("/open#src=https%3A%2F%2Fother.example%2Fdefinition.json"));
        await Assert.That(component.Find("[data-code=source-admitted]").TextContent).Contains("admitted");
        cleanup.SetVoidResult();
        await discard.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(component.Find("[data-code=source-admitted]").TextContent).Contains("admitted");
        await Assert.That(component.FindAll("button").Any(button => button.TextContent == "Open configuration workspace")).IsTrue();
    }

    private sealed class GatedDefinitionStream(byte[] bytes) : MemoryStream(bytes)
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public byte[]? Buffer { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment)) Buffer = segment.Array;
            Entered.TrySetResult();
            await Release.Task;
            return await base.ReadAsync(buffer, CancellationToken.None);
        }
    }
}
