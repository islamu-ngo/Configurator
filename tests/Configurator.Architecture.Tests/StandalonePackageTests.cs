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
            "schemas/setup-definition-v1.schema.json", "schemas/islamu-configurator-command-v1.schema.json",
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
    public async Task BrowserPackageIsCompleteStaticWasmWithoutNativeOrTestAssemblies()
    {
        string directory = Path.Combine(ReleaseTestPaths.PackageRoot, "browser/wwwroot");
        foreach (string path in new[] { "index.html", "LICENSE", "THIRD-PARTY-NOTICES.md",
            "contracts/browser-release-capabilities.json", "schemas/setup-definition-v1.schema.json" })
            await Assert.That(File.Exists(Path.Combine(directory, path))).IsTrue().Because(path);
        string[] frameworkFiles = Directory.GetFiles(Path.Combine(directory, "_framework"));
        await Assert.That(frameworkFiles.Any(path => Path.GetFileName(path).StartsWith("dotnet", StringComparison.Ordinal)
            && Path.GetExtension(path) == ".js")).IsTrue();
        await Assert.That(frameworkFiles.Any(path => Path.GetFileName(path).StartsWith("Configurator.Browser", StringComparison.Ordinal)
            && Path.GetExtension(path) == ".wasm")).IsTrue();
        await Assert.That(Directory.GetFiles(Path.Combine(directory, "dependency-notices"), "*.nuspec", SearchOption.AllDirectories).Length)
            .IsGreaterThan(0);
        using JsonDocument dependencies = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "dependency-notices/packages.json")));
        await Assert.That(dependencies.RootElement.EnumerateArray().Any(item =>
            item.GetString()!.StartsWith("Microsoft.NETCore.App.Runtime.Mono.browser-wasm/", StringComparison.Ordinal))).IsTrue();
        await Assert.That(frameworkFiles.Where(path => Path.GetFileName(path).StartsWith("Configurator.Artifacts", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Configurator.Presentation", StringComparison.Ordinal)
            || Path.GetFileName(path).StartsWith("Event.", StringComparison.Ordinal)
            || Path.GetFileName(path).Contains("TUnit", StringComparison.Ordinal)
            || Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))).IsEmpty();
        using JsonDocument contract = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "contracts/browser-release-capabilities.json")));
        await Assert.That(contract.RootElement.GetProperty("capabilities").GetProperty("secretEntry").GetBoolean()).IsFalse();
        await Assert.That(contract.RootElement.GetProperty("capabilities").GetProperty("targetApply").GetBoolean()).IsFalse();
    }
}
