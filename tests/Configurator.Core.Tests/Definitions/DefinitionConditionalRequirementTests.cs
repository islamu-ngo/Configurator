using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

namespace Configurator.Core.Tests.Definitions;

/// <summary>
/// Specifies optional areas modelled as objects: an object opened by its initial value, and members that become
/// required while a condition elsewhere in the answers holds (<c>requiredWhen</c>).
/// </summary>
public sealed class DefinitionConditionalRequirementTests
{
    private static readonly string[] SetupSecretPath = ["setupSecret"];
    private static readonly string[] LegalNamePath = ["legalName"];

    // "platform" is included by default although it holds a protected member; "setupSecret" is required only when
    // another area sets a replica count other than one; root "token" is required only for the remote mode.
    private const string Configuration = """
        {"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
         "properties":{
           "mode":{"type":"string","enum":["local","remote"],"x-configurator":{"sensitivity":"public"}},
           "token":{"type":"string","x-configurator":{"sensitivity":"sensitive",
             "requiredWhen":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"remote"}}},
           "identity":{"type":"object","additionalProperties":false,"required":["mode","legalName"],
             "x-configurator":{"sensitivity":"public"},
             "properties":{
               "mode":{"type":"string","enum":["interactive","configured"],"x-configurator":{"sensitivity":"public"}},
               "legalName":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
           "administrator":{"type":"object","additionalProperties":false,"required":["subject"],
             "x-configurator":{"sensitivity":"public","initialValue":{},
               "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/identity/mode"},"value":"configured"}},
             "properties":{"subject":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
           "hosting":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
             "properties":{"replicaCount":{"type":"integer","minimum":1,
               "x-configurator":{"sensitivity":"public","initialValue":1}}}},
           "platform":{"type":"object","additionalProperties":false,
             "x-configurator":{"sensitivity":"public","initialValue":{}},
             "properties":{
               "region":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"eu"}},
               "setupSecret":{"type":"string","x-configurator":{"sensitivity":"sensitive",
                 "requiredWhen":{"op":"all","operands":[
                   {"op":"exists","reference":{"scope":"root","path":"/hosting/replicaCount"}},
                   {"op":"notEquals","reference":{"scope":"root","path":"/hosting/replicaCount"},"value":1}]}}}}}}}
        """;

