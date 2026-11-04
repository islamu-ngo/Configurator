namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionContextImportTests
{
    [Test]
    public async Task AnAssessmentCapturedForAnEarlierRevisionCannotAdoptIntoNewAnswers()
    {
        Definition definition = DefinitionFixture.Parse();
        var captured = new AnswerSnapshot(9, new Dictionary<string, JsonElement>());
        using DefinitionImportAssessment assessment = DefinitionImport.Inspect(definition, "config",
            """{"name":"hello"}"""u8.ToArray(), context: captured);
        DefinitionImportResult result = assessment.AssessAutofill(new(10, new Dictionary<string, JsonElement>()));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("context-stale");
        await Assert.That(result.Answers).IsNull();
    }

    [Test]
    public async Task ExplicitPublicContextResolvesCompoundAndMemberGuardsWithoutImportingInactiveKeys()
    {
        Definition definition = DefinitionFixture.Parse(Source);
        AnswerSnapshot current = Answers("worker");
        DefinitionImportResult result = DefinitionImport.Assess(definition, "environment",
            "WORKER=hello\n"u8.ToArray(), current);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.Values["worker"].GetString()).IsEqualTo("hello");
        await Assert.That(result.Answers.Values.ContainsKey("dashboard")).IsFalse();
        await Assert.That(result.Answers.Values["mode"].GetString()).IsEqualTo("worker");
    }

    [Test]
    public async Task AnInactiveMemberCannotBeRevivedByImportedEnvironmentContent()
    {
        Definition definition = DefinitionFixture.Parse(Source);
        DefinitionImportResult result = DefinitionImport.Assess(definition, "environment",
            "WORKER=hello\nDASHBOARD=synthetic-other-recipient\n"u8.ToArray(), Answers("worker"));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Answers).IsNull();
        await Assert.That(result.ToString()).DoesNotContain("synthetic-other-recipient");
    }

    [Test]
    public async Task EachDestinationImportsIndependentLiteralBytesAndPreservesEmptyAssignments()
    {
        foreach ((string dialect, string text, string expected) in new[]
        {
            ("docker-run", "VALUE=  '\"#$;\\&=é  \n", "  '\"#$;\\&=é  "),
            ("compose", "VALUE='  \"#$;\\&=é  '\n", "  \"#$;\\&=é  ")
        })
        {
            JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
            source["configuration"]!["properties"]!["service.name"]!["minLength"] = 0;
            source["outputs"] = JsonNode.Parse("""
                {"service.env":{"id":"environment","format":"dotenv","projection":{
                 "op":"object","members":{"VALUE":{"op":"copy",
                 "reference":{"scope":"root","path":"/service.name"}}}}}}
                """);
            source["outputs"]!["service.env"]!["environmentDialect"] = dialect;
            Definition definition = DefinitionFixture.Parse(source.ToJsonString());
            var current = new AnswerSnapshot(9, new Dictionary<string, JsonElement>());
            DefinitionImportResult literal = DefinitionImport.Assess(definition, "environment", Encoding.UTF8.GetBytes(text), current);
            await Assert.That(literal.Succeeded).IsTrue();
            await Assert.That(literal.Answers!.Values["service.name"].GetString()).IsEqualTo(expected);
            DefinitionImportResult empty = DefinitionImport.Assess(definition, "environment", "VALUE=\n"u8.ToArray(), current);
            await Assert.That(empty.Succeeded).IsTrue();
            await Assert.That(empty.Answers!.Values["service.name"].GetString()).IsEqualTo("");
            DefinitionImportResult absent = DefinitionImport.Assess(definition, "environment", "\n"u8.ToArray(), current);
            await Assert.That(absent.Succeeded).IsTrue();
            await Assert.That(absent.Answers!.Values.ContainsKey("service.name")).IsFalse();
        }
    }

    [Test]
    public async Task DeploymentImportsRequireCapturedContextAndRejectAnotherRecipient()
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["configuration"]!["properties"]!["recipe"] = JsonNode.Parse("""
            {"type":"string","enum":["isolated"],"x-configurator":{"sensitivity":"public","initialValue":"isolated"}}
            """);
        source["configuration"]!["properties"]!["scope"] = JsonNode.Parse("""
            {"type":"string","enum":["worker","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"worker"}}
            """);
        source["configuration"]!["properties"]!["task"] = JsonNode.Parse("""
            {"type":"string","enum":["environment","manifest"],"x-configurator":{"sensitivity":"public","initialValue":"environment"}}
            """);
        source["deployment"] = JsonNode.Parse("""
            {"recipePointer":"/recipe","scopePointer":"/scope",
             "components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
             "recipes":[{"id":"isolated","title":"Isolated","scopes":[
              {"id":"worker","title":"Worker","componentIds":["worker"],"artifactIds":["worker"]},
              {"id":"dashboard","title":"Dashboard","componentIds":["dashboard"],"artifactIds":["dashboard"]}]}]}
            """);
        source["deployment"]!["when"] = JsonNode.Parse("""
            {"op":"equals","reference":{"scope":"root","path":"/task"},"value":"environment"}
            """);
        source["outputs"] = JsonNode.Parse("""
            {"worker.env":{"id":"worker","format":"dotenv","consumerIds":["worker"],
              "surface":"process-environment","environmentDialect":"docker-run",
              "projection":{"op":"object","members":{"VALUE":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}},
             "dashboard.env":{"id":"dashboard","format":"dotenv","consumerIds":["dashboard"],
              "surface":"process-environment","environmentDialect":"docker-run",
              "projection":{"op":"object","members":{"VALUE":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}}}
            """);
        source["outputs"]!["manifest.json"] = JsonNode.Parse("""
            {"id":"manifest","format":"json","projection":{"op":"object",
             "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"manifest"},
             "members":{"name":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}}
            """);
        Definition definition = DefinitionFixture.Parse(source.ToJsonString());
        var context = new AnswerSnapshot(9, new Dictionary<string, JsonElement>
        {
            ["recipe"] = JsonSerializer.SerializeToElement("isolated"),
            ["scope"] = JsonSerializer.SerializeToElement("worker"),
            ["task"] = JsonSerializer.SerializeToElement("environment")
        });
        await Assert.That(DefinitionImport.GetCapability(definition, "worker").Code).IsEqualTo("context-required");
        DefinitionImportResult accepted = DefinitionImport.Assess(definition, "worker", "VALUE=hello\n"u8.ToArray(), context);
        await Assert.That(accepted.Succeeded).IsTrue();
        await Assert.That(accepted.Answers!.Values["scope"].GetString()).IsEqualTo("worker");
        DefinitionImportResult refused = DefinitionImport.Assess(definition, "dashboard", "VALUE=hello\n"u8.ToArray(), context);
        await Assert.That(refused.Succeeded).IsFalse();
        await Assert.That(refused.Code).IsEqualTo("context-artifact-ineligible");
        await Assert.That(DefinitionImport.CaptureEditableScope(definition, "dashboard", context).IsSupported).IsFalse();
        DefinitionImportResult manifestConflict = DefinitionImport.Assess(definition, "manifest",
            """{"name":"hello"}"""u8.ToArray(), context);
        await Assert.That(manifestConflict.Code).IsEqualTo("answer-conflict");
        DefinitionImportResult manifest = DefinitionImport.Assess(definition, "manifest",
            """{"name":"hello"}"""u8.ToArray(), context, new HashSet<string>(StringComparer.Ordinal) { "task" });
        await Assert.That(manifest.Succeeded).IsTrue();
        await Assert.That(manifest.Answers!.Values["task"].GetString()).IsEqualTo("manifest");
        await Assert.That(manifest.Answers.Values["scope"].GetString()).IsEqualTo("worker");
    }

    private static AnswerSnapshot Answers(string mode) => new(9, new Dictionary<string, JsonElement>
    {
        ["mode"] = JsonSerializer.SerializeToElement(mode),
        ["enabled"] = JsonSerializer.SerializeToElement(true)
    });

    private const string Source = """
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"04fa6127-1cf9-41ba-804b-d002db9c23b8","revision":1},
         "project":{"id":"example.import-context","name":"Context import","publisher":"example"},
         "applicationVersions":["1.0"],"entryPoints":["setup"],
         "configuration":{"type":"object","additionalProperties":false,
          "x-configurator":{"sensitivity":"public"},"properties":{
           "mode":{"type":"string","enum":["worker","dashboard"],"x-configurator":{"sensitivity":"public"}},
           "enabled":{"type":"boolean","x-configurator":{"sensitivity":"public"}},
           "worker":{"type":"string","x-configurator":{"sensitivity":"public"}},
           "dashboard":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
         "outputs":{"environment.env":{"id":"environment","format":"dotenv","projection":{
          "op":"object","when":{"op":"all","operands":[
           {"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true},
           {"op":"in","reference":{"scope":"root","path":"/mode"},"values":["worker","dashboard"]}]},
          "members":{
           "WORKER":{"op":"copy","reference":{"scope":"root","path":"/worker"},
            "when":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"worker"}},
           "DASHBOARD":{"op":"copy","reference":{"scope":"root","path":"/dashboard"},
            "when":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"dashboard"}}}}}}}
        """;
}
