namespace Configurator.Presentation.Tests;

using System.Text;
using System.Text.Json;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;

public sealed class DefinitionOutputGuidanceTests
{
    [Test]
    public async Task GuidanceRequiresAcknowledgementAndUsesLiveConditionsWithoutNavigation()
    {
        using var form = new DefinitionFormModel(WorkflowDefinition());
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
        var prepared = form.CaptureOutputExportContext(form.InputRevision, ["settings"]);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
        await Assert.That(form.RecordOutputExport(prepared, ["settings"], DefinitionOutputDeliveryKind.DownloadRequested)).IsTrue();
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Single().TargetOption).IsEqualTo("identity");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var task)).IsTrue();
        await Assert.That(task.GetString()).IsEqualTo("settings");
        long revision = form.InputRevision;
        form.SelectLocale("en");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(1);
        form.SetValue(["enabled"], JsonSerializer.SerializeToElement(false));
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
        var updated = form.CaptureOutputExportContext(form.InputRevision, ["settings"]);
        form.RecordOutputExport(updated, ["settings"], DefinitionOutputDeliveryKind.ClipboardCopied);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
        form.SelectOutput("identity");
        await Assert.That(form.OutputWorkflow.Tasks.Single(item => item.Option == "settings").Status)
            .IsEqualTo(DefinitionOutputExportStatus.ExportedStale);
        form.Undo();
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CapturingCannotNarrowOrForgePreparedArtifactCoverage()
    {
        using var form = new DefinitionFormModel(Definition());
        await Assert.That(() => form.CaptureOutputExportContext(form.InputRevision, ["one"])).Throws<ArgumentException>();
        await Assert.That(() => form.CaptureOutputExportContext(form.InputRevision, ["one", "one"])).Throws<ArgumentException>();
        var captured = form.CaptureOutputExportContext(form.InputRevision, ["one", "two"]);
        string[] successes = ["one"];
        form.RecordOutputExport(captured, successes, DefinitionOutputDeliveryKind.ClipboardCopied);
        successes[0] = "two";
        await Assert.That(form.OutputWorkflow.Tasks.Single().Deliveries.Single().ArtifactId).IsEqualTo("one");
        await Assert.That(form.RecordOutputExport(captured, ["two"], (DefinitionOutputDeliveryKind)99)).IsFalse();
        form.EditValue(["name"], "Changed");
        await Assert.That(() => form.CaptureOutputExportContext(captured.InputRevision, ["one", "two"]))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task LateAcknowledgementCannotCertifyEditedAnswers()
    {
        using var form = new DefinitionFormModel(Definition());
        var captured = form.CaptureOutputExportContext(form.InputRevision, ["one", "two"]);
        form.EditValue(["name"], "Changed");
        await Assert.That(form.RecordOutputExport(captured, ["one"], DefinitionOutputDeliveryKind.DownloadRequested)).IsFalse();
        await Assert.That(form.OutputWorkflow.Tasks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PartialCoverageRetainsDeliveryKindsAndStalesAfterEdit()
    {
        using var form = new DefinitionFormModel(Definition());
        string[] eligible = ["one", "two"];
        var captured = form.CaptureOutputExportContext(form.InputRevision, eligible);
        eligible[0] = "forged";
        await Assert.That(form.RecordOutputExport(captured, ["one"], DefinitionOutputDeliveryKind.DownloadRequested)).IsTrue();
        await Assert.That(form.OutputWorkflow.Tasks.Single().Status).IsEqualTo(DefinitionOutputExportStatus.Partial);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
        await Assert.That(form.RecordOutputExport(captured, ["two"], DefinitionOutputDeliveryKind.ClipboardCopied)).IsTrue();
        await Assert.That(form.OutputWorkflow.Tasks.Single().Status).IsEqualTo(DefinitionOutputExportStatus.ExportedCurrent);
        await Assert.That(form.OutputWorkflow.Tasks.Single().Deliveries.Select(item => item.Kind).Distinct().Count()).IsEqualTo(2);
        form.EditValue(["name"], "Changed");
        await Assert.That(form.OutputWorkflow.Tasks.Single().Status).IsEqualTo(DefinitionOutputExportStatus.ExportedStale);
    }

    [Test]
    public async Task CancellationReplacementAndPrivateClearRetireHeldContexts()
    {
        using var form = new DefinitionFormModel(Definition());
        var cancelled = form.CaptureOutputExportContext(form.InputRevision, ["one", "two"]);
        form.InvalidateOutputExports();
        await Assert.That(form.RecordOutputExport(cancelled, ["one"], DefinitionOutputDeliveryKind.NativeSaveVerified)).IsFalse();
        var replaced = form.CaptureOutputExportContext(form.InputRevision, ["one", "two"]);
        var current = form.CaptureOutputExportContext(form.InputRevision, ["one", "two"]);
        await Assert.That(form.RecordOutputExport(replaced, ["one"], DefinitionOutputDeliveryKind.NativeSaveVerified)).IsFalse();
        await Assert.That(form.RecordOutputExport(current, ["unknown"], DefinitionOutputDeliveryKind.NativeSaveVerified)).IsFalse();
        await Assert.That(form.RecordOutputExport(current, ["one", "one"], DefinitionOutputDeliveryKind.NativeSaveVerified)).IsFalse();
        form.ClearPrivateState();
        await Assert.That(form.RecordOutputExport(current, ["one"], DefinitionOutputDeliveryKind.NativeSaveVerified)).IsFalse();
        await Assert.That(form.OutputWorkflow.Tasks.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ContextCannotCrossFormsOrSurviveDisposal()
    {
        using var form = new DefinitionFormModel(Definition());
        using var other = new DefinitionFormModel(Definition());
        var captured = form.CaptureOutputExportContext(form.InputRevision, ["one", "two"]);
        await Assert.That(other.RecordOutputExport(captured, ["one"], DefinitionOutputDeliveryKind.NativeSaveVerified)).IsFalse();
        form.Dispose();
        await Assert.That(form.RecordOutputExport(captured, ["one"], DefinitionOutputDeliveryKind.NativeSaveVerified)).IsFalse();
    }

    private static Definition Definition() => DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion":"0.0.1","profile":"bounded-v0.0.1",
      "familyId":"0ea9f4c8-4928-4217-9a33-869f919f4141","revision":1,
      "project":{"id":"output-guidance","publisher":"example","name":"Output guidance"},
      "applicationVersions":["1.0.0"],"entryPoints":["setup"],"locales":["en"],
      "configuration":{"type":"object","additionalProperties":false,
        "x-configurator":{"sensitivity":"public"},
        "properties":{"name":{"type":"string","x-configurator":{"sensitivity":"public"}}}},
      "artifacts":[
        {"id":"one","format":"json","fileName":"one.json","targetUpdateMode":"create","restartRequirement":"none","projection":{"op":"literal","value":{}}},
        {"id":"two","format":"json","fileName":"two.json","targetUpdateMode":"create","restartRequirement":"none","projection":{"op":"literal","value":{}}}
      ],
      "requiredCapabilities":["projection.literal","schema.object","schema.string"]
    }
    """));

    internal static Definition WorkflowDefinition() => DefinitionAuthoringCompiler.Compile(
        DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {
          "$schema":"urn:islamu:configurator:authoring:v0.0.1",
          "identity":{"familyId":"8efb17bf-d55c-4890-bf02-6d2cbdfe7614","revision":1},
          "project":{"id":"workflow","name":"Workflow"},"applicationVersions":["1.0"],
          "configuration":{"type":"object","additionalProperties":false,"required":["task"],
            "x-configurator":{"sensitivity":"public"},"properties":{
              "task":{"type":"string","enum":["settings","identity"],
                "x-configurator":{"sensitivity":"public","initialValue":"settings"}},
              "enabled":{"type":"boolean","x-configurator":{"sensitivity":"public","initialValue":true}}
            }},
          "presentation":{"defaultJourney":"configure",
            "sections":[{"id":"output","title":"Output","pointers":["/task","/enabled"]}],
            "journeys":[{"id":"configure","title":"Configure","sections":["output"]}],
            "outputSelector":{"pointer":"/task","options":[
              {"value":"settings","title":"Settings","artifactIds":["settings"],"nextActions":[
                {"targetOption":"identity","title":"Prepare identity","priority":"recommended",
                  "proposedWhen":{"op":"equals","reference":{"scope":"root","path":"/enabled"},"value":true}}
              ]},
              {"value":"identity","title":"Identity","artifactIds":["identity"]}
            ]}},
          "outputs":{
            "settings.json":{"id":"settings","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"settings"}}},
            "identity.json":{"id":"identity","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"}}}
          }
        }
        """)));
}
