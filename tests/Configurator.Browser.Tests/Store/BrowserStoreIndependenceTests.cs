namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Presentation.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

public sealed class BrowserStoreIndependenceTests
{
    [Test]
    public async Task MissingMalformedAndEmptyStoreLeaveLocalAdmissionUsable()
    {
        Func<CancellationToken, Task<IReadOnlyList<DefinitionStoreProject>>>[] failures =
        [
            _ => Task.FromException<IReadOnlyList<DefinitionStoreProject>>(new DefinitionStoreException("store-unavailable")),
            _ => Task.FromResult(DefinitionStoreIndexParser.ParseIndex("private-canary"u8.ToArray())),
            _ => Task.FromResult<IReadOnlyList<DefinitionStoreProject>>([])
        ];

        foreach (Func<CancellationToken, Task<IReadOnlyList<DefinitionStoreProject>>> load in failures)
        {
            await using var context = BrowserFixture.Context();
            context.Services.AddSingleton<IDefinitionStoreProvider>(new TestDefinitionStoreProvider(load: load));

            var component = context.Render<Pages.Catalogue>();
            await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
            await Assert.That(component.Find("a[href^='launch/']").GetAttribute("href"))
                .IsEqualTo($"launch/{BrowserFixture.Family}?digest={BrowserFixture.Definition.Digest.Value}");
        }
    }

