namespace Configurator.Presentation.Tests;

using System.Text.Json;
using System.Diagnostics;
using Configurator.Core.Definitions;

public sealed class DefinitionCorrectionFactsTests
{
    private static readonly decimal[] ExpectedBounds = [1m, 9m];

    [Test]
    public async Task ArrayRowsDoNotOfferAnUnrepresentableReset()
    {
        using var form = Form();
        using JsonDocument rows = JsonDocument.Parse("""[{"count":4}]""");
        await Assert.That(form.SetValue(["rows"], rows.RootElement)).IsTrue();
        await Assert.That(form.GetFieldValidation(DefinitionPath.FromSegments(["rows", "0"])).CanReset).IsFalse();
    }

    [Test]
    [Arguments("protectedRows", """[{"mode":"public"}]""", "0")]
    [Arguments("protectedMap", """{"synthetic":{"mode":"public"}}""", "synthetic")]
    [Arguments("choice", """{"kind":"public","mode":"public"}""", "")]
    public async Task StaticProtectionMasksContainersWhileUnmaskingPublicLeaves(
        string field, string value, string entry)
    {
        using var form = Form();
        using JsonDocument supplied = JsonDocument.Parse(value);
        await Assert.That(form.SetValue(new[] { field }, supplied.RootElement)).IsTrue();

        DefinitionFieldValidation rootFacts = form.GetFieldValidation([field]);
        await Assert.That(rootFacts.RequiresMasking).IsTrue();
        await Assert.That(rootFacts.HasProtectedDescendants).IsTrue();
        await Assert.That(rootFacts.CanApplySuggestion).IsFalse();
        await Assert.That(rootFacts.AllowedValues.Count).IsEqualTo(0);

        if (entry.Length > 0)
        {
            DefinitionFieldValidation entryFacts = form.GetFieldValidation([field, entry]);
            await Assert.That(entryFacts.RequiresMasking).IsTrue();
            await Assert.That(entryFacts.HasProtectedDescendants).IsTrue();
            await Assert.That(entryFacts.CanApplySuggestion).IsFalse();
            await Assert.That(entryFacts.AllowedValues.Count).IsEqualTo(0);
        }

        DefinitionPath leafPath = DefinitionPath.FromSegments(entry.Length == 0
            ? [field, "mode"] : [field, entry, "mode"]);
        DefinitionFieldValidation leafFacts = form.GetFieldValidation(leafPath);
        await Assert.That(leafFacts.RequiresMasking).IsFalse();
        await Assert.That(leafFacts.HasProtectedDescendants).IsFalse();
        await Assert.That(leafFacts.CanApplySuggestion).IsTrue();
        await Assert.That(leafFacts.AllowedValues.Count).IsEqualTo(2);
    }

