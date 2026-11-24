using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core;
using Configurator.Core.Definitions;

namespace Configurator.Cli.Tests;

public sealed class CliDiscoveryAndGrammarTests
{
    [Test]
    [Arguments("env", "render")]
    [Arguments("env", "validate")]
    [Arguments("catalogue", "show")]
    [Arguments("artifact", "open")]
    [Arguments("artifact", "format")]
    [Arguments("artifact", "export")]
    [Arguments("artifact", "preview")]
    [Arguments("definition", "schema")]
    public async Task RemovedLegacyRoutesFailClosedWithUsageAndZeroAliasDispatch(string family, string operation)
    {
        var io = new CliFixture();
        CliExitCode exit = await io.RunAsync(family, operation, "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        JsonObject envelope = ParseMachine(io);
        string code = envelope["diagnostics"]![0]!["code"]!.GetValue<string>();
        await Assert.That(code is "command-unknown" or "operation-unknown").IsTrue();
    }

    [Test]
    public async Task RootVersionAndInfoExposeActualBuildMetadataWithoutGitExecution()
    {
        var textVersionIo = new CliFixture();
        CliExitCode textExit = await textVersionIo.RunAsync("--version");
        await Assert.That(textExit).IsEqualTo(CliExitCode.Success);
        string versionLine = Encoding.UTF8.GetString(textVersionIo.StandardOutput);
        await Assert.That(versionLine).Contains("islamu-configurator");
        await Assert.That(versionLine).Contains(ConfiguratorVersion.Current);

        var machineVersionIo = new CliFixture();
        CliExitCode machineVersionExit = await machineVersionIo.RunAsync("--version", "--machine");
        await Assert.That(machineVersionExit).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(machineVersionIo.StandardOutput)).IsEmpty();
        JsonObject versionEnvelope = ParseMachine(machineVersionIo);
        await Assert.That(versionEnvelope["invocation"]!["commandFamily"]!.GetValue<string>()).IsEqualTo("cli");
        await Assert.That(versionEnvelope["invocation"]!["operation"]!.GetValue<string>()).IsEqualTo("version");
        await Assert.That(versionEnvelope["info"]!["authoringContractVersion"]!.GetValue<string>()).IsEqualTo("0.0.1");
        await Assert.That(versionEnvelope["info"]!["fixtureContractVersion"]!.GetValue<string>()).IsEqualTo("0.0.1");

        var infoIo = new CliFixture();
        CliExitCode infoExit = await infoIo.RunAsync("info", "--machine");
        await Assert.That(infoExit).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(infoIo.StandardOutput)).IsEmpty();
        JsonObject infoEnvelope = ParseMachine(infoIo);
        await Assert.That(infoEnvelope["invocation"]!["commandFamily"]!.GetValue<string>()).IsEqualTo("info");
        await Assert.That(infoEnvelope["invocation"]!["operation"]!.GetValue<string>()).IsEqualTo("info");
        await Assert.That(infoEnvelope["info"]!["schemas"]!.AsArray().Count).IsEqualTo(6);
    }

    [Test]
    public async Task SchemaListAndExportReturnByteExactSchemasWithoutCompetingMachineEnvelopes()
    {
        var listIo = new CliFixture();
        CliExitCode listExit = await listIo.RunAsync("schema", "list", "--machine");
        await Assert.That(listExit).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(listIo.StandardOutput)).IsEmpty();
        JsonArray schemas = ParseMachine(listIo)["schemaCatalog"]!["schemas"]!.AsArray();
        string[] kinds = schemas.Select(item => item!["kind"]!.GetValue<string>()).ToArray();
        await Assert.That(kinds).IsEquivalentTo(["answers", "authoring", "command", "fixtures", "runtime", "store-curation"]);

        foreach (string kind in kinds)
        {
            var exportTextIo = new CliFixture();
            CliExitCode textExit = await exportTextIo.RunAsync("schema", "export", "--kind", kind);
            await Assert.That(textExit).IsEqualTo(CliExitCode.Success);
            using JsonDocument rawDoc = JsonDocument.Parse(exportTextIo.StandardOutput);
            await Assert.That(rawDoc.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
            await Assert.That(rawDoc.RootElement.GetProperty("$schema").GetString())
                .IsEqualTo("https://json-schema.org/draft/2020-12/schema");

            var exportMachineIo = new CliFixture();
            CliExitCode machineExit = await exportMachineIo.RunAsync("schema", "export", "--kind", kind, "--machine");
            await Assert.That(machineExit).IsEqualTo(CliExitCode.Success);
            await Assert.That(CliMachineContractVerifier.Validate(exportMachineIo.StandardOutput)).IsEmpty();
            JsonObject machineSchema = ParseMachine(exportMachineIo)["schema"]!.AsObject();
            await Assert.That(machineSchema["kind"]!.GetValue<string>()).IsEqualTo(kind);

            byte[] canonicalRaw = DefinitionProfile.CanonicalizeJson(exportTextIo.StandardOutput);
            string expectedDigest = Convert.ToHexStringLower(SHA256.HashData(canonicalRaw));
            await Assert.That(machineSchema["digest"]!.GetValue<string>()).IsEqualTo(expectedDigest);
        }

        // Reject stdout raw output combined with --machine to prevent competing JSON documents
        var conflictIo = new CliFixture();
        CliExitCode conflictExit = await conflictIo.RunAsync("schema", "export", "--kind", "authoring", "--output", "-", "--machine");
        await Assert.That(conflictExit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(CliMachineContractVerifier.Validate(conflictIo.StandardOutput)).IsEmpty();
    }

    [Test]
    public async Task DoctorReportsCapabilitiesTruthfullyAndBlocksOnlyWhenRequiredCapabilityUnavailable()
    {
        var unavailableApp = new CliApplication(isProtectedWriterAvailable: () => false);

        var plainIo = new CliFixture();
        CliExitCode plainExit = await plainIo.RunAsync(unavailableApp, "doctor", "--machine");
        await Assert.That(plainExit).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(plainIo.StandardOutput)).IsEmpty();
        await Assert.That(ParseMachine(plainIo)["info"]!["protectedWriteAvailable"]!.GetValue<bool>()).IsFalse();

        var requiredIo = new CliFixture();
        CliExitCode requiredExit = await requiredIo.RunAsync(unavailableApp, "doctor", "--require", "protected-write", "--machine");
        await Assert.That(requiredExit).IsEqualTo(CliExitCode.Blocked);
        await Assert.That(CliMachineContractVerifier.Validate(requiredIo.StandardOutput)).IsEmpty();
        await Assert.That(ParseMachine(requiredIo)["diagnostics"]![0]!["code"]!.GetValue<string>()).IsEqualTo("protected-writer-unavailable");

        var availableApp = new CliApplication(isProtectedWriterAvailable: () => true);
        var okIo = new CliFixture();
        CliExitCode okExit = await okIo.RunAsync(availableApp, "doctor", "--require", "protected-write", "--machine");
        await Assert.That(okExit).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(okIo.StandardOutput)).IsEmpty();
        await Assert.That(ParseMachine(okIo)["info"]!["protectedWriteAvailable"]!.GetValue<bool>()).IsTrue();
    }

    [Test]
    public async Task DefinitionDescribePagesBySixteenEnforcesDigestAndOmitsPrivateDefaults()
    {
        var props = new JsonObject();
        var bindings = new JsonObject();
        for (int index = 1; index <= 20; index++)
        {
            string key = $"FIELD_{index:D2}";
            props[key] = JsonNode.Parse($$$"""
                {"type":"string","title":"Field {{{index}}}","description":"Public field {{{index}}}.",
                 "default":"SYNTH_DEFAULT_MARKER_{{{index}}}",
                 "x-configurator":{"sensitivity":"public","initialValue":"SYNTH_INITIAL_MARKER_{{{index}}}"}}
                """);
            bindings[key] = new JsonObject { ["scope"] = "root", ["path"] = "/" + key };
        }
        string source = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "f1234567-89ab-4cde-8f01-23456789abcd", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "describe.sample", ["name"] = "Describe Sample" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = props
            },
            ["outputs"] = new JsonObject
            {
                [".env"] = new JsonObject { ["id"] = "env", ["format"] = "dotenv", ["bindings"] = bindings }
            }
        }.ToJsonString();

        using var files = new DefinitionTestDirectory();
        string sourcePath = files.Path("configurator.json");
        await File.WriteAllTextAsync(sourcePath, source);

        var page1Io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        CliExitCode page1Exit = await page1Io.RunAsync("definition", "describe", "--source", sourcePath, "--machine");
        await Assert.That(page1Exit).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(page1Io.StandardOutput)).IsEmpty();
        string page1Raw = Encoding.UTF8.GetString(page1Io.StandardOutput);
        await Assert.That(page1Raw).DoesNotContain("SYNTH_DEFAULT_MARKER");
        await Assert.That(page1Raw).DoesNotContain("SYNTH_INITIAL_MARKER");

        JsonObject desc1 = ParseMachine(page1Io)["definitionDescription"]!.AsObject();
        await Assert.That(desc1["total"]!.GetValue<int>()).IsEqualTo(20);
        await Assert.That(desc1["returnedCount"]!.GetValue<int>()).IsEqualTo(16);
        await Assert.That(desc1["terminal"]!.GetValue<bool>()).IsFalse();
        int nextOffset = desc1["nextOffset"]!.GetValue<int>();
        string digest = desc1["digest"]!.GetValue<string>();
        await Assert.That(nextOffset).IsEqualTo(16);

        // Non-initial page without digest rejects
        var missingDigestIo = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        CliExitCode missingDigestExit = await missingDigestIo.RunAsync(
            "definition", "describe", "--source", sourcePath, "--offset", "16", "--machine");
        await Assert.That(missingDigestExit).IsEqualTo(CliExitCode.Data);

        // Non-initial page with matching digest returns remaining 4 fields
        var page2Io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        CliExitCode page2Exit = await page2Io.RunAsync(
            "definition", "describe", "--source", sourcePath, "--offset", "16", "--expected-detail-digest", digest, "--machine");
        await Assert.That(page2Exit).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(page2Io.StandardOutput)).IsEmpty();
        JsonObject desc2 = ParseMachine(page2Io)["definitionDescription"]!.AsObject();
        await Assert.That(desc2["returnedCount"]!.GetValue<int>()).IsEqualTo(4);
        await Assert.That(desc2["terminal"]!.GetValue<bool>()).IsTrue();
        await Assert.That(desc2["nextOffset"]).IsNull();

        // Limit > 16 rejects with Usage
        var overLimitIo = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        CliExitCode overLimitExit = await overLimitIo.RunAsync(
            "definition", "describe", "--source", sourcePath, "--limit", "17", "--machine");
        await Assert.That(overLimitExit).IsEqualTo(CliExitCode.Usage);
    }

    [Test]
    public async Task MalformedDefinitionSourceSurfacesNumericParserCoordinatesWithoutLeakingCanary()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF];
        const string text = "{\r\n  \"العربية\U0001F600\": @SYNTH_CLI_CANARY_TOKEN\r\n}";
        byte[] content = Encoding.UTF8.GetBytes(text);
        byte[] raw = [.. bom, .. content];
        long expectedOffset = bom.Length + Encoding.UTF8.GetByteCount("{\r\n  \"العربية\U0001F600\": ");

        using var files = new DefinitionTestDirectory();
        string sourcePath = files.Path("configurator.json");
        await File.WriteAllBytesAsync(sourcePath, raw);

        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        CliExitCode exit = await io.RunAsync("definition", "check", "--source", sourcePath, "--machine");

        await Assert.That(exit).IsEqualTo(CliExitCode.Data);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        string rawOutput = Encoding.UTF8.GetString(io.StandardOutput);
        await Assert.That(rawOutput).DoesNotContain("SYNTH_CLI_CANARY_TOKEN");
        await Assert.That(rawOutput).DoesNotContain("العربية");
        await Assert.That(rawOutput).DoesNotContain(files.Root);

        JsonObject diagnostic = ParseMachine(io)["diagnostics"]![0]!.AsObject();
        await Assert.That(diagnostic["code"]!.GetValue<string>()).IsEqualTo("document-invalid");
        await Assert.That(diagnostic["line"]!.GetValue<int>()).IsEqualTo(2);
        await Assert.That(diagnostic["column"]!.GetValue<int>()).IsEqualTo(15);
        await Assert.That(diagnostic["byteOffset"]!.GetValue<long>()).IsEqualTo(expectedOffset);
    }

    private static JsonObject ParseMachine(CliFixture io) =>
        JsonNode.Parse(io.StandardOutput)!.AsObject();
}
