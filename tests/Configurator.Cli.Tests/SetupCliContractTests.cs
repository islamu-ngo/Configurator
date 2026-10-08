using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class SetupCliContractTests
{
    [Test]
    public async Task GeneratedSchemaIsClosedBoundedDeterministicAndRegistryOwned()
    {
        byte[] first = SetupCliCommandSchemaMetadata.GenerateSchema();
        await Assert.That(SetupCliCommandSchemaMetadata.GenerateSchema()).IsEquivalentTo(first);
        JsonObject schema = SetupCliMachineContractVerifier.ParseSchema(first);
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema)).IsEmpty();
        await Assert.That(schema["_metadata"]!["commands"]!.AsArray().Select(node => node!["name"]!.GetValue<string>()))
            .IsEquivalentTo(["catalogue", "env", "artifact", "doctor"]);
        await Assert.That(schema["_metadata"]!["commandOptions"]!.AsArray().Select(node => node!.GetValue<string>()))
            .IsEquivalentTo(["--help", "--machine", "--text", "--dry-run", "--input", "--baseline",
                "--output", "--key", "--definition", "--artifact", "--format", "--expected-revision",
                "--revision-field", "--input-revision", "--answers"]);
        await Assert.That(schema["properties"]!["exitCode"]!["enum"]!.AsArray().Select(node => node!.GetValue<int>()))
            .IsEquivalentTo([0, 2, 3, 4, 64, 65, 70, 74]);
    }

    [Test]
    public async Task SchemaVerifierRejectsUndeclaredArtifactOperation()
    {
        JsonObject schema = SetupCliMachineContractVerifier.ParseSchema(SetupCliCommandSchemaMetadata.GenerateSchema());
        JsonNode artifact = schema["_metadata"]!["commands"]!.AsArray()
            .Single(command => command!["name"]!.GetValue<string>() == "artifact")!;
        artifact["operations"]![0]!["name"] = "apply";
        await Assert.That(SetupCliMachineContractVerifier.InspectSchema(schema))
            .Contains("schema-operation-vocabulary");
    }

    [Test]
    public async Task MachineVerifierRejectsFramingControlsAndValueBearingObjects()
    {
        byte[] good = SetupCliMachineContractVerifier.GoodFixture();
        await Assert.That(SetupCliMachineContractVerifier.Validate(good)).IsEmpty();
        byte[][] vectors =
        [
            [.. good.AsSpan(0, good.Length - 1), .. "{}\n"u8],
            [0xEF, 0xBB, 0xBF, .. good],
            [0x1B, .. good],
            SetupCliMachineContractVerifier.Mutate(root => root["message"] = "localized"),
            SetupCliMachineContractVerifier.Mutate(root => root["diagnostics"] = new JsonArray(new JsonObject
            {
                ["code"] = "invalid-artifact", ["path"] = "$.input", ["severity"] = "error", ["value"] = "present"
            })),
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["body"] = "present"),
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["bytes"] = 1)
        ];
        foreach (byte[] vector in vectors)
            await Assert.That(SetupCliMachineContractVerifier.Validate(vector).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task MachineVerifierRejectsDigestExitOrderingReadinessAndBoundsBreakers()
    {
        byte[][] vectors =
        [
            SetupCliMachineContractVerifier.Mutate(root => root["artifacts"]![0]!["digest"] = "ABC"),
            SetupCliMachineContractVerifier.Mutate(root => root["status"] = "unknown"),
            SetupCliMachineContractVerifier.Mutate(root => root["exitCode"] = 4),
            SetupCliMachineContractVerifier.Mutate(root => root["coverage"]!["coveredKeys"] = new JsonArray("z", "a")),
            SetupCliMachineContractVerifier.Mutate(root => root["coverage"]!["coveredKeys"] = new JsonArray("a", "a")),
            SetupCliMachineContractVerifier.Mutate(root => root["readiness"] = new JsonObject
            {
                ["state"] = "ready", ["missingKeys"] = new JsonArray("missing"), ["blockedKeys"] = new JsonArray()
            }),
            SetupCliMachineContractVerifier.Mutate(root => root["diagnostics"] = new JsonArray(new JsonObject
            {
                ["code"] = new string('a', 97), ["path"] = "$.input", ["severity"] = "error"
            }))
        ];
        foreach (byte[] vector in vectors)
            await Assert.That(SetupCliMachineContractVerifier.Validate(vector).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task ActualCommandGrammarRejectsAliasesUnknownDuplicateMixedAndMissingOptions()
    {
        string[][] rejected =
        [
            ["manifest", "create", "--machine"],
            ["tenant-package", "validate", "--machine"],
            ["portability", "export-operator-identity", "--machine"],
            ["legal", "preview", "--machine"],
            ["catalog", "list", "--machine"],
            ["tui", "--machine"],
            ["artifact", "show", "--machine"],
            ["doctor", "--machine", "--text"],
            ["doctor", "--machine", "--machine"],
            ["doctor", "--machine", "--unknown"],
            ["doctor", "--machine", "tail"],
            ["catalogue", "list", "--machine"],
            ["catalogue", "show", "--definition", "definition.json", "--dry-run", "--machine"],
            ["artifact", "diff", "--definition", "definition.json", "--artifact", "config", "--input", "answers.json", "--machine"],
            ["artifact", "validate", "--definition", "-", "--artifact", "config", "--input", "answers.json", "--machine"],
            ["artifact", "diff", "--definition", "definition.json", "--artifact", "config", "--input", "-", "--baseline", "-", "--machine"]
        ];
        foreach (string[] arguments in rejected)
        {
            var io = new CliFixture();
            await Assert.That(io.Run(arguments)).IsEqualTo(SetupCliExitCode.Usage);
            await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            await Assert.That(io.StandardError).IsEmpty();
        }
    }

    [Test]
    public async Task HelpBypassesInputsButStillHasOneMachineEnvelope()
    {
        foreach (var family in SetupCliCommandSchemaMetadata.Commands)
            foreach (var operation in family.Operations)
            {
                var io = new CliFixture();
                string[] args = family.Name == "doctor"
                    ? ["doctor", "--help", "--machine"] : [family.Name, operation.Name, "--help", "--machine"];
                await Assert.That(io.Run(args)).IsEqualTo(SetupCliExitCode.Success);
                await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            }
    }

    [Test]
    public async Task CompiledCliClosureHasNoEventOrTransportAssembly()
    {
        var pending = new Queue<System.Reflection.Assembly>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(typeof(SetupCliApplication).Assembly);
        while (pending.TryDequeue(out var assembly))
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                string name = reference.Name!;
                await Assert.That(name.StartsWith("Event.", StringComparison.Ordinal)
                    || name.StartsWith("Explore.", StringComparison.Ordinal)
                    || name.Contains("Wire.Contracts", StringComparison.Ordinal)
                    || name.Contains("SetupLive", StringComparison.Ordinal)).IsFalse();
                if (name.StartsWith("Configurator.", StringComparison.Ordinal) && visited.Add(name))
                    pending.Enqueue(System.Reflection.Assembly.Load(reference));
            }
        }
        await Assert.That(typeof(SetupCliApplication).Assembly.EntryPoint).IsNotNull();
        await Assert.That(SetupCliExecutableMarker.ExecutableName).IsEqualTo("islamu-configurator");
    }

    [Test]
    public async Task MachineReadinessNeverClaimsHostAcceptance()
    {
        var io = new CliFixture();
        await Assert.That(io.Artifact("open")).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument result = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(result.RootElement.GetProperty("readiness").GetProperty("state").GetString()).IsEqualTo("incomplete");
        await Assert.That(result.RootElement.GetProperty("readiness").GetProperty("missingKeys")[0].GetString())
            .IsEqualTo("host-validation-required");
        await Assert.That(result.RootElement.GetProperty("artifacts")[0].GetProperty("sensitivity").GetString())
            .IsEqualTo("restricted");
    }
}