    [Test]
    public async Task LiteralCollectionFactsReuseTheCurrentValidation()
    {
        using var form = Form();
        using JsonDocument rows = JsonDocument.Parse("""[{"count":4}]""");
        await Assert.That(form.SetValue(["rows"], rows.RootElement)).IsTrue();
        _ = form.Validate();
        long revision = form.InputRevision;
        using var activity = new Activity(nameof(LiteralCollectionFactsReuseTheCurrentValidation));
        activity.Start();
        using var counter = new ValidationCounter(activity);
        _ = DefinitionValidator.Validate(form.Definition, form.Snapshot());
        await Assert.That(counter.Count).IsEqualTo(1);
        await Assert.That(form.GetFieldValidation(DefinitionPath.FromSegments(["rows", "0", "count"])).CanEdit).IsTrue();
        await Assert.That(counter.Count).IsEqualTo(1);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    public async Task AbsentContainerActivityReusesTheCurrentValidation()
    {
        using var form = Form();
        using JsonDocument rows = JsonDocument.Parse("""[{"count":4}]""");
        await Assert.That(form.SetValue(["rows"], rows.RootElement)).IsTrue();
        _ = form.Validate();
        using var activity = new Activity(nameof(AbsentContainerActivityReusesTheCurrentValidation));
        activity.Start();
        using var counter = new ValidationCounter(activity);
        _ = DefinitionValidator.Validate(form.Definition, form.Snapshot());
        await Assert.That(counter.Count).IsEqualTo(1);
        DefinitionPath path = DefinitionPath.FromSegments(["rows", "0", "settings", "caption"]);
        _ = form.IsEditorVisible(path);
        await Assert.That(form.GetFieldValidation(path).CanEdit).IsTrue();
        await Assert.That(counter.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("Conformance.RootMap", "{}")]
    [Arguments("Conformance.RootVariant", """{"mode":"disk","directory":"spool","endpoint":"queue.example.invalid"}""")]
    public async Task RootStructuralDiagnosticsRetainCorrectionReasons(string resource, string values)
    {
        using Stream source = typeof(DefinitionCorrectionFactsTests).Assembly.GetManifestResourceStream(resource)!;
        using var bytes = new MemoryStream();
        source.CopyTo(bytes);
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(bytes.ToArray()));
        using JsonDocument supplied = JsonDocument.Parse(values);
        await Assert.That(form.SetValue(DefinitionPath.FromSegments([]), supplied.RootElement)).IsTrue();
        await Assert.That(form.Validate().IsReady).IsFalse();
        await Assert.That(form.GetFieldValidation(DefinitionPath.FromSegments([])).Reasons
            .Contains(DefinitionFieldCorrectionReason.Constraint)).IsTrue();
    }

    [Test]
    public async Task OrdinaryCorrectionRetainsAuthoredBoundsWithoutRetainingAnswer()
    {
        using var form = Form();
        form.EditValue(["count"], "999");
        var facts = form.GetFieldValidation(["count"]);
        await Assert.That(facts.Reasons.Count > 0).IsTrue();
        await Assert.That(facts.Bounds.Select(bound => bound.Value).ToArray()).IsEquivalentTo(ExpectedBounds);
        await Assert.That(PresentationStrings.FieldCorrection(facts, "en")).DoesNotContain("999");
        await Assert.That(PresentationStrings.FieldCorrection(facts, "ar")).DoesNotContain("999");
        await Assert.That(PresentationStrings.FieldCorrection(facts, "ar"))
            .IsNotEqualTo(PresentationStrings.FieldCorrection(facts, "en"));
        form.EditValue(["count"], "4");
        await Assert.That(form.GetFieldValidation(["count"]).Reasons.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RequiredSiblingReasonsFollowTheCurrentLiteralRevision()
    {
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"a2ca7208-a116-48bb-9899-11c9260138cb","revision":1},
             "project":{"id":"required-sibling-facts","name":"Required sibling facts"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,"required":["settings"],
               "x-configurator":{"sensitivity":"public"},"properties":{"settings":{
               "type":"object","additionalProperties":false,"required":["first","second"],
               "x-configurator":{"sensitivity":"public"},"properties":{
                 "first":{"type":"integer","maximum":9,"x-configurator":{"sensitivity":"public"}},
                 "second":{"type":"integer","maximum":9,"x-configurator":{"sensitivity":"public"}}}}}},
             "outputs":{"config.json":{"format":"json"}}}
            """u8.ToArray()));
        using JsonDocument supplied = JsonDocument.Parse("""{"settings":{}}""");
        await Assert.That(form.SetValue(DefinitionPath.FromSegments([]), supplied.RootElement)).IsTrue();
        DefinitionPath first = DefinitionPath.FromSegments(["settings", "first"]);
        DefinitionPath second = DefinitionPath.FromSegments(["settings", "second"]);
        DefinitionFieldValidation earlier = form.GetFieldValidation(first);

        await Assert.That(earlier.Reasons.SequenceEqual([DefinitionFieldCorrectionReason.Required])).IsTrue();
        await Assert.That(form.EditValue(first, "4")).IsTrue();
        await Assert.That(form.GetFieldValidation(first).Reasons.Count).IsEqualTo(0);
        await Assert.That(form.GetFieldValidation(second).Reasons
            .SequenceEqual([DefinitionFieldCorrectionReason.Required])).IsTrue();
        await Assert.That(form.EditValue(second, "not-a-number")).IsFalse();
        await Assert.That(form.GetFieldValidation(second).Reasons.Contains(DefinitionFieldCorrectionReason.Type)).IsTrue();
        await Assert.That(form.GetFieldValidation(first).Reasons.Count).IsEqualTo(0);
        await Assert.That(form.EditValue(second, "4")).IsTrue();
        await Assert.That(form.GetFieldValidation(second).Reasons.Count).IsEqualTo(0);
        await Assert.That(earlier.Reasons.SequenceEqual([DefinitionFieldCorrectionReason.Required])).IsTrue();
    }

    [Test]
    public async Task MapCorrectionReasonsFollowNamesAfterEntryOrderChanges()
    {
        using var form = Form();
        using JsonDocument before = JsonDocument.Parse("""{"first":99,"second":3}""");
        await Assert.That(form.SetValue(["map"], before.RootElement)).IsTrue();
        DefinitionPath first = DefinitionPath.FromSegments(["map", "first"]);
        DefinitionPath second = DefinitionPath.FromSegments(["map", "second"]);
        DefinitionFieldValidation earlier = form.GetFieldValidation(first);
        await Assert.That(earlier.Reasons.SequenceEqual([DefinitionFieldCorrectionReason.Constraint])).IsTrue();
        await Assert.That(form.GetFieldValidation(second).Reasons.Count).IsEqualTo(0);

        using JsonDocument after = JsonDocument.Parse("""{"second":99,"first":3}""");
        await Assert.That(form.SetValue(["map"], after.RootElement)).IsTrue();
        await Assert.That(form.GetFieldValidation(first).Reasons.Count).IsEqualTo(0);
        await Assert.That(form.GetFieldValidation(second).Reasons
            .SequenceEqual([DefinitionFieldCorrectionReason.Constraint])).IsTrue();
        await Assert.That(earlier.Reasons.SequenceEqual([DefinitionFieldCorrectionReason.Constraint])).IsTrue();
    }

    [Test]
    public async Task RemovedRowCorrectionReasonsDoNotFollowTheSurvivingRow()
    {
        using var form = Form();
        using JsonDocument rows = JsonDocument.Parse("""[{"count":99},{"count":3}]""");
        await Assert.That(form.SetValue(["rows"], rows.RootElement)).IsTrue();
        DefinitionPath first = DefinitionPath.FromSegments(["rows", "0", "count"]);
        DefinitionPath second = DefinitionPath.FromSegments(["rows", "1", "count"]);
        DefinitionFieldValidation earlier = form.GetFieldValidation(first);
        await Assert.That(earlier.Reasons.SequenceEqual([DefinitionFieldCorrectionReason.Constraint])).IsTrue();
        await Assert.That(form.GetFieldValidation(second).Reasons.Count).IsEqualTo(0);

        await Assert.That(form.RemoveListItem(["rows"], 0)).IsTrue();
        await Assert.That(form.GetFieldValidation(first).Reasons.Count).IsEqualTo(0);
        await Assert.That(form.GetFieldValidation(second).CanEdit).IsFalse();
        await Assert.That(earlier.Reasons.SequenceEqual([DefinitionFieldCorrectionReason.Constraint])).IsTrue();
    }

    [Test]
    public async Task ReadonlyNullableAndUnknownLocationsDenyAffordances()
    {
        using var form = Form();
        foreach (string name in new[] { "fixed", "missing" })
        {
            var facts = form.GetFieldValidation(new[] { name });
            await Assert.That(facts.CanEdit).IsFalse();
            await Assert.That(facts.CanSetNull).IsFalse();
            await Assert.That(facts.CanReset).IsFalse();
            await Assert.That(facts.CanApplySuggestion).IsFalse();
        }
        await Assert.That(form.CanEdit(["fixed"])).IsFalse();
    }

    [Test]
    public async Task ProtectedContainerMasksItselfWhileUnmaskingPublicSiblingRecommendations()
    {
        using var form = Form();
        var containerFacts = form.GetFieldValidation(["owner"]);
        await Assert.That(containerFacts.RequiresMasking).IsTrue();
        await Assert.That(containerFacts.HasProtectedDescendants).IsTrue();
        await Assert.That(containerFacts.CanApplySuggestion).IsFalse();
        await Assert.That(containerFacts.AllowedValues.Count).IsEqualTo(0);

        var siblingFacts = form.GetFieldValidation(["owner", "mode"]);
        await Assert.That(siblingFacts.RequiresMasking).IsFalse();
        await Assert.That(siblingFacts.HasProtectedDescendants).IsFalse();
        await Assert.That(siblingFacts.CanApplySuggestion).IsTrue();
        await Assert.That(siblingFacts.AllowedValues.Count).IsEqualTo(2);
        await Assert.That(JsonSerializer.Serialize(siblingFacts)).Contains("synthetic-suggestion");
        await Assert.That(form.GetFieldValidation(["count"]).CanApplySuggestion).IsTrue();
        await Assert.That(form.GetFieldValidation(DefinitionPath.Parse("/count")).CanApplySuggestion).IsTrue();
    }

    [Test]
    public async Task DeclaredNonPublicAncestorCannotBeDemotedByPublicChildAndUnresolvedPathsFailClosed()
    {
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151617","revision":1},
             "project":{"id":"declared-masking","name":"DeclaredMasking"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
               "declaredSensitive":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive"},"properties":{
                 "publicChild":{"type":"string","enum":["synthetic-alpha","synthetic-beta"],
                   "x-configurator":{"sensitivity":"public"}}}},
               "defaultedSensitive":{"type":"object","additionalProperties":false,"properties":{
                 "publicChild":{"type":"string","enum":["synthetic-gamma","synthetic-delta"],
                   "x-configurator":{"sensitivity":"public"}}}},
               "sensitiveRows":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{
                 "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive"},"properties":{
                   "publicChild":{"type":"string","enum":["synthetic-row-a","synthetic-row-b"],
                     "x-configurator":{"sensitivity":"public"}}}}},
               "branchVariant":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
                 {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                   "kind":{"type":"string","const":"priv","x-configurator":{"sensitivity":"public"}},
                   "locked":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"sensitive"},"properties":{
                     "mode":{"type":"string","enum":["priv-a","priv-b"],
                       "x-configurator":{"sensitivity":"public"}}}},
                   "pubSibling":{"type":"string","enum":["sib-a","sib-b"],
                     "x-configurator":{"sensitivity":"public","suggestedValue":"sib-a"}}}},
                 {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                   "kind":{"type":"string","const":"pub","x-configurator":{"sensitivity":"public"}},
                   "pubMode":{"type":"string","enum":["pub-a","pub-b"],
                     "x-configurator":{"sensitivity":"public","suggestedValue":"pub-a"}}}}]}
             }},"outputs":{"config.json":{"format":"json"}}}
            """u8.ToArray()));

