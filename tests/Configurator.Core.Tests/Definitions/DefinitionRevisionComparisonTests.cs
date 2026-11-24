namespace Configurator.Core.Tests.Definitions;

using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionRevisionComparisonTests
{
    [Test]
    public async Task IdenticalDefinitionsReportZeroChangesAndCanonicalIdentity()
    {
        Definition baseline = DefinitionAuthoringTests.Compile();
        Definition candidate = DefinitionAuthoringTests.Compile();

        DefinitionRevisionComparisonReport report = DefinitionRevisionComparison.Compare(baseline, candidate);

        await Assert.That(report.SameFamily).IsTrue();
        await Assert.That(report.CanonicalIdentical).IsTrue();
        await Assert.That(report.HasBehavioralChanges).IsFalse();
        await Assert.That(report.HasDisplayOnlyChanges).IsFalse();
        await Assert.That(report.Changes.Count).IsEqualTo(0);
        await Assert.That(report.BaselineDigest).IsEqualTo(baseline.Digest.Value);
        await Assert.That(report.CandidateDigest).IsEqualTo(candidate.Digest.Value);
    }

    [Test]
    public async Task BehavioralChangesAndDisplayOnlyEditsSeparateCleanlyWithoutGuessingRenames()
    {
        JsonObject baseJson = JsonNode.Parse(DefinitionAuthoringTests.Source)!.AsObject();
        JsonObject props = baseJson["configuration"]!["properties"]!.AsObject();
        props["SERVICE_PORT"]!["title"] = "Service Port";
        props["SERVICE_PORT"]!["minimum"] = 1024;
        props["SERVICE_HOST"] = JsonNode.Parse("""
            {"type":"string","title":"Service Host","description":"Bind hostname.",
             "x-configurator":{"sensitivity":"public","initialValue":"localhost"}}
            """);
        props["OPTIONAL_FLAG"] = JsonNode.Parse("""
            {"type":"boolean","title":"Optional Flag",
             "x-configurator":{"sensitivity":"public","initialValue":false}}
            """);
        props["LEGACY_SETTING"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public"}}
            """);

        JsonObject nextJson = baseJson.DeepClone().AsObject();
        nextJson["identity"]!["revision"] = 2;
        JsonObject nextProps = nextJson["configuration"]!["properties"]!.AsObject();
        // Display-only title edit on SERVICE_HOST
        nextProps["SERVICE_HOST"]!["title"] = "Public Bind Hostname";
        // Behavioral constraint + initialValue edit on SERVICE_PORT
        nextProps["SERVICE_PORT"]!["minimum"] = 2048;
        nextProps["SERVICE_PORT"]!["x-configurator"]!["initialValue"] = 9090;
        // Behavioral sensitivity escalation + secret policy change on SERVICE_TOKEN
        nextProps["SERVICE_TOKEN"]!["x-configurator"]!["secret"] = JsonNode.Parse("""
            {"kind":"opaque-token","acquisition":"local","encoding":"base64url","generation":{"bytes":32}}
            """);
        // Behavioral sensitivity escalation on SERVICE_HOST (public -> secret, remove initialValue)
        nextProps["SERVICE_HOST"]!["x-configurator"]!["sensitivity"] = "secret";
        nextProps["SERVICE_HOST"]!["x-configurator"]!.AsObject().Remove("initialValue");
        // Behavioral activation + requiredness change on OPTIONAL_FLAG
        nextProps["OPTIONAL_FLAG"]!["x-configurator"]!["applicableWhen"] = JsonNode.Parse("""
            {"op":"exists","reference":{"scope":"root","path":"/SERVICE_PORT"}}
            """);
        nextJson["configuration"]!["required"]!.AsArray().Add("OPTIONAL_FLAG");
        // Field removal + addition (must NOT guess rename!)
        nextProps.Remove("LEGACY_SETTING");
        nextProps["NEW_SETTING"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public"}}
            """);
        // Output mapping modification + output addition
        nextJson["outputs"]![".env"]!["bindings"]!["SERVICE_HOST"] = JsonNode.Parse("""
            {"scope":"root","path":"/SERVICE_HOST"}
            """);
        nextJson["outputs"]!["extra.json"] = JsonNode.Parse("""
            {"id":"extra","format":"json"}
            """);

        Definition baseline = DefinitionAuthoringTests.Compile(baseJson.ToJsonString());
        Definition candidate = DefinitionAuthoringTests.Compile(nextJson.ToJsonString());

        DefinitionRevisionComparisonReport report = DefinitionRevisionComparison.Compare(baseline, candidate);

        await Assert.That(report.SameFamily).IsTrue();
        await Assert.That(report.CanonicalIdentical).IsFalse();
        await Assert.That(report.HasBehavioralChanges).IsTrue();
        await Assert.That(report.HasDisplayOnlyChanges).IsTrue();

        // Display-only edit classified strictly as DisplayOnly
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.DisplayOnly,
            DefinitionRevisionChangeCategory.DisplayTextChanged,
            "/configuration/properties/SERVICE_HOST",
            "modified"));

        // Behavioral classifications
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.ConstraintChanged,
            "/configuration/properties/SERVICE_PORT",
            "modified"));
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.InitialValueChanged,
            "/configuration/properties/SERVICE_PORT",
            "modified"));
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.SensitivityChanged,
            "/configuration/properties/SERVICE_HOST",
            "public->secret"));
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.SecretPolicyChanged,
            "/configuration/properties/SERVICE_TOKEN",
            "added"));
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.ActivationChanged,
            "/configuration/properties/OPTIONAL_FLAG",
            "added"));
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.RequirednessChanged,
            "/configuration/properties/OPTIONAL_FLAG",
            "added"));

        // Zero guessed renames: strict removal + addition
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.FieldRemoved,
            "/configuration/properties/LEGACY_SETTING",
            "removed"));
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.FieldAdded,
            "/configuration/properties/NEW_SETTING",
            "added"));

        // Output changes keyed by artifact ID
        await Assert.That(report.Changes.Any(change =>
            change.Impact == DefinitionRevisionImpactKind.Behavioral
            && change.Category == DefinitionRevisionChangeCategory.OutputMappingChanged)).IsTrue();
        await Assert.That(report.Changes).Contains(new DefinitionRevisionChange(
            DefinitionRevisionImpactKind.Behavioral,
            DefinitionRevisionChangeCategory.OutputAdded,
            "/artifacts/extra",
            "added"));
    }

    [Test]
    public async Task PureDisplayTextEditReportsOnlyDisplayOnlyChanges()
    {
        JsonObject baseJson = JsonNode.Parse(DefinitionAuthoringTests.Source)!.AsObject();
        JsonObject nextJson = baseJson.DeepClone().AsObject();
        nextJson["identity"]!["revision"] = 2;
        nextJson["configuration"]!["properties"]!["SERVICE_PORT"]!["title"] = "Updated Port Label";
        nextJson["configuration"]!["properties"]!["SERVICE_PORT"]!["description"] = "Updated operator help.";

        Definition baseline = DefinitionAuthoringTests.Compile(baseJson.ToJsonString());
        Definition candidate = DefinitionAuthoringTests.Compile(nextJson.ToJsonString());

        DefinitionRevisionComparisonReport report = DefinitionRevisionComparison.Compare(baseline, candidate);

        await Assert.That(report.HasBehavioralChanges).IsFalse();
        await Assert.That(report.HasDisplayOnlyChanges).IsTrue();
        await Assert.That(report.Changes.All(change => change.Impact == DefinitionRevisionImpactKind.DisplayOnly)).IsTrue();
    }
}
