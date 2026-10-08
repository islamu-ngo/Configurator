using System.Text.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class PolicyGateTests
{
    [Test]
    public async Task DependencyGateRejectsCopyleftUnreviewedAndMissingMetadata()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"configurator-license-{Guid.NewGuid():N}");
        string cache = Path.Combine(directory, "cache");
        string metadata = Path.Combine(cache, "fixture.package/1.0.0/fixture.package.nuspec");
        foreach (string child in new[] { "src", "tests", "eng", ".ci/policy", "cache/fixture.package/1.0.0" })
            Directory.CreateDirectory(Path.Combine(directory, child));
        try
        {
            File.Copy(Path.Combine(ReleaseTestPaths.Root, ".ci/policy/dependency-licenses.json"),
                Path.Combine(directory, ".ci/policy/dependency-licenses.json"));
            await File.WriteAllTextAsync(Path.Combine(directory, "src/packages.lock.json"),
                """{"version":2,"dependencies":{"net10.0":{"Fixture.Package":{"type":"Direct","resolved":"1.0.0"}}}}""");
            foreach ((string expression, int exitCode) in new[]
            {
                ("GPL-3.0-only", 1), ("LicenseRef-Proprietary", 1), ("MIT", 0)
            })
            {
                await File.WriteAllTextAsync(metadata,
                    $"<package><metadata><license type=\"expression\">{expression}</license></metadata></package>");
                var result = await ReleaseTestPaths.Run("dotnet", "run",
                    ".ci/scripts/validate-dependency-license-policy.cs", "--", directory, cache);
                await Assert.That(result.ExitCode).IsEqualTo(exitCode).Because(result.Output);
            }
            File.Delete(metadata);
            var missing = await ReleaseTestPaths.Run("dotnet", "run",
                ".ci/scripts/validate-dependency-license-policy.cs", "--", directory, cache);
            await Assert.That(missing.ExitCode).IsEqualTo(1).Because(missing.Output);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task NugetAuditRejectsVulnerabilitiesAndEmptyReports()
    {
        string path = Path.Combine(Path.GetTempPath(), $"configurator-audit-{Guid.NewGuid():N}.json");
        try
        {
            foreach (string report in new[]
            {
                """{"version":1,"projects":[{"path":"sample.csproj","frameworks":[{"framework":"net10.0","transitivePackages":[{"id":"unsafe","vulnerabilities":[{"severity":"High","advisoryUrl":"https://example.invalid/advisory"}]}]}]}]}""",
                """{"version":1,"projects":[]}""",
                """{"version":1,"projects":[{}]}""",
                """{"version":1,"projects":[{"path":"sample.csproj","frameworks":[{"framework":"net10.0"}]}],"problems":[{"severity":"warning","message":"audit-source-unavailable"}]}"""
            })
            {
                await File.WriteAllTextAsync(path, report);
                var rejected = await ReleaseTestPaths.Run("dotnet", "run", ".ci/scripts/validate-nuget-vulnerabilities.cs", "--", path);
                await Assert.That(rejected.ExitCode).IsEqualTo(1).Because(rejected.Output);
            }
            foreach (string report in new[]
            {
                """{"version":1,"projects":[{"path":"sample.csproj","frameworks":[{"framework":"net10.0","topLevelPackages":[]}]}]}""",
                """{"version":1,"projects":[{"path":"sample.csproj"}]}"""
            })
            {
                await File.WriteAllTextAsync(path, report);
                var accepted = await ReleaseTestPaths.Run("dotnet", "run", ".ci/scripts/validate-nuget-vulnerabilities.cs", "--", path);
                await Assert.That(accepted.ExitCode).IsEqualTo(0).Because(accepted.Output);
            }
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task ActionGateRejectsInlineUnpinnedUses()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"configurator-action-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "gate.yml");
            await File.WriteAllTextAsync(path, "steps:\n  - uses: actions/checkout@v7\n");
            var rejected = await ReleaseTestPaths.Run("dotnet", "run", ".ci/scripts/validate-action-pins.cs", "--", directory);
            await Assert.That(rejected.ExitCode).IsEqualTo(1).Because(rejected.Output);
            await File.WriteAllTextAsync(path, "steps:\n  - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1\n");
            var accepted = await ReleaseTestPaths.Run("dotnet", "run", ".ci/scripts/validate-action-pins.cs", "--", directory);
            await Assert.That(accepted.ExitCode).IsEqualTo(0).Because(accepted.Output);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task ChecksumsCoverActualBytesAndRejectLinkedInputs()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"configurator-checksum-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "package.json");
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(new { version = 1 });
            await File.WriteAllBytesAsync(input, content);
            string output = Path.Combine(directory, "checksums.sha256");
            var result = await ReleaseTestPaths.Run("dotnet", "run", ".ci/scripts/write-artifact-checksums.cs", "--", directory, output);
            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            await Assert.That(await File.ReadAllTextAsync(output)).IsEqualTo(ReleaseTestPaths.Hash(content) + "  package.json\n");
            File.CreateSymbolicLink(Path.Combine(directory, "alias.json"), input);
            var linked = await ReleaseTestPaths.Run("dotnet", "run", ".ci/scripts/write-artifact-checksums.cs", "--", directory, output);
            await Assert.That(linked.ExitCode).IsNotEqualTo(0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