        foreach (string[] path in new string[][] { ["declaredSensitive", "publicChild"], ["defaultedSensitive", "publicChild"] })
        {
            DefinitionFieldValidation facts = form.GetFieldValidation(path);
            await Assert.That(facts.RequiresMasking).IsTrue();
            await Assert.That(facts.HasProtectedDescendants).IsTrue();
            await Assert.That(facts.CanApplySuggestion).IsFalse();
            await Assert.That(facts.AllowedValues.Count).IsEqualTo(0);
            string serialized = JsonSerializer.Serialize(facts);
            await Assert.That(serialized).DoesNotContain("synthetic-alpha");
            await Assert.That(serialized).DoesNotContain("synthetic-gamma");
        }

        using (JsonDocument rowDoc = JsonDocument.Parse("""[{"publicChild":"synthetic-row-a"}]"""))
            await Assert.That(form.SetValue(["sensitiveRows"], rowDoc.RootElement)).IsTrue();
        DefinitionFieldValidation rowFacts = form.GetFieldValidation(["sensitiveRows", "0", "publicChild"]);
        await Assert.That(rowFacts.RequiresMasking).IsTrue();
        await Assert.That(rowFacts.CanApplySuggestion).IsFalse();
        await Assert.That(rowFacts.AllowedValues.Count).IsEqualTo(0);

