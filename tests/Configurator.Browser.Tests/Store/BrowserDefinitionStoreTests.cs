namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Models;
using Configurator.Browser.Services;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Presentation.Store;

public sealed class BrowserDefinitionStoreTests
{
    [Test]
    public async Task IndexParserFailuresMapToValueSafeLaunchExceptionsWithoutEcho()
    {
        using var context = new BunitContext();
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);

        string[] invalidIndexes =
        [
            "private-canary",
            "{\"schemaVersion\":\"0.0.1\",\"projects\":null}",
            "{\"schemaVersion\":\"unsupported\",\"projects\":[]}"
        ];
        foreach (string document in invalidIndexes)
        {
            var provider = new TestDefinitionStoreProvider(
                load: _ => Task.FromResult(DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(document))));
            await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

            BrowserLaunchException? failure = null;
            try { _ = await catalog.LoadAsync(); }
            catch (BrowserLaunchException caught) { failure = caught; }

            await Assert.That(failure).IsNotNull();
            await Assert.That(failure!.Code).IsEqualTo("store-index-invalid");
            await Assert.That(failure.Message).IsEqualTo("store-index-invalid");
            await Assert.That(failure.Message).DoesNotContain("private-canary");
        }
    }

    [Test]
    public async Task SnapshotMismatchCannotPublishIntoRegistry()
    {
        using var context = new BunitContext();
        Definition definition = WithTitle(BrowserFixture.Definition, "Updated public title");
        DefinitionStoreProject project = CreateProject("demo", "Demo", BrowserFixture.Definition);
        var provider = TestDefinitionStoreProvider.FromIndex([project], ("demo", BrowserFixture.Definition.Revision, definition));
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

        BrowserLaunchException? failure = null;
        try { _ = await catalog.OpenAsync("demo", BrowserFixture.Definition.Revision); }
        catch (BrowserLaunchException caught) { failure = caught; }

        await Assert.That(failure).IsNotNull();
        await Assert.That(failure!.Code).IsEqualTo("store-snapshot-mismatch");
        await Assert.That(registry.Current.Definitions).IsEmpty();
    }

    [Test]
    public async Task ProviderFailuresMapToValueSafeBrowserLaunchCodesWithoutRegistryAdmission()
    {
        using var context = new BunitContext();
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);

        (DefinitionStoreResult Result, string ExpectedCode)[] cases =
        [
            (new DefinitionStoreResult.ProjectNotFound(), "store-revision-unavailable"),
            (new DefinitionStoreResult.RevisionNotFound(), "store-revision-unavailable"),
            (new DefinitionStoreResult.SnapshotMismatch(), "store-snapshot-mismatch"),
            (new DefinitionStoreResult.CorruptAsset(), "store-unavailable")
        ];

        foreach ((DefinitionStoreResult result, string expectedCode) in cases)
        {
            var provider = new TestDefinitionStoreProvider(open: (_, _, _) => Task.FromResult(result));
            await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

            BrowserLaunchException? failure = null;
            try { _ = await catalog.OpenAsync("demo", 1); }
            catch (BrowserLaunchException caught) { failure = caught; }

            await Assert.That(failure).IsNotNull();
            await Assert.That(failure!.Code).IsEqualTo(expectedCode);
            await Assert.That(registry.Current.Definitions).IsEmpty();
        }
    }

    [Test]
    public async Task ConflictingPreviouslyAdmittedRevisionRejectsAfterRegistryRemoval()
    {
        using var context = new BunitContext();
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        registry.Replace([]);
        Definition conflicting = WithTitle(BrowserFixture.Definition, "Conflicting snapshot");
        DefinitionStoreProject project = CreateProject("demo", "Demo", conflicting);
        var provider = TestDefinitionStoreProvider.FromIndex([project], ("demo", conflicting.Revision, conflicting));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

        BrowserLaunchException? failure = null;
        try { _ = await catalog.OpenAsync("demo", conflicting.Revision); }
        catch (BrowserLaunchException caught) { failure = caught; }

        await Assert.That(failure?.Code).IsEqualTo("revision-conflict");
        await Assert.That(registry.Current.Definitions).IsEmpty();
    }

    [Test]
    public async Task EmbeddedStoreRemainsReadableOfflineButBlockedDuringAnswerCustody()
    {
        using var context = new BunitContext();
        var pwa = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        pwa.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        pwa.Setup<long>("subscribeConnectivity", _ => true).SetResult(1L);
        pwa.SetupVoid("unsubscribeConnectivity", 1L).SetVoidResult();
        pwa.Setup<bool>("canReadPublicAssets").SetResult(true);
        pwa.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        pwa.SetupVoid("endWorkspace", _ => true).SetVoidResult();

        using var provider = new EmbeddedDefinitionStoreProvider();
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        policy.ConnectivityChanged(false);
        await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

        IReadOnlyList<DefinitionStoreProject> projects = await catalog.LoadAsync();
        DefinitionStoreProject sample = projects.Single(item => item.ProjectId == "minimal-self-hosted");
        Definition opened = await catalog.OpenAsync(sample.ProjectId, sample.Revisions[0].Revision);
        await Assert.That(policy.CanFetchDefinition).IsFalse();
        await Assert.That(opened.Digest.Value).IsEqualTo(sample.Revisions[0].Digest);

        await policy.BeginWorkspaceAsync();
        BrowserLaunchException? loadFailure = null;
        try { _ = await catalog.LoadAsync(); }
        catch (BrowserLaunchException caught) { loadFailure = caught; }
        BrowserLaunchException? openFailure = null;
        try { _ = await catalog.OpenAsync(sample.ProjectId, sample.Revisions[0].Revision); }
        catch (BrowserLaunchException caught) { openFailure = caught; }

        await Assert.That(loadFailure?.Code).IsEqualTo("store-active");
        await Assert.That(openFailure?.Code).IsEqualTo("store-active");
        await policy.EndWorkspaceAsync();
    }

    [Test]
    public async Task LateSnapshotCannotAdmitAcrossAnAnswerWorkspaceGenerationOrSupersedingLoad()
    {
        using var context = new BunitContext();
        var pwa = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        pwa.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        pwa.Setup<long>("subscribeConnectivity", _ => true).SetResult(1L);
        pwa.SetupVoid("unsubscribeConnectivity", 1L).SetVoidResult();
        pwa.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        pwa.SetupVoid("endWorkspace", _ => true).SetVoidResult();

        var pendingOpen = new TaskCompletionSource<DefinitionStoreResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        DefinitionStoreProject project = CreateProject("demo", "Demo", BrowserFixture.Definition);
        var provider = new TestDefinitionStoreProvider(
            load: _ => Task.FromResult<IReadOnlyList<DefinitionStoreProject>>([project]),
            open: (_, _, _) => pendingOpen.Task);
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

        Task<Definition> inflight = catalog.OpenAsync("demo", BrowserFixture.Definition.Revision);
        await policy.BeginWorkspaceAsync();
        await policy.EndWorkspaceAsync();
        pendingOpen.SetResult(new DefinitionStoreResult.Success(BrowserFixture.Definition));

        BrowserLaunchException? failure = null;
        try { _ = await inflight; }
        catch (BrowserLaunchException caught) { failure = caught; }

        await Assert.That(failure?.Code).IsEqualTo("store-invalidated");
        await Assert.That(registry.Current.Definitions).IsEmpty();
    }

    [Test]
    public async Task StoreAdmissionPreservesCoreAuthoredWhitespaceInMetadata()
    {
        using var context = new BunitContext();
        Definition preserved = WithTitle(BrowserFixture.Definition, "  Preserved Title  ");
        DefinitionStoreProject project = CreateProject("demo", "  Demo Project  ", preserved);
        var provider = TestDefinitionStoreProvider.FromIndex([project], ("demo", preserved.Revision, preserved));
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

        IReadOnlyList<DefinitionStoreProject> projects = await catalog.LoadAsync();
        Definition opened = await catalog.OpenAsync("demo", preserved.Revision);

        await Assert.That(projects[0].Name).IsEqualTo("  Demo Project  ");
        await Assert.That(opened.Title).IsEqualTo("  Preserved Title  ");
        await Assert.That(opened.Digest).IsEqualTo(preserved.Digest);
    }

    [Test]
    public async Task EmbeddedStoreCatalogContainsPublishedProjectsAndOpensExactRevisions()
    {
        using var context = new BunitContext();
        using var provider = new EmbeddedDefinitionStoreProvider();
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(provider, registry, policy);

        IReadOnlyList<DefinitionStoreProject> projects = await catalog.LoadAsync();

        await Assert.That(projects.Any(project => project.ProjectId == "islamu-event")).IsTrue();
        DefinitionStoreProject minimal = projects.Single(project => project.ProjectId == "minimal-self-hosted");
        Definition opened = await catalog.OpenAsync(minimal.ProjectId, minimal.Revisions[0].Revision);
        await Assert.That(opened.FamilyId).IsEqualTo(minimal.FamilyId);
        await Assert.That(opened.Digest.Value).IsEqualTo(minimal.Revisions[0].Digest);
        await Assert.That(registry.Current.Definitions.Any(item => item.Digest == opened.Digest)).IsTrue();
    }

    [Test]
    public async Task ExploreRouteInspectsDefinitionReadOnlyWithoutAnswerCustodyOrArtifacts()
    {
        var emptyRegistry = new BrowserDefinitionRegistry([]);
        await using var context = BrowserFixture.Context(emptyRegistry);
        string family = "a2ef550d-e304-46c5-94e4-1705953f6b50";

        var component = context.Render<Pages.Explore>(parameters => parameters.Add(page => page.FamilyId, family));

        await Assert.That(component.FindAll("[data-explore-definition]").Count).IsEqualTo(1);
        await Assert.That(component.Find("[data-explore-definition]").TextContent).Contains("Minimal self-hosted service");
        await Assert.That(component.FindAll("[data-explore-section]").Count).IsGreaterThan(0);
        await Assert.That(component.FindAll("[data-explore-field]").Count).IsGreaterThan(0);
        await Assert.That(component.Find("a[href^='launch/']").GetAttribute("href")).Contains(family);
        await Assert.That(component.FindAll("#secret-consent")).IsEmpty();
        await Assert.That(component.FindComponents<ConfigurationEditor>()).IsEmpty();
        await Assert.That(emptyRegistry.Current.Definitions).IsEmpty();
    }

    [Test]
    public async Task ExploreSupportsLiveFilterRequirementFilterSectionOutlineAndReset()
    {
        var registry = BrowserDefinitionRegistry.LoadBundled();
        Definition neutral = registry.Current.Resolve(Guid.Parse("4d8950ad-972c-4357-8605-7a7c89c05a17"));
        await using var context = BrowserFixture.Context(registry);

        var component = context.Render<Pages.Explore>(parameters => parameters.Add(page => page.FamilyId, neutral.FamilyId.ToString("D")));

        int initialFields = component.FindAll("[data-explore-field]").Count;
        await Assert.That(initialFields).IsGreaterThan(1);
        await Assert.That(component.FindAll("[data-explore-outline] [data-explore-jump]").Count).IsEqualTo(component.FindAll("[data-explore-section]").Count);
        await Assert.That(component.FindAll("[data-explore-field] .explore-page__badge").All(badge => !string.Equals(badge.TextContent, "Optional", StringComparison.Ordinal))).IsTrue();

        await component.Find("[data-explore-requirement='required']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        int requiredFields = component.FindAll("[data-explore-field]").Count;
        await Assert.That(requiredFields).IsGreaterThan(0);
        await Assert.That(requiredFields).IsLessThan(initialFields);

        await component.Find("[data-explore-requirement='all']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await component.Find("#explore-filter").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "metrics" });
        await Assert.That(component.FindAll("[data-explore-field]").Count).IsLessThan(initialFields);
        await Assert.That(component.FindAll("[data-explore-field]").All(row => row.TextContent.Contains("metrics", StringComparison.OrdinalIgnoreCase))).IsTrue();

        await component.Find("#explore-filter").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "zz-nonexistent-setting" });
        await Assert.That(component.FindAll("[data-explore-field]")).IsEmpty();
        await Assert.That(component.FindAll("[data-explore-empty]").Count).IsEqualTo(1);

        await component.Find("[data-action='clear-explore-filter']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await Assert.That(component.FindAll("[data-explore-field]").Count).IsEqualTo(initialFields);
    }

    [Test]
    public async Task StoreIndexParserAcceptsBoundedSquarePngIconAndFailsClosedOnTamperedIcons()
    {
        byte[] validPng = CreateSyntheticPng(width: 32, height: 32);
        string validDataUri = "data:image/png;base64," + Convert.ToBase64String(validPng);

        JsonObject validProject = CreateProjectJson(
            "demo",
            "Demo",
            BrowserFixture.Definition,
            iconDataUri: validDataUri,
            iconLicense: "CC-BY-4.0",
            iconAttribution: "Synthetic test logo attribution.");

        IReadOnlyList<DefinitionStoreProject> parsed = DefinitionStoreIndexParser.ParseIndex(
            Encoding.UTF8.GetBytes(CreateIndexJson(validProject).ToJsonString()));
        await Assert.That(parsed.Count).IsEqualTo(1);
        await Assert.That(parsed[0].IconDataUri).IsEqualTo(validDataUri);
        await Assert.That(parsed[0].IconLicense).IsEqualTo("CC-BY-4.0");
        await Assert.That(parsed[0].IconAttribution).IsEqualTo("Synthetic test logo attribution.");

        var card = new StoreProjectCardViewModel(parsed[0]);
        await Assert.That(card.HasIcon).IsTrue();
        await Assert.That(card.IconBytes.Length).IsEqualTo(validPng.Length);
        await Assert.That(card.OutputCount).IsEqualTo(BrowserFixture.Definition.Artifacts.Count);
        await Assert.That(card.LatestDigest).IsEqualTo(BrowserFixture.Definition.Digest.Value);

        var cardWithoutIcon = new StoreProjectCardViewModel(CreateProject("minimal-self-hosted", "Minimal self-hosted service", BrowserFixture.Definition));
        await Assert.That(cardWithoutIcon.HasIcon).IsFalse();
        await Assert.That(cardWithoutIcon.IconBytes.Length).IsEqualTo(0);
        await Assert.That(cardWithoutIcon.Monogram).IsEqualTo("MS");

        string[] invalidDataUris =
        [
            "https://private-canary.example/icon.png",
            "data:image/svg+xml;base64," + Convert.ToBase64String("<svg xmlns='http://www.w3.org/2000/svg'><script>private-canary</script></svg>"u8),
            "data:image/png;base64,not!valid!base64!",
            "data:image/png;base64," + Convert.ToBase64String(CreateSyntheticPng(32, 32)).Insert(4, " "),
            "data:image/png;base64," + Convert.ToBase64String(new byte[12 * 1024 + 1]),
            "data:image/png;base64," + Convert.ToBase64String("GIF89a\x20\x00\x20\x00"u8),
            "data:image/png;base64," + Convert.ToBase64String(CreateSyntheticPng(width: 257, height: 257)),
            "data:image/png;base64," + Convert.ToBase64String(CreateSyntheticPng(width: 15, height: 15)),
            "data:image/png;base64," + Convert.ToBase64String(CreateSyntheticPng(width: 32, height: 64))
        ];

        foreach (string badUri in invalidDataUris)
        {
            JsonObject badProject = CreateProjectJson(
                "demo",
                "Demo",
                BrowserFixture.Definition,
                iconDataUri: badUri,
                iconLicense: "CC-BY-4.0",
                iconAttribution: "Attribution.");

            DefinitionStoreException? failure = null;
            try { _ = DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(CreateIndexJson(badProject).ToJsonString())); }
            catch (DefinitionStoreException caught) { failure = caught; }

            await Assert.That(failure).IsNotNull().Because(badUri);
            await Assert.That(failure!.Code).IsEqualTo("store-index-invalid");
            await Assert.That(failure.Message).DoesNotContain("private-canary");
        }

        // Unpaired license/attribution/icon fields must fail closed.
        JsonObject missingAttribution = CreateProjectJson("demo", "Demo", BrowserFixture.Definition, iconDataUri: validDataUri, iconLicense: "CC-BY-4.0");
        JsonObject missingLicense = CreateProjectJson("demo", "Demo", BrowserFixture.Definition, iconDataUri: validDataUri, iconAttribution: "Attribution.");
        JsonObject missingUri = CreateProjectJson("demo", "Demo", BrowserFixture.Definition, iconLicense: "CC-BY-4.0", iconAttribution: "Attribution.");
        foreach (JsonObject unpaired in new[] { missingAttribution, missingLicense, missingUri })
        {
            await Assert.That(() => DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(CreateIndexJson(unpaired).ToJsonString())))
                .Throws<DefinitionStoreException>();
        }
    }

    private static byte[] CreateSyntheticPng(int width, int height)
    {
        byte[] png = new byte[8 + 4 + 4 + 13 + 4 + 12];
        byte[] signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(png, 0);
        // IHDR length = 13
        png[11] = 13;
        png[12] = (byte)'I';
        png[13] = (byte)'H';
        png[14] = (byte)'D';
        png[15] = (byte)'R';
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), height);
        png[24] = 8; // bit depth
        png[25] = 6; // RGBA
        // IEND
        int iend = png.Length - 12;
        png[iend + 4] = (byte)'I';
        png[iend + 5] = (byte)'E';
        png[iend + 6] = (byte)'N';
        png[iend + 7] = (byte)'D';
        return png;
    }

    private static JsonObject CreateIndexJson(params JsonObject[] projects) => new()
    {
        ["schemaVersion"] = ConfiguratorVersion.Current,
        ["projects"] = new JsonArray(projects.Select(project => (JsonNode?)project).ToArray())
    };

    private static JsonObject CreateProjectJson(
        string projectId,
        string name,
        Definition definition,
        string? iconDataUri = null,
        string? iconLicense = null,
        string? iconAttribution = null)
    {
        var project = new JsonObject
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["description"] = "Public metadata",
            ["sourceRepository"] = $"https://publisher.example/{projectId}",
            ["license"] = "MIT",
            ["tags"] = new JsonArray("directory", "self-hosted"),
            ["applicationId"] = definition.ApplicationId,
            ["familyId"] = definition.FamilyId.ToString("D"),
            ["revisions"] = new JsonArray(new JsonObject
            {
                ["revision"] = definition.Revision,
                ["digest"] = definition.Digest.Value,
                ["publisher"] = definition.Publisher,
                ["title"] = definition.Title,
                ["applicationVersions"] = new JsonArray(definition.ApplicationVersions.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()),
                ["entryPoints"] = new JsonArray(definition.EntryPoints.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()),
                ["locales"] = new JsonArray(definition.Locales.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()),
                ["outputNames"] = new JsonArray(definition.Artifacts.Select(a => (JsonNode?)JsonValue.Create(a.FileName)).ToArray()),
                ["snapshotPath"] = $"definitions/{projectId}/{definition.Revision}.json"
            })
        };
        if (iconDataUri is not null) project["iconDataUri"] = iconDataUri;
        if (iconLicense is not null) project["iconLicense"] = iconLicense;
        if (iconAttribution is not null) project["iconAttribution"] = iconAttribution;
        return project;
    }

    internal static DefinitionStoreProject CreateProject(
        string projectId,
        string name,
        params Definition[] definitions) =>
        new()
        {
            ProjectId = projectId,
            Name = name,
            Description = "Public metadata",
            SourceRepository = $"https://publisher.example/{projectId}",
            License = "MIT",
            Tags = ["directory", "self-hosted"],
            ApplicationId = definitions[0].ApplicationId,
            FamilyId = definitions[0].FamilyId,
            Revisions = definitions.Select(CreateRevision).ToArray()
        };

    internal static DefinitionStoreRevision CreateRevision(Definition definition) =>
        new()
        {
            Revision = definition.Revision,
            Digest = definition.Digest.Value,
            Publisher = definition.Publisher,
            Title = definition.Title,
            ApplicationVersions = definition.ApplicationVersions,
            EntryPoints = definition.EntryPoints,
            Locales = definition.Locales,
            OutputNames = definition.Artifacts.Select(artifact => artifact.FileName).ToArray(),
            SnapshotPath = $"definitions/{definition.ApplicationId}/{definition.Revision}.json"
        };

    internal static Definition WithTitle(Definition source, string title)
    {
        JsonNode node = JsonNode.Parse(source.CanonicalBytes.Span)!;
        node["project"]!["name"] = title;
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    internal static Definition WithIdentity(Definition source, Guid familyId, string applicationId, long revision)
    {
        JsonNode node = JsonNode.Parse(source.CanonicalBytes.Span)!;
        node["familyId"] = familyId.ToString("D");
        node["project"]!["id"] = applicationId;
        node["revision"] = revision;
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
    }
}
