namespace Configurator.Presentation.Tests.Store;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core;
using Configurator.Core.Definitions;
using Configurator.Presentation.Store;

public sealed class EmbeddedDefinitionStoreProviderTests
{
    [Test]
    public async Task PresentationAssemblyContainsTheCuratedStore()
    {
        using var provider = new EmbeddedDefinitionStoreProvider();
        IReadOnlyList<DefinitionStoreProject> projects = await provider.LoadAsync();
        DefinitionStoreProject project = projects.Single(item => item.ProjectId == "minimal-self-hosted");
        DefinitionStoreResult result = await provider.OpenAsync(project.ProjectId, project.Revisions[0].Revision);

        await Assert.That(projects.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(result).IsTypeOf<DefinitionStoreResult.Success>();
    }

    [Test]
    public async Task ResourcesAreOrderedCachedAndOpenedWithoutFileOrNetworkInput()
    {
        Definition first = Definition();
        Definition second = Revision(first, 2);
        JsonObject demo = Project("demo", "Zulu", first, second);
        JsonObject other = Project("other", "Alpha", first);
        var resources = Resources(Index(demo, other), (first, "demo"), (second, "demo"), (first, "other"));
        using var provider = new EmbeddedDefinitionStoreProvider(resources.Open);

        IReadOnlyList<DefinitionStoreProject>[] loads = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => provider.LoadAsync()));

