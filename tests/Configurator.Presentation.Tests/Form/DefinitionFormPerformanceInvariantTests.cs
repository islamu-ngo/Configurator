namespace Configurator.Presentation.Tests;

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionFormPerformanceInvariantTests
{
    [Test]
    public async Task OptionalMissingComputesExactCountWithoutMaterializingDiscoveryEntries()
    {
        Definition definition = CreateMixedRequiredOptionalDefinition(requiredCount: 12, optionalCount: 150);
        using var form = new DefinitionFormModel(definition);

        DefinitionFormProjection projection = form.Projection;
        await Assert.That(projection.ProgressSummary.OptionalMissing).IsEqualTo(150);
        await Assert.That(projection.Progress.Required).IsEqualTo(12);

        // Supplying two optional fields decrements OptionalMissing immediately.
        form.EditValue(["opt0000"], "configured");
        form.EditValue(["opt0001"], "configured");
        DefinitionFormProjection updated = form.Projection;
        await Assert.That(updated.ProgressSummary.OptionalMissing).IsEqualTo(148);

        // Cross-check against materialized Entries as an independent specification.
        int expectedFromEntries = updated.Entries.Count(entry => entry.IsActive && !entry.IsRequired && !entry.IsProvided);
        await Assert.That(updated.ProgressSummary.OptionalMissing).IsEqualTo(expectedFromEntries);
    }

    [Test]
    public async Task FieldStatesByIdProvidesCachedOrdinalLookupForAllRootFields()
    {
        Definition definition = CreateMixedRequiredOptionalDefinition(requiredCount: 5, optionalCount: 25);
        using var form = new DefinitionFormModel(definition);

        DefinitionFormProjection projection = form.Projection;
        IReadOnlyDictionary<string, FieldState> first = projection.FieldStatesById;
        IReadOnlyDictionary<string, FieldState> second = projection.FieldStatesById;

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(first.Count).IsEqualTo(30);
        await Assert.That(first[DefinitionProfile.FieldId("req0000")].IsRequired).IsTrue();
        await Assert.That(first[DefinitionProfile.FieldId("opt0000")].IsRequired).IsFalse();
        await Assert.That(first[DefinitionProfile.FieldId("opt0000")].IsProvided).IsFalse();

        form.EditValue(["opt0000"], "supplied");
        await Assert.That(form.Projection.FieldStatesById[DefinitionProfile.FieldId("opt0000")].IsProvided).IsTrue();
    }

    private static Definition CreateMixedRequiredOptionalDefinition(int requiredCount, int optionalCount)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var members = new JsonObject();

        for (int index = 0; index < requiredCount; index++)
        {
            string name = "req" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            properties[name] = new JsonObject { ["type"] = "string", ["minLength"] = 1 };
            required.Add(name);
            members[name] = new JsonObject
            {
                ["op"] = "copy",
                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/" + name }
            };
        }

        for (int index = 0; index < optionalCount; index++)
        {
            string name = "opt" + index.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            properties[name] = new JsonObject { ["type"] = "string" };
            members[name] = new JsonObject
            {
                ["op"] = "copy",
                ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/" + name }
            };
        }

        var root = new JsonObject
        {
            ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
            ["identity"] = new JsonObject { ["familyId"] = "ea050007-4ef0-4c80-9f10-000000000099", ["revision"] = 1 },
            ["project"] = new JsonObject { ["id"] = "example.perf", ["name"] = "Perf", ["publisher"] = "example" },
            ["applicationVersions"] = new JsonArray("1.0"),
            ["entryPoints"] = new JsonArray("setup"),
            ["configuration"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = required,
                ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                ["properties"] = properties
            },
            ["outputs"] = new JsonObject
            {
                ["config.json"] = new JsonObject
                {
                    ["id"] = "config",
                    ["format"] = "json",
                    ["targetUpdateMode"] = "create",
                    ["restartRequirement"] = "none",
                    ["projection"] = new JsonObject { ["op"] = "object", ["members"] = members }
                }
            }
        };

        return DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes(root.ToJsonString()));
    }
}
