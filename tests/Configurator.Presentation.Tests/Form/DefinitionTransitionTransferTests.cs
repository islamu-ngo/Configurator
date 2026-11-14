namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Configurator.Core.Definitions;

public sealed class DefinitionTransitionTransferTests
{
    [Test]
    public async Task InvalidDraftIsUncheckedAndOnlyExplicitAdoptionReplacesIt()
    {
        var runtime = JsonNode.Parse(TransferDefinition().CanonicalBytes.Span)!.AsObject();
        foreach (string name in new[] { "source", "target" })
            runtime["configuration"]!["properties"]![name]!["type"] = "integer";
        string[] capabilities = runtime["requiredCapabilities"]!.AsArray().Select(item => item!.GetValue<string>())
            .Append("schema.integer").Order(StringComparer.Ordinal).ToArray();
        runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(item => JsonValue.Create(item)).ToArray());
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        form.SetValue(["source"], JsonSerializer.SerializeToElement(5));
        form.SelectOutput("identity");
        form.EditValue(["target"], "not-a-number");
        await Assert.That(form.HasInputError(["target"])).IsTrue();
        form.SelectOutput("settings");
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Single().SelectedByDefault).IsFalse();
        await Assert.That(form.ApplyTransfer(review, []).Succeeded).IsTrue();
        await Assert.That(form.HasInputError(["target"])).IsTrue();
        review = form.ReviewSuggestion(DefinitionPath.Parse("/target"));
        await Assert.That(form.ApplyTransfer(review, [review.Candidates.Single().Id]).Succeeded).IsTrue();
        await Assert.That(form.HasInputError(["target"])).IsFalse();
        await Assert.That(form.ReadValue(["target"])?.GetInt32()).IsEqualTo(5);
    }

    [Test]
    public async Task ExplicitNullIsProvidedAndNeverSelectedByDefault()
    {
        var runtime = JsonNode.Parse(TransferDefinition().CanonicalBytes.Span)!.AsObject();
        runtime["configuration"]!["properties"]!["target"]!["type"] = new JsonArray("string", "null");
        string[] capabilities = runtime["requiredCapabilities"]!.AsArray().Select(item => item!.GetValue<string>())
            .Append("schema.nullable").Order(StringComparer.Ordinal).ToArray();
        runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(item => JsonValue.Create(item)).ToArray());
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        form.EditValue(["source"], "Value");
        form.SelectOutput("identity");
        form.SetValue(["target"], JsonSerializer.SerializeToElement<object?>(null));
        var review = form.ReviewSuggestion(DefinitionPath.Parse("/target"));
        await Assert.That(review.Candidates.Single().DestinationProvided).IsTrue();
        await Assert.That(review.Candidates.Single().SelectedByDefault).IsFalse();
        await Assert.That(form.ReadValue(["target"])?.ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task InlineActionUsesTheSameCapturedEligibilityAndSingleRevisionAdoption()
    {
        using var form = new DefinitionFormModel(TransferDefinition());
        form.EditValue(["source"], "Inline");
        form.SelectOutput("identity");
        DefinitionPath target = DefinitionPath.Parse("/target");
        await Assert.That(form.GetFieldValidation(target).CanApplySuggestion).IsTrue();
        long revision = form.InputRevision;
        await Assert.That(form.ApplySuggestion(target)).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.ReadValue(["target"])?.GetString()).IsEqualTo("Inline");
    }

    [Test]
    public async Task MissingBlankInvalidAndConditionFalseSourcesHaveNoUsableCandidate()
    {
        var runtime = JsonNode.Parse(TransferDefinition().CanonicalBytes.Span)!.AsObject();
        runtime["configuration"]!["properties"]!["source"]!["minLength"] = 3;
        runtime["configuration"]!["properties"]!["target"]!["x-configurator"]!["suggestedFrom"]!["when"] =
            JsonNode.Parse("""{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}""");
        string[] capabilities = runtime["requiredCapabilities"]!.AsArray().Select(item => item!.GetValue<string>())
            .Append("schema.bounds").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(item => JsonValue.Create(item)).ToArray());
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        await Assert.That(form.ReviewOutputTransition("identity").Candidates.Count).IsEqualTo(0);
        foreach (string invalid in new[] { "   ", "x" })
        {
            form.EditValue(["source"], invalid);
            await Assert.That(form.ReviewOutputTransition("identity").Candidates.Count).IsEqualTo(0);
        }
        form.EditValue(["source"], "Ready");
        await Assert.That(form.ReviewOutputTransition("identity").Candidates.Count).IsEqualTo(1);
        form.SetValue(["enabled"], JsonSerializer.SerializeToElement(false));
        await Assert.That(form.ReviewOutputTransition("identity").Candidates.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("boolean", "false")]
    [Arguments("integer", "0")]
    public async Task FalseAndZeroRemainProvidedTypedSources(string type, string literal)
    {
        var runtime = JsonNode.Parse(TransferDefinition().CanonicalBytes.Span)!.AsObject();
        foreach (string name in new[] { "source", "target" })
            runtime["configuration"]!["properties"]![name]!["type"] = type;
        if (type == "integer")
        {
            string[] capabilities = runtime["requiredCapabilities"]!.AsArray().Select(item => item!.GetValue<string>())
                .Append("schema.integer").Order(StringComparer.Ordinal).ToArray();
            runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(item => JsonValue.Create(item)).ToArray());
        }
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        using JsonDocument value = JsonDocument.Parse(literal);
        form.SetValue(["source"], value.RootElement);
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Single().SelectedByDefault).IsTrue();
        await Assert.That(form.ApplyTransfer(review, [review.Candidates[0].Id]).Succeeded).IsTrue();
        await Assert.That(form.ReadValue(["target"])?.GetRawText()).IsEqualTo(literal);
    }

    [Test]
    public async Task SuppliedValuesAndOmissionsRequireAnExplicitSelection()
    {
        using var form = new DefinitionFormModel(TransferDefinition());
        form.EditValue(["source"], "Suggested");
        form.SelectOutput("identity");
        form.EditValue(["target"], "Manual");
        form.SelectOutput("settings");
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Single().DestinationProvided).IsTrue();
        await Assert.That(review.Candidates.Single().SelectedByDefault).IsFalse();
        await Assert.That(form.ApplyTransfer(review, []).Succeeded).IsTrue();
        await Assert.That(form.ReadValue(["target"])?.GetString()).IsEqualTo("Manual");
        form.SetValue(["target"], null);
        review = form.ReviewSuggestion(DefinitionPath.Parse("/target"));
        await Assert.That(review.Candidates.Single().SelectedByDefault).IsFalse();
        await Assert.That(form.ApplyTransfer(review, [review.Candidates[0].Id]).Succeeded).IsTrue();
        await Assert.That(form.ReadValue(["target"])?.GetString()).IsEqualTo("Suggested");
        await Assert.That(form.Snapshot().Omitted.Contains(DefinitionPath.Parse("/target"))).IsFalse();
    }

    [Test]
    public async Task ACombinedApplicabilityFailureDoesNotApplyAnyEarlierSelection()
    {
        var runtime = JsonNode.Parse(TransferDefinition().CanonicalBytes.Span)!.AsObject();
        JsonNode properties = runtime["configuration"]!["properties"]!;
        properties["source"]!["type"] = "boolean";
        properties["target"]!["type"] = "boolean";
        properties["target"]!["x-configurator"]!["initialValue"] = true;
        properties["sourceTwo"] = JsonNode.Parse("""{"type":"string","x-configurator":{"sensitivity":"public"}}""");
        properties["targetTwo"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public","suggestedFrom":{"pointer":"/sourceTwo"},
              "applicableWhen":{"op":"all","operands":[
                {"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"},
                {"op":"equals","reference":{"scope":"root","path":"/target"},"value":true}
              ]}}}
            """);
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        form.SetValue(["source"], JsonSerializer.SerializeToElement(false));
        form.EditValue(["sourceTwo"], "Second");
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Count).IsEqualTo(2);
        long revision = form.InputRevision;
        string? undo = form.LastUndoLabel;
        await Assert.That(form.ApplyTransfer(review, review.Candidates.Select(item => item.Id)).Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.LastUndoLabel).IsEqualTo(undo);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(2);
        await Assert.That(form.ReadValue(["target"])).IsNull();
        await Assert.That(form.ReadValue(["targetTwo"])).IsNull();
        await Assert.That(form.ReadValue(["task"])).IsNull();
    }

    [Test]
    public async Task ACombinedAnswerBudgetFailureLeavesAllTargetsAndHistoryUnchanged()
    {
        var runtime = JsonNode.Parse(TransferDefinition().CanonicalBytes.Span)!.AsObject();
        var properties = runtime["configuration"]!["properties"]!.AsObject();
        for (int index = 1; index < 17; index++)
            properties["copy" + index] = properties["target"]!.DeepClone();
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        form.EditValue(["source"], new string('x', 60_000));
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Count).IsEqualTo(17);
        long revision = form.InputRevision;
        string? undo = form.LastUndoLabel;
        await Assert.That(form.ApplyTransfer(review, review.Candidates.Select(item => item.Id)).Succeeded).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.LastUndoLabel).IsEqualTo(undo);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(1);
        await Assert.That(form.ReadValue(["target"])).IsNull();
        await Assert.That(form.ReadValue(["task"])).IsNull();
    }

    [Test]
    public async Task ReentrantObserverEditsAndFailuresCannotRollBackAnAcceptedTransfer()
    {
        using var form = new DefinitionFormModel(TransferDefinition());
        form.EditValue(["source"], "Reviewed");
        var review = form.ReviewOutputTransition("identity");
        long revision = form.InputRevision;
        bool reentered = false;
        form.InputChanged += (_, _) =>
        {
            if (reentered) return;
            reentered = true;
            form.EditValue(["target"], "Newer");
            throw new InvalidOperationException("synthetic-observer-failure");
        };
        var result = form.ApplyTransfer(review, [review.Candidates.Single().Id]);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.ObserverFailed).IsTrue();
        await Assert.That(result.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.InputRevision).IsEqualTo(revision + 2);
        await Assert.That(form.ReadValue(["target"])?.GetString()).IsEqualTo("Newer");
        await Assert.That(form.ReadValue(["task"])?.GetString()).IsEqualTo("identity");
    }

    [Test]
    public async Task OrdinaryTaskSelectionUsesTheEntireAdmittedNestedPointer()
    {
        var runtime = JsonNode.Parse(DefinitionOutputGuidanceTests.WorkflowDefinition().CanonicalBytes.Span)!.AsObject();
        var configuration = runtime["configuration"]!.AsObject();
        var properties = configuration["properties"]!.AsObject();
        JsonNode task = properties["task"]!.DeepClone();
        properties.Remove("task");
        properties["group"] = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("task"),
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            ["properties"] = new JsonObject { ["task"] = task }
        };
        configuration["required"] = new JsonArray("group");
        configuration["x-configurator"]!["propertyOrder"] = new JsonArray("group", "enabled");
        runtime["presentation"]!["outputSelector"]!["pointer"] = "/group/task";
        runtime["presentation"]!["sections"]![0]!["pointers"]![0] = "/group/task";
        foreach (JsonNode? artifact in runtime["artifacts"]!.AsArray())
            artifact!["projection"]!["when"]!["reference"]!["path"] = "/group/task";
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        await Assert.That(form.SelectOutput("identity")).IsTrue();
        await Assert.That(form.Snapshot().Values["group"].GetProperty("task").GetString()).IsEqualTo("identity");
    }

    [Test]
    public async Task StaleReviewCannotOverwriteAnEditOrSwitchTasks()
    {
        using var form = new DefinitionFormModel(TransferDefinition());
        form.EditValue(["source"], "First");
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Count).IsEqualTo(1);
        form.EditValue(["source"], "Second");
        long revision = form.InputRevision;
        var result = form.ApplyTransfer(review, [review.Candidates.Single().Id]);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Code).IsEqualTo("transfer-stale");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.ReadValue(["target"])).IsNull();
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var task)).IsTrue();
        await Assert.That(task.GetString()).IsEqualTo("settings");
    }

    [Test]
    public async Task TransferAndTaskSwitchPublishOneAcceptedRevisionAndUndoEntry()
    {
        using var form = new DefinitionFormModel(TransferDefinition());
        form.EditValue(["source"], "Independent");
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(form.ReadValue(["target"])).IsNull();
        long revision = form.InputRevision;
        var observed = new List<(string? Task, string? Target)>();
        form.InputChanged += (_, _) => observed.Add((
            form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var selected) ? selected.GetString() : null,
            form.ReadValue(["target"])?.GetString()));
        var applied = form.ApplyTransfer(review, [review.Candidates.Single().Id]);
        await Assert.That(applied.Succeeded).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(observed.Count).IsEqualTo(1);
        await Assert.That(observed[0]).IsEqualTo(("identity", "Independent"));
        await Assert.That(form.Undo()).IsTrue();
        await Assert.That(form.ReadValue(["target"])).IsNull();
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var task)).IsTrue();
        await Assert.That(task.GetString()).IsEqualTo("settings");
        await Assert.That(form.Redo()).IsTrue();
        await Assert.That(form.ReadValue(["target"])?.GetString()).IsEqualTo("Independent");
        await Assert.That(form.ApplyTransfer(review, [review.Candidates.Single().Id]).Succeeded).IsFalse();
    }

    [Test]
    public async Task DuplicateAndUnknownSelectionsLeaveEveryAnswerAndSelectorUnchanged()
    {
        using var form = new DefinitionFormModel(TransferDefinition());
        form.EditValue(["source"], "Reviewed");
        foreach (bool duplicate in new[] { true, false })
        {
            var review = form.ReviewOutputTransition("identity");
            long revision = form.InputRevision;
            int id = review.Candidates.Single().Id;
            var applied = form.ApplyTransfer(review, duplicate ? [id, id] : [id, int.MaxValue]);
            await Assert.That(applied.Succeeded).IsFalse();
            await Assert.That(form.InputRevision).IsEqualTo(revision);
            await Assert.That(form.ReadValue(["target"])).IsNull();
            await Assert.That(form.ReadValue(["task"])).IsNull();
        }
    }

    [Test]
    public async Task RetainedInactiveSourceSupportsExplicitInlineAdoptionWithoutAutomaticCopy()
    {
        using var form = new DefinitionFormModel(TransferDefinition());
        form.EditValue(["source"], "Retained");
        form.SelectOutput("identity");
        await Assert.That(form.ReadValue(["target"])).IsNull();
        var review = form.ReviewSuggestion(DefinitionPath.Parse("/target"));
        await Assert.That(review.Candidates.Count).IsEqualTo(1);
        await Assert.That(form.ReadTransferPreview(review, review.Candidates[0].Id)?.GetString()).IsEqualTo("Retained");
        await Assert.That(form.ApplyTransfer(review, [review.Candidates[0].Id]).Succeeded).IsTrue();
        await Assert.That(form.ReadValue(["target"])?.GetString()).IsEqualTo("Retained");
        await Assert.That(form.ReadValue(["source"])?.GetString()).IsEqualTo("Retained");
    }

    internal static Definition TransferDefinition(bool secret = false)
    {
        var runtime = JsonNode.Parse(DefinitionOutputGuidanceTests.WorkflowDefinition().CanonicalBytes.Span)!.AsObject();
        var properties = runtime["configuration"]!["properties"]!.AsObject();
        properties["source"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public",
              "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"settings"}}}
            """);
        properties["target"] = JsonNode.Parse("""
            {"type":"string","x-configurator":{"sensitivity":"public","suggestedFrom":{"pointer":"/source"},
              "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"}}}
            """);
        if (secret)
        {
            properties["source"]!["x-configurator"]!["sensitivity"] = "secret";
            properties["target"]!["x-configurator"]!["sensitivity"] = "secret";
        }
        var capabilities = runtime["requiredCapabilities"]!.AsArray().Select(item => item!.GetValue<string>())
            .Append("values.suggested-from")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(item => JsonValue.Create(item)).ToArray());
        return DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString()));
    }
}