        await Assert.That(loads.All(projects => ReferenceEquals(projects, loads[0]))).IsTrue();
        await Assert.That(string.Join(',', loads[0].Select(project => project.Name))).IsEqualTo("Alpha,Zulu");
        await Assert.That(string.Join(',', loads[0][1].Revisions.Select(revision => revision.Revision))).IsEqualTo("2,1");
        await Assert.That(resources.Names.Count(name => name == "Configurator.Store.index.json")).IsEqualTo(1);
        DefinitionStoreResult opened = await provider.OpenAsync("demo", 1);
        await Assert.That(opened).IsTypeOf<DefinitionStoreResult.Success>();
        var success = (DefinitionStoreResult.Success)opened;
        await Assert.That(success.Definition.Digest).IsEqualTo(first.Digest);
        await Assert.That(resources.Names.All(name => name.StartsWith("Configurator.Store.", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MissingProjectsAndRevisionsHaveDistinctTypedResults()
    {
        Definition definition = Definition();
        var resources = Resources(Index(Project("demo", "Demo", definition)), (definition, "demo"));
        using var provider = new EmbeddedDefinitionStoreProvider(resources.Open);
        await provider.LoadAsync();

        await Assert.That(await provider.OpenAsync("missing", 1)).IsTypeOf<DefinitionStoreResult.ProjectNotFound>();
        await Assert.That(await provider.OpenAsync("demo", 2)).IsTypeOf<DefinitionStoreResult.RevisionNotFound>();
    }

    [Test]
    public async Task EverySnapshotSelectorMustMatchBeforeSuccess()
    {
        string[] members = ["digest", "publisher", "title", "applicationVersions", "entryPoints", "locales", "outputNames", "applicationId", "familyId", "revision"];
        foreach (string member in members)
        {
            Definition definition = Definition();
            JsonObject project = Project("demo", "Demo", definition);
            JsonObject revision = project["revisions"]![0]!.AsObject();
            if (member == "familyId") project[member] = "71cdd5fa-4adc-49fb-9f6a-349807670c31";
            else if (member == "applicationId") project[member] = "other.application";
            else if (member == "digest") revision[member] = new string('f', 64);
            else if (member == "revision")
            {
                revision[member] = definition.Revision + 1;
                revision["snapshotPath"] = $"definitions/demo/{definition.Revision + 1}.json";
            }
            else if (revision[member] is JsonArray array) array[0] = "different";
            else revision[member] = "Different metadata";
            long selectedRevision = member == "revision" ? definition.Revision + 1 : definition.Revision;
            var resources = Resources(Index(project));
            resources.Add($"Configurator.Store.definitions/demo/{selectedRevision}.json", definition.CanonicalBytes.ToArray());
            using var provider = new EmbeddedDefinitionStoreProvider(resources.Open);
            await provider.LoadAsync();

            DefinitionStoreResult result = await provider.OpenAsync("demo", selectedRevision);

            await Assert.That(result).IsTypeOf<DefinitionStoreResult.SnapshotMismatch>();
        }
    }

    [Test]
    public async Task MissingMalformedAndOversizedSnapshotsFailClosed()
    {
        Definition definition = Definition();
        byte[] index = Encoding.UTF8.GetBytes(Index(Project("demo", "Demo", definition)).ToJsonString());
        foreach (byte[]? snapshot in new byte[]?[] { null, [], "private-canary"u8.ToArray(), new byte[4 * 1024 * 1024 + 1] })
        {
            using var provider = new EmbeddedDefinitionStoreProvider(name => name == "Configurator.Store.index.json"
                ? new MemoryStream(index, writable: false)
                : snapshot is null ? null : new MemoryStream(snapshot, writable: false));
            await provider.LoadAsync();

            DefinitionStoreResult result = await provider.OpenAsync("demo", 1);

            await Assert.That(result).IsTypeOf<DefinitionStoreResult.CorruptAsset>();
            await Assert.That(result.ToString()).DoesNotContain("private-canary");
        }
    }

    [Test]
    public async Task InjectedPathsUnknownMembersAndUnsafeRepositoriesRejectWithoutEcho()
    {
        foreach (string path in new[] { "../private-canary.json", "https://private-canary.example/a.json", "definitions/demo/%2e%2e/a.json", "definitions/other/1.json" })
        {
            JsonNode index = Index(Project("demo", "Demo", Definition()));
            index["projects"]![0]!["revisions"]![0]!["snapshotPath"] = path;
            await Assert.That(() => DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(index.ToJsonString())))
                .Throws<DefinitionStoreException>();
        }
        JsonNode unknown = Index(Project("demo", "Demo", Definition()));
        unknown["projects"]![0]!["answer"] = "private-canary";
        await Assert.That(() => DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(unknown.ToJsonString())))
            .Throws<DefinitionStoreException>();
        try { _ = DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(unknown.ToJsonString())); }
        catch (DefinitionStoreException failure)
        {
            await Assert.That(failure.Code).IsEqualTo("store-index-invalid");
            await Assert.That(failure.Message).DoesNotContain("private-canary");
        }
        foreach (string repository in new[] { "javascript:private-canary", "http://publisher.example/demo", "https://private-canary@publisher.example/demo", "https://publisher.example/demo?answer=private-canary", "https://publisher.example/%2e%2e/demo" })
        {
            JsonNode index = Index(Project("demo", "Demo", Definition()));
            index["projects"]![0]!["sourceRepository"] = repository;
            await Assert.That(() => DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(index.ToJsonString())))
                .Throws<DefinitionStoreException>();
        }
    }

    [Test]
    public async Task MalformedDuplicateAndOversizedIndexesHaveOnlyValueSafeFailures()
    {
        string[] documents =
        [
            "private-canary", "{\"schemaVersion\":\"0.0.1\",\"schemaVersion\":\"0.0.1\",\"projects\":[]}",
            "{\"schemaVersion\":\"0.0.1\",\"projects\":null}", "{\"schemaVersion\":\"unsupported\",\"projects\":[]}",
            "{\"schemaVersion\":1,\"projects\":[]}"
        ];
        foreach (string document in documents)
        {
            await Assert.That(() => DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(document)))
                .Throws<DefinitionStoreException>();
            try { _ = DefinitionStoreIndexParser.ParseIndex(Encoding.UTF8.GetBytes(document)); }
            catch (DefinitionStoreException failure)
            {
                await Assert.That(failure.Code).IsEqualTo("store-index-invalid");
                await Assert.That(failure.Message).DoesNotContain("private-canary");
            }
        }
        await Assert.That(() => DefinitionStoreIndexParser.ParseIndex(new byte[DefinitionStoreIndexParser.MaximumIndexBytes + 1]))
            .Throws<DefinitionStoreException>();
    }

    internal static Definition Definition()
    {
        using Stream stream = typeof(EmbeddedDefinitionStoreProviderTests).Assembly.GetManifestResourceStream("Conformance.RootMap")!;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return DefinitionSourceCodec.ParseJson(bytes.ToArray());
    }

    internal static Definition Revision(Definition definition, long revision)
    {
        JsonNode node = JsonNode.Parse(definition.CanonicalBytes.Span)!;
        node["revision"] = revision;
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    internal static JsonObject Index(params JsonObject[] projects) => new()
    {
        ["schemaVersion"] = ConfiguratorVersion.Current,
        ["projects"] = new JsonArray(projects.Select(project => (JsonNode?)project).ToArray())
    };

    internal static JsonObject Project(string id, string name, params Definition[] definitions)
    {
        Definition first = definitions[0];
        return new JsonObject
        {
            ["projectId"] = id,
            ["name"] = name,
            ["description"] = "Public configuration",
            ["sourceRepository"] = "https://publisher.example/demo",
            ["license"] = "MIT",
            ["tags"] = new JsonArray("self-hosted"),
            ["applicationId"] = first.ApplicationId,
            ["familyId"] = first.FamilyId.ToString("D"),
            ["revisions"] = new JsonArray(definitions.Select(definition => (JsonNode?)RevisionMetadata(id, definition)).ToArray())
        };
    }

    private static JsonObject RevisionMetadata(string id, Definition definition) => new()
    {
        ["revision"] = definition.Revision,
        ["digest"] = definition.Digest.Value,
        ["publisher"] = definition.Publisher,
        ["title"] = definition.Title,
        ["applicationVersions"] = Strings(definition.ApplicationVersions),
        ["entryPoints"] = Strings(definition.EntryPoints),
        ["locales"] = Strings(definition.Locales),
        ["outputNames"] = Strings(definition.Artifacts.Select(artifact => artifact.FileName)),
        ["snapshotPath"] = $"definitions/{id}/{definition.Revision}.json"
    };

    private static JsonArray Strings(IEnumerable<string> values) =>
        new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    private static StoreResources Resources(JsonNode index, params (Definition Definition, string ProjectId)[] snapshots)
    {
        var resources = new StoreResources();
        resources.Add("Configurator.Store.index.json", Encoding.UTF8.GetBytes(index.ToJsonString()));
        foreach ((Definition definition, string projectId) in snapshots)
            resources.Add($"Configurator.Store.definitions/{projectId}/{definition.Revision}.json", definition.CanonicalBytes.ToArray());
        return resources;
    }

    private sealed class StoreResources
    {
        private readonly Dictionary<string, byte[]> _resources = new(StringComparer.Ordinal);
        internal List<string> Names { get; } = [];
        internal void Add(string name, byte[] bytes) => _resources.Add(name, bytes);
        internal MemoryStream? Open(string name)
        {
            lock (Names) Names.Add(name);
            return _resources.TryGetValue(name, out byte[]? bytes) ? new MemoryStream(bytes, writable: false) : null;
        }
    }
}