    [Test]
    public async Task NestedMemberIsRequiredOnlyWhileItsConditionHolds()
    {
        Definition definition = Compile(Configuration);
        string platform = FieldId(definition, "platform");

        await Assert.That(Validate(definition, """{}""").IsReady).IsTrue();
        await Assert.That(Validate(definition, """{"hosting":{"replicaCount":1}}""").IsReady).IsTrue();
        DefinitionValidationResult scaled = Validate(definition, """{"hosting":{"replicaCount":3}}""");
        await Assert.That(scaled.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required"
            && diagnostic.FieldId == platform && diagnostic.RelativePath.SequenceEqual(SetupSecretPath))).IsTrue();
        await Assert.That(Validate(definition,
            """{"hosting":{"replicaCount":3},"platform":{"setupSecret":"synthetic-setup-value"}}""").IsReady).IsTrue();
    }

    [Test]
    public async Task AnsweredMemberStaysDecidedBesideAMissingSibling()
    {
        Definition definition = Compile(Configuration);
        string identity = FieldId(definition, "identity"), administrator = FieldId(definition, "administrator");

        // identity/legalName is missing; that must not make identity/mode undecided for the administrator group.
        DefinitionValidationResult result = Validate(definition, """{"identity":{"mode":"configured"}}""");

        await Assert.That(result.Fields.Single(field => field.Id == administrator).IsActive).IsTrue();
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required"
            && diagnostic.FieldId == identity && diagnostic.RelativePath.SequenceEqual(LegalNamePath))).IsTrue();
        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required"
            && diagnostic.FieldId == administrator)).IsTrue();
    }

    [Test]
    public async Task OmittedAreaReleasesItsConditionalRequirement()
    {
        Definition definition = Compile(Configuration);

        DefinitionValidationResult excluded = DefinitionValidator.Validate(definition, AnswerSnapshotCodec.ParseJson(
            Encoding.UTF8.GetBytes("""
                {"answerContractVersion":"0.0.1","values":{"hosting":{"replicaCount":3}},"omitted":["/platform"]}
                """)));

        await Assert.That(excluded.IsReady).IsTrue();
        await Assert.That(excluded.TryGetEffectiveValue(DefinitionPath.Parse("/platform"), out _)).IsFalse();
    }

    [Test]
    public async Task RootMemberIsRequiredOnlyWhileItsConditionHolds()
    {
        Definition definition = Compile(Configuration);
        string token = FieldId(definition, "token");

        await Assert.That(Validate(definition, """{"mode":"local"}""").IsReady).IsTrue();
        DefinitionValidationResult remote = Validate(definition, """{"mode":"remote"}""");
        await Assert.That(remote.Diagnostics.Any(diagnostic => diagnostic.Code == "value-required"
            && diagnostic.FieldId == token)).IsTrue();
        await Assert.That(Validate(definition, """{"mode":"remote","token":"synthetic-token"}""").IsReady).IsTrue();
    }

    [Test]
    public async Task InitialObjectOpensTheAreaAndAppliesMemberInitialValues()
    {
        DefinitionValidationResult result = Validate(Compile(Configuration), """{}""");

        await Assert.That(result.TryGetEffectiveValue(DefinitionPath.Parse("/platform/region"), out JsonElement region)).IsTrue();
        await Assert.That(region.GetString()).IsEqualTo("eu");
        await Assert.That(result.TryGetEffectiveValue(DefinitionPath.Parse("/platform/setupSecret"), out _)).IsFalse();
    }

    [Test]
    [Arguments("""{"setupSecret":"synthetic-setup-value"}""")]
    [Arguments("""{"unknown":true}""")]
    public async Task InitialObjectNeverSuppliesProtectedOrUndeclaredMembers(string initial)
    {
        JsonObject configuration = JsonNode.Parse(Configuration)!.AsObject();
        configuration["properties"]!["platform"]!["x-configurator"]!["initialValue"] = JsonNode.Parse(initial);

        await Assert.That(Failure(() => Compile(configuration.ToJsonString()))).IsEqualTo("public-value-invalid");
    }

    [Test]
    public async Task UnconditionalAndConditionalRequirementCannotBothApply()
    {
        JsonObject configuration = JsonNode.Parse(Configuration)!.AsObject();
        configuration["properties"]!["platform"]!["required"] = new JsonArray("setupSecret");

        await Assert.That(Failure(() => Compile(configuration.ToJsonString()))).IsEqualTo("relationship-invalid");
    }

    [Test]
    public async Task RequirementReadingItsOwnAncestorIsACycle()
    {
        JsonObject configuration = JsonNode.Parse(Configuration)!.AsObject();
        configuration["properties"]!["platform"]!["properties"]!["setupSecret"]!["x-configurator"]!["requiredWhen"] =
            JsonNode.Parse("""{"op":"exists","reference":{"scope":"root","path":"/platform/region"}}""");
        configuration["properties"]!["platform"]!["properties"]!["region"]!["x-configurator"]!["requiredWhen"] =
            JsonNode.Parse("""{"op":"exists","reference":{"scope":"root","path":"/platform"}}""");

        await Assert.That(Failure(() => Compile(configuration.ToJsonString()))).IsEqualTo("activation-cycle");
    }

    [Test]
    public async Task ListItemsHaveNoConditionalRequirement()
    {
        JsonObject configuration = JsonNode.Parse(Configuration)!.AsObject();
        configuration["properties"]!["regions"] = JsonNode.Parse("""
            {"type":"array","x-configurator":{"sensitivity":"public"},
             "items":{"type":"string","x-configurator":{"sensitivity":"public",
               "requiredWhen":{"op":"exists","reference":{"scope":"root","path":"/mode"}}}}}
            """);

        await Assert.That(Failure(() => Compile(configuration.ToJsonString()))).IsEqualTo("condition-context");
    }

    private static Definition Compile(string configuration)
    {
        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = JsonNode.Parse("""{"familyId":"6f1c2a7e-0d4b-4c39-8e51-3a9b7d2c4e10","revision":1}"""),
            ["project"] = JsonNode.Parse("""{"id":"conditional.sample","name":"Conditional Sample"}"""),
            ["applicationVersions"] = new JsonArray("1.0"),
            ["configuration"] = JsonNode.Parse(configuration),
            ["outputs"] = JsonNode.Parse("""{"settings.json":{"format":"json"}}""")
        };
        return DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static DefinitionValidationResult Validate(Definition definition, string values) =>
        DefinitionValidator.Validate(definition, AnswerSnapshotCodec.ParseJson(Encoding.UTF8.GetBytes(
            """{"answerContractVersion":"0.0.1","omitted":[],"values":""" + values + "}")));

    private static string FieldId(Definition definition, string name) =>
        definition.Fields.Single(field => field.Name == name).Id;

    private static string? Failure(Func<Definition> action)
    {
        try { _ = action(); }
        catch (DefinitionException exception) { return exception.Code; }
        return null;
    }
}
