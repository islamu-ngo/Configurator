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
                    .IsEqualTo("islamu-configurator-command/v1");
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
