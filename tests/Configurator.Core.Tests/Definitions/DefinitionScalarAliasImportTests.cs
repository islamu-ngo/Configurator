namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionScalarAliasImportTests
{
    [Test]
    public async Task EqualScalarConsumersRecoverOneAnswerAndBothAbsentRetainAbsence()
    {
        Definition definition = Build();
        foreach (string input in new[] { """{"first":"hello","second":"hello"}""", "{}" })
        {
            using DefinitionImportAssessment assessment = DefinitionImport.Inspect(definition, "aliases", Encoding.UTF8.GetBytes(input));
            DefinitionImportResult result = assessment.AssessAutofill(Empty());
            await Assert.That(result.Succeeded).IsTrue().Because(result.Code);
            if (input == "{}")
                await Assert.That(result.Answers!.Values.ContainsKey("service.name")).IsFalse();
            else
            {
                await Assert.That(result.Answers!.Values["service.name"].GetString()).IsEqualTo("hello");
                await Assert.That(result.ProvidedFields.Count).IsEqualTo(1);
            }
        }
    }

    [Test]
    public async Task DifferentValuesOrPresenceRejectTheWholeAliasGroupWithoutRevealingValues()
    {
        Definition definition = Build();
        foreach (string input in new[]
        {
            """{"first":"synthetic-first","second":"synthetic-second"}""",
            """{"first":"synthetic-first"}""", """{"second":"synthetic-second"}"""
        })
        {
            using DefinitionImportAssessment assessment = DefinitionImport.Inspect(definition, "aliases", Encoding.UTF8.GetBytes(input));
            DefinitionImportResult result = assessment.AssessAutofill(Empty());
            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Answers).IsNull();
            await Assert.That(result.Code).IsEqualTo("alias-conflict");
            await Assert.That(JsonSerializer.Serialize(assessment.Report)).DoesNotContain("synthetic-");
        }
    }

    [Test]
    public async Task DestinationAliasesCompareDecodedTypedValues()
    {
        foreach ((string dialect, string input) in new[]
        {
            ("docker-run", "FIRST=7\nSECOND=7\n"),
            ("compose", "FIRST='7'\nSECOND=\"7\"\n")
        })
        {
            JsonNode source = Source("dotenv");
            source["configuration"]!["properties"]!["service.name"] = JsonNode.Parse("""{"type":"integer"}""");
            source["outputs"]!["aliases.env"]!["environmentDialect"] = dialect;
            Definition definition = DefinitionFixture.Parse(source.ToJsonString());
            using DefinitionImportAssessment assessment = DefinitionImport.Inspect(definition, "aliases", Encoding.UTF8.GetBytes(input));
            DefinitionImportResult result = assessment.AssessAutofill(Empty());
            await Assert.That(result.Succeeded).IsTrue().Because(result.Code);
            await Assert.That(result.Answers!.Values["service.name"].GetInt32()).IsEqualTo(7);
        }
    }

    [Test]
    public async Task AliasGuardsMustHaveTheSameContractEvenWhenBothCurrentlyMatch()
    {
        JsonNode source = Source("json");
        source["configuration"]!["properties"]!["enabled"] = JsonNode.Parse("""
            {"type":"boolean","x-configurator":{"sensitivity":"public"}}
            """);
        JsonNode members = source["outputs"]!["aliases.json"]!["projection"]!["members"]!;
        JsonNode condition = JsonNode.Parse("""
            {"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}
            """)!;
        members["first"]!["when"] = condition.DeepClone();
        members["second"]!["when"] = condition.DeepClone();
        var context = new AnswerSnapshot(0, new Dictionary<string, JsonElement> { ["enabled"] = JsonSerializer.SerializeToElement(true) });
        Definition matching = DefinitionFixture.Parse(source.ToJsonString());
        using DefinitionImportAssessment assessment = DefinitionImport.Inspect(matching, "aliases",
            """{"first":"hello","second":"hello"}"""u8.ToArray(), context: context);
        await Assert.That(assessment.AssessAutofill(context).Succeeded).IsTrue();
        members["second"]!["when"] = new JsonObject { ["op"] = "all", ["operands"] = new JsonArray(condition.DeepClone()) };
        Definition different = DefinitionFixture.Parse(source.ToJsonString());
        await Assert.That(DefinitionImport.GetCapability(different, "aliases", context).StrictAutofillSupported).IsFalse();
    }

    [Test]
    public async Task DifferentOutputParentsCannotShareAnAliasGroup()
    {
        JsonNode source = Source("json");
        source["configuration"]!["properties"]!["enabled"] = JsonNode.Parse("""
            {"type":"boolean","x-configurator":{"sensitivity":"public"}}
            """);
        JsonObject members = source["outputs"]!["aliases.json"]!["projection"]!["members"]!.AsObject();
        JsonNode second = members["second"]!.DeepClone();
        members.Remove("second");
        members["nested"] = JsonNode.Parse("""
            {"op":"object","when":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true},"members":{}}
            """);
        members["nested"]!["members"]!["second"] = second;
        Definition definition = DefinitionFixture.Parse(source.ToJsonString());
        var context = new AnswerSnapshot(0, new Dictionary<string, JsonElement> { ["enabled"] = JsonSerializer.SerializeToElement(true) });
        await Assert.That(DefinitionImport.GetCapability(definition, "aliases", context).StrictAutofillSupported).IsFalse();
    }

    private static Definition Build() => DefinitionFixture.Parse(Source("json").ToJsonString());
    private static AnswerSnapshot Empty() => new(0, new Dictionary<string, JsonElement>());

    private static JsonNode Source(string format)
    {
        JsonNode source = JsonNode.Parse(DefinitionFixture.Json)!;
        source["outputs"] = JsonNode.Parse(format == "dotenv" ? """
            {"aliases.env":{"id":"aliases","format":"dotenv","projection":{"op":"object","members":{
             "FIRST":{"op":"copy","reference":{"scope":"root","path":"/service.name"}},
             "SECOND":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}}}
            """ : """
            {"aliases.json":{"id":"aliases","format":"json","projection":{"op":"object","members":{
             "first":{"op":"copy","reference":{"scope":"root","path":"/service.name"}},
             "second":{"op":"copy","reference":{"scope":"root","path":"/service.name"}}}}}}
            """);
        return source;
    }
}
