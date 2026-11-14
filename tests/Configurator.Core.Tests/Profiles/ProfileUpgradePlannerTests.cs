namespace Configurator.Core.Tests.Profiles;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Core.Profiles;

public sealed class ProfileUpgradePlannerTests
{
    private static readonly Guid ProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Test]
    public async Task FixturesAreAdmittedAndTargetKernelAcceptsIncidentalHiddenValue()
    {
        Definition source = Definition("""{"token":{"type":"string","minLength":2,"x-configurator":{"sensitivity":"secret"}}}""");
        Definition target = Definition("""{"token":{"type":"string","minLength":3,"x-configurator":{"sensitivity":"secret"}}}""", 2);
        await Assert.That(source.Revision).IsEqualTo(1L);
        await Assert.That(DefinitionValidator.Validate(target, Answers("""{"token":"synthetic-hidden-sentinel"}""")).IsReady).IsTrue();
    }

    [Test]
    public async Task HiddenPolicyChangesRequireReplacementDespiteIncidentalValidity()
    {
        Definition source = Definition("""{"token":{"type":"string","minLength":2,"x-configurator":{"sensitivity":"secret"}}}""");
        Definition target = Definition("""{"token":{"type":"string","minLength":3,"x-configurator":{"sensitivity":"secret"}}}""", 2);
        using UnlockedProfile profile = Profile(source, """{"token":"synthetic-hidden-sentinel"}""");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(profile, target);
        await Assert.That(plan.Fields.Single().Eligibility).IsEqualTo(ProfileCarryEligibility.ReplacementRequired);
        await Assert.That(plan.RequiresRevalidation).IsTrue();
        await Assert.That(JsonSerializer.Serialize(plan).Contains("synthetic-hidden-sentinel", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments("""{"type":"integer","minimum":5}""", "3", ProfileCarryEligibility.Invalid)]
    [Arguments("""{"type":"string","enum":["b"]}""", "\"a\"", ProfileCarryEligibility.Invalid)]
    [Arguments("""{"type":"boolean"}""", "\"a\"", ProfileCarryEligibility.Invalid)]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"secret"}}""", "\"a\"", ProfileCarryEligibility.ReplacementRequired)]
    public async Task TargetRulesPreventUnsafeCarry(string targetNode, string value, ProfileCarryEligibility expected)
    {
        string sourceNode = value == "3" ? """{"type":"integer"}""" : """{"type":"string"}""";
        Definition source = Definition("{\"value\":" + sourceNode + "}");
        Definition target = Definition("{\"value\":" + targetNode + "}", 2);
        using UnlockedProfile profile = Profile(source, "{\"value\":" + value + "}");
        await Assert.That(ProfileUpgradePlanner.Plan(profile, target).Fields.Single().Eligibility).IsEqualTo(expected);
    }

    [Test]
    public async Task UnchangedScalarCanBeEligibleButPlanNeverGrantsReadiness()
    {
        Definition source = Definition("""{"value":{"type":"string"}}""");
        Definition target = Definition("""{"value":{"type":"string"}}""", 2);
        using UnlockedProfile profile = Profile(source, """{"value":"synthetic"}""");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(profile, target);
        await Assert.That(plan.Fields.Single().Eligibility).IsEqualTo(ProfileCarryEligibility.Eligible);
        await Assert.That(plan.RequiresRevalidation).IsTrue();
    }

    [Test]
    public async Task CollectionOrdinalsNeverAuthorizeCarryOrExposeDynamicKeys()
    {
        const string properties = """{"rows":{"type":"array","items":{"type":"string"}},"map":{"type":"object","additionalProperties":{"type":"string"}}}""";
        Definition source = Definition(properties);
        Definition target = Definition(properties, 2);
        using UnlockedProfile profile = Profile(source, """{"rows":["synthetic"],"map":{"synthetic-private-key":"synthetic"}}""");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(profile, target);
        await Assert.That(plan.Fields.All(field => field.Eligibility == ProfileCarryEligibility.RevalidationRequired)).IsTrue();
        await Assert.That(JsonSerializer.Serialize(plan).Contains("synthetic-private-key", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task RenameIsRemovedAndAddedWithNoMapping()
    {
        Definition source = Definition("""{"old":{"type":"string"}}""");
        Definition target = Definition("""{"new":{"type":"string"}}""", 2, ["new"]);
        var deltas = DefinitionSchemaDiff.Compare(source, target);
        await Assert.That(deltas.Select(delta => delta.Kind).ToArray())
            .IsEquivalentTo(new[] { DefinitionSchemaChange.AddedRequired, DefinitionSchemaChange.Removed, DefinitionSchemaChange.Required });
    }

    [Test]
    [Arguments("""{"type":"boolean"}""", DefinitionSchemaChange.Type)]
    [Arguments("""{"type":"string","minLength":3}""", DefinitionSchemaChange.Range)]
    [Arguments("""{"type":"string","enum":["choice"]}""", DefinitionSchemaChange.Choice)]
    [Arguments("""{"type":"string","x-configurator":{"sensitivity":"secret"}}""", DefinitionSchemaChange.Sensitivity)]
    [Arguments("""{"type":"string","x-configurator":{"secret":{"kind":"password","acquisition":"existing"}}}""", DefinitionSchemaChange.SecretPolicy)]
    public async Task StructuralChangesUseDeterministicValueFreeSchemaCoordinates(string targetNode, DefinitionSchemaChange kind)
    {
        Definition source = Definition("""{"value":{"type":"string"}}""");
        Definition target = Definition("{\"value\":" + targetNode + "}", 2);
        IReadOnlyList<DefinitionSchemaDelta> changes = DefinitionSchemaDiff.Compare(source, target);
        await Assert.That(changes.Any(delta => delta.SchemaPath == "/configuration/properties/value" && delta.Kind == kind)).IsTrue();
        await Assert.That(changes.SequenceEqual(DefinitionSchemaDiff.Compare(source, target))).IsTrue();
        await Assert.That(JsonSerializer.Serialize(changes).Contains("choice", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task AddedOptionalFieldIsDistinctFromAddedRequiredField()
    {
        Definition source = Definition("""{"old":{"type":"string"}}""");
        Definition target = Definition("""{"old":{"type":"string"},"optional":{"type":"string"}}""", 2);
        await Assert.That(DefinitionSchemaDiff.Compare(source, target).ToArray())
            .IsEquivalentTo(new[] { new DefinitionSchemaDelta("/configuration/properties/optional", DefinitionSchemaChange.AddedOptional) });
    }

    [Test]
    public async Task TargetActivationPreventsCarryOfAnInactiveField()
    {
        Definition source = Definition("""{"enabled":{"type":"boolean","x-configurator":{"sensitivity":"public"}},"value":{"type":"string"}}""");
        Definition target = Definition("""
            {"enabled":{"type":"boolean","x-configurator":{"sensitivity":"public"}},
             "value":{"type":"string","x-configurator":{"applicableWhen":{
               "op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}}}
            """, 2);
        using UnlockedProfile profile = Profile(source, """{"enabled":false,"value":"synthetic"}""");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(profile, target);
        await Assert.That(plan.Fields.Single(field => field.FieldPath == "/value").Eligibility).IsEqualTo(ProfileCarryEligibility.Inactive);
        await Assert.That(plan.SchemaDeltas.Any(delta => delta.Kind == DefinitionSchemaChange.Activation)).IsTrue();
    }

    [Test]
    public async Task MissingRequiredSecretNeverMakesAPlanReady()
    {
        Definition source = Definition("""{"value":{"type":"string"}}""");
        Definition target = Definition("""{"value":{"type":"string"},"token":{"type":"string","x-configurator":{"sensitivity":"secret"}}}""", 2, ["token"]);
        using UnlockedProfile profile = Profile(source, """{"value":"synthetic"}""");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(profile, target);
        await Assert.That(plan.Fields.Single(field => field.FieldPath == "/token").Eligibility).IsEqualTo(ProfileCarryEligibility.Missing);
        await Assert.That(plan.Fields.Single(field => field.FieldPath == "/value").Eligibility).IsEqualTo(ProfileCarryEligibility.RevalidationRequired);
        await Assert.That(plan.RequiresRevalidation).IsTrue();
    }

    [Test]
    public async Task PublicValuesCanStillCompareWhenHistoricalSchemaIsUnavailable()
    {
        Definition source = Definition("""{"value":{"type":"string","x-configurator":{"sensitivity":"public"}}}""");
        Definition target = Definition("""{"value":{"type":"string","x-configurator":{"sensitivity":"public"}}}""", 2);
        CompareBaselineSnapshot before = CompareBaselineCodec.Create(source, Answers("""{"value":"before"}"""), ProfileId, Guid.NewGuid(), "synthetic");
        CompareBaselineSnapshot after = CompareBaselineCodec.Create(target, Answers("""{"value":"after"}"""), ProfileId, Guid.NewGuid(), "synthetic");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Compare(before, after, null, target, "synthetic");
        await Assert.That(plan.SourceSchemaAvailable).IsFalse();
        await Assert.That(plan.SchemaDeltas.Count).IsEqualTo(0);
        await Assert.That(plan.Fields.Single().Comparison).IsEqualTo(ProfileComparisonResult.Changed);
    }

    [Test]
    [Arguments("map", false)]
    [Arguments("map", true)]
    [Arguments("sequence", false)]
    [Arguments("sequence", true)]
    public async Task CompletePublicValuesAndOmittedDynamicEntriesHaveDifferentComparisonEvidence(string shape, bool suppliedSource)
    {
        string oldNode = shape == "map"
            ? """{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{"type":"string","x-configurator":{"sensitivity":"public"}}}"""
            : """{"type":"array","x-configurator":{"sensitivity":"public"},"items":{"type":"string","x-configurator":{"sensitivity":"public"}}}""";
        string newNode = shape == "map"
            ? """{"type":"object","additionalProperties":false,"properties":{},"x-configurator":{"sensitivity":"public"}}"""
            : oldNode;
        Definition source = Definition("{\"value\":" + oldNode + "}");
        Definition target = Definition("{\"value\":" + newNode + "}", 2);
        CompareBaselineSnapshot before = CompareBaselineCodec.Create(source,
            Answers(shape == "map" ? """{"value":{"synthetic-private-key":"before"}}""" : """{"value":["before"]}"""),
            ProfileId, Guid.NewGuid(), "synthetic");
        CompareBaselineSnapshot after = CompareBaselineCodec.Create(target,
            Answers(shape == "map" ? """{"value":{}}""" : """{"value":["after"]}"""),
            ProfileId, Guid.NewGuid(), "synthetic");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Compare(before, after, suppliedSource ? source : null, target, "synthetic");
        await Assert.That(plan.Fields.Single().Comparison).IsEqualTo(
            shape == "sequence" && suppliedSource ? ProfileComparisonResult.Changed : ProfileComparisonResult.Unavailable);
        await Assert.That(JsonSerializer.Serialize(plan).Contains("synthetic-private-key", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task BaselineWithoutMatchingSourceReportsUnavailableSchema()
    {
        Definition source = Definition("""{"value":{"type":"string"}}""");
        Definition target = Definition("""{"value":{"type":"string"}}""", 2);
        CompareBaselineSnapshot baseline = CompareBaselineCodec.Create(source, Answers("""{"value":"synthetic"}"""),
            ProfileId, Guid.NewGuid(), "synthetic");
        ProfileUpgradePlan plan = ProfileUpgradePlanner.Plan(baseline, target);
        await Assert.That(plan.SourceSchemaAvailable).IsFalse();
        await Assert.That(plan.SchemaDeltas.Count).IsEqualTo(0);
        await Assert.That(() => ProfileUpgradePlanner.Plan(baseline, target, target)).Throws<DefinitionException>();
    }

    [Test]
    public async Task SameRevisionConflictingContentAndDifferentFamilyReject()
    {
        Definition source = Definition("""{"value":{"type":"string"}}""");
        Definition conflict = Definition("""{"other":{"type":"string"}}""");
        await Assert.That(() => DefinitionSchemaDiff.Compare(source, conflict)).Throws<DefinitionException>();
        JsonNode node = JsonNode.Parse(Encoding.UTF8.GetString(conflict.CanonicalBytes.Span))!;
        node["familyId"] = Guid.NewGuid();
        Definition unrelated = DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(node.ToJsonString()));
        await Assert.That(() => DefinitionSchemaDiff.Compare(source, unrelated)).Throws<DefinitionException>();
    }

    [Test]
    public async Task ProtectedEqualityIsUnavailableWithoutHeldMatchingKeyAndContext()
    {
        Definition definition = Definition("""{"token":{"type":"string","x-configurator":{"sensitivity":"secret"}}}""");
        using var key = new ProfileSecretCommitment(Guid.NewGuid(), RandomNumberGenerator.GetBytes(32));
        CompareBaselineSnapshot before = CompareBaselineCodec.Create(definition, Answers("""{"token":"synthetic"}"""),
            ProfileId, Guid.NewGuid(), "synthetic", key);
        CompareBaselineSnapshot after = CompareBaselineCodec.Create(definition, Answers("""{"token":"synthetic"}"""),
            ProfileId, Guid.NewGuid(), "synthetic", key);
        await Assert.That(ProfileUpgradePlanner.Compare(before, after, definition, definition, "synthetic").Fields.Single().Comparison)
            .IsEqualTo(ProfileComparisonResult.Unavailable);
        await Assert.That(ProfileUpgradePlanner.Compare(before, after, definition, definition, "synthetic", key).Fields.Single().Comparison)
            .IsEqualTo(ProfileComparisonResult.Unchanged);
        await Assert.That(ProfileUpgradePlanner.Compare(before, after, definition, definition, "other", key).Fields.Single().Comparison)
            .IsEqualTo(ProfileComparisonResult.Unavailable);
    }

    [Test]
    public async Task DisposedBlindOwnerCannotProduceAPlan()
    {
        Definition definition = Definition("""{"value":{"type":"string"}}""");
        UnlockedProfile profile = Profile(definition, """{"value":"synthetic"}""");
        profile.Dispose();
        await Assert.That(() => ProfileUpgradePlanner.Plan(profile, definition)).Throws<ObjectDisposedException>();
    }

    private static AnswerSnapshot Answers(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new AnswerSnapshot(0, document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal));
    }

    private static UnlockedProfile Profile(Definition definition, string json)
    {
        byte[] bytes = ProfilePayloadCodec.Serialize(definition, Answers(json), ProfileProtectionMode.ImportUpgradeOnly,
            ProfileId, Guid.NewGuid(), "synthetic-source", "synthetic");
        try { return ProfilePayloadCodec.Admit(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static Definition Definition(string properties, int revision = 1, string[]? required = null)
    {
        JsonNode source = JsonNode.Parse("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"d678ac1c-a240-4429-b932-7ef4638471ea","revision":1},
             "project":{"id":"synthetic.upgrade","name":"Synthetic upgrade"},"applicationVersions":["1.0"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{}},
             "outputs":{"settings.json":{"format":"json"}}}
            """)!;
        source["identity"]!["revision"] = revision;
        source["configuration"]!["properties"] = JsonNode.Parse(properties);
        if (required is not null) source["configuration"]!["required"] = JsonSerializer.SerializeToNode(required);
        return DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
    }
}
