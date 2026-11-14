namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionSuggestionAdmissionTests
{
    private static JsonObject Source()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("task");
        JsonObject properties = source["configuration"]!["properties"]!.AsObject();
        properties["source"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public"}}""");
        properties["target"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public","suggestedFrom":{"pointer":"/source"}}}""");
        return source;
    }

    private static Definition Compile(JsonObject source) => DefinitionAuthoringTests.Compile(source.ToJsonString());
    private static Definition Parse(JsonObject runtime) => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString()));
    private static JsonObject Properties(JsonObject document) => document["configuration"]!["properties"]!.AsObject();
    private static JsonObject Metadata(JsonObject document, string field) => Properties(document)[field]!["x-configurator"]!.AsObject();

    [Test]
    [Arguments("public", "public", true)]
    [Arguments("public", "sensitive", true)]
    [Arguments("public", "secret", true)]
    [Arguments("sensitive", "public", false)]
    [Arguments("sensitive", "sensitive", true)]
    [Arguments("sensitive", "secret", true)]
    [Arguments("secret", "public", false)]
    [Arguments("secret", "sensitive", false)]
    [Arguments("secret", "secret", true)]
    public async Task AllSensitivityPairsEnforceInformationFlow(string sourceSensitivity, string targetSensitivity, bool accepted)
    {
        JsonObject source = Source();
        Metadata(source, "source")["sensitivity"] = sourceSensitivity;
        Metadata(source, "target")["sensitivity"] = targetSensitivity;
        await CheckBoth(source, accepted, "suggestion-sensitivity");
    }

    [Test]
    [Arguments("sensitive", false)]
    [Arguments("secret", false)]
    [Arguments("public", true)]
    public async Task SourceAncestorFloorsCannotBeLaundered(string sensitivity, bool writeOnly)
    {
        JsonObject source = Source();
        JsonNode leaf = Properties(source)["source"]!.DeepClone();
        Properties(source).Remove("source");
        Properties(source)["group"] = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["x-configurator"] = new JsonObject { ["sensitivity"] = sensitivity },
            ["writeOnly"] = writeOnly,
            ["properties"] = new JsonObject { ["leaf"] = leaf }
        };
        Metadata(source, "target")["suggestedFrom"]!["pointer"] = "/group/leaf";
        await CheckBoth(source, false, "suggestion-sensitivity");
        Metadata(source, "target")["sensitivity"] = "secret";
        await CheckBoth(source, true);
    }

    [Test]
    [Arguments("")]
    [Arguments("/target")]
    [Arguments("/missing")]
    [Arguments("/source/child")]
    public async Task NonScalarMissingAndOverlappingEndpointsReject(string location)
    {
        JsonObject source = Source();
        Metadata(source, "target")["suggestedFrom"]!["pointer"] = location;
        await CheckBoth(source, false, "suggestion-endpoint");
    }

    [Test]
    public async Task DisconnectedLongCyclesRejectButLongChainsAdmit()
    {
        JsonObject source = Source();
        for (int index = 0; index < 80; index++)
        {
            JsonNode field = Properties(source)["target"]!.DeepClone();
            field["x-configurator"]!["suggestedFrom"]!["pointer"] = index == 79 ? "/source" : "/chain" + (index + 1);
            Properties(source)["chain" + index] = field;
        }
        await CheckBoth(source, true);
        Properties(source)["chain79"]!["x-configurator"]!["suggestedFrom"]!["pointer"] = "/chain0";
        await CheckBoth(source, false, "suggestion-cycle");
    }

    [Test]
    public async Task DynamicArrayVariantAndContainerEndpointsReject()
    {
        foreach ((string fieldJson, string location) in new[]
        {
            ("""{"type":"array","items":{"type":"string","x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public"}}""", "/source/0"),
            ("""{"type":"object","additionalProperties":{"type":"string","x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public"}}""", "/source/dynamic"),
            ("""{"type":"object","additionalProperties":false,"properties":{"leaf":{"type":"string"}},"x-configurator":{"sensitivity":"public"}}""", "/source"),
            ("""{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["kind"],"properties":{"kind":{"type":"string","const":"a","x-configurator":{"sensitivity":"public"}},"leaf":{"type":"string"}}},{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"required":["kind"],"properties":{"kind":{"type":"string","const":"b","x-configurator":{"sensitivity":"public"}},"leaf":{"type":"string"}}}]}""", "/source/leaf")
        })
        {
            JsonObject source = Source();
            Properties(source)["source"] = JsonNode.Parse(fieldJson);
            Metadata(source, "target")["suggestedFrom"]!["pointer"] = location;
            await CheckBoth(source, false, "suggestion-endpoint");
        }
    }

    [Test]
    public async Task ContainerAndDynamicDestinationsReject()
    {
        JsonObject source = Source();
        JsonNode target = Properties(source)["target"]!.DeepClone();
        Properties(source)["target"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = target,
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" }
        };
        await Reject(() => Compile(source), "suggestion-endpoint");
        source = Source();
        Properties(source)["target"]!["type"] = "object";
        Properties(source)["target"]!["additionalProperties"] = false;
        await CheckBoth(source, false, "suggestion-endpoint");
    }

    [Test]
    public async Task StaticAndDynamicSuggestionsAreMutuallyExclusive()
    {
        JsonObject source = Source();
        Metadata(source, "target")["suggestedValue"] = "synthetic";
        await CheckBoth(source, false, "suggestion-conflict");
    }

    [Test]
    [Arguments("source")]
    [Arguments("target")]
    public async Task GeneratedSecretEndpointsRejectWhileExistingSecretPairsAdmit(string endpoint)
    {
        JsonObject source = Source();
        foreach (string field in new[] { "source", "target" })
        {
            Metadata(source, field)["sensitivity"] = "secret";
            Metadata(source, field)["secret"] = JsonNode.Parse("""{"kind":"opaque-token","acquisition":"existing","encoding":"base64url"}""");
        }
        await CheckBoth(source, true);
        Metadata(source, endpoint)["secret"]!["acquisition"] = "local";
        Metadata(source, endpoint)["secret"]!["generation"] = new JsonObject { ["bytes"] = 32 };
        await CheckBoth(source, false, "suggestion-generated");
    }

    [Test]
    public async Task LinkFailuresHaveSourceLocationsAndValueFreeDiagnostics()
    {
        JsonObject source = Source();
        JsonObject runtime = JsonNode.Parse(Compile(source).CanonicalBytes.Span)!.AsObject();
        Metadata(source, "source")["sensitivity"] = "secret";
        Metadata(runtime, "source")["sensitivity"] = "secret";
        source["annotations"] = new JsonObject { ["synthetic"] = "UNRELATED_SENTINEL_VALUE" };
        foreach (Action admit in new Action[] { () => Compile(source), () => Parse(runtime) })
        {
            try { admit(); }
            catch (DefinitionException exception)
            {
                await Assert.That(exception.Code).IsEqualTo("suggestion-sensitivity");
                await Assert.That(exception.SchemaPath).IsEqualTo("/configuration/properties/target/x-configurator/suggestedFrom");
                await Assert.That(exception.SourceSpan is not null).IsTrue();
                await Assert.That(exception.ToString().Contains("UNRELATED_SENTINEL_VALUE", StringComparison.Ordinal)).IsFalse();
                continue;
            }
            throw new InvalidOperationException("Expected source-located admission rejection.");
        }
    }

    [Test]
    public async Task ClosedLinkGrammarAndUnsupportedPatternReject()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            source => Metadata(source, "target")["suggestedFrom"]!["unknown"] = true,
            source => Metadata(source, "target")["suggestedFrom"]!["pointer"] = "source",
            source => Metadata(source, "target")["suggestedFrom"]!["when"] = null,
            source => Metadata(source, "target")["suggestedFrom"]!["when"] = JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/missing"},"value":"x"}"""),
            source => Properties(source)["source"]!["pattern"] = ".*"
        })
        {
            JsonObject source = Source();
            mutate(source);
            await Assert.That(() => Compile(source)).Throws<DefinitionException>();
        }
    }

    [Test]
    public async Task NamedAndInlineConditionsLowerAndRemainThreeValued()
    {
        JsonObject source = Source();
        source["configuration"]!["required"]!.AsArray().Add("source");
        JsonNode condition = JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/source"},"value":"ready"}""")!;
        Metadata(source, "target")["suggestedFrom"]!["when"] = condition;
        Definition inline = Compile(source);
        source["predicates"] = new JsonObject { ["ready"] = condition.DeepClone() };
        Metadata(source, "target")["suggestedFrom"]!["when"] = JsonNode.Parse("""{"op":"predicate","name":"ready"}""");
        Definition named = Compile(source);
        await Assert.That(named.Digest).IsEqualTo(inline.Digest);
        DefinitionSuggestedSource suggestion = named.Configuration.Properties["target"].PresentationMetadata.SuggestedSource!;
        foreach ((string json, bool? expected) in new (string, bool?)[] { ("""{"source":"ready"}""", true), ("""{"source":"other"}""", false), ("{}", null) })
        {
            AnswerSnapshot answers = DefinitionFixture.Answers(json);
            await Assert.That(suggestion.EvaluateEligibility(DefinitionValidator.Validate(named, answers), answers)).IsEqualTo(expected);
        }
        Metadata(source, "target")["suggestedFrom"]!["when"]!["name"] = "missing";
        await Reject(() => Compile(source), "predicate-unknown");
    }

    [Test]
    public async Task QualityFindingsDescribeOnlyObservableWorkflowMetadataGaps()
    {
        JsonObject source = Source();
        Definition definition = Compile(source);
        var findings = DefinitionQualityLinter.Lint(definition);
        await Assert.That(findings.Any(finding => finding.Code == "output-selector-missing-next-actions"
            && finding.SchemaPath == "/presentation/outputSelector")).IsTrue();
        await Assert.That(findings.Count(finding => finding.Code == "output-option-missing-description")).IsEqualTo(2);
        JsonArray options = source["presentation"]!["outputSelector"]!["options"]!.AsArray();
        foreach (JsonNode? option in options) option!["description"] = "A separate editable task.";
        options[0]!["nextActions"] = JsonNode.Parse("""[{"targetOption":"identity","title":"Continue"}]""");
        findings = DefinitionQualityLinter.Lint(Compile(source));
        await Assert.That(findings.Any(finding => finding.Code.StartsWith("output-", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task RetainedPublicStoreReleasesRemainAdmissibleWithoutRewritingBytes()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "store", "islamu-event", "releases")))
            root = root.Parent;
        string directory = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Public store missing."),
            "store", "islamu-event", "releases");
        string[] releases = Directory.GetFiles(directory, "*.json");
        await Assert.That(releases.Length >= 4).IsTrue();
        foreach (string release in releases)
        {
            byte[] bytes = await File.ReadAllBytesAsync(release);
            Definition definition = DefinitionCodec.ParseJson(bytes);
            await Assert.That(definition.Revision > 0).IsTrue();
            byte[] retained = await File.ReadAllBytesAsync(release);
            await Assert.That(bytes.AsSpan().SequenceEqual(retained)).IsTrue();
        }
    }

    [Test]
    public async Task MissingAndUnusedFeatureCapabilitiesRejectOnDirectAdmission()
    {
        Definition admitted = Compile(Source());
        JsonObject runtime = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
        JsonArray capabilities = runtime["requiredCapabilities"]!.AsArray();
        int index = capabilities.Select(value => value!.GetValue<string>()).ToList().IndexOf("values.suggested-from");
        capabilities.RemoveAt(index);
        await Reject(() => Parse(runtime), "capabilities-invalid");
        runtime = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
        Metadata(runtime, "target").Remove("suggestedFrom");
        await Reject(() => Parse(runtime), "capabilities-invalid");
    }

    [Test]
    [Arguments("""{"type":"integer","minimum":2,"maximum":8,"multipleOf":2}""", """{"type":"number","minimum":0,"maximum":10,"multipleOf":1}""", true)]
    [Arguments("""{"type":"number"}""", """{"type":"integer"}""", false)]
    [Arguments("""{"type":"integer","minimum":0}""", """{"type":"integer","exclusiveMinimum":0}""", false)]
    [Arguments("""{"type":"integer","exclusiveMinimum":0}""", """{"type":"integer","minimum":0}""", true)]
    [Arguments("""{"type":"number","exclusiveMaximum":10}""", """{"type":"number","maximum":10}""", true)]
    [Arguments("""{"type":"number","maximum":10}""", """{"type":"number","exclusiveMaximum":10}""", false)]
    [Arguments("""{"type":"number","multipleOf":4}""", """{"type":"number","multipleOf":2}""", true)]
    [Arguments("""{"type":"number","multipleOf":2}""", """{"type":"number","multipleOf":4}""", false)]
    [Arguments("""{"type":"string","minLength":2,"maxLength":8}""", """{"type":"string","minLength":1,"maxLength":9}""", true)]
    [Arguments("""{"type":"string"}""", """{"type":"string","minLength":1}""", false)]
    [Arguments("""{"type":"string","enum":["ab","cd"]}""", """{"type":"string","enum":["ab","cd","ef"]}""", true)]
    [Arguments("""{"type":"string","enum":["ab","cd"]}""", """{"type":"string","const":"ab"}""", false)]
    [Arguments("""{"type":"string","const":"ab"}""", """{"type":"string","minLength":2,"maxLength":2}""", true)]
    [Arguments("""{"type":"string"}""", """{"type":"string","enum":["ab"]}""", false)]
    [Arguments("""{"type":["string","null"]}""", """{"type":"string"}""", false)]
    [Arguments("""{"type":"string"}""", """{"type":["string","null"]}""", true)]
    [Arguments("""{"type":"string","format":"email-address"}""", """{"type":"string","format":"email-address"}""", true)]
    [Arguments("""{"type":"string"}""", """{"type":"string","format":"email-address"}""", false)]
    [Arguments("""{"type":"string","const":"\uD83D\uDE00"}""", """{"type":"string","minLength":1,"maxLength":1}""", true)]
    [Arguments("""{"type":"boolean","const":false}""", """{"type":"boolean","enum":[false,true]}""", true)]
    [Arguments("""{"type":"integer","const":0}""", """{"type":"number","minimum":0}""", true)]
    [Arguments("""{"type":"string"}""", """{"type":"string","x-configurator":{"sensitivity":"public","notBlank":true}}""", false)]
    public async Task ScalarCompatibilityRequiresAProvableSubset(string sourceJson, string targetJson, bool accepted)
    {
        JsonObject source = Source();
        Properties(source)["source"] = JsonNode.Parse(sourceJson);
        Properties(source)["target"] = JsonNode.Parse(targetJson);
        Properties(source)["source"]!["x-configurator"] = new JsonObject { ["sensitivity"] = "public" };
        JsonObject targetMetadata = Properties(source)["target"]!["x-configurator"] as JsonObject ?? new JsonObject();
        targetMetadata["sensitivity"] = "public";
        targetMetadata["suggestedFrom"] = new JsonObject { ["pointer"] = "/source" };
        Properties(source)["target"]!["x-configurator"] = targetMetadata.Parent is null ? targetMetadata : targetMetadata.DeepClone();
        await CheckBoth(source, accepted, "suggestion-incompatible");
    }

    private static async Task CheckBoth(JsonObject source, bool accepted, string code = "")
    {
        // Build an independently admitted runtime fixture without the feature, then add the feature
        // to both boundaries. Rejection must come from the link, never an unrelated capability error.
        JsonObject plain = source.DeepClone().AsObject();
        var links = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach ((string name, JsonNode? field) in Properties(plain))
            if (field?["x-configurator"] is JsonObject metadata && metadata["suggestedFrom"] is { } link)
            {
                links.Add(name, link.DeepClone());
                metadata.Remove("suggestedFrom");
            }
        JsonObject runtime = JsonNode.Parse(Compile(plain).CanonicalBytes.Span)!.AsObject();
        foreach ((string name, JsonNode link) in links)
            Metadata(runtime, name)["suggestedFrom"] = link.DeepClone();
        var capabilities = runtime["requiredCapabilities"]!.AsArray().Select(value => value!.GetValue<string>())
            .Append("values.suggested-from").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        if (accepted)
            await Assert.That(Parse(runtime).Digest).IsEqualTo(Compile(source).Digest);
        else
        {
            await Reject(() => Compile(source), code);
            await Reject(() => Parse(runtime), code);
        }
    }

    [Test]
    public async Task SensitivityLaunderingRejectsAfterValidLinkAdmission()
    {
        Definition admitted = Compile(Source());
        await Assert.That(admitted.Configuration.Properties["target"].PresentationMetadata.SuggestedSource!.Location.ToPointer()).IsEqualTo("/source");
        await Assert.That(admitted.RequiredCapabilities.Contains("values.suggested-from")).IsTrue();
        JsonObject runtime = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
        runtime["configuration"]!["properties"]!["source"]!["x-configurator"]!["sensitivity"] = "secret";
        await Reject(() => Parse(runtime), "suggestion-sensitivity");
    }

    private static async Task Reject(Action action, string code)
    {
        try { action(); }
        catch (DefinitionException exception)
        {
            await Assert.That(exception.Code).IsEqualTo(code);
            return;
        }
        throw new InvalidOperationException("Expected bounded suggestion rejection: " + code);
    }
}
