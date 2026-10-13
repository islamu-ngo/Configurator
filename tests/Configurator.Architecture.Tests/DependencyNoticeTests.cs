using System.Text.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[NotInParallel]
public sealed class DependencyNoticeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SupplementalReviewRejectsMissingVersionAndDifferentPackageHash(bool matchingVersion)
    {
        string root = Path.Combine(Path.GetTempPath(), $"configurator-notices-{Guid.NewGuid():N}");
        string cache = Path.Combine(root, "cache");
        string package = Path.Combine(cache, "fixture.package/1.0.0");
        string review = Path.Combine(root, "eng/configurator/dependency-notices/fixture.package",
            matchingVersion ? "1.0.0" : "2.0.0");
        Directory.CreateDirectory(package);
        Directory.CreateDirectory(review);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "Configurator.slnx"), "<Solution />");
            string project = Path.Combine(root, "Fixture.csproj");
            await File.WriteAllTextAsync(project,
                """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
            await File.WriteAllTextAsync(Path.Combine(package, "fixture.package.nuspec"),
                "<package><metadata><id>Fixture.Package</id><version>1.0.0</version></metadata></package>");
            string restoredHash = Convert.ToBase64String(new byte[64]);
            await File.WriteAllTextAsync(Path.Combine(package, ".nupkg.metadata"),
                JsonSerializer.Serialize(new { contentHash = restoredHash }));
            string reviewedHash = matchingVersion
                ? Convert.ToBase64String(Enumerable.Repeat((byte)1, 64).ToArray()) : restoredHash;
            await File.WriteAllTextAsync(Path.Combine(review, "manifest.json"),
                JsonSerializer.Serialize(new { contentHash = reviewedHash }));
            await File.WriteAllTextAsync(Path.Combine(review, "NOTICE"), "Synthetic component notice.");
            string assets = Path.Combine(root, "project.assets.json");
            await File.WriteAllTextAsync(assets, JsonSerializer.Serialize(new
            {
                packageFolders = new Dictionary<string, object> { [cache] = new { } },
                libraries = new Dictionary<string, object>
                {
                    ["Fixture.Package/1.0.0"] = new
                    {
                        type = "package",
                        path = "fixture.package/1.0.0",
                        sha512 = restoredHash,
                        files = Array.Empty<string>()
                    }
                }
            }));
            string output = Path.Combine(root, "output");
            var rejected = await ReleaseTestPaths.Run("dotnet", "run", ".ci/scripts/write-dependency-notices.cs", "--",
                assets, output, project, "linux-x64");
            await Assert.That(rejected.ExitCode).IsEqualTo(65).Because(rejected.Output);
            await Assert.That(File.Exists(Path.Combine(output, "fixture.package/1.0.0/supplemental/NOTICE"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(output, "packages.json"))).IsFalse();
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
