using System.Text.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class ReleaseContractTests
{
    [Test]
    [Arguments("cli", "islamu-configurator", 4)]
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
            await Assert.That(root.GetProperty("selfContained").GetBoolean()).IsTrue();
            await Assert.That(root.GetProperty("publishSingleFile").GetBoolean()).IsTrue();
            await Assert.That(root.GetProperty("publishTrimmed").GetBoolean()).IsFalse();
            await Assert.That(root.GetProperty("includeNativeLibrariesForSelfExtract").GetBoolean()).IsTrue();
            await Assert.That(root.GetProperty("protectedExportHosts").EnumerateArray().Select(item => item.GetString()!)).IsEquivalentTo(["linux"]);
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Arguments("-p:ConfiguratorSurface=unknown")]
    [Arguments("-p:ConfiguratorRid=linux-musl-x64")]
    [Arguments("-p:ConfiguratorRid=osx-x64")]
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
    public async Task DesktopCannotClaimUnverifiedRuntime()
    {
        var result = await ReleaseTestPaths.Run("dotnet", "msbuild", "eng/configurator/Configurator.Release.proj",
            "-nologo", "-verbosity:quiet", "-t:ValidateConfiguratorRelease",
            "-p:ConfiguratorSurface=desktop", "-p:ConfiguratorRid=win-x64");
        await Assert.That(result.ExitCode).IsNotEqualTo(0);
    }
}
