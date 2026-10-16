using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Configurator.Architecture.Tests;

[Category("Packaging")]
public sealed class MaintainerPackageTests
{
    [Test]
    public async Task InstalledLinuxCliValidatesAndCreatesProtectedArtifactWithoutOverwriting()
    {
        if (!OperatingSystem.IsLinux()) return;
        string package = Path.Combine(ReleaseTestPaths.PackageRoot, "cli/linux-x64");
        string directory = Path.Combine(Path.GetTempPath(), "configurator-installed-cli-" + Guid.NewGuid().ToString("N"));
        await Assert.That(Path.GetRelativePath(ReleaseTestPaths.Root, directory)
            .StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)).IsTrue();
        Directory.CreateDirectory(directory);
        try
        {
            string installed = Path.Combine(directory, "installed");
            Directory.CreateDirectory(installed);
            foreach (string source in Directory.EnumerateDirectories(package, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(installed, Path.GetRelativePath(package, source)));
            foreach (string source in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
                File.Copy(source, Path.Combine(installed, Path.GetRelativePath(package, source)));
            string executable = Path.Combine(installed, "islamu-configurator");
            File.SetUnixFileMode(executable, File.GetUnixFileMode(Path.Combine(package, "islamu-configurator")));

            string workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);
            byte[] definition = """
                {"schemaVersion":1,"familyId":"ac899fc7-d7f7-4ef3-9926-7f447387fe8c",
                 "revision":1,"applicationId":"example.service","applicationVersions":["1.0"],
                 "publisher":"example","title":"Installed service","entryPoints":["setup"],"locales":["en"],
                 "fields":[{"id":"service.name","type":"string","required":true,
                    "sensitivity":"sensitive","disclosure":"guided","title":"Service name"}],
                 "artifacts":[{"id":"config","format":"json","fileName":"service.json",
                    "applySemantics":"create","restart":"process",
                    "projection":{"op":"object","members":{"name":{"op":"copy","field":"service.name"}}}}]}
                """u8.ToArray();
            _ = SetupDefinitionCodec.ParseJson(definition);
            await File.WriteAllBytesAsync(Path.Combine(workspace, "definition.json"), definition);
            string answers = Path.Combine(workspace, "answers.json");
            await File.WriteAllTextAsync(answers, """{"service.name":"synthetic-installed-service"}""");
            string artifact = Path.Combine(workspace, "service.json");
            byte[] expected = """{"name":"synthetic-installed-service"}"""u8.ToArray();

            var validate = await ReleaseTestPaths.RunInDirectory(workspace, executable, "artifact", "validate",
                "--definition", "definition.json", "--artifact", "config", "--input", "answers.json", "--machine");
            await AssertInstalledReceipt(validate, "validate", 0, "none", expected);
            await Assert.That(File.Exists(artifact)).IsFalse();

            string[] create = ["artifact", "create", "--definition", "definition.json", "--artifact", "config",
                "--input", "answers.json", "--output", "service.json", "--machine"];
            var written = await ReleaseTestPaths.RunInDirectory(workspace, executable, create);
            await AssertInstalledReceipt(written, "create", 0, "written", expected);
            await Assert.That(await File.ReadAllBytesAsync(artifact)).IsEquivalentTo(expected);
            await Assert.That(File.GetUnixFileMode(artifact)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);

            await File.WriteAllTextAsync(answers, """{"service.name":"synthetic-replacement"}""");
            var conflict = await ReleaseTestPaths.RunInDirectory(workspace, executable, create);
            await AssertInstalledReceipt(conflict, "create", 74, "conflict", """{"name":"synthetic-replacement"}"""u8.ToArray());
            await Assert.That(await File.ReadAllBytesAsync(artifact)).IsEquivalentTo(expected);
            await Assert.That(File.GetUnixFileMode(artifact)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task AssertInstalledReceipt((int ExitCode, string Output, string StandardError) result,
        string operation, int exitCode, string writeStatus, byte[] expectedArtifact)
    {
        await Assert.That(result.ExitCode).IsEqualTo(exitCode);
        await Assert.That(result.StandardError).IsEmpty();
        await Assert.That(Encoding.UTF8.GetByteCount(result.Output)).IsLessThanOrEqualTo(65_536);
        await Assert.That(result.Output.EndsWith('\n')).IsTrue();
        await Assert.That(result.Output.Count(character => character == '\n')).IsEqualTo(1);
        using JsonDocument receipt = JsonDocument.Parse(result.Output);
        JsonElement root = receipt.RootElement;
        await Assert.That(root.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v2");
        await Assert.That(root.GetProperty("invocation").GetProperty("commandFamily").GetString()).IsEqualTo("artifact");
        await Assert.That(root.GetProperty("invocation").GetProperty("operation").GetString()).IsEqualTo(operation);
        await Assert.That(root.GetProperty("exitCode").GetInt32()).IsEqualTo(exitCode);
        await Assert.That(root.GetProperty("completion").GetString()).IsEqualTo(exitCode == 0 ? "completed" : "failed");
        await Assert.That(root.GetProperty("preparation").GetString()).IsEqualTo("ready");
        await Assert.That(root.GetProperty("targetAcceptance").GetString()).IsEqualTo("unassessed");
        await Assert.That(root.GetProperty("delivery").GetString()).IsEqualTo("attempt");
        await Assert.That(root.GetProperty("artifacts").GetArrayLength()).IsEqualTo(1);
        JsonElement output = root.GetProperty("artifacts")[0];
        await Assert.That(output.GetProperty("id").GetString()).IsEqualTo("config");
        await Assert.That(output.GetProperty("sensitivity").GetString()).IsEqualTo("restricted");
        await Assert.That(output.GetProperty("digest").GetString()).IsEqualTo(ReleaseTestPaths.Hash(expectedArtifact));
        await Assert.That(output.GetProperty("writeStatus").GetString()).IsEqualTo(writeStatus);
        await Assert.That(result.Output.Contains("synthetic-", StringComparison.Ordinal)).IsFalse();
        await Assert.That(result.Output.Contains("configurator-installed-cli-", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task PublishedCliCompilesOneSourceWithoutStoreAndRetainsImmutableRelease()
    {
        string package = Path.Combine(ReleaseTestPaths.PackageRoot, "cli/linux-x64");
        string executable = Path.Combine(package, "islamu-configurator");
        foreach (string relative in new[] { "schemas/configurator-authoring-v1.schema.json",
            "skills/configurator-integrate/SKILL.md", "skills/configurator-integrate/resources/authoring.md",
            "skills/configurator-integrate/resources/schema-import.md", "skills/configurator-integrate/resources/store-contribution.md" })
        {
            string distributed = Path.Combine(package, relative);
            await Assert.That(File.Exists(distributed)).IsTrue().Because(relative);
            await Assert.That(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(distributed)))
                .IsEqualTo(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(Path.Combine(ReleaseTestPaths.Root, relative))));
        }

        string directory = Path.Combine(Path.GetTempPath(), "configurator-maintainer-package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "configurator.json");
            string output = Path.Combine(directory, "release.json");
            await File.WriteAllTextAsync(source, """
                {"$schema":"urn:islamu:configurator:authoring:v1",
                 "identity":{"familyId":"d8b0a60f-27ee-4262-a2b3-3af0e12d5034","revision":1},
                 "project":{"id":"example.service","name":"Example Service"},
                 "applicationVersions":["1.0.0"],
                 "fields":{"SERVICE_PORT":{"type":"integer","sensitivity":"public","default":8080,"minimum":1,"maximum":65535}},
                 "outputs":{".env":{"format":"dotenv"}}}
                """);
            byte[] original = await File.ReadAllBytesAsync(source);
            var check = await ReleaseTestPaths.Run(executable, "definition", "check", "--source", source, "--machine");
            await Assert.That(check.ExitCode).IsEqualTo(0).Because(check.Output);
            using (JsonDocument result = JsonDocument.Parse(check.Output))
                await Assert.That(result.RootElement.GetProperty("schemaVersion").GetString())
                    .IsEqualTo("islamu-configurator-command/v2");
            await Assert.That(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(source)))
                .IsEqualTo(ReleaseTestPaths.Hash(original));

            var build = await ReleaseTestPaths.Run(executable, "definition", "build", "--source", source, "--output", output, "--machine");
            await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
            byte[] compiled = await File.ReadAllBytesAsync(output);
            SetupDefinition admitted = SetupDefinitionCodec.ParseJson(compiled);
            await Assert.That(admitted.ApplicationId).IsEqualTo("example.service");
            await Assert.That(admitted.Fields.Single().Id).IsEqualTo("SERVICE_PORT");
            var repeat = await ReleaseTestPaths.Run(executable, "definition", "build", "--source", source, "--output", output, "--machine");
            await Assert.That(repeat.ExitCode).IsEqualTo(0).Because(repeat.Output);
            await Assert.That(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(output)))
                .IsEqualTo(ReleaseTestPaths.Hash(compiled));

            await File.WriteAllTextAsync(source, (await File.ReadAllTextAsync(source)).Replace("8080", "8081", StringComparison.Ordinal));
            var conflict = await ReleaseTestPaths.Run(executable, "definition", "build", "--source", source, "--output", output, "--machine");
            await Assert.That(conflict.ExitCode).IsNotEqualTo(0);
            await Assert.That(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(output)))
                .IsEqualTo(ReleaseTestPaths.Hash(compiled));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
