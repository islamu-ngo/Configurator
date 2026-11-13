using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TUnit.Assertions.Enums;

namespace Configurator.Cli.Tests;

/// <summary>
/// Specifies the definition-bound IDE answer schema, schema-import label hygiene and authoring quality lint.
/// The answer schema is derived from public definition metadata only and must never carry secret exemplars.
/// </summary>
public sealed class DefinitionAnswerSchemaAndLintTests
{
    private static readonly string[] AuthoredServerOrder = ["port", "host"];

    private const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"3c9f0e8a-5d1b-4b6e-9a77-2f4c8d1e0b53","revision":7},
         "project":{"id":"answer.sample","name":"Answer Sample"},"applicationVersions":["1.0"],
         "configuration":{"type":"object","additionalProperties":false,"required":["storage","API_TOKEN"],
           "x-configurator":{"sensitivity":"public"},"properties":{
             "storage":{"type":"string","enum":["LOCAL","S3_COMPATIBLE","CUSTOM"],
               "description":"Where uploads are kept.",
               "x-configurator":{"sensitivity":"public","initialValue":"LOCAL","choices":[
                 {"value":"CUSTOM","label":"Custom endpoint","deprecated":true,"deprecationMessage":"Use S3 Compatible."}]}},
             "timeout":{"type":"integer","minimum":1,"maximum":600,
               "x-configurator":{"sensitivity":"public","units":"seconds","defaultText":"Target waits 30 seconds when omitted."}},
             "legacy_mode":{"type":"boolean","deprecated":true,
               "x-configurator":{"sensitivity":"public","deprecationMessage":"Removed in the next target release."}},
             "server":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
               "properties":{"port":{"type":"integer","x-configurator":{"sensitivity":"public"}},
                 "host":{"type":"string","format":"hostname","x-configurator":{"sensitivity":"public"}}}},
             "API_TOKEN":{"type":"string","minLength":32,"description":"Bearer token for the API.",
               "x-configurator":{"sensitivity":"secret"}}}},
         "outputs":{"settings.json":{"format":"json"}}}
        """;

    [Test]
    public async Task AnswerSchemaPinsIdentityAndTypesEveryValue()
    {
        var io = new CliFixture();
        io.Files["source.json"] = Encoding.UTF8.GetBytes(Source);
        await Assert.That(await io.RunAsync("definition", "answer-schema", "--definition", "source.json",
            "--output", "answers.schema.json", "--machine")).IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        JsonObject schema = JsonNode.Parse(io.Files["answers.schema.json"])!.AsObject();

        await Assert.That(schema["$schema"]!.GetValue<string>()).IsEqualTo("https://json-schema.org/draft/2020-12/schema");
        await Assert.That(schema["x-configurator"]!["familyId"]!.GetValue<string>()).IsEqualTo("3c9f0e8a-5d1b-4b6e-9a77-2f4c8d1e0b53");
        await Assert.That(schema["x-configurator"]!["revision"]!.GetValue<long>()).IsEqualTo(7L);
        await Assert.That(schema["required"]!.AsArray().Select(item => item!.GetValue<string>()).Contains("omitted")).IsFalse();
        await Assert.That(schema["properties"]!["omitted"]!["default"]!.AsArray().Count).IsEqualTo(0);
        JsonObject values = schema["properties"]!["values"]!.AsObject();
        await Assert.That(values["additionalProperties"]!.GetValue<bool>()).IsFalse();
        JsonObject storage = values["properties"]!["storage"]!.AsObject();
        await Assert.That(storage["enum"]!.AsArray().Count).IsEqualTo(3);
        await Assert.That(storage["default"]!.GetValue<string>()).IsEqualTo("LOCAL");
        string markdown = storage["markdownDescription"]!.GetValue<string>();
        await Assert.That(markdown).Contains("| `CUSTOM` | Custom endpoint |");
        await Assert.That(markdown).Contains("| `S3_COMPATIBLE` | S3 Compatible |");
        await Assert.That(markdown).Contains("Use S3 Compatible.");
        string timeout = values["properties"]!["timeout"]!["markdownDescription"]!.GetValue<string>();
        await Assert.That(timeout).Contains("seconds");
        await Assert.That(timeout).Contains("Target waits 30 seconds when omitted.");
        await Assert.That(values["properties"]!["legacy_mode"]!["deprecated"]!.GetValue<bool>()).IsTrue();
        await Assert.That(values["properties"]!["legacy_mode"]!["title"]!.GetValue<string>()).IsEqualTo("Legacy Mode");
        await Assert.That(values["properties"]!["server"]!["properties"]!.AsObject().Select(item => item.Key).ToArray())
            .IsEquivalentTo(AuthoredServerOrder, CollectionOrdering.Matching);
    }

    [Test]
    public async Task SecretLeavesAreWriteOnlyAndCarryNoExemplars()
    {
        var io = new CliFixture();
        io.Files["source.json"] = Encoding.UTF8.GetBytes(Source);
        await Assert.That(await io.RunAsync("definition", "answer-schema", "--definition", "source.json",
            "--output", "-")).IsEqualTo(CliExitCode.Success);
        JsonObject token = JsonNode.Parse(io.StandardOutput)!["properties"]!["values"]!["properties"]!["API_TOKEN"]!.AsObject();

        await Assert.That(token["writeOnly"]!.GetValue<bool>()).IsTrue();
        await Assert.That(token["minLength"]!.GetValue<int>()).IsEqualTo(32);
        foreach (string member in new[] { "default", "const", "enum", "examples" })
            await Assert.That(token.ContainsKey(member)).IsFalse();
    }

    [Test]
    [Arguments("--answers")]
    [Arguments("--input")]
    public async Task AnswerSchemaAcceptsNoPrivateAnswerInputs(string option)
    {
        var io = new CliFixture();
        io.Files["source.json"] = Encoding.UTF8.GetBytes(Source);
        CliExitCode exit = await io.RunAsync("definition", "answer-schema", "--definition", "source.json",
            option, "answers.json", "--output", "answers.schema.json", "--machine");
        await Assert.That(exit).IsEqualTo(CliExitCode.Usage);
        await Assert.That(io.Files.ContainsKey("answers.schema.json")).IsFalse();
        await Assert.That(Encoding.UTF8.GetString(io.StandardOutput)).DoesNotContain("private-answer-marker");
    }

    [Test]
    public async Task SchemaImportStripsTitlesThatOnlyEchoTheKey()
    {
        using var files = new DefinitionTestDirectory();
        string schema = files.Path("schema.json");
        File.WriteAllText(schema, """
            {"type":"object","additionalProperties":false,"properties":{
              "PUBLIC_BASE_URL":{"type":"string","title":"PUBLIC_BASE_URL"},
              "region":{"type":"string","title":"Deployment region"}}}
            """);
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await io.RunAsync("definition", "init", "--root", files.Root, "--project-id", "example.service",
            "--name", "Example Service", "--application-version", "1.0.0", "--format", "json", "--from-schema", schema,
            "--machine")).IsEqualTo(CliExitCode.Success);
        JsonNode properties = JsonNode.Parse(File.ReadAllText(files.Path("configurator.json")))!["configuration"]!["properties"]!;

        await Assert.That(properties["PUBLIC_BASE_URL"]!.AsObject().ContainsKey("title")).IsFalse();
        await Assert.That(properties["region"]!["title"]!.GetValue<string>()).IsEqualTo("Deployment region");
    }

    [Test]
    public async Task QualityLintReportsEveryAdvisoryWithoutChangingAdmission()
    {
        using var files = new DefinitionTestDirectory();
        string path = files.Path("configurator.json");
        File.WriteAllText(path, """
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"7d1c2b3a-4e5f-4a6b-8c7d-9e0f1a2b3c4d","revision":1},
             "project":{"id":"lint.sample","name":"Lint Sample"},"applicationVersions":["1.0"],
             "configuration":{"type":"object","additionalProperties":false,"required":["SIGNING_KEY"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                 "PUBLIC_BASE_URL":{"type":"string","title":"PUBLIC_BASE_URL","x-configurator":{"sensitivity":"public"}},
                 "provider":{"type":"string","enum":["OPENAI_COMPATIBLE","local"],"description":"Copied from $.spec.ai.provider.",
                   "x-configurator":{"sensitivity":"public","choices":[{"value":"OPENAI_COMPATIBLE","label":"OPENAI_COMPATIBLE"},
                     {"value":"local","deprecated":true}]}},
                 "mode":{"type":"string","deprecated":true,"description":"Host validator: security-setting. Host restart: process",
                   "x-configurator":{"sensitivity":"public"}},
                 "SIGNING_KEY":{"type":"string","x-configurator":{"sensitivity":"secret"}}}},
             "presentation":{"defaultJourney":"setup","sections":[
               {"id":"general","title":"General","pointers":["/PUBLIC_BASE_URL","/provider","/mode","/SIGNING_KEY"]},
               {"id":"orphan","title":"Orphan","description":"Never shown.","pointers":[]}],
              "journeys":[{"id":"setup","title":"Setup","sections":["general"]}]},
             "outputs":{"settings.json":{"format":"json","targetUpdateMode":"replace","restartRequirement":"process"}}}
            """);
        var io = new CliFixture { AuthoringFiles = new PublicDefinitionFiles() };
        await Assert.That(await io.RunAsync("definition", "lint", "--source", path, "--quality", "--machine"))
            .IsEqualTo(CliExitCode.Success);
        await Assert.That(CliMachineContractVerifier.Validate(io.StandardOutput)).IsEmpty();
        using JsonDocument output = JsonDocument.Parse(io.StandardOutput);
        string[] codes = output.RootElement.GetProperty("diagnostics").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString()!).ToArray();

        foreach (string code in new[]
        {
            "raw-identifier-title", "verbatim-choice-label", "jsonpath-description", "exporter-diagnostic-description",
            "missing-deprecation-message", "missing-section-description", "required-secret-missing-guidance",
            "unreferenced-journey-section"
        })
            await Assert.That(codes).Contains(code);
        await Assert.That(output.RootElement.GetProperty("diagnostics").EnumerateArray()
            .Any(item => item.GetProperty("severity").GetString() == "error")).IsFalse();
        await Assert.That(await io.RunAsync("definition", "lint", "--source", path, "--machine")).IsEqualTo(CliExitCode.Success);
        using JsonDocument plain = JsonDocument.Parse(io.StandardOutput);
        await Assert.That(plain.RootElement.GetProperty("diagnostics").EnumerateArray()
            .Any(item => item.GetProperty("code").GetString() == "raw-identifier-title")).IsFalse();
    }
}
