namespace Configurator.Architecture.Tests;

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

public sealed class BrowserProfileWorkerArchitectureTests
{
    [Test]
    public async Task ExecutableGraphsKeepBrowserAndWorkerRuntimeDependenciesSeparate()
    {
        var worker = await ReleaseTestPaths.Run("dotnet", "msbuild",
            "src/Configurator.Browser.Worker/Configurator.Browser.Worker.csproj", "-nologo",
            "-verbosity:quiet", "-p:Configuration=Release",
            "-getProperty:OutputType,RunAOTCompilation,PublishTrimmed", "-getItem:ProjectReference");
        await Assert.That(worker.ExitCode).IsEqualTo(0).Because(worker.Output);
        using JsonDocument workerGraph = JsonDocument.Parse(worker.Output);
        JsonElement workerProperties = workerGraph.RootElement.GetProperty("Properties");
        await Assert.That(workerProperties.GetProperty("OutputType").GetString()).IsEqualTo("Exe");
        await Assert.That(workerProperties.GetProperty("RunAOTCompilation").GetString()).IsEqualTo("true");
        await Assert.That(workerProperties.GetProperty("PublishTrimmed").GetString()).IsEqualTo("true");
        string[] dependencies = workerGraph.RootElement.GetProperty("Items").GetProperty("ProjectReference")
            .EnumerateArray().Select(item => Path.GetFileName(item.GetProperty("FullPath").GetString())).ToArray()!;
        await Assert.That(dependencies).IsEquivalentTo(["Configurator.Core.csproj"]);

        var browser = await ReleaseTestPaths.Run("dotnet", "msbuild",
            "src/Configurator.Browser/Configurator.Browser.csproj", "-nologo",
            "-verbosity:quiet", "-p:Configuration=Release",
            "-getProperty:OutputType,RunAOTCompilation", "-getItem:ProjectReference");
        await Assert.That(browser.ExitCode).IsEqualTo(0).Because(browser.Output);
        using JsonDocument browserGraph = JsonDocument.Parse(browser.Output);
        JsonElement browserProperties = browserGraph.RootElement.GetProperty("Properties");
        await Assert.That(browserProperties.GetProperty("OutputType").GetString()).IsEqualTo("Exe");
        await Assert.That(browserProperties.GetProperty("RunAOTCompilation").GetString()).IsEqualTo("true");
        string[] browserDependencies = browserGraph.RootElement.GetProperty("Items").GetProperty("ProjectReference")
            .EnumerateArray().Select(item => Path.GetFileName(item.GetProperty("FullPath").GetString())).ToArray()!;
        await Assert.That(browserDependencies).IsEquivalentTo(["Configurator.Core.csproj", "Configurator.Presentation.csproj"]);
    }

    [Test]
    public async Task BuiltBrowserTreatsPublishedWorkerFilesAsOrdinaryStaticContent()
    {
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(ReleaseTestPaths.Root,
            "src/Configurator.Browser/obj/Release/net10.0/staticwebassets.build.json")));
        JsonElement[] assets = manifest.RootElement.GetProperty("Assets").EnumerateArray().ToArray();
        JsonElement[] workerAssets = assets.Where(asset => asset.GetProperty("RelativePath").GetString()!
            .StartsWith("profile-worker/", StringComparison.Ordinal)).ToArray();
        await Assert.That(workerAssets).IsNotEmpty();
        await Assert.That(workerAssets.Any(asset => asset.GetProperty("RelativePath").GetString()!
            .StartsWith("profile-worker/_framework/Configurator.Browser.Worker.", StringComparison.Ordinal))).IsTrue();
        foreach (JsonElement asset in workerAssets)
        {
            await Assert.That(asset.GetProperty("AssetTraitName").GetString()).IsNotEqualTo("WasmResource");
            await Assert.That(asset.GetProperty("SourceId").GetString()).IsEqualTo("Configurator.Browser");
        }
        await Assert.That(assets.Where(asset => asset.GetProperty("AssetTraitName").GetString() == "WasmResource"
            && asset.GetProperty("SourceId").GetString() != "Configurator.Browser")).IsEmpty();
    }

    [Test]
    public async Task PublishedWorkerPublicFilesHaveMatchingOfflineManifestHashes()
    {
        string directory = Path.Combine(ReleaseTestPaths.PackageRoot, "browser/wwwroot");
        string text = await File.ReadAllTextAsync(Path.Combine(directory, "service-worker-assets.js"));
        using JsonDocument manifest = JsonDocument.Parse(text[text.IndexOf('{')..(text.LastIndexOf('}') + 1)]);
        Dictionary<string, string> inventory = manifest.RootElement.GetProperty("assets").EnumerateArray()
            .ToDictionary(asset => asset.GetProperty("url").GetString()!, asset => asset.GetProperty("hash").GetString()!,
                StringComparer.Ordinal);
        string[] publicFiles = Directory.GetFiles(Path.Combine(directory, "profile-worker"), "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is not (".br" or ".gz")).ToArray();
        await Assert.That(publicFiles).IsNotEmpty();
        foreach (string path in publicFiles)
        {
            string relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            await Assert.That(inventory.ContainsKey(relative)).IsTrue().Because(relative);
            await Assert.That(inventory[relative])
                .IsEqualTo("sha256-" + Convert.ToBase64String(SHA256.HashData(await File.ReadAllBytesAsync(path))))
                .Because(relative);
        }
    }

    [Test]
    public async Task CompiledBrowserNeverLoadsTheWorkerCustodyAssembly()
    {
        using var stream = File.OpenRead(Path.Combine(ReleaseTestPaths.Root,
            "src/Configurator.Browser/bin/Release/net10.0/Configurator.Browser.dll"));
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        await Assert.That(metadata.AssemblyReferences.Select(reference =>
            metadata.GetString(metadata.GetAssemblyReference(reference).Name)))
            .DoesNotContain("Configurator.Browser.Worker");
    }

    [Test]
    public async Task GeneratedCryptoOutputRetainsTheReviewedOriginalLicenseAndResolvedIdentity()
    {
        string vendor = Path.Combine(ReleaseTestPaths.Root, "src/Configurator.Browser.Worker/wwwroot/vendor");
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(vendor, "profile-crypto.manifest.json")));
        JsonElement metadata = manifest.RootElement;
        await Assert.That(metadata.GetProperty("name").GetString()).IsEqualTo("jose");
        await Assert.That(metadata.GetProperty("version").GetString()).IsEqualTo("6.2.17");
        foreach (string name in new[] { "profile-crypto.js", "profile-crypto.LICENSE.txt" })
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(vendor, name));
            await Assert.That(Convert.ToHexStringLower(SHA256.HashData(bytes)))
                .IsEqualTo(metadata.GetProperty("outputs").GetProperty(name).GetProperty("sha256").GetString());
        }
        byte[] originalTerms = await File.ReadAllBytesAsync(Path.Combine(vendor, "profile-crypto.LICENSE.txt"));
        await Assert.That(Convert.ToHexStringLower(SHA256.HashData(originalTerms)))
            .IsEqualTo("8078b0829d6c3e9ecde2f003e1966e4cad3efc6e7a640efbab0313cc166b1af1");
    }
}