        await Assert.That(form.SelectVariant(["branchVariant"], 0)).IsTrue();
        DefinitionFieldValidation privBranchFacts = form.GetFieldValidation(["branchVariant", "locked", "mode"]);
        await Assert.That(privBranchFacts.RequiresMasking).IsTrue();
        await Assert.That(privBranchFacts.CanApplySuggestion).IsFalse();
        await Assert.That(privBranchFacts.AllowedValues.Count).IsEqualTo(0);
        await Assert.That(JsonSerializer.Serialize(privBranchFacts)).DoesNotContain("priv-a");

        DefinitionFieldValidation branchPublicSiblingFacts = form.GetFieldValidation(["branchVariant", "pubSibling"]);
        await Assert.That(branchPublicSiblingFacts.RequiresMasking).IsFalse();
        await Assert.That(branchPublicSiblingFacts.CanApplySuggestion).IsTrue();
        await Assert.That(branchPublicSiblingFacts.AllowedValues.Count).IsEqualTo(2);

        await Assert.That(form.SelectVariant(["branchVariant"], 1)).IsTrue();
        DefinitionFieldValidation pubBranchFacts = form.GetFieldValidation(["branchVariant", "pubMode"]);
        await Assert.That(pubBranchFacts.RequiresMasking).IsFalse();
        await Assert.That(pubBranchFacts.CanApplySuggestion).IsTrue();
        await Assert.That(pubBranchFacts.AllowedValues.Count).IsEqualTo(2);

