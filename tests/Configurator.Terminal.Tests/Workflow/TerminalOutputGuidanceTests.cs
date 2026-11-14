namespace Configurator.Terminal.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Artifacts;
using Configurator.Core.Definitions.Authoring;
using Configurator.Presentation;
using Configurator.Terminal;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.Views;

public sealed class TerminalOutputGuidanceTests
{
    [Test]
    [NotInParallel]
    [Arguments("en", 80)]
    [Arguments("ar", 80)]
    [Arguments("en", 120)]
    [Arguments("ar", 120)]
    public async Task AllSixtyFourTasksStayVisibleWithinPagesAndLastTaskCanBeChosen(string locale, int columns)
    {
        using var form = TaskChooserForm(locale, 64);
        await Assert.That(form.OutputWorkflow.Tasks.Count).IsEqualTo(64);
        using var application = Application.Create();
        application.Init("dotnet");
        using var dialog = new TerminalOutputTasksDialog(form, application);
        foreach (string key in new[] { "PreviousTaskPage", "NextTaskPage" })
        {
            await Assert.That(TerminalStrings.Get(key, locale)).IsNotEqualTo(key);
            await Assert.That(TerminalStrings.Get(key, "ar")).IsNotEqualTo(TerminalStrings.Get(key, "en"));
        }
        dialog.BeginInit();
        dialog.EndInit();
        dialog.Layout(new System.Drawing.Size(columns, columns == 80 ? 24 : 40));
        var reached = new HashSet<string>(StringComparer.Ordinal);
        for (int page = 0; page < 64; page++)
        {
            Button[] visible = dialog.SubViews.OfType<Button>().Where(button => button.Visible).ToArray();
            await Assert.That(visible.All(button => button.Frame.Left >= 0 && button.Frame.Top >= 0
                && button.Frame.Right <= dialog.Viewport.Width && button.Frame.Bottom <= dialog.Viewport.Height)).IsTrue();
            await Assert.That(visible.SelectMany((button, index) => visible.Skip(index + 1)
                .Select(other => System.Drawing.Rectangle.Intersect(button.Frame, other.Frame)))
                .All(overlap => overlap.Width == 0 || overlap.Height == 0)).IsTrue();
            Button[] tasks = visible.Where(button => button.Text.StartsWith("Task ", StringComparison.Ordinal)).ToArray();
            foreach (Button task in tasks) reached.Add(task.Text.Split(" | ", StringSplitOptions.None)[0]);
            Button? last = tasks.SingleOrDefault(button => button.Text.StartsWith("Task 63 | ", StringComparison.Ordinal));
            if (last is not null)
            {
                Button previous = visible.Single(button => button.Text == TerminalStrings.Get("PreviousTaskPage", locale));
                await Assert.That(previous.Enabled).IsTrue();
                previous.SetFocus();
                previous.InvokeCommand(Command.Accept);
                dialog.Layout(dialog.Frame.Size);
                await Assert.That(last.Visible).IsFalse();
                Button forward = dialog.SubViews.OfType<Button>()
                    .Single(button => button.Text == TerminalStrings.Get("NextTaskPage", locale));
                forward.SetFocus();
                forward.InvokeCommand(Command.Accept);
                dialog.Layout(dialog.Frame.Size);
                await Assert.That(last.Visible).IsTrue();
                await Assert.That(forward.Enabled).IsFalse();
                last.SetFocus();
                last.InvokeCommand(Command.Accept);
                break;
            }
            Button next = visible.Single(button => button.Text == TerminalStrings.Get("NextTaskPage", locale));
            await Assert.That(next.Enabled).IsTrue();
            next.SetFocus();
            next.InvokeCommand(Command.Accept);
            dialog.Layout(dialog.Frame.Size);
        }
        await Assert.That(reached.Count).IsEqualTo(64);
        await Assert.That(dialog.TargetOption).IsEqualTo("task63");
        await Assert.That(form.Validate().TryGetEffectiveValue(
            Configurator.Core.Definitions.DefinitionPath.Parse("/task"), out var selected)).IsTrue();
        await Assert.That(selected.GetString()).IsEqualTo("task00");
    }

