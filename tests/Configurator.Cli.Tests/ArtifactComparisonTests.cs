using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Configurator.Cli.Tests;

public sealed class ArtifactComparisonTests
{
    private const string Definition = """
        {"$schema":"urn:islamu:configurator:authoring:v2",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
         "project":{"id":"example","name":"Comparison"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"required":["name"],
          "properties":{"name":{"type":"string","minLength":1}}},
         "outputs":{"config.json":{"id":"config","format":"json"}}}
        """;

    [Test]
    [Arguments("""{"name":"synthetic-private-one"}""", 0, "compatible", "completed")]
    [Arguments("""{}""", 2, "changes-required", "completed")]
    [Arguments("""{"name":false}""", 2, "changes-required", "completed")]
    [Arguments("""{"name":"synthetic-private-one","synthetic-private-key":true}""", 2, "changes-required", "completed")]
    [Arguments("""{"name":""", 65, "invalid-input", "failed")]
    public async Task CompareReportsAssessmentSeparatelyWithoutWriting(string input, int exit, string compatibility, string completion)
    {
        var fixture = new CliFixture();
        fixture.Files["definition.json"] = Encoding.UTF8.GetBytes(Definition);
        byte[] original = Encoding.UTF8.GetBytes(input);
        fixture.Files["existing.json"] = original;
        int files = fixture.Files.Count;
        SetupCliExitCode result = await fixture.RunAsync("artifact", "compare", "--definition", "definition.json",
            "--artifact", "config", "--input", "existing.json", "--machine");
        await Assert.That((int)result).IsEqualTo(exit);
        using JsonDocument document = JsonDocument.Parse(fixture.StandardOutput);
        JsonElement root = document.RootElement;
        await Assert.That(root.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v4");
        await Assert.That(root.GetProperty("completion").GetString()).IsEqualTo(completion);
        await Assert.That(root.GetProperty("preparation").GetString()).IsEqualTo("not-evaluated");
        await Assert.That(root.GetProperty("targetAcceptance").GetString()).IsEqualTo("unassessed");
        await Assert.That(root.GetProperty("readiness").GetProperty("state").GetString()).IsEqualTo("unassessed");
        await Assert.That(root.GetProperty("assessment").GetProperty("compatibility").GetString()).IsEqualTo(compatibility);
        await Assert.That(root.GetProperty("artifacts").GetArrayLength()).IsEqualTo(0);
        await Assert.That(fixture.Files.Count).IsEqualTo(files);
        await Assert.That(fixture.Files["existing.json"].SequenceEqual(original)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(fixture.StandardOutput)).DoesNotContain("synthetic-private");
    }

    [Test]
    public async Task ReceiptIdentifiesVersionFourBeforeAnyAssessment()
    {
        var fixture = new CliFixture();
        await fixture.RunAsync("--help", "--machine");
        using JsonDocument document = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(document.RootElement.GetProperty("schemaVersion").GetString()).IsEqualTo("islamu-configurator-command/v4");
    }

    [Test]
    public async Task ThreeThousandIndependentCoverageEntriesHaveAdvancingTerminalPages()
    {
        string[] expected = Enumerable.Range(0, 3000).Select(index => "field" + index.ToString("D4", CultureInfo.InvariantCulture)).ToArray();
        var result = SetupCliResults.Success(coverage: new(expected, []));
        var command = new SetupCliCommand("artifact", "validate", true, false, false, null, "config", null, null, null, null)
        { Detail = "coverage", Limit = 32 };
        var seen = new List<string>();
        int offset = 0;
        string? digest = null;
        do
        {
            command = command with { Offset = offset, ExpectedDetailDigest = digest };
            using JsonDocument document = JsonDocument.Parse(SetupCliMachineOutput.Serialize(command, result, 65_536));
            JsonElement detail = document.RootElement.GetProperty("detail");
            int returned = detail.GetProperty("returnedCount").GetInt32();
            seen.AddRange(detail.GetProperty("coverage").EnumerateArray().Select(item => item.GetProperty("id").GetString()!));
            digest = detail.GetProperty("digest").GetString();
            if (detail.GetProperty("terminal").GetBoolean())
            {
                await Assert.That(detail.GetProperty("nextOffset").ValueKind).IsEqualTo(JsonValueKind.Null);
                break;
            }
            await Assert.That(returned).IsGreaterThan(0);
            await Assert.That(detail.GetProperty("nextOffset").GetInt32()).IsEqualTo(offset + returned);
            offset = detail.GetProperty("nextOffset").GetInt32();
        } while (offset <= 3000);
        await Assert.That(seen.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    [Arguments(3000, true)]
    [Arguments(4096, false)]
    public async Task EveryRetainedFindingIsAddressableOnceDespiteEscapingAndShortenedPages(int retained, bool complete)
    {
        // Independent ordinal inventory; no parser or production projection constructs expectations.
        SetupCliMachineFinding[] findings = Enumerable.Range(0, retained).Select(index =>
            new SetupCliMachineFinding("unrecognized-input", "input-unrecognized", new([index]),
                index < 32 ? new("name", [new string('"', 256)]) : null)).ToArray();
        SetupCliCommandResult result = FindingResult(findings, complete);
        var command = FindingCommand();
        var seen = new List<int>();
        int offset = 0;
        string? digest = null;
        bool shortened = false;
        while (true)
        {
            command = command with { Offset = offset, ExpectedDetailDigest = digest };
            await Assert.That(SetupCliMachineOutput.ValidateDetail(command, result).Exit).IsEqualTo(SetupCliExitCode.Validation);
            byte[] bytes = SetupCliMachineOutput.Serialize(command, result, 65_536);
            await Assert.That(bytes.Length).IsLessThanOrEqualTo(65_536);
            await Assert.That(SetupCliMachineContractVerifier.Validate(bytes)).IsEmpty();
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement page = document.RootElement.GetProperty("detail");
            await Assert.That(Encoding.UTF8.GetByteCount(page.GetRawText())).IsLessThanOrEqualTo(8192);
            await Assert.That(page.GetProperty("retainedCount").GetInt32()).IsEqualTo(retained);
            await Assert.That(page.TryGetProperty("total", out _)).IsEqualTo(complete);
            int returned = page.GetProperty("returnedCount").GetInt32();
            await Assert.That(returned).IsEqualTo(page.GetProperty("findings").GetArrayLength());
            seen.AddRange(page.GetProperty("findings").EnumerateArray()
                .Select(item => item.GetProperty("source").GetProperty("ordinals")[0].GetInt32()));
            digest = page.GetProperty("digest").GetString();
            if (page.GetProperty("terminal").GetBoolean()) break;
            await Assert.That(returned).IsGreaterThan(0);
            shortened |= returned < 32;
            int next = page.GetProperty("nextOffset").GetInt32();
            await Assert.That(next).IsEqualTo(offset + returned);
            offset = next;
            await Assert.That(offset).IsLessThan(retained);
        }
        await Assert.That(shortened).IsTrue();
        await Assert.That(seen.SequenceEqual(Enumerable.Range(0, retained))).IsTrue();
        command = command with { Offset = retained, ExpectedDetailDigest = digest };
        await Assert.That(SetupCliMachineOutput.ValidateDetail(command, result).Exit).IsEqualTo(SetupCliExitCode.Validation);
        using JsonDocument terminal = JsonDocument.Parse(SetupCliMachineOutput.Serialize(command, result, 65_536));
        await Assert.That(terminal.RootElement.GetProperty("detail").GetProperty("returnedCount").GetInt32()).IsEqualTo(0);
        await Assert.That(terminal.RootElement.GetProperty("detail").GetProperty("terminal").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ReportingRefusesStaleOutOfRangeAndUnencodablePagesExplicitly()
    {
        var result = FindingResult([new("unrecognized-input", "input-unrecognized", new([0]), null)], true);
        var command = FindingCommand();
        using JsonDocument initial = JsonDocument.Parse(SetupCliMachineOutput.Serialize(command, result, 65_536));
        string digest = initial.RootElement.GetProperty("detailDigest").GetString()!;
        foreach (SetupCliCommand invalid in new[]
        {
            command with { Offset = 1 },
            command with { ExpectedDetailDigest = new string('f', 64) },
            command with { Offset = 2, ExpectedDetailDigest = digest }
        })
        {
            SetupCliCommandResult refused = SetupCliMachineOutput.ValidateDetail(invalid, result);
            await Assert.That(refused.Exit).IsEqualTo(SetupCliExitCode.Data);
            byte[] bytes = SetupCliMachineOutput.Serialize(invalid, refused, 65_536);
            await Assert.That(SetupCliMachineContractVerifier.Validate(bytes)).IsEmpty();
            using JsonDocument document = JsonDocument.Parse(bytes);
            await Assert.That(document.RootElement.TryGetProperty("detail", out _)).IsFalse();
        }
        var oversized = FindingResult([new("unrecognized-input", new string('a', 10_000), null, null)], true);
        SetupCliCommandResult failure = SetupCliMachineOutput.ValidateDetail(command, oversized);
        await Assert.That(failure.Exit).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("detail-item-unencodable");
        await Assert.That(SetupCliMachineContractVerifier.Validate(SetupCliMachineOutput.Serialize(command, failure, 65_536))).IsEmpty();
    }

    [Test]
    public async Task PublishedFindingCoordinatesAndInventoryCannotBeChangedThroughCallerCollections()
    {
        int[] ordinals = [7];
        string[] path = ["declared"];
        SetupCliMachineFinding[] findings = [new("invalid-supplied-value", "value-invalid", new(ordinals), new("name", path))];
        SetupCliCommandResult result = FindingResult(findings, true);
        byte[] before = SetupCliMachineOutput.Serialize(FindingCommand(), result, 65_536);
        ordinals[0] = 8;
        path[0] = "changed";
        findings[0] = new("unassessed", "mapping-unresolved", null, null);
        await Assert.That(SetupCliMachineOutput.Serialize(FindingCommand(), result, 65_536).SequenceEqual(before)).IsTrue();
        using JsonDocument document = JsonDocument.Parse(before);
        JsonElement finding = document.RootElement.GetProperty("detail").GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("source").GetProperty("ordinals")[0].GetInt32()).IsEqualTo(7);
        await Assert.That(finding.GetProperty("schema").GetProperty("relativePath")[0].GetString()).IsEqualTo("declared");
    }

    [Test]
    public async Task SanitizedDigestIgnoresPrivateValuesButRejectsChangedAssessmentFacts()
    {
        var fixture = new CliFixture();
        fixture.Files["definition.json"] = Encoding.UTF8.GetBytes(Definition);
        fixture.Files["existing.json"] = """{"name":"synthetic-private-one"}"""u8.ToArray();
        string[] command = ["artifact", "compare", "--definition", "definition.json", "--artifact", "config",
            "--input", "existing.json", "--machine", "--detail", "findings"];
        await Assert.That(await fixture.RunAsync(command)).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument first = JsonDocument.Parse(fixture.StandardOutput);
        string digest = first.RootElement.GetProperty("detailDigest").GetString()!;
        fixture.Files["existing.json"] = """{ "name": "synthetic-private-two" }"""u8.ToArray();
        await Assert.That(await fixture.RunAsync([.. command, "--expected-detail-digest", digest])).IsEqualTo(SetupCliExitCode.Success);
        using JsonDocument second = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(second.RootElement.GetProperty("detailDigest").GetString()).IsEqualTo(digest);
        fixture.Files["existing.json"] = """{"name":false}"""u8.ToArray();
        await Assert.That(await fixture.RunAsync([.. command, "--expected-detail-digest", digest])).IsEqualTo(SetupCliExitCode.Data);
    }

    [Test]
    public async Task ComparisonCanCompleteWhileAutofillRefusesUntilASeparateCopyIsRepaired()
    {
        var fixture = new CliFixture();
        fixture.Files["definition.json"] = Encoding.UTF8.GetBytes(Definition);
        fixture.Files["existing.json"] = """{"name":false}"""u8.ToArray();
        string[] common = ["--definition", "definition.json", "--artifact", "config", "--input", "existing.json", "--machine"];
        await Assert.That(await fixture.RunAsync(["artifact", "compare", .. common])).IsEqualTo(SetupCliExitCode.Validation);
        using JsonDocument report = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(report.RootElement.GetProperty("completion").GetString()).IsEqualTo("completed");
        await Assert.That(report.RootElement.GetProperty("assessment").GetProperty("autofill").GetString()).IsEqualTo("ineligible");
        await Assert.That(await fixture.RunAsync(["artifact", "prefill", .. common, "--output", "recovered.json"])).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(fixture.Files.ContainsKey("recovered.json")).IsFalse();
        fixture.Files["repaired.json"] = """{"name":"synthetic-repaired"}"""u8.ToArray();
        await Assert.That(await fixture.RunAsync("artifact", "prefill", "--definition", "definition.json", "--artifact", "config",
            "--input", "repaired.json", "--output", "recovered.json", "--machine")).IsEqualTo(SetupCliExitCode.Success);
        await Assert.That(Encoding.UTF8.GetString(fixture.Files["existing.json"])).IsEqualTo("""{"name":false}""");
    }

    [Test]
    [Arguments("""{"name":"synthetic-private"}""", 3, "incomplete")]
    [Arguments("""{"name":false}""", 2, "changes-required")]
    public async Task UnrepresentedRequiredInputRemainsIncompleteEvenWhenAnotherFindingBlocks(string input, int exit, string compatibility)
    {
        var fixture = new CliFixture();
        fixture.Files["definition.json"] = """
            {"$schema":"urn:islamu:configurator:authoring:v2",
             "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":1},
             "project":{"id":"example","name":"Partial comparison"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,"required":["name","port"],
              "properties":{"name":{"type":"string"},"port":{"type":"integer"}}},
             "outputs":{"config.json":{"id":"config","format":"json","projection":{"op":"object",
              "members":{"name":{"op":"copy","reference":{"scope":"root","path":"/name"}}}}}}}
            """u8.ToArray();
        fixture.Files["existing.json"] = Encoding.UTF8.GetBytes(input);
        await Assert.That((int)await fixture.RunAsync("artifact", "compare", "--definition", "definition.json",
            "--artifact", "config", "--input", "existing.json", "--machine", "--detail", "findings")).IsEqualTo(exit);
        await Assert.That(SetupCliMachineContractVerifier.Validate(fixture.StandardOutput)).IsEmpty();
        using JsonDocument document = JsonDocument.Parse(fixture.StandardOutput);
        JsonElement assessment = document.RootElement.GetProperty("assessment");
        await Assert.That(assessment.GetProperty("compatibility").GetString()).IsEqualTo(compatibility);
        await Assert.That(assessment.GetProperty("coverage").GetString()).IsEqualTo("incomplete");
        await Assert.That(assessment.GetProperty("inventory").GetString()).IsEqualTo("complete");
        await Assert.That(document.RootElement.GetProperty("completion").GetString()).IsEqualTo("completed");
    }

    [Test]
    public async Task UnsupportedInverseHasNoCompatibilityOrPreparationClaim()
    {
        var fixture = new CliFixture();
        fixture.Files["existing.md"] = "synthetic-private"u8.ToArray();
        await Assert.That(await fixture.RunAsync("artifact", "compare", "--definition", "definition.json",
            "--artifact", "markdown", "--input", "existing.md", "--machine")).IsEqualTo(SetupCliExitCode.Blocked);
        await Assert.That(SetupCliMachineContractVerifier.Validate(fixture.StandardOutput)).IsEmpty();
        using JsonDocument document = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(document.RootElement.GetProperty("assessment").GetProperty("compatibility").GetString()).IsEqualTo("unsupported");
        await Assert.That(document.RootElement.GetProperty("completion").GetString()).IsEqualTo("failed");
    }

    [Test]
    public async Task DefinitionAdmissionFailureIsNotReclassifiedAsAFindingsOptionError()
    {
        var fixture = new CliFixture();
        fixture.Files["definition.json"] = "{bad}"u8.ToArray();
        fixture.Files["existing.json"] = "{}"u8.ToArray();
        await Assert.That(await fixture.RunAsync("artifact", "compare", "--definition", "definition.json", "--artifact", "config",
            "--input", "existing.json", "--machine", "--detail", "findings")).IsEqualTo(SetupCliExitCode.Data);
        await Assert.That(SetupCliMachineContractVerifier.Validate(fixture.StandardOutput)).IsEmpty();
        using JsonDocument document = JsonDocument.Parse(fixture.StandardOutput);
        await Assert.That(document.RootElement.TryGetProperty("assessment", out _)).IsFalse();
        await Assert.That(document.RootElement.GetProperty("completion").GetString()).IsEqualTo("failed");
    }

    private static SetupCliCommand FindingCommand() =>
        new("artifact", "compare", true, false, false, null, "config", null, null, null, null)
        { Detail = "findings", Limit = 32 };

    private static SetupCliCommandResult FindingResult(SetupCliMachineFinding[] findings, bool complete) =>
        new(SetupCliExitCode.Validation, [], [], SetupCliResults.EmptyCoverage(), new("unassessed", [], []))
        {
            Completion = "completed",
            Preparation = "not-evaluated",
            Assessment = new(new string('a', 64), "config", "changes-required", complete ? "complete" : "incomplete",
                complete ? "complete" : "stopped", "ineligible", findings.Length, complete ? findings.Length : null)
            { Findings = findings }
        };
}
