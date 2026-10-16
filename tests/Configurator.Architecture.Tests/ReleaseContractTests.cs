using System.Text.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class ReleaseContractTests
{
    [Test]
    [Arguments("cli", "islamu-configurator", 7)]
    [Arguments("terminal", "islamu-configurator-terminal", 4)]
    [Arguments("desktop", "islamu-configurator-desktop", 1)]
    public async Task EvaluatedReleaseContractPreservesStandaloneAndSupportedHosts(string surface, string executable, int ridCount)
    {
        string path = Path.Combine(Path.GetTempPath(), $"configurator-release-test-{Guid.NewGuid():N}.json");
        try
        {
            var result = await ReleaseTestPaths.Run("dotnet", "msbuild", "eng/configurator/Configurator.Release.proj",
                "-nologo", "-verbosity:quiet", "-t:WriteConfiguratorReleaseContract",
                $"-p:ConfiguratorSurface={surface}", $"-p:ConfiguratorContractOutput={path}");
            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            using JsonDocument contract = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = contract.RootElement;
            await Assert.That(root.GetProperty("executableName").GetString()).IsEqualTo(executable);
            await Assert.That(root.GetProperty("runtimeIdentifiers").GetArrayLength()).IsEqualTo(ridCount);
            if (surface == "cli")
            {
                await Assert.That(root.GetProperty("runtimeIdentifiers").EnumerateArray().Select(item => item.GetString()!))
                    .IsEquivalentTo(["linux-x64", "linux-arm64", "linux-musl-x64", "linux-musl-arm64", "osx-x64", "osx-arm64", "win-x64"]);
            }
            await Assert.That(root.GetProperty("selfContained").GetBoolean()).IsTrue();
            await Assert.That(root.GetProperty("publishSingleFile").GetBoolean()).IsTrue();
            await Assert.That(root.GetProperty("publishTrimmed").GetBoolean()).IsFalse();
            await Assert.That(root.GetProperty("includeNativeLibrariesForSelfExtract").GetBoolean()).IsTrue();
            await Assert.That(root.GetProperty("protectedExportHosts").EnumerateArray().Select(item => item.GetString()!)).IsEquivalentTo(["linux"]);
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Arguments("linux-x64")]
    [Arguments("linux-arm64")]
    [Arguments("linux-musl-x64")]
    [Arguments("linux-musl-arm64")]
    [Arguments("osx-x64")]
    [Arguments("osx-arm64")]
    [Arguments("win-x64")]
    public async Task CliAdmitsEachDeclaredModernRuntime(string rid)
    {
        var result = await ReleaseTestPaths.Run("dotnet", "msbuild", "eng/configurator/Configurator.Release.proj",
            "-nologo", "-verbosity:quiet", "-t:ValidateConfiguratorRelease",
            "-p:ConfiguratorSurface=cli", $"-p:ConfiguratorRid={rid}");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
    }

    [Test]
    [Arguments("cli")]
    [Arguments("terminal")]
    [Arguments("desktop")]
    public async Task SchemaRefreshRemovesRetiredSchemasAndPreservesUnrelatedPackageContent(string surface)
    {
        string outputRoot = Path.Combine(Path.GetTempPath(), $"configurator-schema-refresh-{Guid.NewGuid():N}");
        string package = Path.Combine(outputRoot, surface, "linux-x64");
        string schemas = Path.Combine(package, "schemas");
        string[] currentSchemas = Directory.GetFiles(Path.Combine(ReleaseTestPaths.Root, "schemas"), "*.schema.json");
        try
        {
            Directory.CreateDirectory(schemas);
            Directory.CreateDirectory(Path.Combine(package, "contracts"));
            File.WriteAllText(Path.Combine(schemas, "islamu-configurator-command-v1.schema.json"), "stale command v1");
            File.WriteAllText(Path.Combine(schemas, "retired.schema.json"), "retired schema");
            File.WriteAllText(Path.Combine(package, "keep.txt"), "unrelated package content");
            File.WriteAllText(Path.Combine(package, "contracts", "keep.json"), "{}");

            var result = await ReleaseTestPaths.Run("dotnet", "msbuild", "eng/configurator/Configurator.Release.proj",
                "-nologo", "-verbosity:quiet", "-t:RefreshConfiguratorSchemas",
                $"-p:ConfiguratorSurface={surface}", "-p:ConfiguratorRid=linux-x64", $"-p:ConfiguratorOutputRoot={outputRoot}");

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            await Assert.That(currentSchemas).IsNotEmpty();
            await Assert.That(Directory.GetFiles(schemas, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(schemas, path)))
                .IsEquivalentTo(currentSchemas.Select(Path.GetFileName));
            foreach (string source in currentSchemas)
            {
                await Assert.That(ReleaseTestPaths.Hash(File.ReadAllBytes(Path.Combine(schemas, Path.GetFileName(source)))))
                    .IsEqualTo(ReleaseTestPaths.Hash(File.ReadAllBytes(source)));
            }
            await Assert.That(File.Exists(Path.Combine(schemas, "retired.schema.json"))).IsFalse();
            await Assert.That(File.ReadAllText(Path.Combine(package, "keep.txt"))).IsEqualTo("unrelated package content");
            await Assert.That(File.ReadAllText(Path.Combine(package, "contracts", "keep.json"))).IsEqualTo("{}");
        }
        finally { Directory.Delete(outputRoot, recursive: true); }
    }

    [Test]
    [Arguments("-p:ConfiguratorSurface=unknown")]
    [Arguments("-p:ConfiguratorRid=win7-x64")]
    [Arguments("-p:ConfiguratorRid=win-x86")]
    [Arguments("-p:ConfiguratorRid=osx-arm32")]
    [Arguments("-p:SelfContained=false")]
    [Arguments("-p:PublishTrimmed=true")]
    [Arguments("-p:PublishSingleFile=false")]
    [Arguments("-p:ConfiguratorExecutableName=event-setup")]
    public async Task UnsupportedReleaseContractFailsClosed(string property)
    {
        var result = await ReleaseTestPaths.Run("dotnet", "msbuild", "eng/configurator/Configurator.Release.proj",
            "-nologo", "-verbosity:quiet", "-t:ValidateConfiguratorRelease", property);
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    [Arguments("linux-musl-x64")]
    [Arguments("linux-musl-arm64")]
    [Arguments("osx-x64")]
    public async Task CliRuntimeExpansionDoesNotExpandTerminalAdmission(string rid)
    {
        var result = await ReleaseTestPaths.Run("dotnet", "msbuild", "eng/configurator/Configurator.Release.proj",
            "-nologo", "-verbosity:quiet", "-t:ValidateConfiguratorRelease",
            "-p:ConfiguratorSurface=terminal", $"-p:ConfiguratorRid={rid}");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task DesktopCannotClaimUnverifiedRuntime()
    {
        var result = await ReleaseTestPaths.Run("dotnet", "msbuild", "eng/configurator/Configurator.Release.proj",
            "-nologo", "-verbosity:quiet", "-t:ValidateConfiguratorRelease",
            "-p:ConfiguratorSurface=desktop", "-p:ConfiguratorRid=win-x64");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
    }
}
