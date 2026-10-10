using System.Text.Json;
using Configurator.Core.Definitions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[Category("Packaging")]
public sealed class BrowserStorePackageTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BothVariantsInventoryCompletePublicInterfaceAssets(bool includeStore)
    {
        string directory = Path.Combine(includeStore ? ReleaseTestPaths.StorePackageRoot : ReleaseTestPaths.PackageRoot, "browser/wwwroot");
        using JsonDocument assets = ReadWorkerAssets(directory);
        Dictionary<string, string> inventory = assets.RootElement.GetProperty("assets").EnumerateArray()
            .ToDictionary(asset => asset.GetProperty("url").GetString()!, asset => asset.GetProperty("hash").GetString()!, StringComparer.Ordinal);
        string[] styles = Directory.GetFiles(Path.Combine(directory, "css"), "*.css", SearchOption.AllDirectories);
        await Assert.That(styles.Length).IsGreaterThan(0);
        foreach (string relative in styles.Select(path => Path.GetRelativePath(directory, path)).Concat(
            ["_content/MudBlazor/MudBlazor.min.css", "_content/MudBlazor/MudBlazor.min.js", "browser-appearance.js", "browser-store.js"]))
        {
            await Assert.That(inventory.ContainsKey(relative)).IsTrue().Because(relative);
            string hash = "sha256-" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(directory, relative))));
            await Assert.That(inventory[relative]).IsEqualTo(hash).Because(relative);
        }
    }

    [Test]
    public async Task StoreFreePackageHasNoCatalogueOrCachedSnapshots()
    {
        string directory = Path.Combine(ReleaseTestPaths.PackageRoot, "browser/wwwroot");
        await Assert.That(Directory.Exists(directory)).IsTrue();
        await Assert.That(Directory.Exists(Path.Combine(directory, "definition-store"))).IsFalse();
        using JsonDocument assets = ReadWorkerAssets(directory);
        await Assert.That(assets.RootElement.GetProperty("assets").EnumerateArray().Any(asset =>
            asset.GetProperty("url").GetString()!.StartsWith("definition-store/", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task StoreEnabledPackagePinsEverySnapshotAndCachesItsExactPublishedBytes()
    {
        string directory = Path.Combine(ReleaseTestPaths.StorePackageRoot, "browser/wwwroot");
        using JsonDocument index = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "definition-store/index.json")));
        using JsonDocument assets = ReadWorkerAssets(directory);
        Dictionary<string, string> inventory = assets.RootElement.GetProperty("assets").EnumerateArray()
            .ToDictionary(asset => asset.GetProperty("url").GetString()!, asset => asset.GetProperty("hash").GetString()!, StringComparer.Ordinal);
        await Assert.That(index.RootElement.GetProperty("schemaVersion").GetInt32()).IsEqualTo(1);
        await Assert.That(index.RootElement.GetProperty("projects").GetArrayLength()).IsGreaterThan(0);
        await AssertAssetHash("definition-store/index.json");
        var expectedFiles = new HashSet<string>(StringComparer.Ordinal) { "index.json" };
        foreach (JsonElement project in index.RootElement.GetProperty("projects").EnumerateArray())
        {
            await Assert.That(project.GetProperty("revisions").GetArrayLength()).IsGreaterThan(0);
            long[] numbers = project.GetProperty("revisions").EnumerateArray()
                .Select(revision => revision.GetProperty("revision").GetInt64()).ToArray();
            await Assert.That(numbers.Distinct().Count()).IsEqualTo(numbers.Length);
            await Assert.That(numbers.SequenceEqual(numbers.Order())).IsTrue();
            foreach (JsonElement revision in project.GetProperty("revisions").EnumerateArray())
            {
                string relative = revision.GetProperty("snapshotPath").GetString()!;
                await Assert.That(relative.StartsWith("definitions/", StringComparison.Ordinal)
                    && !relative.Contains("..", StringComparison.Ordinal) && !relative.Contains('\\')).IsTrue();
                string path = "definition-store/" + relative;
                await Assert.That(expectedFiles.Add(relative)).IsTrue();
                byte[] snapshot = File.ReadAllBytes(Path.Combine(directory, path));
                SetupDefinition definition = SetupDefinitionCodec.ParseJson(snapshot);
                await Assert.That(snapshot.AsSpan().SequenceEqual(definition.CanonicalBytes.Span)).IsTrue();
                await Assert.That(definition.FamilyId.ToString("D")).IsEqualTo(project.GetProperty("familyId").GetString());
                await Assert.That(definition.ApplicationId).IsEqualTo(project.GetProperty("applicationId").GetString());
                await Assert.That(definition.Revision).IsEqualTo(revision.GetProperty("revision").GetInt64());
                await Assert.That(definition.Digest.Value).IsEqualTo(revision.GetProperty("digest").GetString());
                await Assert.That(definition.ApplicationVersions.SequenceEqual(revision.GetProperty("applicationVersions")
                    .EnumerateArray().Select(version => version.GetString()!))).IsTrue();
                await AssertAssetHash(path);
            }
        }
        string[] published = Directory.GetFiles(Path.Combine(directory, "definition-store"), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Path.Combine(directory, "definition-store"), path).Replace('\\', '/')).ToArray();
        await Assert.That(expectedFiles.SetEquals(published)).IsTrue()
            .Because("Published store contains only admitted canonical snapshots and their generated index.");

        async Task AssertAssetHash(string relative)
        {
            await Assert.That(inventory.ContainsKey(relative)).IsTrue().Because(relative);
            string hash = "sha256-" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(directory, relative))));
            await Assert.That(inventory[relative]).IsEqualTo(hash).Because(relative);
        }
    }

    private static JsonDocument ReadWorkerAssets(string directory)
    {
        string manifest = File.ReadAllText(Path.Combine(directory, "service-worker-assets.js"));
        int start = manifest.IndexOf('{');
        int end = manifest.LastIndexOf('}');
        return JsonDocument.Parse(manifest[start..(end + 1)]);
    }
}
