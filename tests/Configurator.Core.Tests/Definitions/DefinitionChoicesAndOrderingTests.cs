using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using TUnit.Assertions.Enums;

namespace Configurator.Core.Tests.Definitions;

/// <summary>Specifies single-source choices, partial choice overrides and digest-neutral property ordering.</summary>
public sealed class DefinitionChoicesAndOrderingTests
{
    private static readonly string[] SingleSourceTokens = ["zeta", "alpha", "MID_POINT"];
    private static readonly string[] SingleSourceLabels = ["Last letter", "Alpha", "Mid Point"];
    private static readonly string[] PartialLabels = ["Local", "S3 Compatible", "Custom endpoint"];
    private static readonly bool[] PartialExplicitLabels = [false, false, true];
    private static readonly string[] WildcardOrder = ["host", "port", "database", "password", "username"];
    private static readonly string[] RankedOrder = ["omega", "beta", "alpha", "gamma"];
    private static readonly string[] AuthoredRootOrder = ["zeta", "server", "alpha"];
    private static readonly string[] AuthoredNestedOrder = ["port", "host"];

    private const string Header = """
        "$schema":"urn:islamu:configurator:authoring:v0.0.1",
        "identity":{"familyId":"5f0f8c1e-6c39-4c7a-9a43-0c7b6f2a9e11","revision":1},
        "project":{"id":"choices.sample","name":"Choices Sample"},"applicationVersions":["1.0"],
        "outputs":{"settings.json":{"format":"json"}},
        """;

    [Test]
    public async Task SingleSourceChoicesSynthesizeCanonicalEnumInDeclaredOrder()
    {
        Definition definition = Compile(Field("""
            {"type":"string","x-configurator":{"sensitivity":"public","choices":[
              {"value":"zeta","label":"Last letter"},{"value":"alpha"},{"value":"MID_POINT","explanation":"Halfway."}]}}
            """));

        FieldDefinition field = definition.Fields.Single();
        await Assert.That(field.Choices.Select(choice => choice.GetString()!).ToArray())
            .IsEquivalentTo(SingleSourceTokens, CollectionOrdering.Matching);
        await Assert.That(field.OptionMetadata.Select(choice => choice.DisplayLabel).ToArray())
            .IsEquivalentTo(SingleSourceLabels, CollectionOrdering.Matching);
        await Assert.That(field.OptionMetadata[2].Explanation).IsEqualTo("Halfway.");
        await Assert.That(DefinitionValidator.ValueValid(field.Node, JsonSerializer.SerializeToElement("alpha"))).IsTrue();
        await Assert.That(DefinitionValidator.ValueValid(field.Node, JsonSerializer.SerializeToElement("beta"))).IsFalse();
        using JsonDocument runtime = JsonDocument.Parse(definition.CanonicalBytes);
        await Assert.That(runtime.RootElement.GetProperty("configuration").GetProperty("properties")
            .GetProperty("mode").GetProperty("enum").GetArrayLength()).IsEqualTo(3);
    }

    [Test]
    public async Task PartialChoicesOverrideOnlyTheirTokensAndHumanizeTheRest()
    {
        Definition definition = Compile(Field("""
            {"type":"string","enum":["LOCAL","S3_COMPATIBLE","CUSTOM"],"x-configurator":{"sensitivity":"public",
              "choices":[{"value":"CUSTOM","label":"Custom endpoint","explanation":"Bring your own storage.",
                "deprecated":true,"deprecationMessage":"Use S3 Compatible instead."}]}}
            """));

        IReadOnlyList<DefinitionChoice> choices = definition.Fields.Single().OptionMetadata;
        await Assert.That(choices.Select(choice => choice.DisplayLabel).ToArray())
            .IsEquivalentTo(PartialLabels, CollectionOrdering.Matching);
        await Assert.That(choices.Select(choice => choice.HasExplicitLabel).ToArray())
            .IsEquivalentTo(PartialExplicitLabels, CollectionOrdering.Matching);
        await Assert.That(choices[2].Deprecated).IsTrue();
        await Assert.That(choices[2].DeprecationGuidance).IsEqualTo("Use S3 Compatible instead.");
        await Assert.That(choices[2].MetadataIndex).IsEqualTo(0);
        await Assert.That(choices[0].MetadataIndex).IsNull();
        await Assert.That(DefinitionValidator.ValueValid(definition.Fields.Single().Node,
            JsonSerializer.SerializeToElement("CUSTOM"))).IsTrue();
    }

