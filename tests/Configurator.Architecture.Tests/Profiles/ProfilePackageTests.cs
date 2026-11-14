using System.Security.Cryptography;
using System.Text.Json;

namespace Configurator.Architecture.Tests.Profiles;

[Category("Packaging")]
public sealed class ProfilePackageTests
{
    private static readonly string[] ForbiddenWorkerAssemblyPrefixes =
        ["Configurator.Artifacts", "Configurator.Cli", "Configurator.Desktop", "Configurator.Terminal", "Event.", "Explore.", "TUnit"];

    [Test]
    [Arguments("cli", "islamu-configurator")]
    [Arguments("terminal", "islamu-configurator-terminal")]
    [Arguments("desktop", "islamu-configurator-desktop")]
    public async Task PublishedNativeProfilesRetainReviewedCryptoClosureWithoutTargetAuthority(string surface, string executable)
    {
        string directory = RequirePackage(surface + "/linux-x64", executable, "dependency-notices/packages.json",
            "contracts/frozen-contract-baseline.json");
        using JsonDocument inventory = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(directory, "dependency-notices/packages.json")));
        string[] packages = inventory.RootElement.EnumerateArray().Select(item => item.GetString()!).ToArray();
        await Assert.That(packages).IsNotEmpty();
        foreach (string identity in new[] { "jose-jwt/5.3.0", "BouncyCastle.Cryptography/2.7.0" })
        {
            await Assert.That(packages.Count(item => item.Equals(identity, StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
            string notices = Path.Combine(directory, "dependency-notices", identity.ToLowerInvariant());
            await Assert.That(Directory.GetFiles(notices, "*.nuspec")).IsNotEmpty();
            await Assert.That(Directory.GetFiles(notices, "*", SearchOption.AllDirectories).Any(path =>
                Path.GetFileName(path).Contains("license", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Contains("notice", StringComparison.OrdinalIgnoreCase))).IsTrue();
        }
        using JsonDocument baseline = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(directory, "contracts/frozen-contract-baseline.json")));
        await Assert.That(baseline.RootElement.GetProperty("targetApply").GetBoolean()).IsFalse();
        await Assert.That(baseline.RootElement.GetProperty("signedPublicationEnabled").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task PublishedBrowserProfilesRetainWorkerCryptoAndTransientCustodyContract()
    {
        string directory = RequirePackage("browser/wwwroot", "profile-worker/profile-worker.js",
            "profile-worker/_framework/dotnet.js", "profile-worker/vendor/profile-crypto.js",
            "profile-worker/vendor/profile-crypto.manifest.json", "profile-worker/vendor/profile-crypto.LICENSE.txt",
            "contracts/browser-release-capabilities.json", "service-worker-assets.js");
        string worker = Path.Combine(directory, "profile-worker");
        string[] assemblies = Directory.GetFiles(Path.Combine(worker, "_framework"), "*.wasm");
        await Assert.That(assemblies).IsNotEmpty();
        foreach (string owner in new[] { "Configurator.Browser.Worker", "Configurator.Core" })
            await Assert.That(assemblies.Any(path => Path.GetFileName(path).StartsWith(owner + ".", StringComparison.Ordinal))).IsTrue();
        await Assert.That(assemblies.Where(path => ForbiddenWorkerAssemblyPrefixes
            .Any(prefix => Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal)))).IsEmpty();
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(worker, "vendor/profile-crypto.manifest.json")));
        await Assert.That(manifest.RootElement.GetProperty("name").GetString()).IsEqualTo("jose");
        await Assert.That(manifest.RootElement.GetProperty("version").GetString()).IsEqualTo("6.2.17");
        await Assert.That(manifest.RootElement.GetProperty("license").GetString()).IsEqualTo("MIT");
        JsonElement outputs = manifest.RootElement.GetProperty("outputs");
        await Assert.That(outputs.EnumerateObject().Select(item => item.Name)).IsEquivalentTo(
            ["profile-crypto.js", "profile-crypto.LICENSE.txt"]);
        foreach (string name in new[] { "profile-crypto.js", "profile-crypto.LICENSE.txt" })
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(worker, "vendor", name));
            await Assert.That(bytes.Length).IsGreaterThan(0);
            await Assert.That(Convert.ToHexStringLower(SHA256.HashData(bytes)))
                .IsEqualTo(outputs.GetProperty(name).GetProperty("sha256").GetString());
            await Assert.That(bytes.LongLength).IsEqualTo(outputs.GetProperty(name).GetProperty("bytes").GetInt64());
        }
        await Assert.That(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(
            Path.Combine(worker, "vendor/profile-crypto.LICENSE.txt"))))
            .IsEqualTo("8078b0829d6c3e9ecde2f003e1966e4cad3efc6e7a640efbab0313cc166b1af1");
        using JsonDocument contract = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(directory, "contracts/browser-release-capabilities.json")));
        JsonElement capabilities = contract.RootElement.GetProperty("capabilities");
        foreach (string name in new[] { "persistence", "answerPersistence", "nativeProtectedSave",
            "targetAuthentication", "targetApply", "telemetry" })
            await Assert.That(capabilities.GetProperty(name).GetBoolean()).IsFalse().Because(name);
        await Assert.That(capabilities.GetProperty("secretEntryRequiresWorkspaceConsent").GetBoolean()).IsTrue();
        await Assert.That(capabilities.GetProperty("privateExportRequiresUserAction").GetBoolean()).IsTrue();
        string[] assets = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        await Assert.That(assets).IsNotEmpty();
        await Assert.That(assets.Where(path => path.EndsWith(".cfgvault", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".cfgcompare.json", StringComparison.OrdinalIgnoreCase))).IsEmpty();
    }

    [Test]
    public async Task PublishedCliExercisesSyntheticProfileFormatAndCipherPolicy()
    {
        string directory = RequirePackage("cli/linux-x64", "islamu-configurator", "contracts/package.json",
            "schemas/configurator-cli.schema.json");
        var result = await ReleaseTestPaths.RunInDirectory(ReleaseTestPaths.Root, "dotnet", "run",
            ".ci/scripts/validate-cli-package.cs", "--", "--package-directory", directory,
            "--expected-rid", "linux-x64");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because("Published CLI synthetic validation must pass.");
        await Assert.That(result.StandardError).IsEmpty();
        await Assert.That(result.Output).Contains("CLI_PACKAGE_COHORT encrypted-profile-policy passed");
        await Assert.That(result.Output).Contains("CLI_PACKAGE_RESULT passed failedCohorts=0");
    }

    private static string RequirePackage(string relative, params string[] required)
    {
        string directory = Path.Combine(ReleaseTestPaths.PackageRoot, relative);
        if (!Directory.Exists(directory) || required.Any(path => !File.Exists(Path.Combine(directory, path))))
            throw new InvalidOperationException("PROFILE_PACKAGE_PRECONDITION: publish all required real package inputs before security qualification.");
        return directory;
    }
}
