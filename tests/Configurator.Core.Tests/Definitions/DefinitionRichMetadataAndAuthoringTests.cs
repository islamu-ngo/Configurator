using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using TUnit.Assertions.Enums;

namespace Configurator.Core.Tests.Definitions;

/// <summary>
/// Specifies de-flattened authoring: nested presentation pointers, collapsible section groups, reusable
/// acyclic predicates, value-keyed choice localization and concise output bindings.
/// </summary>
public sealed class DefinitionRichMetadataAndAuthoringTests
{
    private static readonly string[] ServerPointers = ["/server/domain", "/server/port"];
    private static readonly string[] ProviderPointers = ["/ai/provider"];

    private const string Configuration = """
        {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
         "properties":{
           "server":{"type":"object","additionalProperties":false,"required":["domain"],"x-configurator":{"sensitivity":"public"},
             "properties":{
               "domain":{"type":"string","format":"hostname","x-configurator":{"sensitivity":"public"}},
               "port":{"type":"integer","minimum":1,"maximum":65535,"x-configurator":{"sensitivity":"public"}}}},
           "ai":{"type":"object","additionalProperties":false,"required":["enabled"],"x-configurator":{"sensitivity":"public"},
             "properties":{
               "enabled":{"type":"boolean","x-configurator":{"sensitivity":"public","initialValue":false}},
               "provider":{"type":"string","enum":["OPENAI_COMPATIBLE","ANTHROPIC"],
                 "x-configurator":{"sensitivity":"public","applicableWhen":{"op":"predicate","name":"aiEnabled"},
                   "choices":[{"value":"OPENAI_COMPATIBLE","label":"OpenAI-compatible endpoint"}]}}}}}}
        """;

    [Test]
    public async Task NestedSectionPointersGroupsAndDisclosureAdmit()
    {
        Definition definition = Compile(Document(presentation: """
            {"defaultJourney":"setup","sections":[
              {"id":"server","title":"Server","detail":"basic","pointers":["/server/domain","/server/port"]},
              {"id":"ai","title":"Assistant","pointers":["/ai"],"groups":[
                {"id":"provider","title":"Provider","detail":"advanced","collapsed":true,
                 "pointers":["/ai/provider"],"applicableWhen":{"op":"predicate","name":"aiEnabled"}}]}],
             "journeys":[{"id":"setup","title":"Setup","detail":"advanced","sections":["server","ai"]}]}
            """));

        DefinitionPresentation presentation = definition.Presentation!;
        await Assert.That(presentation.Sections[0].Pointers.ToArray()).IsEquivalentTo(ServerPointers, CollectionOrdering.Matching);
        await Assert.That(presentation.Sections[0].Disclosure).IsEqualTo(FieldDisclosure.Essential);
        DefinitionSectionGroup group = presentation.Sections[1].Groups.Single();
        await Assert.That(group.Pointers.ToArray()).IsEquivalentTo(ProviderPointers, CollectionOrdering.Matching);
        await Assert.That(group.Disclosure).IsEqualTo(FieldDisclosure.Advanced);
        await Assert.That(group.InitiallyCollapsed).IsTrue();
        await Assert.That(group.ApplicableWhen.HasValue).IsTrue();
        await Assert.That(presentation.Experiences[0].InitialDisclosure).IsEqualTo(FieldDisclosure.Advanced);
    }

    [Test]
    public async Task GroupPointersOutsideTheirSectionReject()
    {
        await Assert.That(Failure(() => Compile(Document(presentation: """
            {"defaultJourney":"setup","sections":[
              {"id":"server","title":"Server","pointers":["/server"],"groups":[
                {"id":"leak","title":"Leak","pointers":["/ai/provider"]}]}],
             "journeys":[{"id":"setup","title":"Setup","sections":["server"]}]}
            """)))).IsEqualTo("presentation-reference");
    }

    [Test]
    public async Task NamedPredicatesInlineDeterministicallyIntoRuntimeConditions()
    {
        Definition definition = Compile(Document());
        using JsonDocument runtime = JsonDocument.Parse(definition.CanonicalBytes);
        JsonElement condition = runtime.RootElement.GetProperty("configuration").GetProperty("properties").GetProperty("ai")
            .GetProperty("properties").GetProperty("provider").GetProperty("x-configurator").GetProperty("applicableWhen");

        await Assert.That(JsonNode.DeepEquals(JsonNode.Parse(condition.GetRawText()), JsonNode.Parse("""
            {"op":"equals","reference":{"scope":"root","path":"/ai/enabled"},"value":true}
            """))).IsTrue();
        await Assert.That(runtime.RootElement.TryGetProperty("predicates", out _)).IsFalse();
    }

