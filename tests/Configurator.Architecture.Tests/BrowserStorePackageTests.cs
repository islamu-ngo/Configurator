using System.Security.Cryptography;
using System.Text.Json;
using Configurator.Presentation.Store;

namespace Configurator.Architecture.Tests;

[Category("Packaging")]
public sealed class BrowserStorePackageTests
{
    [Test]
    public async Task PublishedBrowserPackageOmitsLegacyStaticStoreAndEmbedsPresentationStore()
    {
        string directory = Path.Combine(ReleaseTestPaths.PackageRoot, "browser/wwwroot");
        await Assert.That(Directory.Exists(Path.Combine(directory, "definition-store"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(directory, "js/browser-store.js"))).IsFalse();

        string workerAssets = await File.ReadAllTextAsync(Path.Combine(directory, "service-worker-assets.js"));
        using JsonDocument assets = JsonDocument.Parse(workerAssets[workerAssets.IndexOf('{')..(workerAssets.LastIndexOf('}') + 1)]);
        Dictionary<string, string> inventory = assets.RootElement.GetProperty("assets").EnumerateArray()
            .ToDictionary(asset => asset.GetProperty("url").GetString()!, asset => asset.GetProperty("hash").GetString()!,
                StringComparer.Ordinal);

        await Assert.That(inventory.Keys.Any(url => url.StartsWith("definition-store/", StringComparison.Ordinal))).IsFalse();
        await Assert.That(inventory.ContainsKey("js/browser-store.js")).IsFalse();
        await Assert.That(inventory.Keys.Any(url => url.StartsWith("_framework/Configurator.Presentation.", StringComparison.Ordinal))).IsTrue();

        foreach (string relative in new[]
        {
            "_content/MudBlazor/MudBlazor.min.css",
            "_content/MudBlazor/MudBlazor.min.js",
            "js/browser-appearance.js",
            "js/browser-definition-source.js",
            "js/browser-artifacts.js",
            "js/workspace-interactions.js",
            "js/configuration-rendering.js"
        })
        {
            await Assert.That(inventory.ContainsKey(relative)).IsTrue().Because(relative);
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(directory, relative));
            await Assert.That(inventory[relative]).IsEqualTo("sha256-" + Convert.ToBase64String(SHA256.HashData(bytes)));
        }

        string serviceWorker = await File.ReadAllTextAsync(Path.Combine(directory, "service-worker.js"));
        await Assert.That(serviceWorker).DoesNotContain("browser-store.js");
        await Assert.That(serviceWorker).DoesNotContain("definition-store/");
    }

    [Test]
    public async Task EmbeddedStoreProviderMatchesGeneratedCheckedInStoreCatalog()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "configurator-store-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var build = await ReleaseTestPaths.Run("dotnet", "run", "--file",
                "eng/configurator/BuildDefinitionStore.cs", "--",
                "--source", Path.Combine(ReleaseTestPaths.Root, "store"),
                "--output", tempRoot,
                "--write");
            await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);

            IReadOnlyList<DefinitionStoreProject> expected =
                DefinitionStoreIndexParser.ParseIndex(await File.ReadAllBytesAsync(Path.Combine(tempRoot, "index.json")));

            using var provider = new EmbeddedDefinitionStoreProvider();
            IReadOnlyList<DefinitionStoreProject> embedded = await provider.LoadAsync();

            await Assert.That(embedded.Count).IsEqualTo(expected.Count);
            foreach (DefinitionStoreProject project in expected)
            {
                DefinitionStoreProject? match = embedded.SingleOrDefault(candidate => candidate.ProjectId == project.ProjectId);
                await Assert.That(match).IsNotNull().Because(project.ProjectId);
                await Assert.That(match!.FamilyId).IsEqualTo(project.FamilyId);
                await Assert.That(match.Revisions.Count).IsEqualTo(project.Revisions.Count);
                foreach (DefinitionStoreRevision revision in project.Revisions)
                {
                    DefinitionStoreResult opened = await provider.OpenAsync(project.ProjectId, revision.Revision);
                    await Assert.That(opened).IsTypeOf<DefinitionStoreResult.Success>().Because($"{project.ProjectId}@{revision.Revision}");
                    var success = (DefinitionStoreResult.Success)opened;
                    await Assert.That(success.Definition.Digest.Value).IsEqualTo(revision.Digest);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }
}