    [Test]
    public async Task CanonicalMatchesMergeSourcesAndSearchNamesAndTagsWithoutRenderingMetadata()
    {
        await using var context = BrowserFixture.Context();
        DefinitionStoreProject demo = BrowserDefinitionStoreTests.CreateProject("demo", "Demo Portal", BrowserFixture.Definition) with
        {
            Description = "<script>alert(1)</script>Public metadata",
            Tags = ["identity", "directory"]
        };
        context.Services.AddSingleton<IDefinitionStoreProvider>(
            TestDefinitionStoreProvider.FromIndex([demo], ("demo", BrowserFixture.Definition.Revision, BrowserFixture.Definition)));

        var component = context.Render<Pages.Catalogue>();
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await Assert.That(component.Find("article").TextContent).Contains("Demo Portal");
        await Assert.That(component.FindAll("script")).IsEmpty();

        await component.Find("#catalogue-search").InputAsync(new ChangeEventArgs { Value = "Demo Portal" });
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await component.Find("#catalogue-search").InputAsync(new ChangeEventArgs { Value = "identity" });
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await component.Find("#catalogue-search").InputAsync(new ChangeEventArgs { Value = "<script>alert(1)</script>" });
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await component.Find("#catalogue-search").InputAsync(new ChangeEventArgs { Value = "nomatch" });
        await Assert.That(component.FindAll("article")).IsEmpty();
        await component.Find("#catalogue-search").InputAsync(new ChangeEventArgs { Value = "" });
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ConfigureAdmitsExactRevisionAndOpensWithOneAction()
    {
        await using var context = BrowserFixture.Context();
        Definition other = BrowserDefinitionStoreTests.WithIdentity(
            BrowserFixture.Definition,
            Guid.Parse("8e4db644-29d7-4f88-90b5-a8b8aa4f8d22"),
            "publisher.example.store",
            1);
        DefinitionStoreProject project = BrowserDefinitionStoreTests.CreateProject("demo", "Demo Store", other);
        context.Services.AddSingleton<IDefinitionStoreProvider>(
            TestDefinitionStoreProvider.FromIndex([project], ("demo", other.Revision, other)));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("store/demo");

        var component = context.Render<Pages.StoreProject>(parameters => parameters.Add(page => page.ProjectId, "demo"));
        await Assert.That(component.FindAll("button")).IsEmpty();
        await component.Find("#store-revision").ChangeAsync(new ChangeEventArgs { Value = "1" });
        await component.Find("button").ClickAsync(new MouseEventArgs());

        await Assert.That(navigation.Uri).EndsWith($"launch/{other.FamilyId:D}?digest={other.Digest.Value}");
        await Assert.That(context.Services.GetRequiredService<BrowserDefinitionRegistry>().Current.Definitions
            .Any(item => item.Digest == other.Digest)).IsTrue();
    }

    [Test]
    public async Task ApplicationGroupingRetainsDifferentFamiliesAndDoesNotGroupByTitle()
    {
        await using var context = BrowserFixture.Context();
        Definition differentFamily = BrowserDefinitionStoreTests.WithIdentity(
            BrowserFixture.Definition,
            Guid.Parse("d5c6dd9c-df05-40a2-b680-3265d4ea715d"),
            BrowserFixture.Definition.ApplicationId,
            1);
        Definition sameTitleDifferentApp = BrowserDefinitionStoreTests.WithIdentity(
            BrowserFixture.Definition,
            Guid.Parse("ec8dc8d0-0ea5-4a48-90d8-cf40275e9a3d"),
            "other.application",
            1);
        DefinitionStoreProject sameApp = BrowserDefinitionStoreTests.CreateProject("demo", BrowserFixture.Definition.Title, differentFamily);
        DefinitionStoreProject otherApp = BrowserDefinitionStoreTests.CreateProject("other", BrowserFixture.Definition.Title, sameTitleDifferentApp);
        context.Services.AddSingleton<IDefinitionStoreProvider>(
            TestDefinitionStoreProvider.FromIndex(
                [sameApp, otherApp],
                ("demo", differentFamily.Revision, differentFamily),
                ("other", sameTitleDifferentApp.Revision, sameTitleDifferentApp)));

        var component = context.Render<Pages.Catalogue>();
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll("article a[href^='launch/']")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("article details a[href='store/demo?revision=1']")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task InvalidRequestedRevisionNeverFallsBackToAnotherCandidate()
    {
        await using var context = BrowserFixture.Context();
        Definition second = Revision(BrowserFixture.Definition, 2);
        DefinitionStoreProject demo = BrowserDefinitionStoreTests.CreateProject("demo", "Demo", BrowserFixture.Definition, second);
        context.Services.AddSingleton<IDefinitionStoreProvider>(
            TestDefinitionStoreProvider.FromIndex(
                [demo],
                ("demo", BrowserFixture.Definition.Revision, BrowserFixture.Definition),
                ("demo", second.Revision, second)));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("store/demo?revision=99");

        var component = context.Render<Pages.StoreProject>(parameters => parameters.Add(page => page.ProjectId, "demo"));
        await Assert.That(component.FindAll("[data-code='store-revision-unavailable']")).Count().IsEqualTo(1);
        await Assert.That(component.Find("#store-revision").GetAttribute("value")).IsEmpty();
        await Assert.That(component.FindAll("button")).IsEmpty();
    }

    [Test]
    public async Task ClaimedDigestCannotHideConflictingDirectoryMetadataBehindLocalSource()
    {
        await using var context = BrowserFixture.Context();
        Definition modified = BrowserDefinitionStoreTests.WithTitle(BrowserFixture.Definition, "Different title");
        DefinitionStoreProject demo = BrowserDefinitionStoreTests.CreateProject("demo", "Demo", modified);
        demo = demo with { Revisions = [demo.Revisions[0] with { Digest = BrowserFixture.Definition.Digest.Value }] };
        context.Services.AddSingleton<IDefinitionStoreProvider>(
            TestDefinitionStoreProvider.FromIndex([demo], ("demo", modified.Revision, modified)));

        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var component = context.Render<Pages.Catalogue>();
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("article details section")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll("article details a[href='store/demo?revision=1']")).Count().IsEqualTo(1);

        navigation.NavigateTo("store/demo?revision=1");
        var projectPage = context.Render<Pages.StoreProject>(parameters => parameters.Add(page => page.ProjectId, "demo"));
        await projectPage.Find("button").ClickAsync(new MouseEventArgs());
        await Assert.That(projectPage.FindAll("[data-code='store-snapshot-mismatch']")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ConflictingDirectoryClaimsRemainSeparateSelectableCandidates()
    {
        await using var context = BrowserFixture.Context(new BrowserDefinitionRegistry([]));
        Definition first = BrowserDefinitionStoreTests.WithIdentity(
            BrowserFixture.Definition,
            Guid.Parse("2357b45a-43e1-49d0-94bd-fd41a8cf0011"),
            "shared.application",
            1);
        Definition second = BrowserDefinitionStoreTests.WithIdentity(
            BrowserFixture.Definition,
            Guid.Parse("82964f9f-b865-4ec4-9e77-b84c714fb81c"),
            "shared.application",
            2);
        DefinitionStoreProject projectOne = BrowserDefinitionStoreTests.CreateProject("claim-one", "Shared App", first);
        DefinitionStoreProject projectTwo = BrowserDefinitionStoreTests.CreateProject("claim-two", "Shared App", second);
        context.Services.AddSingleton<IDefinitionStoreProvider>(
            TestDefinitionStoreProvider.FromIndex(
                [projectOne, projectTwo],
                ("claim-one", first.Revision, first),
                ("claim-two", second.Revision, second)));

        var component = context.Render<Pages.Catalogue>();
        await Assert.That(component.FindAll("article")).Count().IsEqualTo(1);
        await Assert.That(component.FindAll("article details section")).Count().IsEqualTo(2);
        await Assert.That(component.FindAll("[data-action='open-directory']")).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DirectoryActionAdmitsBeforeNavigationAndDisposalRejectsLateSnapshots(bool dispose)
    {
        await using var context = BrowserFixture.Context();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<DefinitionStoreResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Definition storeDefinition = BrowserDefinitionStoreTests.WithIdentity(
            BrowserFixture.Definition,
            Guid.Parse("5df3b349-8ee2-4b02-b8df-40e557eb52f1"),
            "publisher.example.app",
            1);
        DefinitionStoreProject demo = BrowserDefinitionStoreTests.CreateProject("demo", "Demo Store", storeDefinition);
        context.Services.AddSingleton<IDefinitionStoreProvider>(new TestDefinitionStoreProvider(
            load: _ => Task.FromResult<IReadOnlyList<DefinitionStoreProject>>([demo]),
            open: (_, _, _) =>
            {
                started.TrySetResult();
                return pending.Task;
            }));
        var registry = context.Services.GetRequiredService<BrowserDefinitionRegistry>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var component = context.Render<Pages.Catalogue>();

        Task click = component.InvokeAsync(() => component.Find("[data-action='open-directory']").ClickAsync(new MouseEventArgs()));
        await started.Task;
        if (dispose) await context.DisposeComponentsAsync();
        pending.SetResult(new DefinitionStoreResult.Success(storeDefinition));
        await click;

        if (dispose)
        {
            await Assert.That(registry.Current.Definitions.Any(item => item.Digest == storeDefinition.Digest)).IsFalse();
            await Assert.That(navigation.Uri).DoesNotContain(storeDefinition.FamilyId.ToString("D"));
        }
        else
        {
            await Assert.That(registry.Current.Definitions.Any(item => item.Digest == storeDefinition.Digest)).IsTrue();
            await Assert.That(navigation.Uri).EndsWith($"launch/{storeDefinition.FamilyId:D}?digest={storeDefinition.Digest.Value}");
        }
    }

    [Test]
    public async Task EmbeddedStoreRendersInCatalogueAndOpensSelectedProjectRevision()
    {
        await using var context = BrowserFixture.Context();
        var navigation = context.Services.GetRequiredService<NavigationManager>();

        var catalogue = context.Render<Pages.Catalogue>();
        await Assert.That(catalogue.FindAll("article").Any(card => card.TextContent.Contains("Minimal self-hosted service", StringComparison.Ordinal))).IsTrue();

        navigation.NavigateTo("store/minimal-self-hosted");
        var projectPage = context.Render<Pages.StoreProject>(parameters => parameters.Add(page => page.ProjectId, "minimal-self-hosted"));
        await projectPage.Find("#store-revision").ChangeAsync(new ChangeEventArgs { Value = "1" });
        await projectPage.Find("button").ClickAsync(new MouseEventArgs());

        await Assert.That(navigation.Uri).Contains("launch/");
        await Assert.That(navigation.Uri).Contains("digest=");
    }

    private static Definition Revision(Definition source, long revision)
    {
        JsonNode node = JsonNode.Parse(source.CanonicalBytes.Span)!;
        node["revision"] = revision;
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
    }
}
