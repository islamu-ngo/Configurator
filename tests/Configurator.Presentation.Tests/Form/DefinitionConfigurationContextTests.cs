namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionConfigurationContextTests
{
    [Test]
    public async Task CompatiblePairCommitsOneRevisionAndInvalidPairLeavesAnswersUntouched()
    {
        using var form = new DefinitionFormModel(Definition());
        int changes = 0;
        form.InputChanged += (_, _) => changes++;
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(1);
        await Assert.That(changes).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values["recipe"].GetString()).IsEqualTo("split");
        await Assert.That(form.Snapshot().Values["scope"].GetString()).IsEqualTo("dashboard");
        await Assert.That(form.SelectConfigurationContext("combined", "dashboard")).IsFalse();
        await Assert.That(form.SelectConfigurationContext("unknown", "all")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(1);
        await Assert.That(changes).IsEqualTo(1);
        await Assert.That(form.ConfigurationContext!.ScopeId).IsEqualTo("dashboard");
        await Assert.That(form.IsEditorVisible(["recipe"])).IsFalse();
        await Assert.That(form.IsEditorVisible(["scope"])).IsFalse();
        await Assert.That(form.Projection.Sections.SelectMany(section => section.Fields).Contains("recipe")).IsFalse();
        await Assert.That(form.Projection.Sections.SelectMany(section => section.Fields).Contains("scope")).IsFalse();
    }

    [Test]
    public async Task ScopedRequiredWorkDoesNotIncludeAnotherRecipientsMissingSecret()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["dashboardSecret"], JsonSerializer.SerializeToElement("synthetic-dashboard-session"));
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(form.Projection.Progress.Missing).IsEqualTo(0);
        await Assert.That(form.Projection.RelevantEntries.Any(entry => entry.FieldId == "workerSecret")).IsFalse();
        await Assert.That(form.SelectConfigurationContext("combined", "all")).IsTrue();
        await Assert.That(form.Validate().IsReady).IsFalse();
    }

    [Test]
    public async Task ContextSwitchRejectsAnImportReadThatCompletesLater()
    {
        using var form = new DefinitionFormModel(Definition());
        var pending = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionImportResult> import = form.ImportAsync("combined", _ => pending.Task);
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        pending.SetResult("""{"worker":"synthetic-worker-administrator","dashboard":"synthetic-dashboard-session"}"""u8.ToArray());
        await Assert.That((await import.WaitAsync(TimeSpan.FromSeconds(5))).Succeeded).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("workerSecret")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(1);
    }

    [Test]
    public async Task CanonicalReplacementPreservesPublicAnswersOwnedByAnotherScope()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["workerNote"], JsonSerializer.SerializeToElement("retained-worker-note"));
        form.SetValue(["dashboardNote"], JsonSerializer.SerializeToElement("replace-dashboard-note"));
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        using DefinitionDocumentSession document = form.OpenDocument();
        byte[] source = document.ReadSource();
        await Assert.That(Encoding.UTF8.GetString(source)).DoesNotContain("retained-worker-note");
        AnswerSnapshot represented = AnswerSnapshotCodec.ParseJson(source);
        byte[] replacement = AnswerSnapshotCodec.Serialize(new AnswerSnapshot(form.InputRevision,
            represented.Values.Where(pair => pair.Key != "dashboardNote").ToDictionary()));
        using DefinitionDocumentParseResult parsed = document.Parse(replacement, DefinitionDocumentApplyMode.ReplaceRepresentedScope);
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(document.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["workerNote"].GetString()).IsEqualTo("retained-worker-note");
        await Assert.That(form.Snapshot().Values.ContainsKey("dashboardNote")).IsFalse();
    }

    [Test]
    public async Task CanonicalReplacementSplitsMixedRecipientObjectsAtSelectedLeafLocations()
    {
        using var form = new DefinitionFormModel(DefinitionWithSharedNotes());
        form.SetValue(["shared"], JsonSerializer.SerializeToElement(new { workerNote = "retained-worker-note", dashboardNote = "dashboard-note" }));
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        using DefinitionDocumentSession document = form.OpenDocument();
        byte[] source = document.ReadSource();
        await Assert.That(Encoding.UTF8.GetString(source)).DoesNotContain("retained-worker-note");
        AnswerSnapshot represented = AnswerSnapshotCodec.ParseJson(source);
        byte[] replacement = AnswerSnapshotCodec.Serialize(new AnswerSnapshot(form.InputRevision,
            represented.Values.Where(pair => pair.Key != "shared").ToDictionary()));
        using DefinitionDocumentParseResult parsed = document.Parse(replacement, DefinitionDocumentApplyMode.ReplaceRepresentedScope);
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(document.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["shared"].GetProperty("workerNote").GetString()).IsEqualTo("retained-worker-note");
        await Assert.That(form.Snapshot().Values["shared"].TryGetProperty("dashboardNote", out _)).IsFalse();
    }

    [Test]
    public async Task InactiveNestedDraftErrorsAreRetainedWithoutBlockingTheSelectedScope()
    {
        using var form = new DefinitionFormModel(DefinitionWithSharedNotes(numericWorker: true));
        form.SetValue(["shared"], JsonSerializer.SerializeToElement(new { workerNote = 7, dashboardNote = "dashboard-note" }));
        form.EditValue(["shared", "workerNote"], "invalid-number");
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(form.SelectConfigurationContext("combined", "all")).IsTrue();
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(form.ReadDraftText(["shared", "workerNote"])).IsEqualTo("invalid-number");
    }

    [Test]
    public async Task ContextSwitchRetiresHeldDocumentParsingAndItsCandidate()
    {
        using var form = new DefinitionFormModel(Definition());
        using DefinitionDocumentSession document = form.OpenDocument();
        byte[] source = document.ReadSource();
        var pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DefinitionDocumentParseResult> parse = document.ParseAsync(_ => pending.Task);
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        pending.SetResult(source);
        using DefinitionDocumentParseResult result = await parse.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(form.ConfigurationContext!.ScopeId).IsEqualTo("dashboard");
        await Assert.That(form.InputRevision).IsEqualTo(1);
    }

    [Test]
    public async Task NarrowingRetainsInactiveAnswersWithoutIncludingTheirSecretsInCompleteOrDraftBytes()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["workerSecret"], JsonSerializer.SerializeToElement("synthetic-worker-administrator"));
        form.SetValue(["dashboardSecret"], JsonSerializer.SerializeToElement("synthetic-dashboard-session"));
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("workerSecret")).IsTrue();
        using DefinitionArtifact? complete = DefinitionArtifactComposer.Compose(form.Definition, "dashboard", form.Snapshot()).Artifact;
        await Assert.That(complete).IsNotNull();
        await Assert.That(Encoding.UTF8.GetString(complete!.Bytes.Span)).DoesNotContain("synthetic-worker-administrator");
        using DefinitionDraftArtifactResult draft = DefinitionDraftArtifactComposer.Compose(form.Definition, "dashboard", form.Snapshot());
        await Assert.That(Encoding.UTF8.GetString(draft.Artifact!.Bytes.Span)).DoesNotContain("synthetic-worker-administrator");
        await Assert.That(DefinitionArtifactComposer.Compose(form.Definition, "combined", form.Snapshot()).Succeeded).IsFalse();
        await Assert.That(form.EligibleArtifacts.Select(item => item.Id).SequenceEqual(["dashboard"])).IsTrue();
        await Assert.That(form.SelectConfigurationContext("combined", "all")).IsTrue();
        await Assert.That(form.Validate().IsReady).IsTrue();
    }

    [Test]
    public async Task SharedDefinitionKeepsContextAndDisclosureIndependentAcrossWorkspaces()
    {
        Definition definition = Definition();
        using var first = new DefinitionFormModel(definition);
        using var second = new DefinitionFormModel(definition);
        await Assert.That(first.SelectConfigurationContext("split", "dashboard")).IsTrue();
        await Assert.That(second.ConfigurationContext!.RecipeId).IsEqualTo("combined");
        long revision = first.InputRevision;
        first.SetDisclosureOverride(FieldDisclosure.Advanced);
        await Assert.That(first.SelectLocale("en")).IsTrue();
        await Assert.That(first.InputRevision).IsEqualTo(revision);
        await Assert.That(first.Projection.ConfigurationContext!.ScopeId).IsEqualTo("dashboard");
        await Assert.That(second.ConfigurationContext.ScopeId).IsEqualTo("all");
    }

    [Test]
    public async Task RevocationClearsSecretsRetainedOutsideTheCurrentScope()
    {
        using var form = new DefinitionFormModel(Definition(), DefinitionSessionPrivacy.BrowserSecretsEnabled);
        form.SetValue(["workerSecret"], JsonSerializer.SerializeToElement("synthetic-worker-administrator"));
        form.SetValue(["dashboardSecret"], JsonSerializer.SerializeToElement("synthetic-dashboard-session"));
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        form.SetSecretEntryEnabled(false);
        await Assert.That(form.Snapshot().Values.ContainsKey("workerSecret")).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("dashboardSecret")).IsFalse();
    }

    [Test]
    public async Task ContextSwitchInvalidatesHeldPreparationBeforeItCanBecomeCurrent()
    {
        using var form = new DefinitionFormModel(Definition());
        form.SetValue(["workerSecret"], JsonSerializer.SerializeToElement("synthetic-worker-administrator"));
        form.SetValue(["dashboardSecret"], JsonSerializer.SerializeToElement("synthetic-dashboard-session"));
        var operation = new DefinitionPresentationOperation(form.Definition, "combined", form.Snapshot());
        using PresentationOutcome prepared = await operation.ExecuteAsync(CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(prepared.Output.Span)).Contains("synthetic-worker-administrator");
        var pending = new TaskCompletionSource<PresentationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discarded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = new PresentationSession(new WeakReferenceMessenger());
        WorkspaceId.TryCreate("deployment-context", out WorkspaceId workspaceId);
        using var workspace = session.CreateWorkspace(workspaceId, new HeldOperation(pending.Task));
        workspace.CompletionDiscarded += (_, _) => discarded.TrySetResult();
        form.InputChanged += (_, _) => workspace.Deactivate();
        Task execution = workspace.ExecuteAsync(Guid.NewGuid());
        await Assert.That(form.SelectConfigurationContext("split", "dashboard")).IsTrue();
        pending.SetResult(prepared);
        await execution.WaitAsync(TimeSpan.FromSeconds(5));
        await discarded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(workspace.Output.IsEmpty).IsTrue();
        await Assert.That(workspace.Result).IsNull();
        await Assert.That(form.ConfigurationContext!.ScopeId).IsEqualTo("dashboard");
    }

    private sealed class HeldOperation(Task<PresentationOutcome> pending) : IPresentationOperation
    {
        public Task<PresentationOutcome> ExecuteAsync(CancellationToken cancellationToken) => pending;
    }

    private static Definition DefinitionWithSharedNotes(bool numericWorker = false)
    {
        JsonObject runtime = JsonNode.Parse(Definition().CanonicalBytes.Span)!.AsObject();
        JsonObject properties = runtime["configuration"]!["properties"]!.AsObject();
        properties["shared"] = new JsonObject
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["x-configurator"] = new JsonObject { ["sensitivity"] = "public" },
            ["properties"] = new JsonObject
            {
                ["workerNote"] = properties["workerNote"]!.DeepClone(),
                ["dashboardNote"] = properties["dashboardNote"]!.DeepClone()
            }
        };
        if (numericWorker)
        {
            properties["shared"]!["properties"]!["workerNote"]!["type"] = "integer";
            string[] capabilities = runtime["requiredCapabilities"]!.AsArray()
                .Select(value => value!.GetValue<string>()).Append("schema.integer").Order(StringComparer.Ordinal).ToArray();
            runtime["requiredCapabilities"] = new JsonArray(capabilities.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        }
        properties.Remove("workerNote");
        properties.Remove("dashboardNote");
        // The compiled root records authored member order; this fixture restructures members after compilation.
        runtime["configuration"]!["x-configurator"]!.AsObject().Remove("propertyOrder");
        foreach (JsonNode? artifact in runtime["artifacts"]!.AsArray())
            foreach ((string name, JsonNode? member) in artifact!["projection"]!["members"]!.AsObject())
                if (name is "workerNote" or "dashboardNote") member!["reference"]!["path"] = "/shared/" + name;
        return DefinitionCodec.ParseJson(JsonSerializer.SerializeToUtf8Bytes(runtime));
    }

    private static Definition Definition() => DefinitionAuthoringCompiler.Compile(DefinitionAuthoringCodec.ParseJson("""
    {
      "$schema":"urn:islamu:configurator:authoring:v0.0.1",
      "identity":{"familyId":"0d2be708-4468-48a3-b1f4-24cb1bd78aae","revision":1},
      "project":{"id":"example.worker-dashboard","name":"Worker and dashboard"},
      "applicationVersions":["1.0"],
      "configuration":{"type":"object","additionalProperties":false,"required":["recipe","scope","workerSecret","dashboardSecret"],
        "x-configurator":{"sensitivity":"public"},
        "properties":{
          "recipe":{"type":"string","enum":["combined","split"],"x-configurator":{"sensitivity":"public","initialValue":"combined"}},
          "scope":{"type":"string","enum":["all","worker","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
          "workerSecret":{"type":"string","x-configurator":{"sensitivity":"secret"}},
          "dashboardSecret":{"type":"string","x-configurator":{"sensitivity":"secret"}},
          "workerNote":{"type":"string","x-configurator":{"sensitivity":"public"}},
          "dashboardNote":{"type":"string","x-configurator":{"sensitivity":"public"}}
        }},
      "deployment":{"recipePointer":"/recipe","scopePointer":"/scope",
        "components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
        "recipes":[
          {"id":"combined","title":"Combined","scopes":[{"id":"all","title":"All","componentIds":["worker","dashboard"],"artifactIds":["combined"],"primaryArtifactId":"combined"}]},
          {"id":"split","title":"Separate processes","scopes":[
            {"id":"all","title":"All","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
            {"id":"worker","title":"Worker only","componentIds":["worker"],"artifactIds":["worker"],"primaryArtifactId":"worker"},
            {"id":"dashboard","title":"Dashboard only","componentIds":["dashboard"],"artifactIds":["dashboard"],"primaryArtifactId":"dashboard"}]}]},
      "outputs":{
        "combined.json":{"id":"combined","format":"json","consumerIds":["worker","dashboard"],"targetUpdateMode":"create","restartRequirement":"process","bindings":{"/worker":{"scope":"root","path":"/workerSecret"},"/dashboard":{"scope":"root","path":"/dashboardSecret"},"/workerNote":{"scope":"root","path":"/workerNote"},"/dashboardNote":{"scope":"root","path":"/dashboardNote"}}},
        "worker.json":{"id":"worker","format":"json","consumerIds":["worker"],"targetUpdateMode":"create","restartRequirement":"process","bindings":{"/worker":{"scope":"root","path":"/workerSecret"},"/workerNote":{"scope":"root","path":"/workerNote"}}},
        "dashboard.json":{"id":"dashboard","format":"json","consumerIds":["dashboard"],"targetUpdateMode":"create","restartRequirement":"process","bindings":{"/dashboard":{"scope":"root","path":"/dashboardSecret"},"/dashboardNote":{"scope":"root","path":"/dashboardNote"}}}
      }
    }
    """u8.ToArray()));
}