    [Test]
    [Arguments("""{"type":"string","enum":["A"],"x-configurator":{"sensitivity":"public","choices":[{"value":"B"}]}}""", "choices-invalid")]
    [Arguments("""{"type":"string","enum":["A"],"x-configurator":{"sensitivity":"public","choices":[{"value":"A"},{"value":"A"}]}}""", "choices-invalid")]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"public","choices":[{"value":"A"},{"value":"A"}]}}""", "choices-invalid")]
    [Arguments("""{"type":"string","enum":["A"],"x-configurator":{"sensitivity":"public","choices":[{"value":"A","deprecationMessage":"Gone."}]}}""", "deprecation-invalid")]
    [Arguments("""{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public","choices":[{"value":"A"}]},"properties":{}}""", "choices-invalid")]
    public async Task InvalidChoiceMetadataRejectsWithStableCodes(string field, string code)
    {
        await Assert.That(Failure(() => Compile(Field(field)))).IsEqualTo(code);
    }

    [Test]
    public async Task PropertyOrderWithWildcardOrdersNestedPropertiesWithoutChangingDigest()
    {
        const string connection = """
            {"type":"object","additionalProperties":false,
             "x-configurator":{"sensitivity":"public","propertyOrder":["host","port","database","*"]},
             "properties":{
               "username":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "password":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "database":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "port":{"type":"integer","x-configurator":{"sensitivity":"public"}},
               "host":{"type":"string","x-configurator":{"sensitivity":"public"}}}}
            """;
        Definition first = Compile(Field(connection));
        Definition second = Compile(Field(connection));
        Definition readmitted = DefinitionCodec.ParseJson(first.CanonicalBytes);
        string[] expected = WildcardOrder;

        await Assert.That(first.Digest).IsEqualTo(second.Digest);
        await Assert.That(readmitted.Digest).IsEqualTo(first.Digest);
        await Assert.That(first.Fields.Single().Node.OrderedPropertyNames.ToArray()).IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(readmitted.Fields.Single().Properties.Select(property => property.Name).ToArray())
            .IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(first.Fields.Single().Node.Properties.Keys.First()).IsEqualTo("database");
    }

    [Test]
    public async Task PerPropertyOrderPrecedesUnorderedSiblings()
    {
        Definition definition = Compile(Field("""
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
             "properties":{
               "alpha":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "omega":{"type":"string","x-configurator":{"sensitivity":"public","order":1}},
               "beta":{"type":"string","x-configurator":{"sensitivity":"public","order":2}},
               "gamma":{"type":"string","x-configurator":{"sensitivity":"public"}}}}
            """));

        await Assert.That(definition.Fields.Single().Properties.Select(property => property.Name).ToArray())
            .IsEquivalentTo(RankedOrder, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("""["host","unknown"]""")]
    [Arguments("""["host","host"]""")]
    [Arguments("""["*","host","*"]""")]
    [Arguments("""[]""")]
    public async Task InvalidPropertyOrderRejects(string order)
    {
        string field = """
            {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public","propertyOrder":
            """ + order + """
            },"properties":{"host":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "port":{"type":"integer","x-configurator":{"sensitivity":"public"}}}}
            """;
        await Assert.That(Failure(() => Compile(Field(field)))).IsEqualTo("property-order-invalid");
    }

    [Test]
    public async Task PropertyOrderOutsideObjectsRejects()
    {
        await Assert.That(Failure(() => Compile(Field(
            """{"type":"string","x-configurator":{"sensitivity":"public","propertyOrder":["a"]}}"""))))
            .IsEqualTo("property-order-invalid");
    }

    [Test]
    public async Task AuthoredYamlMappingOrderIsPreservedWhenNoExplicitOrderIsDeclared()
    {
        const string yaml = """
            $schema: urn:islamu:configurator:authoring:v0.0.1
            identity: {familyId: 5f0f8c1e-6c39-4c7a-9a43-0c7b6f2a9e11, revision: 1}
            project: {id: choices.sample, name: Choices Sample}
            applicationVersions: ["1.0"]
            outputs: {settings.json: {format: json}}
            configuration:
              type: object
              additionalProperties: false
              x-configurator: {sensitivity: public}
              properties:
                zeta: {type: string, x-configurator: {sensitivity: public}}
                server:
                  type: object
                  additionalProperties: false
                  x-configurator: {sensitivity: public}
                  properties:
                    port: {type: integer, x-configurator: {sensitivity: public}}
                    host: {type: string, x-configurator: {sensitivity: public}}
                alpha: {type: string, x-configurator: {sensitivity: public}}
            """;
        Definition definition = DefinitionAuthoringCompiler.Compile(
            DefinitionAuthoringCodec.ParseYaml(Encoding.UTF8.GetBytes(yaml)));

        await Assert.That(definition.Fields.Select(field => field.Name).ToArray())
            .IsEquivalentTo(AuthoredRootOrder, CollectionOrdering.Matching);
        await Assert.That(definition.Fields[1].Properties.Select(field => field.Name).ToArray())
            .IsEquivalentTo(AuthoredNestedOrder, CollectionOrdering.Matching);
    }

    private static string Field(string schema) => "{" + Header + """
        "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
          "properties":{"mode":
        """ + schema + "}}}";

    private static Definition Compile(string source) =>
        DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source)));

    private static string? Failure(Func<Definition> action)
    {
        try { _ = action(); }
        catch (DefinitionException exception) { return exception.Code; }
        return null;
    }

}
