namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionOutputNextActionTests
{
    private static JsonObject Source()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("task");
        Options(source)[0]!["nextActions"] = new JsonArray(new JsonObject
        {
            ["targetOption"] = "identity",
            ["title"] = "Prepare identity"
        });
        return source;
    }

    private static JsonArray Options(JsonObject source) => source["presentation"]!["outputSelector"]!["options"]!.AsArray();
    private static Definition Compile(JsonObject source) => DefinitionAuthoringTests.Compile(source.ToJsonString());
    private static Definition Parse(JsonObject runtime) => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString()));

    [Test]
    public async Task UnknownTargetRejectsAfterValidFeatureAdmission()
    {
        Definition admitted = Compile(Source());
        await Assert.That(admitted.Presentation!.OutputSelector!.Options[0].NextActions.Single().TargetOption).IsEqualTo("identity");
        JsonObject runtime = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
        Options(runtime)[0]!["nextActions"]![0]!["targetOption"] = "missing";
        await Reject(() => Parse(runtime), "next-action-target");
    }

    [Test]
    public async Task OptionalAbsenceAndEmptyActionsRequireNoCapability()
    {
        JsonObject source = DefinitionPresentationAdmissionTests.OutputSelectorSource("task");
        foreach (bool empty in new[] { false, true })
        {
            if (empty) Options(source)[0]!["nextActions"] = new JsonArray();
            Definition definition = Compile(source);
            await Assert.That(definition.Presentation!.OutputSelector!.Options[0].NextActions.Count).IsEqualTo(0);
            await Assert.That(definition.RequiredCapabilities.Contains("presentation.next-actions")).IsFalse();
            await Assert.That(definition.Presentation.OutputSelector.Options[0].Description is null).IsTrue();
        }
    }

    [Test]
    public async Task ActionsPreserveAuthoredOrderDefaultsAndImmutableSnapshots()
    {
        JsonObject source = Source();
        Options(source)[0]!["description"] = "Settings to prepare.";
        Options(source)[0]!["nextActions"]![0]!["description"] = "Prepare this separately.";
        Options(source)[1]!["nextActions"] = new JsonArray(new JsonObject
        {
            ["targetOption"] = "settings",
            ["title"] = "Return to settings",
            ["priority"] = "optional"
        });
        Definition definition = Compile(source);
        DefinitionOutputOption option = definition.Presentation!.OutputSelector!.Options[0];
        await Assert.That(option.Description).IsEqualTo("Settings to prepare.");
        await Assert.That(option.NextActions[0].Priority).IsEqualTo("available");
        await Assert.That(option.NextActions[0].Description).IsEqualTo("Prepare this separately.");
        await Assert.That(definition.Presentation.OutputSelector.Options[1].NextActions[0].TargetOption).IsEqualTo("settings");
        await Assert.That(definition.Presentation.OutputSelector.Options[1].NextActions[0].Priority).IsEqualTo("optional");
        Options(source)[0]!["nextActions"]!.AsArray().Clear();
        await Assert.That(option.NextActions.Count).IsEqualTo(1);
        await Assert.That(() => ((IList<DefinitionOutputNextAction>)option.NextActions).Clear()).Throws<NotSupportedException>();
        await Assert.That(DefinitionCodec.ParseJson(definition.CanonicalBytes).Digest).IsEqualTo(definition.Digest);
    }

    [Test]
    [Arguments("missing", "next-action-target")]
    [Arguments("settings", "next-action-target")]
    [Arguments("IDENTITY", "next-action-target")]
    public async Task UnknownSelfAndInexactTargetsRejectInBothBoundaries(string target, string code)
    {
        JsonObject source = Source();
        JsonObject runtime = JsonNode.Parse(Compile(source).CanonicalBytes.Span)!.AsObject();
        foreach (JsonObject document in new[] { source, runtime })
            Options(document)[0]!["nextActions"]![0]!["targetOption"] = target;
        await Reject(() => Compile(source), code);
        await Reject(() => Parse(runtime), code);
    }

    [Test]
    public async Task DuplicateTargetsRejectWithoutForbiddingNavigationCycles()
    {
        JsonObject source = Source();
        JsonArray actions = Options(source)[0]!["nextActions"]!.AsArray();
        actions.Add(actions[0]!.DeepClone());
        await Reject(() => Compile(source), "next-action-duplicate");
    }

    [Test]
    [Arguments("recommended")]
    [Arguments("available")]
    [Arguments("optional")]
    public async Task PrioritiesAndTextBoundsAdmitTheirExactEdges(string priority)
    {
        JsonObject source = Source();
        Options(source)[0]!["description"] = new string('d', 2048);
        JsonNode action = Options(source)[0]!["nextActions"]![0]!;
        action["priority"] = priority;
        action["title"] = new string('t', 128);
        action["description"] = new string('d', 2048);
        await Assert.That(Compile(source).Presentation!.OutputSelector!.Options[0].NextActions[0].Priority).IsEqualTo(priority);
    }

    [Test]
    [Arguments("priority", "\"urgent\"")]
    [Arguments("title", "\"\"")]
    [Arguments("title", "null")]
    [Arguments("targetOption", "null")]
    [Arguments("proposedWhen", "null")]
    [Arguments("command", "\"synthetic-command\"")]
    [Arguments("url", "\"https://example.org\"")]
    public async Task ClosedActionMembersRejectInvalidDeclarations(string member, string json)
    {
        JsonObject source = Source();
        JsonObject runtime = JsonNode.Parse(Compile(source).CanonicalBytes.Span)!.AsObject();
        Options(source)[0]!["nextActions"]![0]![member] = JsonNode.Parse(json);
        Options(runtime)[0]!["nextActions"]![0]![member] = JsonNode.Parse(json);
        await Assert.That(() => Compile(source)).Throws<DefinitionException>();
        await Assert.That(() => Parse(runtime)).Throws<DefinitionException>();
    }

    [Test]
    public async Task TextOverflowAndInvalidCollectionsReject()
    {
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            option => option["description"] = new string('d', 2049),
            option => option["nextActions"]![0]!["title"] = new string('t', 129),
            option => option["nextActions"]![0]!["description"] = new string('d', 2049),
            option => option["nextActions"] = null,
            option => option["nextActions"] = new JsonObject(),
            option => option["nextActions"] = new JsonArray((JsonNode?)null)
        })
        {
            JsonObject runtime = JsonNode.Parse(Compile(Source()).CanonicalBytes.Span)!.AsObject();
            mutate(Options(runtime)[0]!.AsObject());
            await Assert.That(() => Parse(runtime)).Throws<DefinitionException>();
        }
    }

    [Test]
    public async Task ExactCapabilitiesRejectMissingExtraAndUnsortedDeclarations()
    {
        Definition admitted = Compile(Source());
        await Assert.That(admitted.RequiredCapabilities.Contains("presentation.next-actions")).IsTrue();
        foreach (Action<JsonArray> mutate in new Action<JsonArray>[]
        {
            capabilities => capabilities.RemoveAt(capabilities.Select(value => value!.GetValue<string>()).ToList().IndexOf("presentation.next-actions")),
            capabilities => capabilities.Add("presentation.next-actions"),
            capabilities => { JsonNode first = capabilities[0]!.DeepClone(); capabilities.RemoveAt(0); capabilities.Add(first); }
        })
        {
            JsonObject runtime = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
            mutate(runtime["requiredCapabilities"]!.AsArray());
            await Reject(() => Parse(runtime), "capabilities-invalid");
        }
        JsonObject absent = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
        Options(absent)[0]!.AsObject().Remove("nextActions");
        await Reject(() => Parse(absent), "capabilities-invalid");
    }

    [Test]
    public async Task NamedAndInlinePredicatesLowerToIdenticalRuntimeAndEvaluateInCore()
    {
        JsonObject inline = Source();
        JsonNode condition = JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"settings"}""")!;
        Options(inline)[0]!["nextActions"]![0]!["proposedWhen"] = condition;
        JsonObject named = inline.DeepClone().AsObject();
        named["predicates"] = new JsonObject { ["ready"] = condition.DeepClone() };
        Options(named)[0]!["nextActions"]![0]!["proposedWhen"] = JsonNode.Parse("""{"op":"predicate","name":"ready"}""");
        Definition definition = Compile(named);
        await Assert.That(definition.Digest).IsEqualTo(Compile(inline).Digest);
        DefinitionOutputNextAction action = definition.Presentation!.OutputSelector!.Options[0].NextActions[0];
        await Assert.That(action.ProposedWhen!.Value.GetProperty("op").GetString()).IsEqualTo("equals");
        await Assert.That(action.EvaluateProposal(DefinitionValidator.Validate(definition, DefinitionFixture.Answers("""{"task":"settings"}""")))).IsEqualTo(true);
        await Assert.That(action.EvaluateProposal(DefinitionValidator.Validate(definition, DefinitionFixture.Answers("""{"task":"identity"}""")))).IsEqualTo(false);
        named["configuration"]!["properties"]!["task"]!["x-configurator"]!.AsObject().Remove("initialValue");
        Definition undecided = Compile(named);
        await Assert.That(undecided.Presentation!.OutputSelector!.Options[0].NextActions[0]
            .EvaluateProposal(DefinitionValidator.Validate(undecided, DefinitionFixture.Answers("{}")))).IsEqualTo((bool?)null);
        Definition unconditional = Compile(Source());
        await Assert.That(unconditional.Presentation!.OutputSelector!.Options[0].NextActions[0]
            .EvaluateProposal(DefinitionValidator.Validate(unconditional, DefinitionFixture.Answers("{}")))).IsEqualTo(true);
    }

    [Test]
    public async Task InvalidPredicateReferencesAndRuntimeNamedPredicatesReject()
    {
        JsonObject source = Source();
        Options(source)[0]!["nextActions"]![0]!["proposedWhen"] = JsonNode.Parse("""{"op":"predicate","name":"missing"}""");
        await Reject(() => Compile(source), "predicate-unknown");
        JsonObject runtime = JsonNode.Parse(Compile(Source()).CanonicalBytes.Span)!.AsObject();
        Options(runtime)[0]!["nextActions"]![0]!["proposedWhen"] = JsonNode.Parse("""{"op":"predicate","name":"missing"}""");
        await Reject(() => Parse(runtime), "condition-operation");
        Options(source)[0]!["nextActions"]![0]!["proposedWhen"] = JsonNode.Parse("""{"op":"exists","reference":{"scope":"root","path":"/missing"}}""");
        await Assert.That(() => Compile(source)).Throws<DefinitionException>();
    }

    [Test]
    public async Task LocalizationUsesRealNumericPositionsAndSourceSpans()
    {
        JsonObject source = Source();
        Options(source)[0]!["description"] = "Settings.";
        Options(source)[0]!["nextActions"]![0]!["description"] = "Identity.";
        const string actionPath = "/presentation/outputSelector/options/0/nextActions/0";
        source["localization"] = new JsonObject
        {
            ["defaultLocale"] = "en",
            ["translations"] = new JsonObject
            {
                ["ar"] = new JsonObject
                {
                    ["direction"] = "rtl",
                    ["overrides"] = new JsonObject
                    {
                        [actionPath + "/title"] = "Translated action",
                        [actionPath + "/description"] = "Translated description",
                        ["/presentation/outputSelector/options/0/description"] = "Translated option"
                    }
                }
            }
        };
        Definition definition = Compile(source);
        await Assert.That(new DefinitionLocalization(definition).Text("ar", actionPath + "/title", "Base")).IsEqualTo("Translated action");
        await Assert.That(definition.SourceMap.TryGetSpan(actionPath + "/title", out DefinitionSourceSpan span)).IsTrue();
        await Assert.That(span.StartLine).IsEqualTo(1);
        source["localization"]!["translations"]!["ar"]!["overrides"]![actionPath.Replace("/0/nextActions", "/settings/nextActions", StringComparison.Ordinal) + "/title"] = "Invalid";
        await Reject(() => Compile(source), "localization-target");
        source.Remove("localization");
        Options(source)[0]!["nextActions"]![0]!["targetOption"] = "missing";
        try { _ = Compile(source); }
        catch (DefinitionException exception)
        {
            await Assert.That(exception.SchemaPath).IsEqualTo(actionPath);
            await Assert.That(exception.SourceSpan is not null).IsTrue();
            return;
        }
        throw new InvalidOperationException("Expected next-action diagnostic with source span.");
    }

    [Test]
    public async Task SixtyFourOptionsAdmitSixtyThreeActionsEachAndRejectOverflow()
    {
        JsonObject source = Source();
        var options = new JsonArray();
        var values = new JsonArray();
        var outputs = new JsonObject();
        for (int index = 0; index < 64; index++)
        {
            string value = "option" + index;
            values.Add(value);
            var actions = new JsonArray();
            for (int target = 0; target < 64; target++)
                if (target != index) actions.Add(new JsonObject { ["targetOption"] = "option" + target, ["title"] = "Continue" });
            options.Add(new JsonObject { ["value"] = value, ["title"] = "Option", ["artifactIds"] = new JsonArray(value), ["nextActions"] = actions });
            outputs[value + ".json"] = new JsonObject
            {
                ["id"] = value,
                ["format"] = "json",
                ["projection"] = new JsonObject
                {
                    ["op"] = "literal",
                    ["value"] = new JsonObject(),
                    ["when"] = new JsonObject
                    {
                        ["op"] = "equals",
                        ["value"] = value,
                        ["reference"] = new JsonObject { ["scope"] = "root", ["path"] = "/task" }
                    }
                }
            };
        }
        source["configuration"]!["properties"]!["task"]!["enum"] = values;
        source["configuration"]!["properties"]!["task"]!["x-configurator"]!.AsObject().Remove("initialValue");
        source["presentation"]!["outputSelector"]!["options"] = options;
        source["outputs"] = outputs;
        Definition admitted = Compile(source);
        await Assert.That(admitted.Presentation!.OutputSelector!.Options.Count).IsEqualTo(64);
        await Assert.That(admitted.Presentation.OutputSelector.Options.Sum(option => option.NextActions.Count)).IsEqualTo(4032);
        Options(source)[0]!["nextActions"]!.AsArray().Add(new JsonObject { ["targetOption"] = "option1", ["title"] = "Overflow" });
        await Reject(() => Compile(source), "next-action-limit");
        JsonObject runtime = JsonNode.Parse(admitted.CanonicalBytes.Span)!.AsObject();
        Options(runtime).Add(Options(runtime)[0]!.DeepClone());
        await Reject(() => Parse(runtime), "output-selector-limit");
    }

    private static async Task Reject(Action action, string code)
    {
        try { action(); }
        catch (DefinitionException exception)
        {
            await Assert.That(exception.Code).IsEqualTo(code);
            return;
        }
        throw new InvalidOperationException("Expected bounded admission rejection: " + code);
    }
}
