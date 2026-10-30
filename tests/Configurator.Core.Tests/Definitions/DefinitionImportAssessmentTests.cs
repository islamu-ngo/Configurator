namespace Configurator.Core.Tests.Definitions;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionImportAssessmentTests
{
    [Test]
    public async Task MissingRepresentedRequiredValueDoesNotUseInitialization()
    {
        SetupDefinition definition = Definition("""
            {"name":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"new"}}}
            """, """{"name":{"op":"copy","reference":{"scope":"root","path":"/name"}}}""", """["name"]""");
        using var assessment = Inspect(definition, "{}");
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        await Assert.That(assessment.Report.Findings.Any(f => f.Kind == DefinitionImportFindingKind.MissingRequired &&
            f.Schema?.FieldId == "name")).IsTrue();
        await Assert.That(assessment.Report.AutofillEligible).IsTrue();
    }

    [Test]
    [Arguments(false, "{}", DefinitionImportCompatibility.Incomplete)]
    [Arguments(true, "{}", DefinitionImportCompatibility.Compatible)]
    [Arguments(true, """{"tls":false}""", DefinitionImportCompatibility.Compatible)]
    [Arguments(true, """{"tls":"private-invalid-controller"}""", DefinitionImportCompatibility.ChangesRequired)]
    [Arguments(true, """{"tls":true}""", DefinitionImportCompatibility.ChangesRequired)]
    public async Task TlsControllerEvidenceCannotInventAbsence(bool mapped, string input, DefinitionImportCompatibility expected)
    {
        string members = mapped
            ? """{"tls":{"op":"copy","reference":{"scope":"root","path":"/tls"}},"cert":{"op":"copy","reference":{"scope":"root","path":"/cert"}}}"""
            : """{"cert":{"op":"copy","reference":{"scope":"root","path":"/cert"}}}""";
        SetupDefinition definition = Definition("""
            {"tls":{"type":"boolean"},"cert":{"type":"string","x-configurator":{"sensitivity":"secret",
              "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/tls"},"value":true}}}}
            """, members, """["cert"]""");
        using var assessment = Inspect(definition, input);
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(expected);
        if (!mapped)
        {
            await Assert.That(assessment.Report.CoverageComplete).IsFalse();
            await Assert.That(assessment.Report.Findings.Any(f => f.Kind == DefinitionImportFindingKind.Unassessed)).IsTrue();
        }
        await Assert.That(JsonSerializer.Serialize(assessment)).DoesNotContain("private-invalid-controller");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PropertyGuardDistinguishesKnownAbsenceFromNonObservation(bool mapped)
    {
        var source = Source("""{"mode":{"type":"string"},"cert":{"type":"string"}}""",
            mapped ? """{"mode":{"op":"copy","reference":{"scope":"root","path":"/mode"}},"cert":{"op":"copy","reference":{"scope":"root","path":"/cert"}}}"""
                : """{"cert":{"op":"copy","reference":{"scope":"root","path":"/cert"}}}""");
        source["configuration"]!["if"] = JsonNode.Parse("""{"properties":{"mode":{"const":"secure"}}}""");
        source["configuration"]!["then"] = JsonNode.Parse("""{"required":["cert"]}""");
        using var assessment = Inspect(DefinitionFixture.Parse(source.ToJsonString()), "{}");
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(mapped
            ? DefinitionImportCompatibility.ChangesRequired : DefinitionImportCompatibility.Incomplete);
        await Assert.That(assessment.Report.Findings.Any(f => f.Kind == DefinitionImportFindingKind.MissingRequired))
            .IsEqualTo(mapped);
    }

    [Test]
    public async Task PartialChildrenDoNotProveCardinalityOrUnrepresentedRequiredAbsence()
    {
        SetupDefinition definition = Definition("""
            {"options":{"type":"object","additionalProperties":false,"minProperties":2,"required":["hidden"],
              "properties":{"visible":{"type":"integer"},"hidden":{"type":"string"}}}}
            """, """{"x":{"op":"copy","reference":{"scope":"root","path":"/options/visible"}}}""");
        using var assessment = Inspect(definition, """{"x":7}""");
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.Incomplete);
        await Assert.That(assessment.Report.Findings.Any(f => f.Kind == DefinitionImportFindingKind.MissingRequired ||
            f.Kind == DefinitionImportFindingKind.InvalidSuppliedValue)).IsFalse();
        await Assert.That(assessment.Report.AutofillEligible).IsFalse();
    }

    [Test]
    public async Task UnknownStructureAndLiteralMismatchAccumulateWithoutLeakingOrAdopting()
    {
        var source = Source("""{"name":{"type":"integer"}}""", """
            {"api":{"op":"literal","value":2},"name":{"op":"copy","reference":{"scope":"root","path":"/name"}}}
            """);
        using var assessment = Inspect(DefinitionFixture.Parse(source.ToJsonString()),
            """{"api":1,"name":"private-value-sentinel","private-key-sentinel":{"nested":"private-subtree-sentinel"}}""");
        await Assert.That(assessment.Report.Findings.Select(f => f.Kind)).Contains(DefinitionImportFindingKind.UnrecognizedInput);
        await Assert.That(assessment.Report.Findings.Select(f => f.Kind)).Contains(DefinitionImportFindingKind.StructuralOutputChange);
        await Assert.That(assessment.Report.Findings.Select(f => f.Kind)).Contains(DefinitionImportFindingKind.InvalidSuppliedValue);
        await Assert.That(assessment.AssessAutofill(Empty()).Answers).IsNull();
        string safe = JsonSerializer.Serialize(assessment) + assessment + assessment.Report +
            string.Join(';', assessment.Report.Findings);
        await Assert.That(safe).DoesNotContain("sentinel");
        await Assert.That(assessment.Report.Findings.First(f => f.Kind == DefinitionImportFindingKind.UnrecognizedInput).Schema).IsNull();
    }

    [Test]
    [Arguments("""{"private-malformed-sentinel":""")]
    [Arguments("""{"x":1,"x":2}""")]
    [Arguments("""{"x":1e1000}""")]
    public async Task MalformedInputHasNoUsableCandidateOrDiagnosticContent(string input)
    {
        using var assessment = Inspect(Definition("""{"x":{"type":"integer"}}""",
            """{"x":{"op":"copy","reference":{"scope":"root","path":"/x"}}}"""), input);
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.InvalidInput);
        await Assert.That(assessment.Report.ExecutionCompleted).IsFalse();
        await Assert.That(assessment.AssessAutofill(Empty()).Answers).IsNull();
        await Assert.That(JsonSerializer.Serialize(assessment) + assessment).DoesNotContain("sentinel");
    }

    [Test]
    public async Task StructuredRepeatIndexAndFixedListRecoverIndependentLiteralAnswers()
    {
        var source = Source("""
            {"rows":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["name","count"],
              "properties":{"name":{"type":"string"},"count":{"type":"integer"}}}}}
            """, """
            {"rows":{"op":"repeat","reference":{"scope":"root","path":"/rows"},"item":{"op":"object","members":{
              "ordinal":{"op":"index"},"data":{"op":"list","items":[
                {"op":"copy","reference":{"scope":"item","path":"/name"}},
                {"op":"copy","reference":{"scope":"item","path":"/count"}}]}}}}}
            """);
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        using var assessment = Inspect(definition, """{"rows":[{"ordinal":0,"data":["a",0]},{"ordinal":1,"data":["",2]}]}""");
        DefinitionImportResult result = assessment.AssessAutofill(Empty());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.Values["rows"].GetRawText()).IsEqualTo("""[{"count":0,"name":"a"},{"count":2,"name":""}]""");
        using var wrong = Inspect(definition, """{"rows":[{"ordinal":1,"data":["a",0]}]}""");
        await Assert.That(wrong.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        await Assert.That(wrong.Report.AutofillEligible).IsFalse();
    }

    [Test]
    public async Task ExactWholeCopyPreservesNullFalseZeroEmptyAndDistinctNames()
    {
        var source = Source("""
            {"x":{"type":["string","null"]},"b":{"type":"boolean"},"n":{"type":"integer"},
            "s":{"type":"string"},"a":{"type":"array","items":{"type":"string"}},
            "map":{"type":"object","additionalProperties":{"type":"string"}}}
            """, "{}");
        source["outputs"]!["config.json"]!["projection"] = JsonNode.Parse("""{"op":"copy","reference":{"scope":"root","path":""}}""");
        using var assessment = Inspect(DefinitionFixture.Parse(source.ToJsonString()),
            """{"x":null,"b":false,"n":0,"s":"","a":[],"map":{"A":"","a":"","é":"","e\u0301":""}}""");
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.Compatible);
        var answers = assessment.AssessAutofill(Empty()).Answers!;
        await Assert.That(answers.Values["x"].ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(answers.Values["map"].EnumerateObject().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task ActualCheckedInEventManifestProvesPublicContextAndAtomicRecovery()
    {
        using Stream definitionSource = typeof(DefinitionImportAssessmentTests).Assembly.GetManifestResourceStream("EventDefinition")!;
        using var definitionBytes = new MemoryStream();
        await definitionSource.CopyToAsync(definitionBytes);
        SetupDefinition definition = SetupDefinitionCodec.ParseJson(definitionBytes.ToArray());
        using Stream expectedSource = typeof(DefinitionImportAssessmentTests).Assembly
            .GetManifestResourceStream("EventExpected.configuration-manifest-1-tenants.json")!;
        using var expectedBytes = new MemoryStream();
        await expectedSource.CopyToAsync(expectedBytes);
        byte[] input = expectedBytes.ToArray();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var assessment = DefinitionImport.Inspect(definition, "configuration-manifest", input);
        double milliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Console.WriteLine(FormattableString.Invariant(
            $"COMPARISON_METRIC cohort=event inputBytes={input.Length} allocatedBytes={allocated} elapsedMs={milliseconds:F3} work={assessment.Report.WorkUnits} inverse={assessment.Report.InverseNodes} validation={assessment.Report.ValidationNodes}"));
        await Assert.That(assessment.Report.ExecutionCompleted).IsTrue();
        await Assert.That(assessment.Report.Findings.Any(f => f.Kind == DefinitionImportFindingKind.ContextDerivedValue &&
            f.Schema?.FieldId == "operation" && f.Source is null)).IsTrue();
        DefinitionImportResult result = assessment.AssessAutofill(Empty());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.Values["operation"].GetString()).IsEqualTo("configuration-manifest");
        await Assert.That(result.Answers.Values["configuration-manifest.tenants"][0]
            .GetProperty("metadata").GetProperty("name").GetString()).IsEqualTo("community");
        await Assert.That(assessment.Report.WorkUnits).IsLessThanOrEqualTo(131_072);
    }

    [Test]
    public async Task DisposalRevokesPrivateAccessButRetainsDetachedSafeFacts()
    {
        var assessment = Inspect(Definition("""{"x":{"type":"string"}}""",
            """{"x":{"op":"copy","reference":{"scope":"root","path":"/x"}}}"""), """{"x":"private-sentinel"}""");
        DefinitionImportReport report = assessment.Report;
        assessment.Dispose();
        await Assert.That(() => assessment.AssessAutofill(Empty())).Throws<ObjectDisposedException>();
        await Assert.That(JsonSerializer.Serialize(report)).DoesNotContain("private-sentinel");
    }

    [Test]
    public async Task PreCancelledInspectionPublishesNothing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(() => DefinitionImport.Inspect(Definition("""{"x":{"type":"string"}}""",
            """{"x":{"op":"copy","reference":{"scope":"root","path":"/x"}}}"""), "config", "{}"u8.ToArray(),
            cancellation.Token)).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task CompleteThreeThousandFindingInventoryIsNotDisplayTruncation()
    {
        SetupDefinition definition = Definition("""{"items":{"type":"array","items":{"type":"integer"}}}""",
            """{"items":{"op":"copy","reference":{"scope":"root","path":"/items"}}}""");
        string input = JsonSerializer.Serialize(new { items = Enumerable.Repeat("private-sentinel", 3000).ToArray() });
        using var assessment = Inspect(definition, input);
        await Assert.That(assessment.Report.InventoryComplete).IsTrue();
        await Assert.That(assessment.Report.Findings.Count(f => f.Kind == DefinitionImportFindingKind.InvalidSuppliedValue)).IsEqualTo(3000);
        await Assert.That(assessment.Report.TotalFindingCount).IsEqualTo(assessment.Report.RetainedFindingCount);
        await Assert.That(assessment.Report.Findings[^1].Schema!.RelativePath.Count).IsEqualTo(0);
        await Assert.That(assessment.Report.Findings[^1].Source!.Ordinals[^1]).IsEqualTo(2999);
        await Assert.That(JsonSerializer.Serialize(assessment.Report)).DoesNotContain("private-sentinel");
    }

    [Test]
    public async Task RetentionStopKeepsKnownBlockersAndReportsOnlyALowerBound()
    {
        SetupDefinition definition = Definition("""
            {"a":{"type":"array","items":{"type":"integer"}},"b":{"type":"array","items":{"type":"integer"}}}
            """, """
            {"a":{"op":"copy","reference":{"scope":"root","path":"/a"}},"b":{"op":"copy","reference":{"scope":"root","path":"/b"}}}
            """);
        using var assessment = Inspect(definition, JsonSerializer.Serialize(new
        {
            a = Enumerable.Repeat("invalid", 2500).ToArray(),
            b = Enumerable.Repeat("invalid", 2500).ToArray()
        }));
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        await Assert.That(assessment.Report.InventoryComplete).IsFalse();
        await Assert.That(assessment.Report.CoverageComplete).IsFalse();
        await Assert.That(assessment.Report.TotalFindingCount).IsNull();
        await Assert.That(assessment.Report.RetainedFindingCount).IsEqualTo(4096);
        await Assert.That(assessment.Report.AutofillEligible).IsFalse();
        await Assert.That(assessment.AssessAutofill(Empty()).Answers).IsNull();
    }

    [Test]
    public async Task CombinedTraversalStopDoesNotEraseAnAlreadyObservedInvalidValue()
    {
        var properties = new JsonObject();
        for (int index = 0; index < 8; index++) properties["p" + index] = JsonNode.Parse("""{"type":"integer"}""");
        var source = Source("""{"rows":{"type":"array","items":{"type":"object","additionalProperties":false,"properties":{}}}}""",
            """{"rows":{"op":"copy","reference":{"scope":"root","path":"/rows"}}}""");
        source["configuration"]!["properties"]!["rows"]!["items"]!["properties"] = properties;
        var rows = new JsonArray();
        for (int index = 0; index < 4096; index++)
        {
            var row = new JsonObject();
            for (int property = 0; property < 8; property++) row["p" + property] = 1;
            if (index == 0) row["p0"] = "private-invalid";
            rows.Add(row);
        }
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        byte[] input = System.Text.Encoding.UTF8.GetBytes(new JsonObject { ["rows"] = rows }.ToJsonString());
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var assessment = DefinitionImport.Inspect(definition, "config", input);
        double milliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Console.WriteLine(FormattableString.Invariant(
            $"COMPARISON_METRIC cohort=maximum-repeated inputBytes={input.Length} allocatedBytes={allocated} elapsedMs={milliseconds:F3} work={assessment.Report.WorkUnits} inverse={assessment.Report.InverseNodes} validation={assessment.Report.ValidationNodes}"));
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        await Assert.That(assessment.Report.InventoryComplete).IsFalse();
        await Assert.That(assessment.Report.WorkUnits).IsLessThanOrEqualTo(131_072);
        await Assert.That(assessment.Report.InverseNodes).IsLessThanOrEqualTo(65_536);
        await Assert.That(assessment.Report.ValidationNodes).IsLessThanOrEqualTo(65_536);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PartialRootCountsCannotClaimCompatibleOrHideAProvedMaximumViolation(bool exceeds)
    {
        JsonNode source = Source("""{"a":{"type":"integer"},"b":{"type":"integer"},"c":{"type":"integer"}}""",
            """{"a":{"op":"copy","reference":{"scope":"root","path":"/a"}},"b":{"op":"copy","reference":{"scope":"root","path":"/b"}}}""");
        source["configuration"]!["maxProperties"] = 1;
        using var assessment = Inspect(DefinitionFixture.Parse(source.ToJsonString()),
            exceeds ? """{"a":1,"b":2}""" : """{"a":1}""");

        await Assert.That(assessment.Report.CoverageComplete).IsFalse();
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(exceeds
            ? DefinitionImportCompatibility.ChangesRequired : DefinitionImportCompatibility.Incomplete);
        await Assert.That(assessment.Report.Findings.Any(finding =>
            finding.Kind == DefinitionImportFindingKind.InvalidSuppliedValue)).IsEqualTo(exceeds);
    }

    [Test]
    public async Task ProvedMissingRequiredSurvivesALaterEnumBudgetStop()
    {
        JsonNode source = Source("""
            {"absent":{"type":"integer"},"items":{"type":"array","items":{"type":"string"}}}
            """, """
            {"absent":{"op":"copy","reference":{"scope":"root","path":"/absent"}},
             "items":{"op":"copy","reference":{"scope":"root","path":"/items"}}}
            """);
        source["configuration"]!["required"] = new JsonArray("absent");
        var choices = new JsonArray();
        for (int index = 0; index < 64; index++) choices.Add("choice-" + index);
        source["configuration"]!["properties"]!["items"]!["items"]!["enum"] = choices;
        using var assessment = Inspect(DefinitionFixture.Parse(source.ToJsonString()),
            JsonSerializer.Serialize(new { items = Enumerable.Repeat("choice-63", 4096).ToArray() }));

        await Assert.That(assessment.Report.InventoryComplete).IsFalse();
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        await Assert.That(assessment.Report.Findings.Any(finding =>
            finding.Kind == DefinitionImportFindingKind.MissingRequired && finding.Schema?.FieldId == "absent")).IsTrue();
        await Assert.That(assessment.AssessAutofill(Empty()).Succeeded).IsFalse();
    }

    [Test]
    public async Task InvalidVariantCannotActivateARequiredDependentByExistence()
    {
        JsonNode source = Source("""
            {"control":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
              {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                "kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}}}},
              {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                "kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}}}}
            ]},
            "dependent":{"type":"integer","x-configurator":{"applicableWhen":{
              "op":"exists","reference":{"scope":"root","path":"/control"}}}}}
            """, """
            {"control":{"op":"copy","reference":{"scope":"root","path":"/control"}},
             "dependent":{"op":"copy","reference":{"scope":"root","path":"/dependent"}}}
            """);
        source["configuration"]!["required"] = new JsonArray("dependent");
        using var assessment = Inspect(DefinitionFixture.Parse(source.ToJsonString()),
            """{"control":{"kind":"private-invalid-variant"}}""");

        await Assert.That(assessment.Report.Findings.Any(finding =>
            finding.Kind == DefinitionImportFindingKind.InvalidSuppliedValue)).IsTrue();
        await Assert.That(assessment.Report.Findings.Any(finding =>
            finding.Kind == DefinitionImportFindingKind.MissingRequired)).IsFalse();
        await Assert.That(assessment.Report.Findings.Any(finding =>
            finding.Kind == DefinitionImportFindingKind.Unassessed)).IsTrue();
    }

    [Test]
    public async Task RefusedAutofillNeverReturnsFutureWorkspaceCorrectionAuthority()
    {
        using var assessment = Inspect(Definition("""
            {"rows":{"type":"array","items":{"type":"object","additionalProperties":false,
              "properties":{"count":{"type":"integer"}}}}}
            """, """{"rows":{"op":"copy","reference":{"scope":"root","path":"/rows"}}}"""),
            """{"rows":[{"count":"private-invalid-value"}]}""");
        DefinitionImportResult result = assessment.AssessAutofill(Empty());

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Answers).IsNull();
        await Assert.That(result.Diagnostic).IsNotNull();
        await Assert.That(result.Diagnostic!.FieldId).IsEqualTo("rows");
        await Assert.That(result.Diagnostic.Location).IsNull();
        await Assert.That(result.Schema!.FieldId).IsEqualTo("rows");
        await Assert.That(result.Schema.RelativePath.Count).IsEqualTo(0);
        await Assert.That(JsonSerializer.Serialize(result)).DoesNotContain("private-invalid-value");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InvalidMixedMapEntriesKeepDistinctSafeSourceOrdinals(bool root)
    {
        JsonNode source = Source("""
            {"map":{"type":"object","properties":{"declared":{"type":"string"}},
              "additionalProperties":{"type":"integer"}}}
            """, """{"map":{"op":"copy","reference":{"scope":"root","path":"/map"}}}""");
        if (root)
        {
            source["configuration"] = source["configuration"]!["properties"]!["map"]!.DeepClone();
            source["outputs"]!["config.json"]!["projection"] = JsonNode.Parse("""
                {"op":"copy","reference":{"scope":"root","path":""}}
                """);
        }
        using var assessment = Inspect(DefinitionFixture.Parse(source.ToJsonString()), root
            ? """{"declared":"public-metadata","private-z":"invalid","private-a":"invalid"}"""
            : """{"map":{"declared":"public-metadata","private-z":"invalid","private-a":"invalid"}}""");
        DefinitionImportFinding[] findings = assessment.Report.Findings.Where(finding =>
            finding.Kind == DefinitionImportFindingKind.InvalidSuppliedValue).ToArray();

        await Assert.That(findings.Length).IsEqualTo(2);
        await Assert.That(findings[0].Source!.Ordinals.SequenceEqual(root ? [1] : [0, 1])).IsTrue();
        await Assert.That(findings[1].Source!.Ordinals.SequenceEqual(root ? [2] : [0, 2])).IsTrue();
        await Assert.That(findings.All(finding => root
            ? finding.Schema is null : finding.Schema?.FieldId == "map" && finding.Schema.RelativePath.Count == 0)).IsTrue();
        await Assert.That(JsonSerializer.Serialize(assessment.Report)).DoesNotContain("private-");
    }

    [Test]
    public async Task DotenvTypedStructuralLiteralsAcceptTheirIndependentExportRepresentation()
    {
        JsonNode source = Source("""{"value":{"type":"string"}}""", """
            {"VERSION":{"op":"literal","value":2},"ENABLED":{"op":"literal","value":true},
             "VALUE":{"op":"copy","reference":{"scope":"root","path":"/value"}}}
            """);
        source["outputs"]!["config.json"]!["format"] = "dotenv";
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        using var assessment = DefinitionImport.Inspect(definition, "config",
            "VERSION=2\nENABLED=true\nVALUE=sample\n"u8.ToArray());

        await Assert.That(DefinitionImport.GetCapability(definition, "config").InspectionSupported).IsTrue();
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.Compatible);
        DefinitionImportResult result = assessment.AssessAutofill(Empty());
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Answers!.Values["value"].GetString()).IsEqualTo("sample");
        using var changed = DefinitionImport.Inspect(definition, "config",
            "VERSION=3\nENABLED=true\nVALUE=sample\n"u8.ToArray());
        await Assert.That(changed.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.ChangesRequired);
        await Assert.That(changed.AssessAutofill(Empty()).Succeeded).IsFalse();
    }

    [Test]
    public async Task RepeatedEnumComparisonsConsumeTheCombinedTraversalBudget()
    {
        JsonNode source = Source("""{"items":{"type":"array","items":{"type":"string"}}}""",
            """{"items":{"op":"copy","reference":{"scope":"root","path":"/items"}}}""");
        var choices = new JsonArray();
        for (int index = 0; index < 64; index++) choices.Add("choice-" + index);
        source["configuration"]!["properties"]!["items"]!["items"]!["enum"] = choices;
        SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
        using var assessment = Inspect(definition,
            JsonSerializer.Serialize(new { items = Enumerable.Repeat("choice-63", 4096).ToArray() }));

        await Assert.That(assessment.Report.InventoryComplete).IsFalse();
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.Incomplete);
        await Assert.That(assessment.Report.AutofillEligible).IsFalse();
        await Assert.That(assessment.Report.WorkUnits).IsLessThanOrEqualTo(131_072);
    }

    [Test]
    public async Task MissingOptionalContainerIsNotCreatedByProjectedInitializedDescendants()
    {
        SetupDefinition definition = Definition("""
            {"options":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
              "name":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"new"}}}}}
            """, """{"name":{"op":"copy","reference":{"scope":"root","path":"/options/name"}}}""");
        using var assessment = Inspect(definition, "{}");
        await Assert.That(assessment.Report.Findings.Any(f => f.Kind == DefinitionImportFindingKind.MissingRequired)).IsFalse();
        await Assert.That(assessment.AssessAutofill(Empty()).Answers!.Values.ContainsKey("options")).IsFalse();
    }

    [Test]
    public async Task AmbiguousFixedListFilteredRepeatAndSecretSelectorRefuse()
    {
        var source = Source("""
            {"mode":{"type":"string","enum":["go"],"x-configurator":{"sensitivity":"secret"}},"x":{"type":"string"},
             "rows":{"type":"array","items":{"type":"object","additionalProperties":false,
               "properties":{"state":{"type":"string"}}}}}
            """, "{}");
        foreach (string projection in new[]
        {
            """{"op":"object","members":{"v":{"op":"list","items":[{"op":"copy","reference":{"scope":"root","path":"/x"}},{"op":"literal","value":1}]}}}""",
            """{"op":"object","members":{"v":{"op":"repeat","reference":{"scope":"root","path":"/rows"},"item":{"op":"copy","reference":{"scope":"item","path":""},"when":{"op":"equals","reference":{"scope":"item","path":"/state"},"value":"keep"}}}}}""",
            """{"op":"object","members":{"v":{"op":"copy","reference":{"scope":"root","path":"/x"}}},"when":{"op":"equals","reference":{"scope":"root","path":"/mode"},"value":"go"}}"""
        })
        {
            source["outputs"]!["config.json"]!["projection"] = JsonNode.Parse(projection);
            SetupDefinition definition = DefinitionFixture.Parse(source.ToJsonString());
            await Assert.That(DefinitionImport.GetCapability(definition, "config").InspectionSupported).IsFalse();
            using var assessment = Inspect(definition, "{}");
            await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.Unsupported);
        }
    }

    [Test]
    public async Task NestedObservationDoesNotMakeAnUnrepresentedSiblingControllerFalse()
    {
        SetupDefinition definition = Definition("""
            {"options":{"type":"object","additionalProperties":false,"properties":{"tls":{"type":"boolean"},"name":{"type":"string"}}},
             "cert":{"type":"string","x-configurator":{"applicableWhen":{
               "op":"not","operands":[{"op":"equals","reference":{"scope":"root","path":"/options/tls"},"value":false}]}}}}
            """, """
            {"name":{"op":"copy","reference":{"scope":"root","path":"/options/name"}},
             "cert":{"op":"copy","reference":{"scope":"root","path":"/cert"}}}
            """, """["cert"]""");
        using var assessment = Inspect(definition, """{"name":"example"}""");
        await Assert.That(assessment.Report.Compatibility).IsEqualTo(DefinitionImportCompatibility.Incomplete);
        await Assert.That(assessment.Report.Findings.Any(f => f.Schema?.FieldId == "cert" &&
            f.Kind == DefinitionImportFindingKind.Unassessed)).IsTrue();
        await Assert.That(assessment.Report.Findings.Any(f => f.Kind == DefinitionImportFindingKind.MissingRequired)).IsFalse();
    }

    private static DefinitionImportAssessment Inspect(SetupDefinition definition, string input) =>
        DefinitionImport.Inspect(definition, "config", Encoding.UTF8.GetBytes(input));

    private static SetupAnswerSnapshot Empty() => new(0, new Dictionary<string, JsonElement>());

    private static SetupDefinition Definition(string properties, string members, string required = "[]")
    {
        JsonNode source = Source(properties, members);
        source["configuration"]!["required"] = JsonNode.Parse(required);
        return DefinitionFixture.Parse(source.ToJsonString());
    }

    private static JsonNode Source(string properties, string members)
    {
        JsonNode source = JsonNode.Parse("""
            {"$schema":"urn:islamu:configurator:authoring:v2",
             "identity":{"familyId":"2b51dfa0-024c-4d9b-a5ae-e38da01ec862","revision":1},
             "project":{"id":"example","name":"Example","publisher":"example"},
             "applicationVersions":["1"],"entryPoints":["setup"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{}},
             "outputs":{"config.json":{"id":"config","format":"json","projection":{"op":"object","members":{}}}}}
            """)!;
        source["configuration"]!["properties"] = JsonNode.Parse(properties);
        source["outputs"]!["config.json"]!["projection"]!["members"] = JsonNode.Parse(members);
        return source;
    }
}
