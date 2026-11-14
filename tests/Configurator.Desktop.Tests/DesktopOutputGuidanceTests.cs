namespace Configurator.Desktop.Tests;

using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Core.Definitions.Authoring;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;
using Configurator.Presentation;

public sealed class DesktopOutputGuidanceTests
{
    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OpenReviewFollowsLocaleWithoutReplacingSelectionOrConfirmationAuthority(bool masked)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopOutputGuidanceTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    var runtime = JsonNode.Parse(WorkflowDefinition(masked: masked))!.AsObject();
                    runtime["configuration"]!["properties"]!["source"]!["title"] = "Source title";
                    runtime["configuration"]!["properties"]!["target"]!["title"] = "Target title";
                    runtime["localization"]!["translations"]!["ar"]!["overrides"] =
                        JsonNode.Parse("""
                        {"/configuration/properties/source/title":"المصدر",
                         "/configuration/properties/target/title":"الوجهة"}
                        """);
                    window.Show();
                    bool admitted = shell.Loader.LoadDefinition(Encoding.UTF8.GetBytes(runtime.ToJsonString()));
                    var form = shell.Workspace.Form!;
                    form.EditValue(["source"], "Synthetic source");
                    form.SelectOutput("identity");
                    form.EditValue(["target"], "Synthetic destination");
                    form.SelectOutput("settings");
                    var export = form.CaptureOutputExportContext(form.InputRevision, ["settings"]);
                    form.RecordOutputExport(export, ["settings"], DefinitionOutputDeliveryKind.NativeSaveVerified);
                    var target = shell.Workspace.OutputTasks.Single(task => task.Id == "identity");
                    shell.Workspace.OutputTask = target;
                    var obsolete = shell.Workspace.ApplyTransferCommand;
                    shell.Workspace.CancelTransferCommand.Execute(null);
                    shell.Workspace.OutputTask = target;
                    var row = shell.Workspace.TransferCandidates.Single();
                    row.IsSelected = true;
                    var current = shell.Workspace.ApplyTransferCommand;
                    long revision = form.InputRevision;
                    string answers = System.Text.Json.JsonSerializer.Serialize(form.Snapshot().Values);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    shell.Workspace.SelectedLocale = "ar";
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var review = window.FindNamedControl<StackPanel>("TransferReview")!;
                    var choice = review.GetLogicalDescendants().OfType<CheckBox>().Single();
                    var apply = window.FindNamedControl<Button>("TransferApply")!;
                    string expectedTitle = "المصدر → الوجهة — " + PresentationStrings.Get("TransferOverwrite", "ar");
                    bool titleUpdated = AutomationProperties.GetName(choice) == expectedTitle &&
                        choice.GetLogicalDescendants().OfType<TextBlock>().Any(text => text.Text == expectedTitle);
                    bool applyUpdated = Equals(apply.Content, PresentationStrings.Get("TransferApplyAndSwitch", "ar")) &&
                        AutomationProperties.GetName(apply) == PresentationStrings.Get("TransferApplyAndSwitch", "ar");
                    bool previewUpdated = review.GetLogicalDescendants().OfType<TextBlock>().Any(text =>
                        text.Text == (masked ? PresentationStrings.Get("TransferMasked", "ar") : "Synthetic source"));
                    bool selectionPreserved = ReferenceEquals(row, shell.Workspace.TransferCandidates.Single()) &&
                        choice.IsChecked == true && row.IsSelected;
                    bool authorityPreserved = ReferenceEquals(current, shell.Workspace.ApplyTransferCommand) &&
                        current.CanExecute(null) && !obsolete.CanExecute(null);
                    obsolete.Execute(null);
                    bool unchanged = shell.Workspace.HasTransferReview && form.InputRevision == revision &&
                        System.Text.Json.JsonSerializer.Serialize(form.Snapshot().Values) == answers;
                    bool exported = form.OutputWorkflow.Tasks.Single(task => task.Option == "settings").Status ==
                        DefinitionOutputExportStatus.ExportedCurrent;
                    current.Execute(null);
                    return (admitted, titleUpdated, applyUpdated, previewUpdated, selectionPreserved,
                        authorityPreserved, unchanged, exported,
                        applied: ReadTarget(form) == "Synthetic source" && form.InputRevision == revision + 1);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.admitted).IsTrue();
            await Assert.That(result.titleUpdated).IsTrue();
            await Assert.That(result.applyUpdated).IsTrue();
            await Assert.That(result.previewUpdated).IsTrue();
            await Assert.That(result.selectionPreserved).IsTrue();
            await Assert.That(result.authorityPreserved).IsTrue();
            await Assert.That(result.unchanged).IsTrue();
            await Assert.That(result.exported).IsTrue();
            await Assert.That(result.applied).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [Arguments("verified")]
    [Arguments("unverified")]
    [Arguments("refused")]
    [Arguments("unknown")]
    public async Task OnlyVerifiedNativeSaveOffersCompanionTask(string outcome)
    {
        using var shell = DesktopTestContext.Root(save: (_, _, _) => outcome == "unknown"
            ? Task.FromException<ProtectedArtifactCommitResult>(new IOException("synthetic-save-fault"))
            : Task.FromResult(outcome switch
            {
                "verified" => ProtectedArtifactCommitResult.Installed(true),
                "unverified" => ProtectedArtifactCommitResult.Installed(false),
                _ => ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.IoFailure)
            }), protectedOutputAvailable: true);
        await Assert.That(shell.Loader.LoadDefinition(WorkflowDefinition())).IsTrue();
        await Assert.That(shell.Workspace.Form!.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(0);
        await Assert.That(await shell.Publisher.PrepareSelectionAsync()).IsEqualTo(DesktopPreparationOutcome.Prepared);
        await shell.Publisher.SaveSelectionAsync();
        await Assert.That(shell.Publisher.PublicationHistory.Count).IsEqualTo(1);
        await Assert.That(shell.Workspace.Form.OutputWorkflow.ProposedNextActions.Count)
            .IsEqualTo(outcome == "verified" ? 1 : 0);
        shell.Workspace.Form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var task);
        await Assert.That(task.GetString()).IsEqualTo("settings");
    }

    [Test]
    public async Task SimultaneousOutputsWithoutTaskSelectorNeverOfferCompanionNavigation()
    {
        using var shell = DesktopTestContext.Root(save: (_, _, _) =>
            Task.FromResult(ProtectedArtifactCommitResult.Installed(true)), protectedOutputAvailable: true);
        await Assert.That(shell.Loader.LoadDefinition(DesktopApplicationTests.Definition())).IsTrue();
        await shell.Publisher.PrepareSelectionAsync();
        await shell.Publisher.SaveSelectionAsync();
        await Assert.That(shell.Workspace.HasOutputTasks).IsFalse();
        await Assert.That(shell.Workspace.ProposedNextActions.Count).IsEqualTo(0);
        await Assert.That(shell.Workspace.Form!.OutputWorkflow.Tasks.Single().Status)
            .IsEqualTo(DefinitionOutputExportStatus.Partial);
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task InstantiatedReviewOffersUncheckedOverwriteAndExplicitApply(string locale)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopOutputGuidanceTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    shell.Loader.LoadDefinition(WorkflowDefinition(masked: true));
                    var form = shell.Workspace.Form!;
                    form.SelectLocale(locale);
                    form.EditValue(["source"], "Synthetic protected source");
                    form.SelectOutput("identity");
                    form.EditValue(["target"], "Synthetic protected target");
                    form.SelectOutput("settings");
                    shell.Navigation.ConfigureCommand.Execute(null);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var picker = window.FindNamedControl<ComboBox>("OutputTask")!;
                    picker.SelectedIndex = 1;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var review = window.FindNamedControl<StackPanel>("TransferReview")!;
                    var choice = review.GetLogicalDescendants().OfType<CheckBox>().Single();
                    bool uncheckedOverwrite = choice.IsChecked == false &&
                        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(choice));
                    string preview = shell.Workspace.TransferCandidates.Single().Preview;
                    var apply = window.FindNamedControl<Button>("TransferApply")!;
                    bool named = AutomationProperties.GetName(apply) ==
                        PresentationStrings.Get("TransferApplyAndSwitch", locale);
                    choice.IsChecked = true;
                    apply.Command!.Execute(null);
                    Dispatcher.UIThread.RunJobs();
                    return (uncheckedOverwrite, named, masked: preview == PresentationStrings.Get("TransferMasked", locale),
                        copied: ReadTarget(form), direction: shell.Workspace.FlowDirection);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.uncheckedOverwrite).IsTrue();
            await Assert.That(result.named).IsTrue();
            await Assert.That(result.masked).IsTrue();
            await Assert.That(result.copied).IsEqualTo("Synthetic protected source");
            await Assert.That(result.direction).IsEqualTo(locale == "ar"
                ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight);
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [Arguments("apply")]
    [Arguments("switch")]
    [Arguments("cancel")]
    [Arguments("stale")]
    [Arguments("replace")]
    [Arguments("clear")]
    [Arguments("dispose")]
    public async Task SuppliedDestinationRequiresExplicitOverwriteAndRetiredReviewsCannotApply(string action)
    {
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
        await Assert.That(shell.Loader.LoadDefinition(WorkflowDefinition())).IsTrue();
        var form = shell.Workspace.Form!;
        form.EditValue(["source"], "Synthetic source");
        form.SelectOutput("identity");
        form.EditValue(["target"], "Manual destination");
        form.SelectOutput("settings");
        shell.Workspace.OutputTask = shell.Workspace.OutputTasks.Single(task => task.Id == "identity");
        await Assert.That(shell.Workspace.HasTransferReview).IsTrue();
        var row = shell.Workspace.TransferCandidates.Single();
        await Assert.That(row.IsSelected).IsFalse();
        await Assert.That(row.Preview).IsEqualTo("Synthetic source");
        row.IsSelected = true;
        switch (action)
        {
            case "apply": shell.Workspace.ApplyTransferCommand.Execute(null); break;
            case "switch": shell.Workspace.SwitchWithoutCopyCommand.Execute(null); break;
            case "cancel": shell.Workspace.CancelTransferCommand.Execute(null); break;
            case "stale":
                form.EditValue(["source"], "New source");
                shell.Workspace.ApplyTransferCommand.Execute(null);
                break;
            case "replace":
                shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
                shell.Workspace.ApplyTransferCommand.Execute(null);
                break;
            case "clear":
                form.ClearPrivateState();
                shell.Workspace.ApplyTransferCommand.Execute(null);
                break;
            case "dispose":
                shell.Workspace.Dispose();
                shell.Workspace.ApplyTransferCommand.Execute(null);
                break;
        }
        await Assert.That(shell.Workspace.HasTransferReview).IsFalse();
        await Assert.That(row.Preview).IsEqualTo(string.Empty);
        if (action is not ("replace" or "clear" or "dispose"))
        {
            await Assert.That(ReadTarget(form))
                .IsEqualTo(action == "apply" ? "Synthetic source" : "Manual destination");
            form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var selected);
            await Assert.That(selected.GetString()).IsEqualTo(action is "apply" or "switch" ? "identity" : "settings");
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InlineAdoptionOpensExplicitReviewAndKeepsProtectedPreviewMasked(bool masked)
    {
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
        await Assert.That(shell.Loader.LoadDefinition(WorkflowDefinition(masked: masked))).IsTrue();
        var form = shell.Workspace.Form!;
        form.EditValue(["source"], "Synthetic retained value");
        form.SelectOutput("identity");
        shell.Workspace.Fields.Single(field => field.Id == "target").ApplySuggestionCommand.Execute(null);
        await Assert.That(ReadTarget(form)).IsNull();
        await Assert.That(shell.Workspace.HasTransferReview).IsTrue();
        await Assert.That(shell.Workspace.TransferCandidates.Single().Preview)
            .IsEqualTo(masked ? PresentationStrings.Get("TransferMasked", "en") : "Synthetic retained value");
        shell.Workspace.ApplyTransferCommand.Execute(null);
        await Assert.That(ReadTarget(form)).IsEqualTo("Synthetic retained value");
    }

    [Test]
    public async Task PartialBundleRecordsOnlyVerifiedArtifactsWithoutErasingPhysicalReceipts()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("desktop-guidance-").FullName;
        try
        {
            using var shell = DesktopTestContext.Root(outputDirectory: directory, protectedOutputAvailable: true,
                publishBundle: (artifacts, destination, observe, token) =>
                    ProtectedArtifactBundlePublisher.PublishAsync(new ProtectedArtifactWriter(),
                        artifacts.OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal).ToArray(),
                        destination, outcome =>
                        {
                            observe(outcome);
                            if (outcome.ArtifactId == "settings" && outcome.Result is { VerificationSucceeded: true })
                                File.WriteAllText(Path.Combine(directory, "extra.json"), "{}");
                        }, token));
            await Assert.That(shell.Loader.LoadDefinition(WorkflowDefinition(bundle: true))).IsTrue();
            shell.Publisher.SelectArtifact("settings", true);
            shell.Publisher.SelectArtifact("settings-extra", true);
            await shell.Publisher.PrepareSelectionAsync();
            await shell.Publisher.SaveSelectionAsync();
            var workflow = shell.Workspace.Form!.OutputWorkflow;
            await Assert.That(workflow.Tasks.Single(task => task.Option == "settings").Status)
                .IsEqualTo(DefinitionOutputExportStatus.Partial);
            await Assert.That(workflow.Tasks.Single(task => task.Option == "settings").Deliveries.Single().ArtifactId)
                .IsEqualTo("settings");
            await Assert.That(workflow.ProposedNextActions.Count).IsEqualTo(1);
            await Assert.That(shell.Publisher.PublicationHistory.Single().Outcomes.Count).IsEqualTo(2);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    [Arguments("apply")]
    [Arguments("switch")]
    [Arguments("cancel")]
    public async Task HeldConfirmationCannotActOnANewerReviewAtTheSameAnswerRevision(string action)
    {
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
        await Assert.That(shell.Loader.LoadDefinition(WorkflowDefinition())).IsTrue();
        var form = shell.Workspace.Form!;
        form.EditValue(["source"], "Reviewed source");
        shell.Workspace.OutputTask = shell.Workspace.OutputTasks.Single(task => task.Id == "identity");
        var held = action switch
        {
            "switch" => shell.Workspace.SwitchWithoutCopyCommand,
            "cancel" => shell.Workspace.CancelTransferCommand,
            _ => shell.Workspace.ApplyTransferCommand
        };
        shell.Workspace.CancelTransferCommand.Execute(null);
        shell.Workspace.OutputTask = shell.Workspace.OutputTasks.Single(task => task.Id == "identity");
        long revision = form.InputRevision;
        var currentCandidates = shell.Workspace.TransferCandidates;
        held.Execute(null);
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(shell.Workspace.HasTransferReview).IsTrue();
        await Assert.That(ReferenceEquals(currentCandidates, shell.Workspace.TransferCandidates)).IsTrue();
        await Assert.That(ReadTarget(form)).IsNull();
    }

    private static string? ReadTarget(DefinitionFormModel form) =>
        form.Snapshot().Values.TryGetValue("target", out var value) ? value.GetString() : null;

    [Test]
    public async Task LateVerifiedSaveRetainsReceiptWithoutGuidingReplacement()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var shell = DesktopTestContext.Root(protectedOutputAvailable: true, save: async (_, _, _) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            return ProtectedArtifactCommitResult.Installed(true);
        });
        shell.Loader.LoadDefinition(WorkflowDefinition());
        await shell.Publisher.PrepareSelectionAsync();
        Task saving = shell.Publisher.SaveSelectionAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        shell.Loader.LoadDefinition(DesktopApplicationTests.Definition());
        release.TrySetResult();
        await saving.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(shell.Publisher.PublicationHistory.Single().Outcomes.Single().Result!.VerificationSucceeded).IsTrue();
        await Assert.That(shell.Workspace.Form!.OutputWorkflow.Tasks.Count).IsEqualTo(0);
        await Assert.That(shell.Workspace.ProposedNextActions.Count).IsEqualTo(0);
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task NativeTaskPickerIsDistinctFromDirectoryPickerAndSwitchesWithoutCopy(string locale)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopOutputGuidanceTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    bool admitted = shell.Loader.LoadDefinition(WorkflowDefinition());
                    shell.Workspace.Form!.SelectLocale(locale);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var picker = window.FindNamedControl<ComboBox>("OutputTask");
                    bool named = picker is not null &&
                        AutomationProperties.GetName(picker) == shell.Workspace.Strings["OutputTask"];
                    if (picker is not null) picker.SelectedIndex = 1;
                    Dispatcher.UIThread.RunJobs();
                    shell.Navigation.ReviewCommand.Execute(null);
                    window.UpdateLayout();
                    shell.Workspace.Form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/task"), out var task);
                    return (admitted, named, task: task.GetString(),
                        directory: window.FindNamedControl<Button>("SelectOutput") is not null);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(result.admitted).IsTrue();
            await Assert.That(result.named).IsTrue();
            await Assert.That(result.task).IsEqualTo("identity");
            await Assert.That(result.directory).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    internal static byte[] WorkflowDefinition(bool bundle = false, bool masked = false)
    {
        var definition = DefinitionAuthoringCompiler.Compile(
        DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {
          "$schema":"urn:islamu:configurator:authoring:v0.0.1",
          "identity":{"familyId":"10494a9f-f3b2-4a14-976f-b515118005d3","revision":1},
          "project":{"id":"desktop-workflow","name":"Desktop workflow"},
          "applicationVersions":["1.0"],
          "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}},
          "configuration":{"type":"object","additionalProperties":false,"required":["task"],
            "x-configurator":{"sensitivity":"public"},"properties":{
              "task":{"type":"string","enum":["settings","identity"],
                "x-configurator":{"sensitivity":"public","initialValue":"settings"}},
              "source":{"type":"string","x-configurator":{"sensitivity":"public"}},
              "target":{"type":"string","x-configurator":{"sensitivity":"public",
                "suggestedFrom":{"pointer":"/source"},
                "applicableWhen":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"}}}
            }},
          "presentation":{"defaultJourney":"configure",
            "sections":[{"id":"output","title":"Output","pointers":["/task","/source","/target"]}],
            "journeys":[{"id":"configure","title":"Configure","sections":["output"]}],
            "outputSelector":{"pointer":"/task","options":[
              {"value":"settings","title":"Settings","artifactIds":["settings"],"nextActions":[
                {"targetOption":"identity","title":"Prepare identity","priority":"recommended"}]},
              {"value":"identity","title":"Identity","artifactIds":["identity"]}]}},
          "outputs":{
            "settings.json":{"id":"settings","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"settings"}}},
            "identity.json":{"id":"identity","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"}}}
          }
        }
        """)));
        var runtime = JsonNode.Parse(definition.CanonicalBytes.Span)!.AsObject();
        if (bundle)
        {
            var extra = runtime["artifacts"]!.AsArray().Single(artifact =>
                artifact!["id"]!.GetValue<string>() == "settings")!.DeepClone();
            extra["id"] = "settings-extra";
            extra["fileName"] = "extra.json";
            runtime["artifacts"]!.AsArray().Add(extra);
            runtime["presentation"]!["outputSelector"]!["options"]![0]!["artifactIds"]!.AsArray().Add("settings-extra");
        }
        if (masked)
            foreach (string field in new[] { "source", "target" })
                runtime["configuration"]!["properties"]![field]!["x-configurator"]!["sensitivity"] = "secret";
        return Encoding.UTF8.GetBytes(runtime.ToJsonString());
    }
}
