using System.Security.Cryptography;
using System.Text.Json;

namespace Configurator.Architecture.Tests;

[Category("Packaging")]
public sealed class BrowserEditorPackageTests
{
    [Test]
    public async Task NativeEditorAssetOwnerRejectsTamperingOffline()
    {
        var result = await ReleaseTestPaths.Run("dotnet", "run", "--file",
            "eng/configurator/BuildBrowserEditor.cs", "--", "--self-test");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
    }

    [Test]
    public async Task CheckedInEditorBindsApprovedInputsClosureAndOutputs()
    {
        var result = await ReleaseTestPaths.Run("dotnet", "run", "--file",
            "eng/configurator/BuildBrowserEditor.cs", "--", "--check", ReleaseTestPaths.Root);
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PublishedEditorRetainsExactAssetsAndOfflineInventory(bool includeStore)
    {
        string directory = Path.Combine(includeStore ? ReleaseTestPaths.StorePackageRoot : ReleaseTestPaths.PackageRoot,
            "browser/wwwroot");
        string source = Path.Combine(ReleaseTestPaths.Root, "src/Configurator.Browser/wwwroot");
        string worker = await File.ReadAllTextAsync(Path.Combine(directory, "service-worker-assets.js"));
        using JsonDocument assets = JsonDocument.Parse(worker[worker.IndexOf('{')..(worker.LastIndexOf('}') + 1)]);
        Dictionary<string, string> inventory = assets.RootElement.GetProperty("assets").EnumerateArray()
            .ToDictionary(asset => asset.GetProperty("url").GetString()!, asset => asset.GetProperty("hash").GetString()!,
                StringComparer.Ordinal);
        string[] paths =
        [
            "js/configuration-developer.js",
            "vendor/configuration-editor.js",
            "vendor/configuration-editor.manifest.json",
            "vendor/configuration-editor.LICENSE.txt"
        ];
        foreach (string relative in paths)
        {
            byte[] published = await File.ReadAllBytesAsync(Path.Combine(directory, relative));
            byte[] owned = await File.ReadAllBytesAsync(Path.Combine(source, relative));
            await Assert.That(published.AsSpan().SequenceEqual(owned)).IsTrue().Because(relative);
            await Assert.That(inventory.ContainsKey(relative)).IsTrue().Because(relative);
            await Assert.That(inventory[relative]).IsEqualTo("sha256-" + Convert.ToBase64String(SHA256.HashData(published)));
        }
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(directory, "vendor/configuration-editor.manifest.json")));
        foreach (JsonProperty output in manifest.RootElement.GetProperty("outputs").EnumerateObject())
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(directory, "vendor", output.Name));
            await Assert.That(ReleaseTestPaths.Hash(bytes)).IsEqualTo(output.Value.GetProperty("sha256").GetString());
            await Assert.That(bytes.LongLength).IsEqualTo(output.Value.GetProperty("bytes").GetInt64());
        }
    }
}
