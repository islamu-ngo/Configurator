using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Configurator.Cli.Tests;

public sealed class SetupCliApplicationTests
{
    [Test]
    public async Task MaximumBundleSelectionStillProducesOneBoundedMachineEnvelope()
    {
        var io = new CliFixture();
        JsonObject definition = JsonNode.Parse(CliFixture.Definition)!.AsObject();
        JsonNode template = definition["outputs"]!["service.json"]!;
        var outputs = new JsonObject();
        var ids = new List<string>();
        for (int index = 0; index < 64; index++)
        {
            string id = "a" + index.ToString("D2", CultureInfo.InvariantCulture) + new string('a', 125);
            JsonObject artifact = (JsonObject)template.DeepClone();
            artifact["id"] = id;
            outputs.Add($"file{index:D2}.json", artifact);
            ids.Add(id);
        }
        definition["outputs"] = outputs;
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());

        await Assert.That(await io.RunAsync(["artifact", "bundle-check", "--definition", "definition.json",
            .. ids.SelectMany(id => new[] { "--artifact", id }), "--input", "answers.json", "--machine"]))
            .IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        await Assert.That(io.StandardOutput.Length).IsLessThanOrEqualTo(65_536);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
    }

    [Test]
    public async Task BundleCommandReportsCompleteConflictBlockedAndPartialOutcomesWithoutPrivateBytes()
    {
        string[] args = ["--definition", "definition.json", "--artifact", "config", "--artifact", "yaml",
            "--input", "answers.json", "--output", "bundle", "--machine"];
        string configurationPath = Path.Combine("bundle", "service.json");
        string yamlPath = Path.Combine("bundle", "service.yaml");
        var complete = new CliFixture();
        await Assert.That(await complete.RunAsync(["artifact", "bundle-check", .. args.Where(item => item != "--output" && item != "bundle")]))
            .IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(await complete.RunAsync(["artifact", "bundle-create", .. args])).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(complete.Files.ContainsKey(configurationPath)).IsTrue();
        await Assert.That(complete.Files.ContainsKey(yamlPath)).IsTrue();
        await Assert.That(SetupCliMachineContractVerifier.Validate(complete.StandardOutput)).IsEmpty();
        await Assert.That(Encoding.UTF8.GetString(complete.StandardOutput)).DoesNotContain("private-answer-marker");
        await Assert.That(Encoding.UTF8.GetString(complete.StandardOutput)).DoesNotContain("bundle" + Path.DirectorySeparatorChar);

        var conflict = new CliFixture();
        conflict.Files[yamlPath] = "untouched"u8.ToArray();
        await Assert.That(await conflict.RunAsync(["artifact", "bundle-create", .. args])).IsEqualTo(SetupCliExitCode.Blocked);
        await Assert.That(conflict.Files.ContainsKey(configurationPath)).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(conflict.Files[yamlPath])).IsEqualTo("untouched");

        var blocked = new CliFixture { BundleBlocked = true };
        await Assert.That(await blocked.RunAsync(["artifact", "bundle-create", .. args])).IsEqualTo(SetupCliExitCode.Blocked);
        await Assert.That(blocked.Files.ContainsKey(configurationPath)).IsFalse();

        var partial = new CliFixture { BundleFailureAfter = 1 };
        await Assert.That(await partial.RunAsync(["artifact", "bundle-create", .. args])).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(partial.Files.ContainsKey(configurationPath)).IsTrue();
        await Assert.That(partial.Files.ContainsKey(yamlPath)).IsFalse();
        using JsonDocument machine = JsonDocument.Parse(partial.StandardOutput);
        await Assert.That(machine.RootElement.GetProperty("artifacts")[0].GetProperty("writeStatus").GetString())
            .IsEqualTo("written");
        await Assert.That(machine.RootElement.GetProperty("artifacts")[1].GetProperty("writeStatus").GetString())
            .IsEqualTo("failed");
        await Assert.That(Encoding.UTF8.GetString(partial.StandardOutput)).DoesNotContain("private-answer-marker");

        var revisionBound = new CliFixture();
        await Assert.That(await revisionBound.RunAsync("artifact", "bundle-create", "--definition", "definition.json",
            "--artifact", "config", "--artifact", "request", "--input", "answers.json", "--output", "bundle",
            "--machine")).IsEqualTo(SetupCliExitCode.Blocked);
        await Assert.That(revisionBound.Files.ContainsKey(configurationPath)).IsFalse();
    }

    [Test]
    public async Task ExistingFileCheckAndPrefillUseDistinctMachineOperations()
    {
        var io = new CliFixture();
        io.Files["existing.json"] = """{"name":"private-import-marker","enabled":true}"""u8.ToArray();
        string[] args = ["--definition", "definition.json", "--artifact", "config",
            "--input", "existing.json", "--machine"];
        await Assert.That(await io.RunAsync(["artifact", "check-existing", .. args])).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using (JsonDocument receipt = JsonDocument.Parse(io.StandardOutput))
        {
            await Assert.That(receipt.RootElement.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v4");
            await Assert.That(receipt.RootElement.TryGetProperty("assessment", out _)).IsFalse();
            await Assert.That(receipt.RootElement.GetProperty("preparation").GetString()).IsEqualTo("ready");
        }
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-import-marker");
        await Assert.That(await io.RunAsync(["artifact", "prefill", .. args, "--output", "prefilled.json"]))
            .IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.Files["prefilled.json"]))
            .Contains("private-import-marker");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-import-marker");

        io.Files["existing.json"] = """{"name":"private-import-marker","name":"second"}"""u8.ToArray();
        await Assert.That(await io.RunAsync(["artifact", "check-existing", .. args])).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-import-marker");

        io.Files["existing.json"] = """{"name":"private-import-marker","enabled":true}"""u8.ToArray();
        io.Files["answers.json"] = """{"answerContractVersion":1,"values":{"service.name":"different"},"omitted":[]}"""u8.ToArray();
        await Assert.That(await io.RunAsync(["artifact", "prefill", .. args, "--answers", "answers.json",
            "--output", "rejected.json"])).IsEqualTo(SetupCliExitCode.Validation);
        await Assert.That(io.Files.ContainsKey("rejected.json")).IsFalse();
    }

    [Test]
    public async Task GenericOperationsPrepareCanonicalTargetArtifactsWithoutEvent()
    {
        foreach (string operation in new[] { "create", "open", "validate", "format", "diff", "coverage", "export", "preview" })
        {
            var io = new CliFixture();
            string[] options = operation switch
            {
                "create" or "format" or "export" or "preview" => ["--output", "prepared.json"],
                "diff" => ["--baseline", "baseline.json"],
                _ => []
            };
            await Assert.That(await io.ArtifactAsync(operation, options: options)).IsEqualTo(SetupCliExitCode.Success);
            await Assert.That(SetupCliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
            await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
            if (io.Files.TryGetValue("prepared.json", out byte[]? output))
                await Assert.That(Encoding.UTF8.GetString(output)).IsEqualTo("""{"enabled":true,"name":"private-answer-marker"}""");
        }
    }

    [Test]
    public async Task DiffComparesProjectedBytesNotUnprojectedAnswers()
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("diff", options: ["--baseline", "baseline.json"])).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument same = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(same.RootElement.GetProperty("coverage").GetProperty("coveredCount").GetInt32()).IsEqualTo(1);
        io.Files["baseline.json"] = """{"answerContractVersion":1,"values":{"service.name":"different-marker"},"omitted":[]}"""u8.ToArray();
        await Assert.That(await io.ArtifactAsync("diff", options: ["--baseline", "baseline.json"])).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument changed = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(changed.RootElement.GetProperty("coverage").GetProperty("missingCount").GetInt32()).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("different-marker");
    }

    [Test]
    public async Task CatalogueExposesOnlyStructuralDefinitionMetadata()
    {
        var io = new CliFixture();
        foreach (string operation in new[] { "list", "show", "describe" })
        {
            string[] key = operation == "list" ? [] : ["--key", "service.name"];
            await Assert.That(await io.RunAsync(["catalogue", operation, "--definition", "definition.json",
                "--output", operation + ".json", "--machine", .. key])).IsEqualTo(SetupCliExitCode.Success);
            using JsonDocument result = JsonDocument.Parse(io.Files[operation + ".json"]);
            JsonElement field = operation == "list" ? result.RootElement.EnumerateArray()
                .First(item => item.GetProperty("id").GetString() == "service.name") : result.RootElement;
            await Assert.That(field.GetProperty("id").GetString()).IsEqualTo("service.name");
            await Assert.That(field.TryGetProperty("title", out _)).IsFalse();
            await Assert.That(field.TryGetProperty("default", out _)).IsFalse();
            if (operation == "describe")
                await Assert.That(field.GetProperty("conditionalActivation").GetBoolean()).IsFalse();
        }
        await Assert.That(await io.RunAsync("catalogue", "show", "--definition", "definition.json",
            "--key", "missing", "--dry-run", "--machine")).IsEqualTo(SetupCliExitCode.Validation);
    }

    [Test]
    public async Task EnvironmentUsesSelectedDefinitionDotenvProjectionOnly()
    {
        var io = new CliFixture();
        await Assert.That(await io.RunAsync("env", "render", "--definition", "definition.json", "--artifact", "environment",
            "--input", "answers.json", "--output", "environment.env", "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.Files["environment.env"])).Contains("SERVICE_NAME=private-answer-marker");
        await Assert.That(await io.RunAsync("env", "validate", "--definition", "definition.json", "--artifact", "environment",
            "--input", "answers.json", "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(await io.RunAsync("env", "validate", "--definition", "definition.json", "--artifact", "config",
            "--input", "answers.json", "--machine")).IsEqualTo(SetupCliExitCode.Usage);
    }

    [Test]
    public async Task FormatsComeFromDefinitionAndMarkdownNeverEntersStdout()
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("export", "yaml", "--output", "out.yaml", "--format", "yaml")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.Files["out.yaml"])).Contains("private-answer-marker");
        await Assert.That(await io.ArtifactAsync("format", "config", "--format", "yaml", "--dry-run")).IsEqualTo(SetupCliExitCode.Usage);
        await Assert.That(await io.ArtifactAsync("preview", "markdown", "--output", "draft.md")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(io.Files["draft.md"])).Contains("Draft");
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("Draft");
        await Assert.That(await io.ArtifactAsync("preview", "markdown", "--output", "-", "--text")).IsEqualTo(SetupCliExitCode.Usage);
        await Assert.That(await io.RunAsync("artifact", "preview", "--definition", "definition.json", "--artifact", "markdown",
            "--input", "answers.json", "--output", "-", "--dry-run")).IsEqualTo(SetupCliExitCode.Blocked);
    }

    [Test]
    public async Task ReadinessCoverageActivationAndInvalidAnswersHaveTruthfulOutcomes()
    {
        var io = new CliFixture();
        io.Files["answers.json"] = """{"answerContractVersion":1,"values":{},"omitted":[]}"""u8.ToArray();
        await Assert.That(await io.ArtifactAsync("coverage")).IsEqualTo(SetupCliExitCode.Incomplete);
        using JsonDocument missing = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(missing.RootElement.GetProperty("coverage").GetProperty("missingCount").GetInt32()).IsEqualTo(1);
        io.Files["answers.json"] = """{"answerContractVersion":1,"values":{"service.name":42},"omitted":[]}"""u8.ToArray();
        await Assert.That(await io.ArtifactAsync("validate")).IsEqualTo(SetupCliExitCode.Validation);
        io.Files["answers.json"] = """{"answerContractVersion":1,"values":{"unknown":"hidden-marker"},"omitted":[]}"""u8.ToArray();
        await Assert.That(await io.ArtifactAsync("validate")).IsEqualTo(SetupCliExitCode.Validation);
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("hidden-marker");
        JsonObject definition = JsonNode.Parse(CliFixture.Definition)!.AsObject();
        definition["configuration"]!["properties"]!["service.name"]!["x-configurator"]!["applicableWhen"] =
            JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}""");
        io.Files["definition.json"] = Encoding.UTF8.GetBytes(definition.ToJsonString());
        io.Files["answers.json"] = """{"answerContractVersion":1,"values":{"enabled":false},"omitted":[]}"""u8.ToArray();
        await Assert.That(await io.ArtifactAsync("validate")).IsEqualTo(SetupCliExitCode.Success);
    }

    [Test]
    public async Task DryRunWritesNothingAndExistingOutputIsNeverReplaced()
    {
        var io = new CliFixture();
        await Assert.That(await io.ArtifactAsync("create", options: ["--dry-run"])).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(io.Files.Keys).IsEquivalentTo(["definition.json", "answers.json", "baseline.json"]);
        await Assert.That(await io.ArtifactAsync("create", options: ["--output", "prepared.json"])).IsEqualTo(SetupCliExitCode.Success);
        byte[] original = io.Files["prepared.json"];
        await Assert.That(await io.ArtifactAsync("create", options: ["--output", "prepared.json"])).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(io.Files["prepared.json"]).IsEquivalentTo(original);
    }

    [Test]
    public async Task MalformedDuplicateAndOversizedInputsFailWithoutPublishing()
    {
        var io = new CliFixture();
        foreach (byte[] bytes in new[] { "[]"u8.ToArray(), "{bad}"u8.ToArray(),
            """{"answerContractVersion":1,"values":{"service.name":"first","service.name":"second"},"omitted":[]}"""u8.ToArray(), new byte[4 * 1024 * 1024] })
        {
            io.Files["answers.json"] = bytes;
            await Assert.That(await io.ArtifactAsync("create", options: ["--output", "prepared.json"])).IsEqualTo(SetupCliExitCode.Data);
            await Assert.That(io.Files.ContainsKey("prepared.json")).IsFalse();
        }
        io.Files["answers.json"] = new byte[(4 * 1024 * 1024) + 1];
        await Assert.That(await io.ArtifactAsync("validate")).IsEqualTo(SetupCliExitCode.Io);
        await Assert.That(io.MaximumRead).IsEqualTo(4 * 1024 * 1024);
        io.Files["definition.json"] = "{bad}"u8.ToArray();
        await Assert.That(await io.ArtifactAsync("validate")).IsEqualTo(SetupCliExitCode.Data);
    }

    [Test]
    public async Task MachineOutputRemainsSingleFramedAndCultureIndependent()
    {
        var io = new CliFixture();
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            await Assert.That(await io.ArtifactAsync("validate")).IsEqualTo(SetupCliExitCode.Success);
            byte[] first = io.StandardOutput;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            await Assert.That(await io.ArtifactAsync("validate")).IsEqualTo(SetupCliExitCode.Success);
            await Assert.That(io.StandardOutput).IsEquivalentTo(first);
            await Assert.That(SetupCliMachineContractVerifier.Validate(first)).IsEmpty();
            await Assert.That(first.Count(value => value == (byte)'\n')).IsEqualTo(1);
            await Assert.That(io.StandardError).IsEmpty();
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
