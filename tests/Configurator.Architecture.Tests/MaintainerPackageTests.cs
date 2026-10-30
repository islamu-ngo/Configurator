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
    public async Task InstalledCliValidatesAndEnforcesHostProtectedCreateContract()
    {
        string package = ReleaseTestPaths.NativeCliPackage;
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
            string executable = Path.Combine(installed, ReleaseTestPaths.CliExecutableName);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(executable, File.GetUnixFileMode(Path.Combine(package, ReleaseTestPaths.CliExecutableName)));

            string workspace = Path.Combine(directory, "workspace");
            Directory.CreateDirectory(workspace);
            byte[] definition = """
                {"schemaVersion":2,"profile":"bounded-v2","familyId":"ac899fc7-d7f7-4ef3-9926-7f447387fe8c",
                 "revision":1,"project":{"id":"example.service","name":"Installed service","publisher":"example"},"applicationVersions":["1.0"],
                 "entryPoints":["setup"],"locales":["en"],
                 "requiredCapabilities":["projection.copy","projection.object","schema.object","schema.string"],
                 "configuration":{"type":"object","additionalProperties":false,"required":["service.name"],"properties":{
                    "service.name":{"type":"string","title":"Service name","x-configurator":{"sensitivity":"sensitive","detail":"standard"}}}},
                 "artifacts":[{"id":"config","format":"json","fileName":"service.json",
                    "targetUpdateMode":"create","restartRequirement":"process",
                    "projection":{"op":"object","members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}}]}
                """u8.ToArray();
            _ = SetupDefinitionCodec.ParseJson(definition);
            await File.WriteAllBytesAsync(Path.Combine(workspace, "definition.json"), definition);
            string answers = Path.Combine(workspace, "answers.json");
            await File.WriteAllTextAsync(answers, """{"answerContractVersion":1,"values":{"service.name":"synthetic-installed-service"},"omitted":[]}""");
            string artifact = Path.Combine(workspace, "service.json");
            byte[] expected = """{"name":"synthetic-installed-service"}"""u8.ToArray();

            var validate = await ReleaseTestPaths.RunInDirectory(workspace, executable, "artifact", "validate",
                "--definition", "definition.json", "--artifact", "config", "--input", "answers.json", "--machine");
            await AssertInstalledReceipt(validate, "validate", 0, "none", expected);
            await Assert.That(File.Exists(artifact)).IsFalse();

            string[] create = ["artifact", "create", "--definition", "definition.json", "--artifact", "config",
                "--input", "answers.json", "--output", "service.json", "--machine"];
            var written = await ReleaseTestPaths.RunInDirectory(workspace, executable, create);
            if (!OperatingSystem.IsLinux())
            {
                await AssertInstalledReceipt(written, "create", 74, "blocked", expected);
                await Assert.That(File.Exists(artifact)).IsFalse();
                await Assert.That(Directory.GetFiles(workspace).Length).IsEqualTo(2);
                Console.WriteLine($"COHORT installed-protected-create unsupported-refusal-verified rid={ReleaseTestPaths.NativeCliRid}");
                return;
            }
            await AssertInstalledReceipt(written, "create", 0, "written", expected);
            await Assert.That((await File.ReadAllBytesAsync(artifact)).SequenceEqual(expected)).IsTrue();
            await Assert.That(File.GetUnixFileMode(artifact)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);

            await File.WriteAllTextAsync(answers, """{"answerContractVersion":1,"values":{"service.name":"synthetic-replacement"},"omitted":[]}""");
            var conflict = await ReleaseTestPaths.RunInDirectory(workspace, executable, create);
            await AssertInstalledReceipt(conflict, "create", 74, "conflict", """{"name":"synthetic-replacement"}"""u8.ToArray());
            await Assert.That((await File.ReadAllBytesAsync(artifact)).SequenceEqual(expected)).IsTrue();
            await Assert.That(File.GetUnixFileMode(artifact)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Console.WriteLine($"COHORT installed-protected-create executed-create-and-conflict rid={ReleaseTestPaths.NativeCliRid}");
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
        await Assert.That(root.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v4");
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
        await Assert.That(output.GetProperty("installation").GetString())
            .IsEqualTo(operation == "validate" ? "not-applicable" : writeStatus == "written" ? "established" : "not-established");
        await Assert.That(output.GetProperty("verification").GetString())
            .IsEqualTo(operation == "validate" ? "not-applicable" : writeStatus == "written" ? "succeeded" : "not-attempted");
        await Assert.That(result.Output.Contains("synthetic-", StringComparison.Ordinal)).IsFalse();
        await Assert.That(result.Output.Contains("configurator-installed-cli-", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments("authoring", "configurator-authoring-v2.schema.json", "urn:islamu:configurator:authoring:v2")]
    [Arguments("runtime", "setup-definition-v2.schema.json", "urn:islamu:configurator:setup-definition:v2")]
    [Arguments("answers", "configurator-answers-v1.schema.json", "urn:islamu:configurator:answers:v1")]
    [Arguments("command", "islamu-configurator-command-v4.schema.json", "https://schemas.islamu.org/configurator/command/v4/schema.json")]
    public async Task PublishedCliSchemaDiscoveryMatchesDistributedContract(string kind, string fileName, string id)
    {
        string package = ReleaseTestPaths.NativeCliPackage;
        string executable = Path.Combine(package, ReleaseTestPaths.CliExecutableName);
        var result = await ReleaseTestPaths.RunInDirectory(package, executable,
            "definition", "schema", "--kind", kind, "--machine");
        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(result.StandardError).IsEmpty();
        await Assert.That(Encoding.UTF8.GetByteCount(result.Output)).IsLessThanOrEqualTo(65_536);
        await Assert.That(result.Output.EndsWith('\n')).IsTrue();
        await Assert.That(result.Output.Count(character => character == '\n')).IsEqualTo(1);
        using JsonDocument receipt = JsonDocument.Parse(result.Output);
        JsonElement root = receipt.RootElement;
        await Assert.That(root.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v4");
        await Assert.That(root.GetProperty("invocation").GetProperty("commandFamily").GetString()).IsEqualTo("definition");
        await Assert.That(root.GetProperty("invocation").GetProperty("operation").GetString()).IsEqualTo("schema");
        await Assert.That(root.GetProperty("exitCode").GetInt32()).IsEqualTo(0);
        await Assert.That(root.GetProperty("targetAcceptance").GetString()).IsEqualTo("unassessed");
        await Assert.That(root.GetProperty("artifacts").EnumerateArray()).IsEmpty();
        JsonElement schema = root.GetProperty("schema");
        await Assert.That(schema.GetProperty("kind").GetString()).IsEqualTo(kind);
        await Assert.That(schema.GetProperty("id").GetString()).IsEqualTo(id);
        await Assert.That(schema.GetProperty("profile").GetString()).IsEqualTo("bounded-v2");
        await Assert.That(schema.GetProperty("authoringContractVersion").GetInt32()).IsEqualTo(2);
        await Assert.That(schema.GetProperty("runtimeSchemaVersion").GetInt32()).IsEqualTo(2);
        await Assert.That(schema.GetProperty("answerContractVersion").GetInt32()).IsEqualTo(1);
        byte[] distributedBytes = await File.ReadAllBytesAsync(Path.Combine(package, "schemas", fileName));
        using JsonDocument distributed = JsonDocument.Parse(distributedBytes);
        await Assert.That(distributed.RootElement.GetProperty("$id").GetString()).IsEqualTo(id);
        await Assert.That(JsonElement.DeepEquals(schema.GetProperty("document"), distributed.RootElement)).IsTrue();
        await Assert.That(schema.GetProperty("digest").GetString())
            .IsEqualTo(ReleaseTestPaths.Hash(DefinitionProfile.CanonicalizeJson(distributedBytes)));
        using JsonDocument runtime = JsonDocument.Parse(
            await File.ReadAllBytesAsync(Path.Combine(package, "schemas/setup-definition-v2.schema.json")));
        await Assert.That(schema.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString()))
            .IsEquivalentTo(runtime.RootElement.GetProperty("properties").GetProperty("requiredCapabilities")
                .GetProperty("items").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    [Test]
    public async Task PublishedCliCompilesOneSourceWithoutStoreAndRetainsImmutableRelease()
    {
        string package = ReleaseTestPaths.NativeCliPackage;
        string executable = Path.Combine(package, ReleaseTestPaths.CliExecutableName);
        foreach (string relative in new[] { "schemas/configurator-authoring-v2.schema.json",
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
                {"$schema":"urn:islamu:configurator:authoring:v2",
                 "identity":{"familyId":"d8b0a60f-27ee-4262-a2b3-3af0e12d5034","revision":1},
                 "project":{"id":"example.service","name":"Example Service"},
                 "applicationVersions":["1.0.0"],
                 "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                    "properties":{"SERVICE_PORT":{"type":"integer","minimum":1,"maximum":65535,
                    "x-configurator":{"sensitivity":"public","initialValue":8080}}}},
                 "outputs":{".env":{"format":"dotenv","bindings":{"SERVICE_PORT":{"scope":"root","path":"/SERVICE_PORT"}}}}}
                """);
            byte[] original = await File.ReadAllBytesAsync(source);
            var check = await ReleaseTestPaths.Run(executable, "definition", "check", "--source", source, "--machine");
            await Assert.That(check.ExitCode).IsEqualTo(0).Because(check.Output);
            using (JsonDocument result = JsonDocument.Parse(check.Output))
            {
                await Assert.That(result.RootElement.GetProperty("schemaVersion").GetString())
                    .IsEqualTo("islamu-configurator-command/v4");
                await Assert.That(result.RootElement.GetProperty("authoringContractVersion").GetInt32()).IsEqualTo(2);
                await Assert.That(result.RootElement.GetProperty("runtimeSchemaVersion").GetInt32()).IsEqualTo(2);
            }
            await Assert.That(ReleaseTestPaths.Hash(await File.ReadAllBytesAsync(source)))
                .IsEqualTo(ReleaseTestPaths.Hash(original));

            var build = await ReleaseTestPaths.Run(executable, "definition", "build", "--source", source, "--output", output, "--machine");
            await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
            byte[] compiled = await File.ReadAllBytesAsync(output);
            SetupDefinition admitted = SetupDefinitionCodec.ParseJson(compiled);
            await Assert.That(admitted.ApplicationId).IsEqualTo("example.service");
            await Assert.That(admitted.Fields.Single().Id).IsEqualTo("SERVICE_PORT");
            await Assert.That(admitted.ApplicationVersions).IsEquivalentTo(["1.0.0"]);
            await Assert.That(compiled.SequenceEqual(admitted.CanonicalBytes.ToArray())).IsTrue();
            using (JsonDocument receipt = JsonDocument.Parse(build.Output))
            {
                await Assert.That(receipt.RootElement.GetProperty("schemaVersion").GetString())
                    .IsEqualTo("islamu-configurator-command/v4");
                await Assert.That(receipt.RootElement.GetProperty("artifacts")[0].GetProperty("digest").GetString())
                    .IsEqualTo(ReleaseTestPaths.Hash(compiled));
            }
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
