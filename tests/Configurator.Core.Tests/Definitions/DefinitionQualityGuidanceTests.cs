namespace Configurator.Core.Tests.Definitions;

using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionQualityGuidanceTests
{
    [Test]
    public async Task MissingRequiredAndChoiceGuidanceReportsSchemaLocationsWithoutBlockingAdmission()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("required");
        JsonObject configuration = source["configuration"]!.AsObject();
        JsonObject properties = configuration["properties"]!.AsObject();
        properties["optional"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public"}}""");
        properties["guided"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public","guidance":{"instructions":"Choose a hostname."}}}""");
        properties["choice"] = JsonNode.Parse("""{"type":"string","enum":["a","b"],"x-configurator":{"sensitivity":"public","choices":[{"value":"a","label":"A"},{"value":"b","label":"B","explanation":"Use B for local mode."}]}}""");
        configuration["required"] = new JsonArray("required", "guided");

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        DefinitionQualityAdvisory[] guidance = DefinitionQualityLinter.Lint(admitted)
            .Where(item => item.Code is "required-setting-missing-guidance" or "choice-missing-guidance").ToArray();

        await Assert.That(guidance).IsEquivalentTo(new[]
        {
            new DefinitionQualityAdvisory("choice-missing-guidance", "/configuration/properties/required/enum/0"),
            new DefinitionQualityAdvisory("choice-missing-guidance", "/configuration/properties/required/enum/1"),
            new DefinitionQualityAdvisory("required-setting-missing-guidance", "/configuration/properties/required"),
            new DefinitionQualityAdvisory("choice-missing-guidance", "/configuration/properties/choice/x-configurator/choices/0")
        });
        await Assert.That(admitted.Configuration.Properties.ContainsKey("required")).IsTrue();
    }

    [Test]
    public async Task RequiredLeafInsideOptionalContainerFlagsMissingGuidanceWithoutInflatingRequiredDensity()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("selector");
        JsonObject configuration = source["configuration"]!.AsObject();
        JsonObject properties = configuration["properties"]!.AsObject();
        JsonNode group = JsonNode.Parse("""
            {"type":"object","additionalProperties":false,"required":["leaf","token"],
             "x-configurator":{"sensitivity":"public"},
             "properties":{
               "leaf":{"type":"string","x-configurator":{"sensitivity":"public"}},
               "token":{"type":"string","x-configurator":{"sensitivity":"secret","secret":{"kind":"password","acquisition":"local","generation":{"length":32}}}}
             }}
            """)!;
        properties["optionalGroup"] = group.DeepClone();
        properties["requiredGroup"] = group;
        configuration["required"]!.AsArray().Add("requiredGroup");

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        DefinitionQualityAdvisory[] findings = DefinitionQualityLinter.Lint(admitted).ToArray();
        string[] settingPaths = findings
            .Where(item => item.Code == "required-setting-missing-guidance")
            .Select(item => item.SchemaPath).ToArray();
        string[] secretPaths = findings
            .Where(item => item.Code == "required-secret-missing-guidance")
            .Select(item => item.SchemaPath).ToArray();

        await Assert.That(settingPaths).Contains("/configuration/properties/requiredGroup/properties/leaf");
        await Assert.That(settingPaths).Contains("/configuration/properties/optionalGroup/properties/leaf");
        await Assert.That(secretPaths).Contains("/configuration/properties/requiredGroup/properties/token");
        await Assert.That(secretPaths).Contains("/configuration/properties/optionalGroup/properties/token");
    }

    [Test]
    public async Task RequiredWhenLeafFlagsMissingGuidanceForPublicAndSecretLeaves()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("selector");
        JsonObject configuration = source["configuration"]!.AsObject();
        JsonObject properties = configuration["properties"]!.AsObject();
        properties["enabled"] = JsonNode.Parse("""{"type":"boolean","description":"Toggle feature.","x-configurator":{"sensitivity":"public","initialValue":false}}""");
        properties["conditionalHost"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public",
              "requiredWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}}
            """);
        properties["conditionalSecret"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"secret",
              "secret":{"kind":"opaque-token","acquisition":"local","encoding":"base64url","generation":{"bytes":32}},
              "requiredWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}}
            """);
        properties["guidedConditional"] = JsonNode.Parse("""
            {"type":"string","description":"Documented conditional setting.",
             "x-configurator":{"sensitivity":"public",
               "requiredWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}}
            """);

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        DefinitionQualityAdvisory[] findings = DefinitionQualityLinter.Lint(admitted).ToArray();

        await Assert.That(findings).Contains(
            new DefinitionQualityAdvisory("required-setting-missing-guidance", "/configuration/properties/conditionalHost"));
        await Assert.That(findings).Contains(
            new DefinitionQualityAdvisory("required-secret-missing-guidance", "/configuration/properties/conditionalSecret"));
        await Assert.That(findings.Any(item => item.SchemaPath == "/configuration/properties/guidedConditional")).IsFalse();
    }

    [Test]
    public async Task ConditionalIfThenElseAndDependentRequiredTargetsFlagMissingGuidance()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("selector");
        JsonObject configuration = source["configuration"]!.AsObject();
        JsonObject properties = configuration["properties"]!.AsObject();
        properties["trigger"] = JsonNode.Parse("""
            {"type":"string","description":"Mode trigger.","enum":["remote","local"],
             "x-configurator":{"sensitivity":"public","choices":[
               {"value":"remote","label":"Remote","explanation":"Connects to a remote host."},
               {"value":"local","label":"Local","explanation":"Runs locally."}
             ]}}
            """);
        properties["thenTarget"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public"}}""");
        properties["elseTarget"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"secret","secret":{"kind":"password","acquisition":"local","generation":{"length":32}}}}""");
        properties["depTrigger"] = JsonNode.Parse("""{"type":"string","description":"Dependency trigger.","x-configurator":{"sensitivity":"public"}}""");
        properties["depTarget"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public"}}""");
        configuration["if"] = JsonNode.Parse("""{"required":["trigger"],"properties":{"trigger":{"const":"remote"}}}""");
        configuration["then"] = JsonNode.Parse("""{"required":["thenTarget"]}""");
        configuration["else"] = JsonNode.Parse("""{"required":["elseTarget"]}""");
        configuration["dependentRequired"] = JsonNode.Parse("""{"depTrigger":["depTarget"]}""");

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        DefinitionQualityAdvisory[] findings = DefinitionQualityLinter.Lint(admitted).ToArray();

        await Assert.That(findings).Contains(
            new DefinitionQualityAdvisory("required-setting-missing-guidance", "/configuration/properties/thenTarget"));
        await Assert.That(findings).Contains(
            new DefinitionQualityAdvisory("required-secret-missing-guidance", "/configuration/properties/elseTarget"));
        await Assert.That(findings).Contains(
            new DefinitionQualityAdvisory("required-setting-missing-guidance", "/configuration/properties/depTarget"));
    }

    [Test]
    public async Task EnumLeafWithZeroOrPartialChoicesFlagsChoiceMissingGuidance()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("selector");
        JsonObject properties = source["configuration"]!["properties"]!.AsObject();
        properties["bareEnum"] = JsonNode.Parse("""
            {"type":"string","description":"Unannotated enum.","enum":["alpha","beta"],
             "x-configurator":{"sensitivity":"public"}}
            """);
        properties["partialEnum"] = JsonNode.Parse("""
            {"type":"string","description":"Partially annotated enum.","enum":["fast","safe"],
             "x-configurator":{"sensitivity":"public","choices":[
               {"value":"fast","label":"Fast","explanation":"Prioritizes speed."}
             ]}}
            """);
        properties["completeEnum"] = JsonNode.Parse("""
            {"type":"string","description":"Fully annotated enum.","enum":["one","two"],
             "x-configurator":{"sensitivity":"public","choices":[
               {"value":"one","label":"One","explanation":"First mode."},
               {"value":"two","label":"Two","explanation":"Second mode."}
             ]}}
            """);

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        string[] choicePaths = DefinitionQualityLinter.Lint(admitted)
            .Where(item => item.Code == "choice-missing-guidance")
            .Select(item => item.SchemaPath).ToArray();

        await Assert.That(choicePaths).Contains("/configuration/properties/bareEnum/enum/0");
        await Assert.That(choicePaths).Contains("/configuration/properties/bareEnum/enum/1");
        await Assert.That(choicePaths).Contains("/configuration/properties/partialEnum/enum/1");
        await Assert.That(choicePaths.Any(path => path.StartsWith("/configuration/properties/completeEnum/", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task SecretMissingAcquisitionGuidanceFlagsRequiredAndOptionalSecretsWithoutAcquisitionOrInstructions()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("selector");
        JsonObject configuration = source["configuration"]!.AsObject();
        JsonObject properties = configuration["properties"]!.AsObject();
        properties["reqBareSecret"] = JsonNode.Parse("""{"type":"string","description":"Signing key.","x-configurator":{"sensitivity":"secret"}}""");
        properties["optBareSecret"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"secret"}}""");
        properties["optPolicySecret"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"secret","secret":{"kind":"password","acquisition":"local","generation":{"length":32}}}}""");
        properties["optInstructedSecret"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"secret","guidance":{"instructions":"Obtain from the issuer portal before continuing."}}}""");
        configuration["required"]!.AsArray().Add("reqBareSecret");

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        string[] paths = DefinitionQualityLinter.Lint(admitted)
            .Where(item => item.Code == "secret-missing-acquisition-guidance")
            .Select(item => item.SchemaPath).ToArray();

        await Assert.That(paths).Contains("/configuration/properties/reqBareSecret");
        await Assert.That(paths).Contains("/configuration/properties/optBareSecret");
        await Assert.That(paths).DoesNotContain("/configuration/properties/optPolicySecret");
        await Assert.That(paths).DoesNotContain("/configuration/properties/optInstructedSecret");
    }

    [Test]
    public async Task ChoiceDuplicateExplanationFlagsRepeatedChoiceExplanationsIgnoringCaseAndWhitespace()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("selector");
        JsonObject properties = source["configuration"]!["properties"]!.AsObject();
        properties["mode"] = JsonNode.Parse("""
            {"type":"string","enum":["one","two","three"],
             "x-configurator":{"sensitivity":"public","choices":[
               {"value":"one","label":"Primary","explanation":"Runs on the local host."},
               {"value":"two","label":"Secondary","explanation":"  runs on the local host. "},
               {"value":"three","label":"Relay","explanation":"Forwards traffic to a remote relay."}
             ]}}
            """);

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        string[] paths = DefinitionQualityLinter.Lint(admitted)
            .Where(item => item.Code == "choice-duplicate-explanation")
            .Select(item => item.SchemaPath).ToArray();

        await Assert.That(paths).IsEquivalentTo(["/configuration/properties/mode/x-configurator/choices/1"]);
    }

    [Test]
    public async Task DescriptionMissingFormatHintFlagsFormatFieldsWithoutConcreteExampleOrHint()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("selector");
        JsonObject properties = source["configuration"]!["properties"]!.AsObject();
        properties["bareUrl"] = JsonNode.Parse("""{"type":"string","format":"uri","description":"Service endpoint URL.","x-configurator":{"sensitivity":"public"}}""");
        properties["bareEmail"] = JsonNode.Parse("""{"type":"string","format":"email-address","x-configurator":{"sensitivity":"public"}}""");
        properties["guidedUrl"] = JsonNode.Parse("""{"type":"string","format":"uri","description":"Public API origin, e.g. https://api.example.org.","x-configurator":{"sensitivity":"public"}}""");
        properties["guidedEmail"] = JsonNode.Parse("""{"type":"string","format":"email-address","description":"Sender address such as `noreply@example.org`.","x-configurator":{"sensitivity":"public"}}""");

        Definition admitted = DefinitionAuthoringTests.Compile(source.ToJsonString());
        string[] paths = DefinitionQualityLinter.Lint(admitted)
            .Where(item => item.Code == "description-missing-format-hint")
            .Select(item => item.SchemaPath).ToArray();

        await Assert.That(paths).Contains("/configuration/properties/bareUrl");
        await Assert.That(paths).Contains("/configuration/properties/bareEmail");
        await Assert.That(paths).DoesNotContain("/configuration/properties/guidedUrl");
        await Assert.That(paths).DoesNotContain("/configuration/properties/guidedEmail");
    }

    [Test]
    public async Task SectionHighRequiredDensityFlagsMoreThanTwelveUngroupedRequiredLeavesStrictly()
    {
        static Definition BuildWithRequiredLeaves(int count, bool groupFirstFive)
        {
            var props = new JsonObject
            {
                ["selector"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public"}}""")
            };
            var required = new JsonArray("selector");
            var pointers = new JsonArray();
            var groupPointers = new JsonArray();
            for (int i = 1; i <= count; i++)
            {
                string key = $"req{i}";
                props[key] = JsonNode.Parse("""{"type":"string","description":"Configured option value.","x-configurator":{"sensitivity":"public"}}""");
                required.Add(key);
                pointers.Add("/" + key);
                if (groupFirstFive && i <= 5) groupPointers.Add("/" + key);
            }
            var sections = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "density",
                    ["title"] = "Density",
                    ["description"] = "Density boundary section.",
                    ["pointers"] = pointers,
                    ["groups"] = groupFirstFive
                        ? new JsonArray(new JsonObject
                        {
                            ["id"] = "primary",
                            ["title"] = "Primary",
                            ["pointers"] = groupPointers
                        })
                        : new JsonArray()
                }
            };
            var root = new JsonObject
            {
                ["$schema"] = "urn:islamu:configurator:authoring:v0.0.1",
                ["identity"] = new JsonObject { ["familyId"] = "f89b6e0d-42e1-4c22-8f61-5e1a7f3e4b22", ["revision"] = 1 },
                ["project"] = new JsonObject { ["id"] = "density.sample", ["name"] = "Density Sample" },
                ["applicationVersions"] = new JsonArray("1.0"),
                ["configuration"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = required,
                    ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
                    ["properties"] = props
                },
                ["presentation"] = new JsonObject
                {
                    ["defaultJourney"] = "setup",
                    ["sections"] = sections,
                    ["journeys"] = new JsonArray(new JsonObject
                    {
                        ["id"] = "setup",
                        ["title"] = "Setup",
                        ["sections"] = new JsonArray("density")
                    })
                },
                ["outputs"] = new JsonObject
                {
                    ["config.json"] = new JsonObject { ["format"] = "json", ["targetUpdateMode"] = "create", ["restartRequirement"] = "process" }
                }
            };
            return DefinitionAuthoringTests.Compile(root.ToJsonString());
        }

        Definition twelve = BuildWithRequiredLeaves(12, groupFirstFive: false);
        await Assert.That(DefinitionQualityLinter.Lint(twelve)
            .Any(item => item.Code == "section-high-required-density")).IsFalse();

        Definition thirteen = BuildWithRequiredLeaves(13, groupFirstFive: false);
        await Assert.That(DefinitionQualityLinter.Lint(thirteen)
            .Where(item => item.Code == "section-high-required-density")
            .Select(item => item.SchemaPath).ToArray())
            .IsEquivalentTo(["/presentation/sections/0"]);

        Definition thirteenWithGroup = BuildWithRequiredLeaves(13, groupFirstFive: true);
        await Assert.That(DefinitionQualityLinter.Lint(thirteenWithGroup)
            .Any(item => item.Code == "section-high-required-density")).IsFalse();
    }
}