        foreach (string[] missing in new string[][] { ["declaredSensitive", "missing"], ["branchVariant", "missing"] })
        {
            DefinitionFieldValidation unresolved = form.GetFieldValidation(missing);
            await Assert.That(unresolved.RequiresMasking).IsTrue();
            await Assert.That(unresolved.CanApplySuggestion).IsFalse();
        }
    }

    [Test]
    public async Task LiteralMapCoordinatesAndRootProduceValueSafeFacts()
    {
        using var form = Form();
        form.SetValue(["map"], JsonSerializer.SerializeToElement(new Dictionary<string, int> { ["synthetic-private-key"] = 999 }));
        var facts = form.GetFieldValidation(DefinitionPath.FromSegments(["map", "synthetic-private-key"]));
        await Assert.That(facts.Reasons.Count > 0).IsTrue();
        await Assert.That(JsonSerializer.Serialize(facts)).DoesNotContain("synthetic-private-key");
        await Assert.That(JsonSerializer.Serialize(facts)).DoesNotContain("999");
        await Assert.That(form.GetFieldValidation(DefinitionPath.FromSegments([])).CanEdit).IsTrue();
    }

    [Test]
    public async Task BrowserConsentControlsSecretAffordancesWithoutPublishingSuggestion()
    {
        using var form = Form(DefinitionSessionPrivacy.BrowserNoSecrets);
        await Assert.That(form.SupplyObject(["owner"])).IsTrue();
        await Assert.That(form.GetFieldValidation(["owner"]).RequiresMasking).IsTrue();
        await Assert.That(form.GetFieldValidation(["owner"]).CanApplySuggestion).IsFalse();
        await Assert.That(form.GetFieldValidation(["owner", "token"]).CanEdit).IsFalse();
        await Assert.That(form.GetFieldValidation(["owner", "token"]).CanApplySuggestion).IsFalse();
        form.SetSecretEntryEnabled(true);
        await Assert.That(form.GetFieldValidation(["owner", "token"]).CanEdit).IsTrue();
        await Assert.That(form.GetFieldValidation(["owner", "token"]).CanApplySuggestion).IsFalse();
        await Assert.That(form.GetFieldValidation(["owner", "mode"]).RequiresMasking).IsFalse();
        await Assert.That(form.GetFieldValidation(["owner", "mode"]).CanApplySuggestion).IsTrue();
    }

    [Test]
    public async Task ActivationChangesRetireSuggestionAndNullAffordancesWithoutLosingAnswers()
    {
        using var form = Form();
        form.EditValue(["count"], "4");
        await Assert.That(form.GetFieldValidation(["count"]).CanApplySuggestion).IsTrue();
        form.EditValue(["enabled"], "false");
        var inactive = form.GetFieldValidation(["count"]);
        await Assert.That(form.CanEdit(["count"])).IsFalse();
        await Assert.That(inactive.CanApplySuggestion).IsFalse();
        await Assert.That(inactive.CanSetNull).IsFalse();
        await Assert.That(form.Snapshot().Values["count"].GetInt32()).IsEqualTo(4);
        form.EditValue(["enabled"], "true");
        await Assert.That(form.GetFieldValidation(["count"]).CanEdit).IsTrue();
    }

    private static DefinitionFormModel Form(DefinitionSessionPrivacy privacy = DefinitionSessionPrivacy.NativeComplete) =>
        new(DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151616","revision":1},
             "project":{"id":"correction-facts","name":"Corrections"},"applicationVersions":["1"],
             "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
               "enabled":{"type":"boolean","x-configurator":{"sensitivity":"public","initialValue":true}},
               "count":{"type":"integer","minimum":1,"maximum":9,"enum":[1,4,9],
                 "x-configurator":{"sensitivity":"public","suggestedValue":4,
                   "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}},
               "fixed":{"type":["integer","null"],"readOnly":true,"x-configurator":{"sensitivity":"public","initialValue":4}},
               "owner":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                 "token":{"type":"string","writeOnly":true},
                 "mode":{"type":"string","enum":["synthetic-suggestion","second"],"x-configurator":{"sensitivity":"public","suggestedValue":"synthetic-suggestion"}}}},
               "protectedRows":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{
                 "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                   "mode":{"type":"string","enum":["public","alternate"],"x-configurator":{"sensitivity":"public","suggestedValue":"public"}},
                   "token":{"type":"string","writeOnly":true}}}},
               "protectedMap":{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
                 "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                   "mode":{"type":"string","enum":["public","alternate"],"x-configurator":{"sensitivity":"public","suggestedValue":"public"}},
                   "token":{"type":"string","writeOnly":true}}}},
               "choice":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
                 {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                   "kind":{"type":"string","const":"public","x-configurator":{"sensitivity":"public"}},
                   "mode":{"type":"string","enum":["public","alternate"],"x-configurator":{"sensitivity":"public","suggestedValue":"public"}}}},
                 {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
                   "kind":{"type":"string","const":"protected","x-configurator":{"sensitivity":"public"}},
                   "token":{"type":"string","writeOnly":true}}}]},
               "rows":{"type":"array","x-configurator":{"sensitivity":"public"},"items":{
                 "type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                   "count":{"type":"integer","maximum":9,"x-configurator":{"sensitivity":"public"}},
                   "settings":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
                     "caption":{"type":"string","x-configurator":{"sensitivity":"public"}}}}}}},
               "map":{"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
                 "type":"integer","maximum":9,"x-configurator":{"sensitivity":"public"}}}
             }},"outputs":{"config.json":{"format":"json"}}}
            """u8.ToArray()), privacy);

    private sealed class ValidationCounter : IDisposable
    {
        private readonly Activity _activity;
        private readonly ActivityListener _listener;

        internal ValidationCounter(Activity activity)
        {
            _activity = activity;
            _listener = new()
            {
                ShouldListenTo = source => source.Name == "Configurator.Core.Preparation",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStarted = OnStarted
            };
            ActivitySource.AddActivityListener(_listener);
        }

        internal int Count { get; private set; }
        private void OnStarted(Activity activity)
        {
            if (activity.OperationName == "ValidationCompleted" && ReferenceEquals(activity.Parent, _activity)) Count++;
        }
        public void Dispose() => _listener.Dispose();
    }
}