    [Test]
    [NotInParallel]
    [Arguments("en", 80)]
    [Arguments("ar", 80)]
    [Arguments("en", 120)]
    [Arguments("ar", 120)]
    public async Task FiveTaskConstructorLayoutKeepsLastTaskAndCancelSeparate(string locale, int columns)
    {
        using var form = TaskChooserForm(locale, 5);
        using var application = Application.Create();
        application.Init("dotnet");
        using var dialog = new TerminalOutputTasksDialog(form, application);
        dialog.BeginInit();
        dialog.EndInit();
        dialog.Layout(new System.Drawing.Size(columns, columns == 80 ? 24 : 40));
        Button[] visible = dialog.SubViews.OfType<Button>().Where(button => button.Visible).ToArray();
        await Assert.That(visible.Count(button => button.Text.StartsWith("Task ", StringComparison.Ordinal))).IsEqualTo(5);
        await Assert.That(visible.All(button => button.Frame.Left >= 0 && button.Frame.Top >= 0
            && button.Frame.Right <= dialog.Viewport.Width && button.Frame.Bottom <= dialog.Viewport.Height)).IsTrue();
        await Assert.That(visible.SelectMany((button, index) => visible.Skip(index + 1)
            .Select(other => System.Drawing.Rectangle.Intersect(button.Frame, other.Frame)))
            .All(overlap => overlap.Width == 0 || overlap.Height == 0)).IsTrue();
        Button last = visible.Single(button => button.Text.StartsWith("Task 4 | ", StringComparison.Ordinal));
        last.SetFocus();
        last.InvokeCommand(Command.Accept);
        await Assert.That(dialog.TargetOption).IsEqualTo("task04");
        Button cancel = visible.Single(button => button.Text == PresentationStrings.Get("TransferCancel", locale));
        cancel.SetFocus();
        cancel.InvokeCommand(Command.Accept);
        await Assert.That(dialog.TargetOption).IsNull();
    }

