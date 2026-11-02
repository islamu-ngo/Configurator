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
        foreach (byte[] bytes in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("not JSON"), Encoding.UTF8.GetBytes("{\"schemaVersion\":\"0.0.1\",\"projects\":[]}") })
        {
            var registry = new BrowserDefinitionRegistry([]);
            using var context = BrowserFixture.Context(registry);
            await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
            await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
            context.Services.AddSingleton(catalog);
            var module = context.JSInterop.SetupModule("./browser-store.js");
            module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(bytes);
            var component = context.Render<Pages.Catalogue>();
            await Assert.That(component.Find("a[href=open]")).IsNotNull();
            await Assert.That(component.Find(".catalogue__directory-status[role=status]")).IsNotNull();
            await using var source = new BrowserDefinitionSource(registry, context.JSInterop.JSRuntime, policy);
            using var input = new MemoryStream(BrowserFixture.Definition.CanonicalBytes.ToArray());
            Definition admitted = await source.ReadLocalAsync(input);
            await Assert.That(admitted.Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        }
    }

    [Test]
    public async Task CanonicalMatchesMergeSourcesAndSearchNamesAndTagsWithoutRenderingMetadata()
    {
        var registry = new BrowserDefinitionRegistry([]);
        await using var context = BrowserFixture.Context(registry);
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
        var component = context.Render<Pages.Catalogue>();
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await Assert.That(component.Find("details a[href='store/demo?revision=1']").TextContent).Contains("Zulu <script>metadata</script>");
        await Assert.That(component.Find("details a[href='store/another?revision=1']").TextContent).Contains("alpha");
        await Assert.That(component.FindAll("script")).IsEmpty();
        await component.Find("#catalogue-search").InputAsync("zULu");
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await component.Find("#catalogue-search").InputAsync("alpha");
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await component.Find("#catalogue-search").InputAsync("self-hosted");
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await component.Find("#catalogue-search").InputAsync("no-matching-project");
        await Assert.That(component.FindAll("article")).IsEmpty();
        await Assert.That(component.Find("a[href=open]")).IsNotNull();
        await component.Find("#catalogue-search").InputAsync(string.Empty);
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ConfigureAdmitsExactRevisionAndOpensWithOneAction()
    {
        var registry = new BrowserDefinitionRegistry([]);
        await using var context = BrowserFixture.Context(registry);
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
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        await Assert.That(navigation.Uri).Contains($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
    }

    [Test]
    public async Task ApplicationGroupingRetainsDifferentFamiliesAndDoesNotGroupByTitle()
    {
        await using var context = BrowserFixture.Context();
        var index = BrowserDefinitionStoreTests.Index();
        var project = index["projects"]![0]!;
        project["familyId"] = "71cdd5fa-4adc-49fb-9f6a-349807670c31";
        var unrelated = project.DeepClone();
        unrelated["projectId"] = "unrelated";
        unrelated["applicationId"] = "unrelated.application";
        unrelated["familyId"] = "61cdd5fa-4adc-49fb-9f6a-349807670c31";
        unrelated["revisions"]![0]!["snapshotPath"] = "definitions/unrelated/1.json";
        index["projects"]!.AsArray().Add(unrelated);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes)
            .SetResult(Encoding.UTF8.GetBytes(index.ToJsonString()));
        var component = context.Render<Pages.Catalogue>();

        await Assert.That(component.FindAll("article")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll("article a[href^='launch/']")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("article details a[href='store/demo?revision=1']")).Count().IsEqualTo(1);
        await Assert.That(context.Services.GetRequiredService<BrowserDefinitionRegistry>().Current.Definitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task InvalidRequestedRevisionNeverFallsBackToAnotherCandidate()
    {
        await using var context = BrowserFixture.Context();
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes)
            .SetResult(Encoding.UTF8.GetBytes(BrowserDefinitionStoreTests.Index().ToJsonString()));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("store/demo?revision=999");
        var component = context.Render<Pages.StoreProject>(parameters => parameters.Add(page => page.ProjectId, "demo"));
        await Assert.That(component.FindAll("[data-code='store-revision-unavailable']")).Count().IsEqualTo(1);
        await Assert.That(component.Find("#store-revision").GetAttribute("aria-describedby")).IsEqualTo("store-revision-error");
        await Assert.That(context.JSInterop.Invocations.Any(call => call.Identifier == "Blazor._internal.domWrapper.focus")).IsTrue();
        await Assert.That(component.FindAll("button")).IsEmpty();
        await Assert.That(module.Invocations.Count(call => call.Identifier == "readAsset")).IsEqualTo(1);
    }

    [Test]
    public async Task ClaimedDigestCannotHideConflictingDirectoryMetadataBehindLocalSource()
    {
        await using var context = BrowserFixture.Context();
        var index = BrowserDefinitionStoreTests.Index();
        index["projects"]![0]!["revisions"]![0]!["publisher"] = "Conflicting publisher label";
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes)
            .SetResult(Encoding.UTF8.GetBytes(index.ToJsonString()));
        module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes)
            .SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        var component = context.Render<Pages.Catalogue>();
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("article details section")).Count().IsEqualTo(2);
        await component.Find("article details button").ClickAsync(new());
        await Assert.That(component.FindAll("[data-code='store-snapshot-mismatch']")).Count().IsEqualTo(1);
        await Assert.That(context.Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/");
        await Assert.That(context.Services.GetRequiredService<BrowserDefinitionRegistry>().Current.Definitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ConflictingDirectoryClaimsRemainSeparateSelectableCandidates()
    {
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        var index = BrowserDefinitionStoreTests.Index();
        var alternative = index["projects"]![0]!.DeepClone();
        alternative["projectId"] = "another";
        alternative["revisions"]![0]!["snapshotPath"] = "definitions/another/1.json";
        index["projects"]![0]!["revisions"]![0]!["publisher"] = "Conflicting publisher label";
        index["projects"]!.AsArray().Add(alternative);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes)
            .SetResult(Encoding.UTF8.GetBytes(index.ToJsonString()));
        module.Setup<byte[]>("readAsset", "definitions/another/1.json", BrowserDefinitionSource.MaximumDocumentBytes)
            .SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        var component = context.Render<Pages.Catalogue>();
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("article details section")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll("[data-action='open-directory']")).IsEmpty();
        var source = component.Find("a[href='store/another?revision=1']").ParentElement!.ParentElement!;
        await source.QuerySelector("button")!.ClickAsync(new());
        await Assert.That(context.Services.GetRequiredService<NavigationManager>().Uri)
            .Contains($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DirectoryActionAdmitsBeforeNavigationAndDisposalRejectsLateSnapshots(bool dispose)
    {
        var registry = new BrowserDefinitionRegistry([]);
        await using var context = BrowserFixture.Context(registry);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes)
            .SetResult(Encoding.UTF8.GetBytes(BrowserDefinitionStoreTests.Index().ToJsonString()));
        var pending = module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes);
        var component = context.Render<Pages.Catalogue>();
        NavigationManager navigation = context.Services.GetRequiredService<NavigationManager>();
        Task click = component.Find("[data-action='open-directory']").ClickAsync(new());
        await Assert.That(pending.Invocations).Count().IsEqualTo(1);
        await Assert.That(registry.Current.Definitions).IsEmpty();
        await Assert.That(navigation.Uri).IsEqualTo("http://localhost/");
        if (dispose) await component.InvokeAsync(component.Instance.Dispose);
        pending.SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        await click.WaitAsync(TimeSpan.FromSeconds(5));
        if (dispose)
        {
            await Assert.That(registry.Current.Definitions).IsEmpty();
            await Assert.That(navigation.Uri).IsEqualTo("http://localhost/");
        }
        else
        {
            await Assert.That(registry.Current.Definitions).Count().IsEqualTo(1);
            await Assert.That(navigation.Uri).Contains($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        }
    }
}
