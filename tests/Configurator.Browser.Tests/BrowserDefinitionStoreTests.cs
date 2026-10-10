namespace Configurator.Browser.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Bunit;
using Configurator.Browser.Services;
using Configurator.Core.Definitions;

public sealed class BrowserDefinitionStoreTests
{
    [Test]
    public async Task InjectedPathsUnknownMembersAndOversizedMetadataRejectWithoutEcho()
    {
        foreach (string path in new[] { "../private-canary.json", "https://private-canary.example/a.json", "definitions/demo/%2e%2e/a.json", "definitions/other/1.json" })
        {
            JsonNode index = Index();
            index["projects"]![0]!["revisions"]![0]!["snapshotPath"] = path;
            await Assert.That(() => DefinitionStoreCatalog.ParseIndex(Encoding.UTF8.GetBytes(index.ToJsonString()))).Throws<BrowserLaunchException>();
        }
        JsonNode unknown = Index();
        unknown["projects"]![0]!["answer"] = "private-canary";
        await Assert.That(() => DefinitionStoreCatalog.ParseIndex(Encoding.UTF8.GetBytes(unknown.ToJsonString()))).Throws<BrowserLaunchException>();
        JsonNode oversized = Index();
        oversized["projects"]![0]!["name"] = new string('x', 257);
        await Assert.That(() => DefinitionStoreCatalog.ParseIndex(Encoding.UTF8.GetBytes(oversized.ToJsonString()))).Throws<BrowserLaunchException>();
        foreach (string repository in new[] { "javascript:private-canary", "http://publisher.example/demo", "https://private-canary@publisher.example/demo", "https://publisher.example/demo?answer=private-canary", "https://publisher.example/%2e%2e/demo" })
        {
            JsonNode index = Index();
            index["projects"]![0]!["sourceRepository"] = repository;
            await Assert.That(() => DefinitionStoreCatalog.ParseIndex(Encoding.UTF8.GetBytes(index.ToJsonString()))).Throws<BrowserLaunchException>();
        }
    }