    private static DefinitionFormModel TaskChooserForm(string locale, int count)
    {
        using var original = Form();
        var runtime = JsonNode.Parse(original.Definition.CanonicalBytes.Span)!.AsObject();
        runtime["locales"] = new JsonArray("ar", "en");
        runtime["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        var options = new JsonArray();
        var artifacts = new JsonArray();
        var values = new JsonArray();
        JsonNode template = runtime["artifacts"]!.AsArray()
            .Single(item => item!["id"]!.GetValue<string>() == "settings")!;
        for (int index = 0; index < count; index++)
        {
            string value = "task" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
            values.Add(value);
            var option = new JsonObject
            {
                ["value"] = value,
                ["title"] = "Task " + index,
                ["artifactIds"] = new JsonArray(value)
            };
            if (index == 0)
                option["nextActions"] = new JsonArray(new JsonObject
                {
                    ["targetOption"] = "task" + (count - 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
                    ["title"] = "Last task",
                    ["priority"] = "optional"
                });
            options.Add(option);
            JsonNode artifact = template.DeepClone();
            artifact["id"] = value;
            artifact["fileName"] = value + ".json";
            artifact["projection"]!["when"]!["value"] = value;
            artifacts.Add(artifact);
        }
        runtime["configuration"]!["properties"]!["task"]!["enum"] = values;
        runtime["configuration"]!["properties"]!["task"]!["x-configurator"]!["initialValue"] = "task00";
        runtime["presentation"]!["outputSelector"]!["options"] = options;
        runtime["artifacts"] = artifacts;
        var form = new DefinitionFormModel(Configurator.Core.Definitions.DefinitionCodec.ParseJson(
            Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        form.SelectLocale(locale);
        return form;
    }

    [Test]
    [NotInParallel]
    [Arguments("en", 80)]
    [Arguments("ar", 80)]
    [Arguments("en", 120)]
    [Arguments("ar", 120)]
    public async Task ReviewActionsFitNarrowAndWideNativeViewports(string locale, int columns)
    {
        using var form = TransferForm();
        form.SelectLocale(locale);
        form.EditValue(["source"], "synthetic-layout");
        using var application = Application.Create();
        application.Init("dotnet");
        using var dialog = new TerminalTransferReviewDialog(form, form.ReviewOutputTransition("identity"), application);
        dialog.BeginInit();
        dialog.EndInit();
        dialog.Frame = new System.Drawing.Rectangle(0, 0, columns * 9 / 10, 17);
        await Assert.That(dialog.Layout(dialog.Frame.Size)).IsTrue();
        Button[] buttons = dialog.SubViews.OfType<Button>().ToArray();
        await Assert.That(buttons.All(button => button.Frame.Left >= 0 && button.Frame.Top >= 0
            && button.Frame.Right <= dialog.Viewport.Width && button.Frame.Bottom <= dialog.Viewport.Height)).IsTrue();
        await Assert.That(buttons.SelectMany((button, index) => buttons.Skip(index + 1)
            .Select(other => System.Drawing.Rectangle.Intersect(button.Frame, other.Frame)))
            .All(overlap => overlap.Width == 0 || overlap.Height == 0)).IsTrue();
    }

    [Test]
    [Arguments("grafana-13.2.2")]
    [Arguments("minimal-self-hosted")]
    [Arguments("secret-generation")]
    public async Task NonTaskExamplesRemainQuietAfterAcknowledgedExport(string example)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Configurator.slnx"))) root = root.Parent;
        using var form = new DefinitionFormModel(Configurator.Core.Definitions.DefinitionSourceCodec.ParseJson(
            await File.ReadAllBytesAsync(Path.Combine(root!.FullName, "examples", example, "setup-definition.json"))));
        string[] eligible = form.EligibleArtifacts.Select(artifact => artifact.Id).ToArray();
        await Assert.That(eligible.Length).IsGreaterThan(0);
        var context = form.CaptureOutputExportContext(form.InputRevision, eligible);
        await Assert.That(form.RecordOutputExport(context, eligible, DefinitionOutputDeliveryKind.NativeSaveVerified)).IsTrue();
        await Assert.That(form.OutputWorkflow.ProposedNextActions).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PartialOrUnknownPublicationAcknowledgesOnlyVerifiedPrefix(bool unknown)
    {
        using var original = Form();
        var runtime = JsonNode.Parse(original.Definition.CanonicalBytes.Span)!.AsObject();
        var artifact = runtime["artifacts"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == "settings")!.DeepClone();
        artifact["id"] = "companion";
        artifact["fileName"] = "companion.json";
        runtime["artifacts"]!.AsArray().Add(artifact);
        runtime["presentation"]!["outputSelector"]!["options"]![0]!["artifactIds"]!.AsArray().Add("companion");
        using var form = new DefinitionFormModel(Configurator.Core.Definitions.DefinitionCodec.ParseJson(
            Encoding.UTF8.GetBytes(runtime.ToJsonString())));
        using var operation = Operation(form, (_, _, observe, _) =>
        {
            var verified = new ProtectedArtifactBundleOutcome("settings", ProtectedArtifactCommitResult.Installed(true));
            observe(verified);
            if (unknown) throw new InvalidOperationException("synthetic-publication-failure");
            return Task.FromResult(new TerminalArtifactResult([verified,
                new("companion", ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.TargetExists))]));
        });
        operation.ArtifactId = "companion";
        operation.ToggleCurrentArtifact();
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        try { await operation.ExecuteAsync(CancellationToken.None); }
        catch (InvalidOperationException) when (unknown) { }
        operation.AcknowledgePublication(operation.PublicationHistory.Single());

        var state = form.OutputWorkflow.Tasks.Single(task => task.Option == "settings");
        await Assert.That(state.Status).IsEqualTo(DefinitionOutputExportStatus.Partial);
        await Assert.That(state.Deliveries.Select(delivery => delivery.ArtifactId)).IsEquivalentTo(["settings"]);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(1);
        await Assert.That(operation.PublicationHistory.Single().IsUnknown).IsEqualTo(unknown);
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task WindowSaveRetainsTransientAnswersAndShowsFollowUpUntilExplicitClear(string locale)
    {
        using var form = TransferForm(secret: true);
        form.SelectLocale(locale);
        form.EditValue(["source"], "synthetic-retained");
        using var application = Application.Create();
        application.Init("dotnet");
        using var operation = Operation(form, (_, _, _, _) => Task.FromResult(
            new TerminalArtifactResult([new("settings", ProtectedArtifactCommitResult.Installed(true))])));
        using var session = new PresentationSession(new StrongReferenceMessenger());
        WorkspaceId.TryCreate("terminal-guidance", out var id);
        using var workspace = session.CreateWorkspace(id, operation);
        workspace.Activate();
        using var secret = new TerminalSecretBuffer(262144, urlSafeOnly: false);
        var callbacks = System.Threading.Channels.Channel.CreateUnbounded<Action>();
        using var window = new TerminalWindow(application, workspace, operation, secret,
            dispatch: callback => callbacks.Writer.TryWrite(callback));
        window.BeginInit();
        window.EndInit();
        window.Frame = new System.Drawing.Rectangle(0, 0, 110, 36);
        window.ApplyViewportPolicy(window.Frame.Size);
        window.Layout(window.Frame.Size);
        Button files = window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get("StageFiles", locale));
        await Assert.That(files.Visible && files.Enabled).IsTrue();
        files.SetFocus();
        bool routed = false;
        files.Accepting += (_, _) => routed = true;
        files.InvokeCommand(Command.Accept);
        await Assert.That(routed).IsTrue();
        await Assert.That(window.ActiveStage).IsEqualTo(TerminalWorkbenchStage.FilesAndSave);
        window.Prepare();
        await window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        while (callbacks.Reader.TryRead(out var callback)) callback();
        window.Save();
        await window.SaveCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        await workspace.WaitForPendingOperationsAsync().WaitAsync(TimeSpan.FromSeconds(5));
        while (callbacks.Reader.TryRead(out var callback)) callback();

        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values["source"].GetString()).IsEqualTo("synthetic-retained");
        await Assert.That(window.SubViews.OfType<Label>().Any(label => label.Visible
            && label.Text.Contains(PresentationStrings.Get("NextActionsTitle", locale), StringComparison.Ordinal))).IsTrue();
        await Assert.That(window.StatusText).DoesNotContain("synthetic-retained");
        window.ClearPrivateState();
        await Assert.That(form.OutputWorkflow.ProposedNextActions).IsEmpty();
        await Assert.That(form.Snapshot().Values.ContainsKey("source")).IsFalse();
        await Assert.That(operation.PublicationHistory.Count).IsEqualTo(1);
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task KeyboardTaskChoiceOpensReviewWithoutChangingAnswers(string locale)
    {
        using var form = TransferForm();
        form.SelectLocale(locale);
        using var dialog = new TerminalOutputTasksDialog(form);
        var state = form.OutputWorkflow.Tasks.Single(task => task.Option == "identity");
        dialog.SubViews.OfType<Button>().Single(button =>
            button.Text.StartsWith("Identity | ", StringComparison.Ordinal)).InvokeCommand(Command.Accept);
        await Assert.That(dialog.TargetOption).IsEqualTo("identity");
        await Assert.That(form.Validate().TryGetEffectiveValue(
            Configurator.Core.Definitions.DefinitionPath.Parse("/task"), out var value)).IsTrue();
        await Assert.That(value.GetString()).IsEqualTo("settings");
        await Assert.That(dialog.SubViews.OfType<Button>().Any(button =>
            button.Text.Contains(PresentationStrings.OutputStatus(state, locale), StringComparison.Ordinal))).IsTrue();
        foreach (string key in new[] { "OutputTasks", "ReuseValue", "TransferSelect", "TransferNextCandidate" })
        {
            await Assert.That(TerminalStrings.Get(key, locale)).IsNotEqualTo(key);
            await Assert.That(TerminalStrings.Get(key, "ar")).IsNotEqualTo(TerminalStrings.Get(key, "en"));
        }
    }

    [Test]
    [NotInParallel]
    [Arguments("en", false)]
    [Arguments("ar", false)]
    [Arguments("en", true)]
    [Arguments("ar", true)]
    public async Task KeyboardReviewRequiresExplicitOverwriteAndMasksProtectedPreview(string locale, bool secret)
    {
        using var form = TransferForm(secret);
        form.SelectLocale(locale);
        form.EditValue(["source"], "synthetic-source");
        form.SelectOutput("identity");
        form.EditValue(["target"], "manual");
        form.SelectOutput("settings");
        var review = form.ReviewOutputTransition("identity");
        await Assert.That(review.Candidates.Count).IsEqualTo(1);
        using var dialog = new TerminalTransferReviewDialog(form, review);
        string detail = dialog.SubViews.OfType<Label>().Single().Text;
        await Assert.That(detail.Contains("synthetic-source", StringComparison.Ordinal)).IsEqualTo(!secret);
        await Assert.That(detail).Contains(PresentationStrings.Get("TransferOverwrite", locale));
        await Assert.That(detail).Contains(TerminalStrings.Get("NotSelected", locale));

        dialog.SubViews.OfType<Button>().Single(button =>
            button.Text == TerminalStrings.Get("TransferSelect", locale)).InvokeCommand(Command.Accept);
        dialog.Apply(false);

        await Assert.That(dialog.TransferResult!.Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["target"].GetString()).IsEqualTo("synthetic-source");
        await Assert.That(form.Snapshot().Values["task"].GetString()).IsEqualTo("identity");
        await Assert.That(dialog.SubViews.OfType<Label>().Single().Text).IsEmpty();
    }

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SwitchWithoutCopyAndUncheckedApplyPreserveSuppliedDestination(bool switchWithoutCopy)
    {
        using var form = TransferForm();
        form.EditValue(["source"], "synthetic-source");
        form.SelectOutput("identity");
        form.EditValue(["target"], "manual");
        form.SelectOutput("settings");
        using var dialog = new TerminalTransferReviewDialog(form, form.ReviewOutputTransition("identity"));

        dialog.Apply(switchWithoutCopy);

        await Assert.That(dialog.TransferResult!.Succeeded).IsTrue();
        await Assert.That(form.Snapshot().Values["target"].GetString()).IsEqualTo("manual");
        await Assert.That(form.Snapshot().Values["task"].GetString()).IsEqualTo("identity");
    }

    [Test]
    [NotInParallel]
    public async Task InlineReviewAdoptsOneUndoableEditAndRejectsStaleReuse()
    {
        using var form = TransferForm();
        form.EditValue(["source"], "synthetic-source");
        form.SelectOutput("identity");
        long revision = form.InputRevision;
        using var dialog = new TerminalTransferReviewDialog(form,
            form.ReviewSuggestion(Configurator.Core.Definitions.DefinitionPath.Parse("/target")));
        dialog.Apply(false);
        await Assert.That(dialog.TransferResult!.Succeeded).IsTrue();
        await Assert.That(form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(form.Snapshot().Values["target"].GetString()).IsEqualTo("synthetic-source");
        form.Undo();
        await Assert.That(form.Snapshot().Values.ContainsKey("target")).IsFalse();
        dialog.Apply(false);
        await Assert.That(dialog.TransferResult!.Succeeded).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task EditingDuringReviewClearsPreviewAndRefusesStaleAdoption()
    {
        using var form = TransferForm();
        form.EditValue(["source"], "synthetic-source");
        using var dialog = new TerminalTransferReviewDialog(form, form.ReviewOutputTransition("identity"));
        form.EditValue(["source"], "changed");
        await Assert.That(dialog.SubViews.OfType<Label>().Single().Text).IsEmpty();

        dialog.Apply(false);

        await Assert.That(dialog.TransferResult!.Succeeded).IsFalse();
        await Assert.That(form.Snapshot().Values.ContainsKey("target")).IsFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OnlyVerifiedNativeSaveOffersFollowUpWithoutSwitching(bool verified)
    {
        using var form = Form();
        using var operation = Operation(form, (_, _, _, _) => Task.FromResult(
            new TerminalArtifactResult([new("settings", ProtectedArtifactCommitResult.Installed(verified))])));
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        await Assert.That(form.OutputWorkflow.ProposedNextActions).IsEmpty();

        await operation.ExecuteAsync(CancellationToken.None);
        operation.AcknowledgePublication(operation.PublicationHistory.Single());

        await Assert.That(form.OutputWorkflow.Tasks.Single(task => task.Option == "settings").Status)
            .IsEqualTo(verified ? DefinitionOutputExportStatus.ExportedCurrent : DefinitionOutputExportStatus.NotExported);
        await Assert.That(form.OutputWorkflow.ProposedNextActions.Count).IsEqualTo(verified ? 1 : 0);
        await Assert.That(form.Validate().TryGetEffectiveValue(
            Configurator.Core.Definitions.DefinitionPath.Parse("/task"), out var selectedTask)).IsTrue();
        await Assert.That(selectedTask.GetString()).IsEqualTo("settings");
        await Assert.That(operation.PublicationHistory.Single().Outcomes.Single().Result!.InstallationEstablished).IsTrue();
    }

    [Test]
    public async Task RefusedSaveDoesNotOfferFollowUp()
    {
        using var form = Form();
        using var operation = Operation(form, (_, _, _, _) => Task.FromResult(
            new TerminalArtifactResult([new("settings",
                ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.UnsupportedPlatform))])));
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);

        await operation.ExecuteAsync(CancellationToken.None);
        operation.AcknowledgePublication(operation.PublicationHistory.Single());

        await Assert.That(form.OutputWorkflow.ProposedNextActions).IsEmpty();
        await Assert.That(form.OutputWorkflow.Tasks.Single(task => task.Option == "settings").Status)
            .IsEqualTo(DefinitionOutputExportStatus.NotExported);
    }

    [Test]
    public async Task EditedAnswersRejectLateVerifiedSaveButRetainPhysicalReceipt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var form = Form();
        using var operation = Operation(form, async (_, _, _, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return new TerminalArtifactResult([new("settings", ProtectedArtifactCommitResult.Installed(true))]);
        });
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        Task execution = operation.ExecuteAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            form.SetValue(["enabled"], JsonSerializer.SerializeToElement(false));
        }
        finally { release.TrySetResult(); }
        await execution.WaitAsync(TimeSpan.FromSeconds(5));
        operation.AcknowledgePublication(operation.PublicationHistory.Single());

        await Assert.That(form.OutputWorkflow.Tasks.Single(task => task.Option == "settings").Status)
            .IsEqualTo(DefinitionOutputExportStatus.NotExported);
        await Assert.That(form.OutputWorkflow.ProposedNextActions).IsEmpty();
        await Assert.That(operation.PublicationHistory.Single().Outcomes.Single().Result!.VerificationSucceeded).IsTrue();
    }

    private static TerminalArtifactOperation Operation(DefinitionFormModel form,
        Func<IReadOnlyList<DefinitionArtifact>, string, Action<ProtectedArtifactBundleOutcome>,
            CancellationToken, Task<TerminalArtifactResult>> publish) =>
        new(form, new AvailableWriter(), Path.GetTempPath(), publishBundle: publish);

    private sealed class AvailableWriter : IProtectedArtifactWriter
    {
        public bool IsAvailable => true;
        public Task<ProtectedArtifactPreparation> PrepareAsync(Configurator.Core.ArtifactKind kind, string targetPath,
            ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static DefinitionFormModel TransferForm(bool secret = false)
    {
        using var original = Form();
        var runtime = JsonNode.Parse(original.Definition.CanonicalBytes.Span)!.AsObject();
        runtime["locales"] = new JsonArray("ar", "en");
        runtime["localization"] = JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
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
        runtime["requiredCapabilities"] = new JsonArray(runtime["requiredCapabilities"]!.AsArray()
            .Select(item => item!.GetValue<string>()).Append("values.suggested-from")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(item => JsonValue.Create(item)).ToArray());
        return new(Configurator.Core.Definitions.DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(runtime.ToJsonString())));
    }

    private static DefinitionFormModel Form() => new(DefinitionAuthoringCompiler.Compile(
        DefinitionAuthoringCodec.ParseJson(Encoding.UTF8.GetBytes("""
        {
          "$schema":"urn:islamu:configurator:authoring:v0.0.1",
          "identity":{"familyId":"94640ed8-df36-4e49-b18f-d9db56049cee","revision":1},
          "project":{"id":"terminal-workflow","name":"Terminal workflow"},"applicationVersions":["1.0"],
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
                {"targetOption":"identity","title":"Prepare identity","priority":"recommended"}]},
              {"value":"identity","title":"Identity","artifactIds":["identity"]}
            ]}},
          "outputs":{
            "settings.json":{"id":"settings","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"settings"}}},
            "identity.json":{"id":"identity","format":"json","projection":{"op":"literal","value":{},
              "when":{"op":"equals","reference":{"scope":"root","path":"/task"},"value":"identity"}}}
          }
        }
        """))));
}
