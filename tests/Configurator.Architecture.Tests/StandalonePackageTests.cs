using System.Text.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[Category("Packaging")]
public sealed class StandalonePackageTests
{
    [Test]
    [Arguments("cli", "islamu-configurator")]
    [Arguments("terminal", "islamu-configurator-terminal")]
    [Arguments("desktop", "islamu-configurator-desktop")]
    public async Task NativePackageIncludesStandaloneExecutableAndOwnership(string surface, string executable)
    {
        string directory = Path.Combine(ReleaseTestPaths.PackageRoot, surface, "linux-x64");
        await Assert.That(File.Exists(Path.Combine(directory, executable))).IsTrue();
        foreach (string path in new[] { "LICENSE", "THIRD-PARTY-NOTICES.md",
            "schemas/setup-definition-v1.schema.json", "schemas/islamu-configurator-command-v2.schema.json",
            "contracts/package.json", "contracts/native-release-capabilities.json" })
            await Assert.That(File.Exists(Path.Combine(directory, path))).IsTrue().Because(path);
        using JsonDocument contract = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "contracts/package.json")));
        await Assert.That(contract.RootElement.GetProperty("surface").GetString()).IsEqualTo(surface);
        await Assert.That(contract.RootElement.GetProperty("executableName").GetString()).IsEqualTo(executable);
        await Assert.That(Directory.GetFiles(Path.Combine(directory, "dependency-notices"), "*.nuspec", SearchOption.AllDirectories).Length)
            .IsGreaterThan(0);
        using JsonDocument dependencies = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "dependency-notices/packages.json")));
        await Assert.That(dependencies.RootElement.EnumerateArray().Any(item =>
            item.GetString()!.StartsWith("Microsoft.NETCore.App.Runtime.linux-x64/", StringComparison.Ordinal))).IsTrue();
        await Assert.That(dependencies.RootElement.EnumerateArray().Select(item => item.GetString()!)
            .Where(name => name.StartsWith("Avalonia.Headless/", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Avalonia.Fonts.Inter/", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("TUnit", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Event.", StringComparison.Ordinal))).IsEmpty();
        await AssertTerminalDependencyNotices(directory, surface == "terminal");
        foreach (string notice in new[] { "LICENSE", "THIRD-PARTY-NOTICES.md" })
            await Assert.That(ReleaseTestPaths.Hash(File.ReadAllBytes(Path.Combine(directory, notice))))
                .IsEqualTo(ReleaseTestPaths.Hash(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root, notice))));
        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        await Assert.That(files.Where(path => Path.GetExtension(path) is ".dll" or ".pdb"
            || Path.GetFileName(path).StartsWith("Event.", StringComparison.Ordinal)
            || Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))).IsEmpty();
        if (surface == "cli")
        {
            var result = await ReleaseTestPaths.Run(Path.Combine(directory, executable), "--help");
            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BrowserPackageIsCompleteStaticWasmWithoutNativeOrTestAssemblies(bool includeStore)
    {
        string directory = Path.Combine(includeStore ? ReleaseTestPaths.StorePackageRoot : ReleaseTestPaths.PackageRoot, "browser/wwwroot");
        foreach (string path in new[] { "index.html", "manifest.webmanifest", "service-worker.js", "service-worker-assets.js",
            "pwa-lifecycle.js", "LICENSE", "THIRD-PARTY-NOTICES.md",
            "css/layers.css", "css/reset.css", "css/base.css", "css/tokens.css", "css/mudblazor-overrides.css",
            "css/components.css", "css/utilities.css", "browser-appearance.js", "browser-store.js",
            "_content/MudBlazor/MudBlazor.min.css", "_content/MudBlazor/MudBlazor.min.js",
            "contracts/browser-release-capabilities.json", "schemas/setup-definition-v1.schema.json" })
            await Assert.That(File.Exists(Path.Combine(directory, path))).IsTrue().Because(path);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "manifest.webmanifest")));
        JsonElement icons = manifest.RootElement.GetProperty("icons");
        await Assert.That(icons.GetArrayLength()).IsGreaterThan(0);
        foreach (JsonElement icon in icons.EnumerateArray())
        {
            string relative = icon.GetProperty("src").GetString()!;
            await Assert.That(Uri.TryCreate(relative, UriKind.Relative, out _) && !relative.Contains("..", StringComparison.Ordinal)).IsTrue();
            await Assert.That(File.Exists(Path.Combine(directory, relative))).IsTrue().Because(relative);
        }
        string[] frameworkFiles = Directory.GetFiles(Path.Combine(directory, "_framework"));
        await Assert.That(frameworkFiles.Any(path => Path.GetFileName(path).StartsWith("dotnet", StringComparison.Ordinal)
            && Path.GetExtension(path) == ".js")).IsTrue();
        await Assert.That(frameworkFiles.Any(path => Path.GetFileName(path).StartsWith("Configurator.Browser", StringComparison.Ordinal)
            && Path.GetExtension(path) == ".wasm")).IsTrue();
        foreach (string owner in new[] { "Core", "Presentation" })
            await Assert.That(frameworkFiles.Any(path => Path.GetFileName(path).StartsWith($"Configurator.{owner}", StringComparison.Ordinal)
                && Path.GetExtension(path) == ".wasm")).IsTrue().Because($"Browser-safe {owner} must be packaged.");
        await Assert.That(Directory.GetFiles(Path.Combine(directory, "dependency-notices"), "*.nuspec", SearchOption.AllDirectories).Length)
            .IsGreaterThan(0);
        using JsonDocument dependencies = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "dependency-notices/packages.json")));
        await Assert.That(dependencies.RootElement.EnumerateArray().Any(item =>
            item.GetString()!.StartsWith("Microsoft.NETCore.App.Runtime.Mono.browser-wasm/", StringComparison.Ordinal))).IsTrue();
        await AssertTerminalDependencyNotices(directory, expected: false);
        await Assert.That(frameworkFiles.Where(path => Path.GetFileName(path).StartsWith("Configurator.Artifacts", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Configurator.Cli", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Configurator.Terminal", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Configurator.Desktop", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Avalonia", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Terminal.Gui", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Spectre.Console", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Event.", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Explore.", StringComparison.Ordinal)
            || Path.GetFileName(path).Contains("TUnit", StringComparison.Ordinal)
            || Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))).IsEmpty();
        using JsonDocument contract = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "contracts/browser-release-capabilities.json")));
        JsonElement capabilities = contract.RootElement.GetProperty("capabilities");
        foreach (string enabled in new[] { "secretEntry", "secretEntryRequiresWorkspaceConsent", "privateExport",
            "privateExportRequiresUserAction", "publicAssetCache", "explicitPublicDefinitionCache" })
            await Assert.That(capabilities.GetProperty(enabled).GetBoolean()).IsTrue().Because(enabled);
        foreach (string disabled in new[] { "persistence", "answerPersistence", "nativeProtectedSave", "targetAuthentication", "targetApply", "telemetry" })
            await Assert.That(capabilities.GetProperty(disabled).GetBoolean()).IsFalse().Because(disabled);
    }

    private static async Task AssertTerminalDependencyNotices(string directory, bool expected)
    {
        string notices = Path.Combine(directory, "dependency-notices");
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(notices, "packages.json")));
        string[] packages = inventory.RootElement.EnumerateArray().Select(item => item.GetString()!).ToArray();
        await Assert.That(packages.Where(item => item.StartsWith("ISLAMU.Terminal.Gui/", StringComparison.OrdinalIgnoreCase))).IsEmpty();
        await Assert.That(Directory.Exists(Path.Combine(notices, "islamu.terminal.gui"))).IsFalse();
        foreach (string id in new[] { "Terminal.Gui", "TextMateSharp", "TextMateSharp.Grammars", "Onigwrap" })
        {
            string? package = packages.SingleOrDefault(item => item.StartsWith(id + "/", StringComparison.OrdinalIgnoreCase));
            await Assert.That(package is not null).IsEqualTo(expected).Because(id);
            string packageDirectory = Path.Combine(notices, id.ToLowerInvariant());
            await Assert.That(Directory.Exists(packageDirectory)).IsEqualTo(expected).Because(id);
            if (package is null) continue;
            string[] identity = package.Split('/');
            string versionDirectory = Path.Combine(packageDirectory, identity[1].ToLowerInvariant());
            await Assert.That(Directory.GetFiles(versionDirectory, "*.nuspec")).IsNotEmpty().Because(id);
            if (id != "Onigwrap")
            {
                string filename = id == "TextMateSharp.Grammars" ? "THIRD-PARTY-NOTICES.txt" : "LICENSE";
                string supplemental = Path.Combine(identity[0].ToLowerInvariant(), identity[1].ToLowerInvariant());
                await Assert.That(ReleaseTestPaths.Hash(File.ReadAllBytes(Path.Combine(notices, supplemental,
                    "supplemental", filename))))
                    .IsEqualTo(ReleaseTestPaths.Hash(File.ReadAllBytes(Path.Combine(ReleaseTestPaths.Root,
                        "eng/configurator/dependency-notices", supplemental, filename))));
            }
            if (id == "Onigwrap")
                await Assert.That(Directory.GetFiles(versionDirectory, "*", SearchOption.AllDirectories).Any(path =>
                    Path.GetFileName(path).Contains("license", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(path).Contains("notice", StringComparison.OrdinalIgnoreCase))).IsTrue();
        }
    }
}