    [Test]
    [Arguments("""{"aiEnabled":{"op":"predicate","name":"other"},"other":{"op":"not","operands":[{"op":"predicate","name":"aiEnabled"}]}}""", "predicate-cycle")]
    [Arguments("""{"aiEnabled":{"op":"predicate","name":"missing"}}""", "predicate-unknown")]
    [Arguments("""{"aiEnabled":{"op":"predicate","name":"aiEnabled"}}""", "predicate-cycle")]
    public async Task CyclicOrUnknownPredicatesFailClosed(string predicates, string code)
    {
        await Assert.That(Failure(() => Compile(Document(predicates: predicates)))).IsEqualTo(code);
    }

    [Test]
    public async Task PredicateChainsDeeperThanTheBudgetReject()
    {
        var predicates = new JsonObject { ["p0"] = JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/ai/enabled"},"value":true}""") };
        for (int index = 1; index <= 17; index++)
            predicates["p" + index] = new JsonObject { ["op"] = "predicate", ["name"] = "p" + (index - 1) };
        predicates["aiEnabled"] = new JsonObject { ["op"] = "predicate", ["name"] = "p17" };
        await Assert.That(Failure(() => Compile(Document(predicates: predicates.ToJsonString())))).IsEqualTo("predicate-limit");
    }

    [Test]
    public async Task ValueKeyedChoiceLocalizationSurvivesOptionInsertion()
    {
        const string overrides = """
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
              "/configuration/properties/ai/properties/provider/x-configurator/choices[value=OPENAI_COMPATIBLE]/label":"نقطة متوافقة مع OpenAI",
              "/configuration/properties/ai/properties/provider/x-configurator/choices[value=ANTHROPIC]/label":"أنثروبيك"}}}}
            """;
        Definition original = Compile(Document(localization: overrides));
        JsonObject inserted = JsonNode.Parse(Document(localization: overrides))!.AsObject();
        JsonNode provider = inserted["configuration"]!["properties"]!["ai"]!["properties"]!["provider"]!;
        provider["enum"]!.AsArray().Insert(0, "NONE");
        provider["x-configurator"]!["choices"]!.AsArray().Insert(0, JsonNode.Parse("""{"value":"NONE","label":"No assistant"}"""));
        Definition shifted = Compile(inserted.ToJsonString());

        await Assert.That(Translation(original, "/configuration/properties/ai/properties/provider/x-configurator/choices/0/label"))
            .IsEqualTo("نقطة متوافقة مع OpenAI");
        await Assert.That(Translation(shifted, "/configuration/properties/ai/properties/provider/x-configurator/choices/1/label"))
            .IsEqualTo("نقطة متوافقة مع OpenAI");
        DefinitionChoice anthropic = shifted.Fields.Single(field => field.Name == "ai").Properties
            .Single(field => field.Name == "provider").OptionMetadata.Single(choice => choice.WireToken == "ANTHROPIC");
        await Assert.That(anthropic.DisplayLabel).IsEqualTo("Anthropic");
        await Assert.That(Translation(shifted, "/configuration/properties/ai/properties/provider/x-configurator/choices/"
            + anthropic.MetadataIndex + "/label")).IsEqualTo("أنثروبيك");
    }

    [Test]
    public async Task ValueKeyedLocalizationOfUnknownChoicesRejects()
    {
        await Assert.That(Failure(() => Compile(Document(localization: """
            {"defaultLocale":"en","translations":{"ar":{"overrides":{
              "/configuration/properties/ai/properties/provider/x-configurator/choices[value=MISSING]/label":"مفقود"}}}}
            """)))).IsEqualTo("localization-target");
    }

    [Test]
    public async Task ExpressiveOutputsLowerToBoundedProjections()
    {
        Definition definition = Compile(Document(outputs: """
            {"base.env":{"format":"dotenv","bindings":{"SERVER_DOMAIN":"/server/domain","STATIC_MODE":{"literal":"offline"}}},
             "assistant.env":{"format":"dotenv","when":{"op":"predicate","name":"aiEnabled"},
               "includeBindings":["base.env"],"bindObject":[{"source":"/ai","only":["provider"]}],
               "bindings":{"STATIC_MODE":{"literal":"assisted"}}},
             "server.json":{"format":"json","bindObject":[{"source":"/server","target":"/listener"}]}}
            """));
        AnswerSnapshot enabled = Answers("""{"server":{"domain":"api.example.org","port":8443},"ai":{"enabled":true,"provider":"ANTHROPIC"}}""");
        AnswerSnapshot disabled = Answers("""{"server":{"domain":"api.example.org"},"ai":{"enabled":false}}""");

        await Assert.That(Render(definition, "assistant.env", enabled))
            .IsEqualTo("SERVER_DOMAIN=api.example.org\nSTATIC_MODE=assisted\nprovider=ANTHROPIC\n");
        await Assert.That(Render(definition, "base.env", enabled)).IsEqualTo("SERVER_DOMAIN=api.example.org\nSTATIC_MODE=offline\n");
        await Assert.That(Render(definition, "server.json", enabled)).IsEqualTo("""{"listener":{"domain":"api.example.org","port":8443}}""");
        await Assert.That(DefinitionBundleComposer.ComposeActive(definition, disabled).Artifacts
            .Any(artifact => artifact.FileName == "assistant.env")).IsFalse();
    }

    [Test]
    [Arguments("""{"a.env":{"format":"dotenv","includeBindings":["b.env"]},"b.env":{"format":"dotenv","includeBindings":["a.env"]}}""", "authoring-include-cycle")]
    [Arguments("""{"a.env":{"format":"dotenv","includeBindings":["missing.env"]}}""", "authoring-mapping")]
    [Arguments("""{"a.env":{"format":"dotenv","bindObject":[{"source":"/server/domain"}]}}""", "authoring-mapping")]
    [Arguments("""{"a.env":{"format":"dotenv","bindObject":[{"source":"/server","target":"/x"}]}}""", "authoring-mapping")]
    public async Task InvalidOutputShorthandsReject(string outputs, string code)
    {
        await Assert.That(Failure(() => Compile(Document(outputs: outputs)))).IsEqualTo(code);
    }

    [Test]
    public async Task NestedOutputSelectorPointersAdmit()
    {
        string source = Document(outputs: """
            {"a.json":{"format":"json","when":{"op":"equals","reference":{"scope":"root","path":"/delivery/target"},"value":"a"}},
             "b.json":{"format":"json","when":{"op":"equals","reference":{"scope":"root","path":"/delivery/target"},"value":"b"}}}
            """, presentation: """
            {"defaultJourney":"setup","sections":[{"id":"delivery","title":"Delivery","pointers":["/delivery/target"]}],
             "journeys":[{"id":"setup","title":"Setup","sections":["delivery"]}],
             "outputSelector":{"pointer":"/delivery/target","options":[
               {"value":"a","title":"A","artifactIds":["out-a.json"]},{"value":"b","title":"B","artifactIds":["out-b.json"]}]}}
            """);
        JsonObject root = JsonNode.Parse(source)!.AsObject();
        root["configuration"]!["properties"]!["delivery"] = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"required":["target"],"x-configurator":{"sensitivity":"public"},
             "properties":{"target":{"type":"string","enum":["a","b"],"x-configurator":{"sensitivity":"public","initialValue":"a"}}}}
            """);
        root["configuration"]!["required"] = new JsonArray("delivery");
        Definition definition = Compile(root.ToJsonString());
        await Assert.That(definition.Presentation!.OutputSelector!.Location.ToPointer()).IsEqualTo("/delivery/target");
    }

    private static string Document(string? presentation = null, string? localization = null, string? outputs = null,
        string predicates = """{"aiEnabled":{"op":"equals","reference":{"scope":"root","path":"/ai/enabled"},"value":true}}""")
    {
        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = JsonNode.Parse("""{"familyId":"2b4e4d55-3f0a-4f51-9d6e-1c0f0b8a7e42","revision":1}"""),
            ["project"] = JsonNode.Parse("""{"id":"rich.sample","name":"Rich Sample"}"""),
            ["applicationVersions"] = new JsonArray("1.0"),
            ["predicates"] = JsonNode.Parse(predicates),
            ["configuration"] = JsonNode.Parse(Configuration),
            ["outputs"] = JsonNode.Parse(outputs ?? """{"settings.json":{"format":"json"}}""")
        };
        if (presentation is not null) root["presentation"] = JsonNode.Parse(presentation);
        if (localization is not null) root["localization"] = JsonNode.Parse(localization);
        return root.ToJsonString();
    }

    private static Definition Compile(string source) =>
        DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source)));

    private static string? Translation(Definition definition, string pointer)
    {
        using JsonDocument runtime = JsonDocument.Parse(definition.CanonicalBytes);
        return runtime.RootElement.GetProperty("localization").GetProperty("translations").GetProperty("ar")
            .GetProperty("overrides").TryGetProperty(pointer, out JsonElement value) ? value.GetString() : null;
    }

    private static string Render(Definition definition, string fileName, AnswerSnapshot answers)
    {
        DefinitionArtifactResult result = DefinitionArtifactComposer.Compose(definition,
            definition.Artifacts.Single(artifact => artifact.FileName == fileName).Id, answers);
        return result.Succeeded ? Encoding.UTF8.GetString(result.Artifact!.Bytes.Span)
            : "failed:" + string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    private static AnswerSnapshot Answers(string values) => AnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes(
        """{"answerContractVersion":"0.0.1","omitted":[],"values":""" + values + "}"));

    private static string? Failure(Func<Definition> action)
    {
        try { _ = action(); }
        catch (DefinitionException exception) { return exception.Code; }
        return null;
    }
}