    [Test]
    public async Task SnapshotMismatchCannotPublishIntoRegistry()
    {
        using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(Index().ToJsonString()));
        module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes).SetResult(BrowserFixture.Revision(2).CanonicalBytes.ToArray());
        await catalog.LoadAsync();
        await Assert.That(async () => { await catalog.OpenAsync("demo", 1); }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Definitions).IsEmpty();
    }

    [Test]
    public async Task DefinitionAndDiscoverySelectorsMustAllMatchBeforeAdmission()
    {
        foreach (string member in new[] { "digest", "publisher", "title", "applicationVersions", "entryPoints", "locales", "outputNames", "applicationId", "familyId" })
        {
            using var context = BrowserFixture.Context();
            var registry = new BrowserDefinitionRegistry([]);
            await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
            await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
            JsonNode index = Index();
            JsonNode project = index["projects"]![0]!;
            JsonNode revision = project["revisions"]![0]!;
            if (member == "familyId") project[member] = "71cdd5fa-4adc-49fb-9f6a-349807670c31";
            else if (member == "applicationId") project[member] = "other.application";
            else if (member == "digest") revision[member] = new string('f', 64);
            else if (revision[member] is JsonArray array) array[0] = "different";
            else revision[member] = "Different metadata";
            var module = context.JSInterop.SetupModule("./browser-store.js");
            module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(index.ToJsonString()));
            module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes).SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
            await catalog.LoadAsync();
            await Assert.That(async () => { await catalog.OpenAsync("demo", 1); }).Throws<BrowserLaunchException>();
            await Assert.That(registry.Current.Definitions).IsEmpty();
        }
    }

    [Test]
    public async Task ConflictingPreviouslyAdmittedRevisionRejectsAfterRegistryRemoval()
    {
        using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([BrowserFixture.Definition]);
        registry.Replace([]);
        JsonNode changed = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        changed["title"] = "Conflicting revision";
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(changed.ToJsonString()));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(Index(definition).ToJsonString()));
        module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes).SetResult(definition.CanonicalBytes.ToArray());
        await catalog.LoadAsync();
        await Assert.That(async () => { await catalog.OpenAsync("demo", 1); }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Definitions).IsEmpty();
    }

    [Test]
    public async Task PackagedAssetsRemainReadableOfflineButAreBlockedDuringAnswerCustody()
    {
        using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var lifecycle = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        lifecycle.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, false));
        lifecycle.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await policy.InitializeAsync();
        await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(Index().ToJsonString()));
        module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes).SetResult(BrowserFixture.Definition.CanonicalBytes.ToArray());
        await catalog.LoadAsync();
        SetupDefinition admitted = await catalog.OpenAsync("demo", 1);
        await Assert.That(admitted.Digest).IsEqualTo(BrowserFixture.Definition.Digest);
        await policy.BeginWorkspaceAsync();
        await Assert.That(async () => { await catalog.LoadAsync(); }).Throws<BrowserLaunchException>();
        await Assert.That(async () => { await catalog.OpenAsync("demo", 1); }).Throws<BrowserLaunchException>();
    }

    [Test]
    public async Task LateSnapshotCannotAdmitAcrossAnAnswerWorkspaceGeneration()
    {
        using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        var lifecycle = context.JSInterop.SetupModule("./pwa-lifecycle.js");
        lifecycle.Setup<BrowserOfflineState>("initialize").SetResult(new(true, true, true));
        lifecycle.Setup<bool>("beginWorkspace", _ => true).SetResult(true);
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(Index().ToJsonString()));
        var pending = module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes);
        await catalog.LoadAsync();
        Task<SetupDefinition> selection = catalog.OpenAsync("demo", 1);
        await Assert.That(pending.Invocations).Count().IsEqualTo(1);
        await policy.BeginWorkspaceAsync();
        await policy.EndWorkspaceAsync();
        byte[] bytes = BrowserFixture.Definition.CanonicalBytes.ToArray();
        pending.SetResult(bytes);
        await Assert.That(async () => { await selection.WaitAsync(TimeSpan.FromSeconds(5)); }).Throws<BrowserLaunchException>();
        await Assert.That(registry.Current.Definitions).IsEmpty();
        await Assert.That(bytes.All(value => value == 0)).IsTrue();
    }

    [Test]
    public async Task MalformedDuplicateAndOversizedIndexesHaveOnlyValueSafeFailureCodes()
    {
        foreach (string document in new[]
        {
            "private-canary", "{\"schemaVersion\":1,\"schemaVersion\":1,\"projects\":[]}",
            "{\"schemaVersion\":1,\"projects\":null}", "{\"schemaVersion\":2,\"projects\":[]}"
        })
        {
            BrowserLaunchException? failure = null;
            try { _ = DefinitionStoreCatalog.ParseIndex(Encoding.UTF8.GetBytes(document)); }
            catch (BrowserLaunchException exception) { failure = exception; }
            await Assert.That(failure).IsNotNull();
            await Assert.That(failure!.Message).IsEqualTo("store-index-invalid");
        }
        await Assert.That(() => DefinitionStoreCatalog.ParseIndex(new byte[DefinitionStoreCatalog.MaximumIndexBytes + 1])).Throws<BrowserLaunchException>();
    }

    [Test]
    public async Task StoreAdmissionPreservesCoreAuthoredWhitespaceInMetadata()
    {
        using var context = BrowserFixture.Context();
        var registry = new BrowserDefinitionRegistry([]);
        JsonNode node = JsonNode.Parse(BrowserFixture.Definition.CanonicalBytes.Span)!;
        node["title"] = " Authored title ";
        node["publisher"] = " Authored publisher ";
        node["applicationId"] = " Authored application ";
        node["applicationVersions"] = new JsonArray(" Compatibility label ");
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
        await using var policy = new BrowserOfflinePolicy(context.JSInterop.JSRuntime);
        await using var catalog = new DefinitionStoreCatalog(registry, context.JSInterop.JSRuntime, policy);
        var module = context.JSInterop.SetupModule("./browser-store.js");
        module.Setup<byte[]>("readAsset", "index.json", DefinitionStoreCatalog.MaximumIndexBytes).SetResult(Encoding.UTF8.GetBytes(Index(definition).ToJsonString()));
        module.Setup<byte[]>("readAsset", "definitions/demo/1.json", BrowserDefinitionSource.MaximumDocumentBytes).SetResult(definition.CanonicalBytes.ToArray());
        await catalog.LoadAsync();
        SetupDefinition admitted = await catalog.OpenAsync("demo", 1);
        await Assert.That(admitted.Title).IsEqualTo(" Authored title ");
        await Assert.That(admitted.Digest).IsEqualTo(definition.Digest);
    }

    internal static JsonNode Index(SetupDefinition? definition = null)
    {
        definition ??= BrowserFixture.Definition;
        return JsonNode.Parse($$"""
            {"schemaVersion":1,"projects":[{"projectId":"demo","name":"Demo","description":"Public configuration","sourceRepository":"https://publisher.example/demo","license":"MIT","tags":["self-hosted"],"applicationId":"{{definition.ApplicationId}}","familyId":"{{definition.FamilyId:D}}","revisions":[{"revision":{{definition.Revision}},"digest":"{{definition.Digest.Value}}","publisher":"{{definition.Publisher}}","title":"{{definition.Title}}","applicationVersions":{{new JsonArray(definition.ApplicationVersions.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()).ToJsonString()}},"entryPoints":{{new JsonArray(definition.EntryPoints.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()).ToJsonString()}},"locales":{{new JsonArray(definition.Locales.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()).ToJsonString()}},"outputNames":{{new JsonArray(definition.Artifacts.Select(value => (JsonNode?)JsonValue.Create(value.FileName)).ToArray()).ToJsonString()}},"snapshotPath":"definitions/demo/{{definition.Revision}}.json"}]}]}
            """)!;
    }
}
