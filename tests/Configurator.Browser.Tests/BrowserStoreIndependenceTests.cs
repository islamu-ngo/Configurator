namespace Configurator.Browser.Tests;

using System.Text;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserStoreIndependenceTests
{
    [Test]
    public async Task MissingMalformedAndEmptyStoreLeaveLocalAdmissionUsable()
    {
        foreach (byte[] bytes in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("not JSON"), Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"projects\":[]}") })
        {
            var registry = new BrowserDefinitionRegistry([]);
            using var context = BrowserFixture.Context(registry);
            await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
            await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
            context.Services.AddSingleton(catalog);
            var module = context.JSInterop.SetupModule("./browser-store.js");
            module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(bytes);
            var component = context.Render<Pages.Store>();
            await Assert.That(component.Find("a[href=open]")).IsNotNull();
            await Assert.That(component.FindAll("[role=status]")).Count().IsEqualTo(1);
            await using var source = new BrowserDefinitionSource(registry, context.JSInterop.JSRuntime, policy);
            using var input = new MemoryStream(BrowserFixture.Definition.CanonicalBytes.ToArray());
            SetupDefinition admitted = await source.ReadLocalAsync(input);
            await Assert.That(admitted.Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        }
    }

    [Test]
    public async Task NamesAndTagsFilterInStableOrderAndMetadataIsEscaped()
    {
        var registry = new BrowserDefinitionRegistry([]);
        using var context = BrowserFixture.Context(registry);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
        context.Services.AddSingleton(catalog);
        var index = BrowserDefinitionStoreTests.Index();
        var other = index["projects"]![0]!.DeepClone();
        other["projectId"] = "another";
        other["name"] = "alpha";
        other["revisions"]![0]!["snapshotPath"] = "definitions/another/1.json";
        index["projects"]![0]!["name"] = "Zulu <script>metadata</script>";
        index["projects"]!.AsArray().Add(other);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(index.ToJsonString()));
        var component = context.Render<Pages.Store>();
        await Assert.That(component.FindAll("article h2 a")[1].TextContent).IsEqualTo("Zulu <script>metadata</script>");
        await Assert.That(component.FindAll("article h2 a")[0].TextContent).IsEqualTo("alpha");
        await Assert.That(component.FindAll("script")).IsEmpty();
        await component.Find("#store-search").InputAsync("zULu");
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await component.Find("#store-search").InputAsync(string.Empty);
        await component.Find("#store-tag").ChangeAsync("self-hosted");
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(2);
        await component.Find("#store-search").InputAsync("no-matching-project");
        await Assert.That(component.FindAll("article")).IsEmpty();
        await Assert.That(component.FindAll(".notice h2")).Count().IsEqualTo(1);
        await component.Find("#store-search").InputAsync(string.Empty);
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll(".notice h2")).IsEmpty();
    }

    [Test]
    public async Task ConfigureRequiresExplicitRevisionAndCanonicalReviewBeforePinnedLaunch()
    {
        var registry = new BrowserDefinitionRegistry([]);
        using var context = BrowserFixture.Context(registry);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
        context.Services.AddSingleton(catalog);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(BrowserDefinitionStoreTests.Index().ToJsonString()));
        module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes).SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        var component = context.Render<Pages.StoreProject>(parameters => parameters.Add(page => page.ProjectId, "demo"));
        await Assert.That(component.FindAll("button")).IsEmpty();
        await component.Find("#store-revision").ChangeAsync("1");
        await component.Find("button").ClickAsync(new());
        await Assert.That(registry.Current.Resolve(BrowserFixture.Definition.FamilyId).Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        await component.Find("button").ClickAsync(new());
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        await Assert.That(navigation.Uri).Contains($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
    }
}
