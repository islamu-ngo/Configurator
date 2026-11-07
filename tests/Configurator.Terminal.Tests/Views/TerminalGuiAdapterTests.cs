namespace Configurator.Terminal.Tests;

using System.Drawing;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;
using global::Terminal.Gui.App;
using global::Terminal.Gui.Input;
using global::Terminal.Gui.Text;
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

public sealed class TerminalGuiAdapterTests
{
#if CONFIGURATOR_NATIVE_IMPORT_PROBE
    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DiagnosticNaturalStatusDrawCompletesWithoutDependingOnDeferredSentinel(bool coalescing)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(initialize: true, beforePreparation: token => release.Task.WaitAsync(token));
        var window = harness.Window;
        IApplication application = window.ProbeApplication;
        Task<bool> proof = Configurator.Engineering.MeasureNativePreparation.VerifyTerminalFeedbackOrderingAsync(window, () =>
        {
            if (coalescing) window.Prepare();
            window.Validate();
        }, coalescing);
        Task stop = proof.ContinueWith(_ =>
        {
            application.AddTimeout(TimeSpan.Zero, () =>
            {
                application.RequestStop(window);
                return false;
            });
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        object watchdog = application.AddTimeout(TimeSpan.FromSeconds(12), () =>
        {
            application.RequestStop(window);
            return false;
        }) ?? throw new InvalidOperationException("diagnostic-owner-loop-required");
        try
        {
            application.Run(window);
            await Assert.That(await proof.WaitAsync(TimeSpan.FromSeconds(1))).IsTrue();
            await stop.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            release.TrySetResult();
            application.RemoveTimeout(watchdog);
        }
    }
#endif

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task RootSecretGenerationUsesLiteralDynamicKeyAndStaysMasked(string locale)
    {
        var source = EditingSource("");
        source.Remove("presentation");
        source["configuration"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
             "type":"string","x-configurator":{"sensitivity":"secret","secret":{
              "kind":"password","acquisition":"local","generation":{"length":32}}}}}
            """);
        source["localization"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(
            System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SelectLocale(locale);
        using var harness = new Harness(initialize: true, manualDispatch: true, form: form);
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("literal/key")).IsTrue();
        harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("AddMapEntry", locale))
            .InvokeCommand(Command.Accept);
        harness.DrainCallbacks();
        Button generate = harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get("GenerateSecret", locale));
        await Assert.That(generate.Enabled).IsTrue();
        generate.InvokeCommand(Command.Accept);
        await harness.Window.SecretCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        harness.DrainCallbacks();
        string generated = form.Snapshot().Values["literal/key"].GetString()!;
        await Assert.That(generated.Length).IsEqualTo(32);
        await Assert.That(harness.Window.MaskedInput.Visible).IsTrue();
        await Assert.That(harness.Window.PublicInput.Text.Contains(generated, StringComparison.Ordinal)).IsFalse();
        await Assert.That(harness.Window.StatusText.Contains(generated, StringComparison.Ordinal)).IsFalse();
        await Assert.That(form.Fields).IsEmpty();
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task RootRecursiveActionsPreserveNullOmissionResetAndSuggestion(string locale)
    {
        var source = EditingSource("");
        source["configuration"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["mode"],"x-configurator":{"sensitivity":"public"},
              "properties":{"mode":{"type":"string","const":"disk","x-configurator":{"sensitivity":"public"}},
               "batch":{"type":["integer","null"],"x-configurator":{"sensitivity":"public","initialValue":2,"suggestedValue":3}}}},
             {"type":"object","additionalProperties":false,"required":["mode"],"x-configurator":{"sensitivity":"public"},
              "properties":{"mode":{"type":"string","const":"queue","x-configurator":{"sensitivity":"public"}}}}]}
            """);
        source.Remove("presentation");
        source["localization"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(
            System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SelectLocale(locale);
        form.SetAllSectionsExpanded(true);
        using var harness = new Harness(initialize: true, form: form);
        void Click(string key) => harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get(key, locale)).InvokeCommand(Command.Accept);
        Click("ChooseVariant");
        for (int step = 0; step < 3 && !(harness.Window.PublicInput.Visible && harness.Window.PublicInput.Text == "2"); step++)
            harness.Window.MoveField(1);
        await Assert.That(harness.Window.PublicInput.Text).IsEqualTo("2");
        Click("ApplySuggestion");
        await Assert.That(form.Snapshot().Values["batch"].GetInt32()).IsEqualTo(3);
        Click("SetNull");
        await Assert.That(form.Snapshot().Values["batch"].ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Null);
        Click("OmitValue");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/batch"), out _)).IsFalse();
        Click("ResetValue");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/batch"), out var initialized)).IsTrue();
        await Assert.That(initialized.GetInt32()).IsEqualTo(2);
        await Assert.That(harness.Window.PublicInput.Visible).IsTrue();
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task RootProtectedContainersMaskOrdinaryChildrenAndKeepKeyboardNavigationOutOfBuffers(string locale)
    {
        var source = EditingSource("");
        source.Remove("presentation");
        source["configuration"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"type":"object","x-configurator":{"sensitivity":"public"},"additionalProperties":{
             "type":"object","x-configurator":{"sensitivity":"secret"},"additionalProperties":false,
             "properties":{"child":{"type":"string","x-configurator":{"sensitivity":"public"}}}}}
            """);
        source["localization"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(
            System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SelectLocale(locale);
        form.SetAllSectionsExpanded(true);
        using var harness = new Harness(initialize: true, form: form);
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("private/key")).IsTrue();
        harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("AddMapEntry", locale))
            .InvokeCommand(Command.Accept);
        harness.Window.MoveField(1);
        await Assert.That(harness.Window.MaskedInput.Visible).IsTrue();
        await Assert.That(harness.Window.PublicInput.Visible).IsFalse();
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("synthetic-protected-child")).IsTrue();
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values["private/key"].GetProperty("child").GetString()).IsEqualTo("synthetic-protected-child");
        harness.Window.MaskedInput.NewKeyDownEvent(Key.Tab);
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values["private/key"].GetProperty("child").GetString()).IsEqualTo("synthetic-protected-child");
        await Assert.That(harness.Window.StatusText).DoesNotContain("synthetic-protected-child");
        await Assert.That(harness.Window.SubViews.OfType<Label>().Any(label =>
            label.Text.Contains("private/key", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task RootVariantSwitchRetiresOldBranchAndReachesCorrection(string locale)
    {
        var source = EditingSource("");
        source["configuration"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["mode","directory"],"x-configurator":{"sensitivity":"public"},
              "properties":{"mode":{"type":"string","const":"disk","x-configurator":{"sensitivity":"public"}},
               "directory":{"type":"string","minLength":1,"x-configurator":{"sensitivity":"public"}}}},
             {"type":"object","additionalProperties":false,"required":["mode","endpoint","token"],"x-configurator":{"sensitivity":"public"},
              "properties":{"mode":{"type":"string","const":"queue","x-configurator":{"sensitivity":"public"}},
               "endpoint":{"title":"Endpoint","type":"string","minLength":1,"x-configurator":{"sensitivity":"public"}},
               "token":{"title":"Token","type":"string","minLength":1,"writeOnly":true,"x-configurator":{"sensitivity":"secret"}}}}]}
            """);
        source.Remove("presentation");
        source["localization"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
            """);
        using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(
            System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.SelectLocale(locale);
        form.SetAllSectionsExpanded(true);
        using var harness = new Harness(initialize: true, form: form);
        void ChooseBranch() => harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get("ChooseVariant", locale))
            .InvokeCommand(Command.Accept);
        ChooseBranch();
        harness.Window.FocusNextCorrection();
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("spool")).IsTrue();
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values["directory"].GetString()).IsEqualTo("spool");
        bool AtBranch() => harness.Window.SubViews.OfType<Button>().Any(button =>
            button.Visible && button.Text == TerminalStrings.Get("ChooseVariant", locale));
        for (int step = 0; step < 4 && !AtBranch(); step++) harness.Window.MoveField(1);
        await Assert.That(AtBranch()).IsTrue();
        ChooseBranch();
        await Assert.That(form.Snapshot().Values.ContainsKey("directory")).IsFalse();
        bool FocusCorrection(string title)
        {
            for (int step = 0; step < 2; step++)
            {
                harness.Window.FocusNextCorrection();
                if (harness.Window.SubViews.OfType<Label>().Any(label =>
                    label.Text.Contains($" {title} (", StringComparison.Ordinal))) return true;
            }
            return false;
        }
        await Assert.That(FocusCorrection("Endpoint")).IsTrue();
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("queue.example.invalid")).IsTrue();
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(FocusCorrection("Token")).IsTrue();
        await Assert.That(harness.Window.MaskedInput.Visible).IsTrue();
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("synthetic-root-token")).IsTrue();
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values["token"].GetString()).IsEqualTo("synthetic-root-token");
        await Assert.That(form.Snapshot().Values["mode"].GetString()).IsEqualTo("queue");
        await Assert.That(form.Validate().IsReady).IsTrue();
        await Assert.That(harness.Window.PublicInput.Text).DoesNotContain("synthetic-root-token");
        await Assert.That(harness.Window.StatusText).DoesNotContain("synthetic-root-token");
        await Assert.That(harness.Window.SubViews.OfType<Label>().Any(label =>
            label.Text.Contains("synthetic-root-token", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task ReadonlyNullableFieldHidesSharedNullResetAndSuggestionActions()
    {
        using var harness = new Harness(initialize: true, form: new DefinitionFormModel(EditingDefinition("""
            "value":{"title":"Fixed","type":["integer","null"],"readOnly":true,
              "x-configurator":{"sensitivity":"public","detail":"basic","initialValue":4,"suggestedValue":7}}
            """)));
        foreach (string key in new[] { "SetNull", "ResetValue", "ApplySuggestion" })
            await Assert.That(harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get(key)).Visible).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task PreparationFaultDoesNotAskForAnswerCorrections()
    {
        using var harness = new Harness(manualDispatch: true,
            beforePreparation: _ => throw new InvalidOperationException("synthetic-fault"));
        harness.Window.Prepare();
        await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        harness.DrainCallbacks();
        await Assert.That(harness.Window.StatusText).IsEqualTo(TerminalStrings.Get("PreparationFailed"));
        await Assert.That(harness.Window.StatusText).DoesNotContain("synthetic-fault");
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task EarlyScopeActionChangesOneRevisionAndRestrictsDestinationNavigation()
    {
        using var form = new DefinitionFormModel(DeploymentDefinition());
        form.SetValue(["dashboardName"], System.Text.Json.JsonSerializer.SerializeToElement("dashboard"));
        using var harness = new Harness(initialize: true, manualDispatch: true,
            form: form);
        Button scope = harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text.Contains("Dashboard only", StringComparison.Ordinal));
        long revision = harness.Form.InputRevision;
        await Assert.That(scope.SetFocus()).IsTrue();
        scope.InvokeCommand(Command.Accept);
        harness.DrainCallbacks();
        await Assert.That(harness.Form.InputRevision).IsEqualTo(revision + 1);
        await Assert.That(harness.Form.Snapshot().Values["scope"].GetString()).IsEqualTo("dashboard");
        await Assert.That(harness.Form.Validate().IsReady).IsTrue();
        await Assert.That(harness.Operation.ArtifactId).IsEqualTo("dashboard");
        await Assert.That(harness.Operation.SelectedArtifactIds.SequenceEqual(["dashboard"])).IsTrue();
        Button artifact = harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get("Artifact"));
        artifact.InvokeCommand(Command.Accept);
        await Assert.That(harness.Operation.ArtifactId).IsEqualTo("dashboard");
        await Assert.That(() => harness.Operation.ArtifactId = "worker").Throws<InvalidOperationException>();
        await Assert.That(harness.Window.SubViews.OfType<Label>().Any(label =>
            label.Text.Contains("Worker password", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task ScopeChangeRejectsHeldPreparationAndPreservesInactiveAnswers()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var form = new DefinitionFormModel(DeploymentDefinition());
        form.SetValue(["workerPassword"], System.Text.Json.JsonSerializer.SerializeToElement("synthetic-worker-sentinel"));
        form.SetValue(["dashboardName"], System.Text.Json.JsonSerializer.SerializeToElement("dashboard"));
        using var harness = new Harness(manualDispatch: true, form: form, beforePreparation: async token =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
        });
        harness.Operation.ArtifactId = "worker";
        harness.Operation.ToggleCurrentArtifact();
        harness.Window.Prepare();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text.Contains("Dashboard only", StringComparison.Ordinal))
                .InvokeCommand(Command.Accept);
            harness.DrainCallbacks();
        }
        finally { release.TrySetResult(); }
        await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        harness.DrainCallbacks();
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
        await Assert.That(form.Snapshot().Values["workerPassword"].GetString()).IsEqualTo("synthetic-worker-sentinel");
        await Assert.That(harness.Window.StatusText).DoesNotContain("synthetic-worker-sentinel");
        await Assert.That(harness.Operation.SelectedArtifactIds.SequenceEqual(["dashboard"])).IsTrue();
    }

    private static Definition DeploymentDefinition()
    {
        var source = EditingSource("""
            "recipe":{"title":"Recipe","type":"string","enum":["split"],"x-configurator":{"sensitivity":"public","initialValue":"split"}},
            "scope":{"title":"Scope","type":"string","enum":["all","dashboard"],"x-configurator":{"sensitivity":"public","initialValue":"all"}},
            "workerPassword":{"title":"Worker password","type":"string","x-configurator":{"sensitivity":"secret"}},
            "dashboardName":{"title":"Dashboard name","type":"string","x-configurator":{"sensitivity":"public"}}
            """, """["recipe","scope","workerPassword","dashboardName"]""");
        source["deployment"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"recipePointer":"/recipe","scopePointer":"/scope",
             "components":[{"id":"worker","title":"Worker"},{"id":"dashboard","title":"Dashboard"}],
             "recipes":[{"id":"split","title":"Separate processes","scopes":[
               {"id":"all","title":"All components","componentIds":["worker","dashboard"],"artifactIds":["worker","dashboard"]},
               {"id":"dashboard","title":"Dashboard only","componentIds":["dashboard"],"artifactIds":["dashboard"],"primaryArtifactId":"dashboard"}]}]}
            """);
        source["outputs"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"worker.json":{"id":"worker","format":"json","targetUpdateMode":"create","restartRequirement":"process",
              "consumerIds":["worker"],"bindings":{"/password":{"scope":"root","path":"/workerPassword"}}},
             "dashboard.json":{"id":"dashboard","format":"json","targetUpdateMode":"create","restartRequirement":"process",
              "consumerIds":["dashboard"],"bindings":{"/name":{"scope":"root","path":"/dashboardName"}}}}
            """);
        return DefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    [Test]
    [NotInParallel]
    public async Task ScopedNativeSaveCreatesOnlyItsDeclaredRecipientFile()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("configurator-scoped-terminal-").FullName;
        try
        {
            using var form = new DefinitionFormModel(DeploymentDefinition());
            form.SetValue(["workerPassword"], System.Text.Json.JsonSerializer.SerializeToElement("synthetic-worker-sentinel"));
            form.SetValue(["dashboardName"], System.Text.Json.JsonSerializer.SerializeToElement("dashboard"));
            using var harness = new Harness(initialize: true, directory: directory, manualDispatch: true, form: form);
            harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text.Contains("Dashboard only", StringComparison.Ordinal))
                .InvokeCommand(Command.Accept);
            harness.DrainCallbacks();
            harness.Window.Prepare();
            await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            await Assert.That(harness.Operation.IsPrepared).IsTrue();
            var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Workspace.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PresentationWorkspace.IsBusy) && !harness.Workspace.IsBusy)
                    settled.TrySetResult();
            };
            harness.Window.Save();
            await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(directory, "dashboard.json")))
                .IsEqualTo("""{"name":"dashboard"}""");
            await Assert.That(File.Exists(Path.Combine(directory, "worker.json"))).IsFalse();
            await Assert.That(File.GetUnixFileMode(Path.Combine(directory, "dashboard.json")))
                .IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    [NotInParallel]
    public async Task RtlLocaleAlignsMetadataWithoutReversingLatinFallback()
    {
        using var harness = new Harness(initialize: true, manualDispatch: true);
        long revision = harness.Form.InputRevision;
        string name = harness.Form.Snapshot().Values["name"].GetString()!;
        await harness.ChangeView(() => harness.Form.SelectLocale("ar"));
        Label label = harness.Window.SubViews.OfType<Label>()
            .Single(item => item.Text.StartsWith("1/", StringComparison.Ordinal));
        await Assert.That(label.TextDirection).IsEqualTo(TextDirection.LeftRight_TopBottom);
        await Assert.That(label.TextAlignment).IsEqualTo(Alignment.End);
        await Assert.That(harness.Form.InputRevision).IsEqualTo(revision);
        await Assert.That(harness.Form.Snapshot().Values["name"].GetString()).IsEqualTo(name);
    }

    [Test]
    [NotInParallel]
    public async Task InlineEditsRefreshPresenceWithoutReplacingTheEditor()
    {
        using var harness = new Harness(initialize: true, form: new DefinitionFormModel(EditingDefinition("""
            "value":{"title":"Value","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}
            """)));
        Label label = harness.Window.SubViews.OfType<Label>()
            .Single(item => item.Text.StartsWith("1/1 Value ", StringComparison.Ordinal));
        var editor = harness.Window.PublicInput;
        await Assert.That(label.Text).Contains(" | " + TerminalStrings.Get("Omitted"));
        editor.Text = "supplied";
        await Assert.That(harness.Form.Snapshot().Values["value"].GetString()).IsEqualTo("supplied");
        await Assert.That(label.Text).DoesNotContain(" | " + TerminalStrings.Get("Omitted"));
        await Assert.That(ReferenceEquals(editor, harness.Window.PublicInput)).IsTrue();
    }

    [Test]
    public async Task ExplicitEmptyEnumChoiceIsNotOmission()
    {
        using var form = new DefinitionFormModel(EditingDefinition("""
            "choice":{"title":"Choice","type":"string","enum":["","other"],"x-configurator":{"sensitivity":"public","detail":"basic"}}
            """));
        var slot = new TerminalFieldEditor(form).Slots.Single();
        await Assert.That(TerminalFieldEditor.Write(slot, "")).IsTrue();
        await Assert.That(form.Snapshot().Values["choice"].GetRawText()).IsEqualTo("\"\"");
        await Assert.That(TerminalFieldEditor.Clear(slot)).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("choice")).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task DeliberateEmptyRowAndErasedRowAgreeWhileNavigationDoesNotRevise()
    {
        using var harness = new Harness(initialize: true, form: new DefinitionFormModel(EditingDefinition("""
            "items":{"title":"Items","type":"array","items":{"type":"string","x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public","detail":"basic"}},
            "after":{"title":"After","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}
            """)));
        Button add = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("AddItem"));
        add.InvokeCommand(Command.Accept);
        await Assert.That(harness.Form.Snapshot().Values["items"].GetRawText()).IsEqualTo("""[""]""");
        harness.Window.PublicInput.Text = "erase";
        harness.Window.PublicInput.Text = "";
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(harness.Form.Snapshot().Values["items"].GetRawText()).IsEqualTo("""[""]""");
        long before = harness.Form.InputRevision;
        harness.Window.MoveField(1);
        harness.Window.MoveField(-1);
        await Assert.That(harness.Form.InputRevision).IsEqualTo(before);
    }

    [Test]
    [Arguments("boolean")]
    [Arguments("integer")]
    [Arguments("number")]
    public async Task UntouchedTypedAppendCanBeAbandoned(string type)
    {
        using var form = new DefinitionFormModel(EditingDefinition($$$$"""
            "items":{"title":"Items","type":"array","items":{"type":"{{{{type}}}}","x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public","detail":"basic"}}
            """));
        var slot = new TerminalFieldEditor(form).Slots.Single();
        await Assert.That(TerminalFieldEditor.Write(slot, string.Empty)).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("items")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(0);
    }

    [Test]
    [NotInParallel]
    public async Task ChoiceCyclingStartsFromVisibleDraftAndRetainsRejectedInput()
    {
        using var harness = new Harness(initialize: true, form: new DefinitionFormModel(EditingDefinition("""
            "choice":{"title":"Choice","type":"string","enum":["one","two","three"],"x-configurator":{"sensitivity":"public","detail":"basic"}}
            """)));
        Button choice = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("Choose"));
        harness.Window.PublicInput.Text = "two";
        choice.InvokeCommand(Command.Accept);
        await Assert.That(harness.Window.PublicInput.Text).IsEqualTo("three");
        harness.Window.PublicInput.Text = "invalid-visible-draft";
        choice.InvokeCommand(Command.Accept);
        await Assert.That(harness.Window.PublicInput.Text).IsEqualTo("invalid-visible-draft");
        // V2 retains typed but constraint-invalid values for correction; cycling
        // must not silently replace that explicit input with a declared choice.
        await Assert.That(harness.Form.Snapshot().Values["choice"].GetString()).IsEqualTo("invalid-visible-draft");
        await Assert.That(harness.Form.Validate().IsReady).IsFalse();
    }

    [Test]
    [NotInParallel]
    [Arguments("scalar", false)]
    [Arguments("nested", false)]
    [Arguments("list", false)]
    [Arguments("scalar", true)]
    [Arguments("nested", true)]
    [Arguments("list", true)]
    public async Task DisclosureChangesRestoreExactRejectedDraftWithoutChangingAnswers(string shape, bool masked)
    {
        string accepted = shape switch
        {
            "nested" => """{"leaf":7,"sibling":"kept"}""",
            "list" => "[7,9]",
            _ => "7"
        };
        using var form = new DefinitionFormModel(CorrectionDefinition(shape, masked));
        if (masked)
        {
            using var value = System.Text.Json.JsonDocument.Parse(accepted);
            form.SetValue(["value"], value.RootElement);
        }
        DefinitionAnswerOrigin origin = masked ? DefinitionAnswerOrigin.Explicit : DefinitionAnswerOrigin.Defaulted;
        using var harness = new Harness(initialize: true, form: form, manualDispatch: true);
        string[] path = shape switch
        {
            "nested" => ["value", "leaf"],
            "list" => ["value", "1"],
            _ => ["value"]
        };
        if (shape == "nested") harness.Window.MoveField(1);
        if (shape == "list")
            harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("NextItem"))
                .InvokeCommand(Command.Accept);
        string identity = form.GetEditorIdentity(path);
        string draft = masked ? shape == "list" ? "9x" : "7x" : "  not-an-integer  ";
        if (masked)
        {
            harness.Window.MaskedInput.SetFocus();
            await Assert.That(harness.Window.MaskedInput.NewKeyDownEvent(new Key('x'))).IsTrue();
        }
        else harness.Window.PublicInput.Text = draft;
        await Assert.That(harness.Window.CommitField()).IsFalse();
        long revision = form.InputRevision;
        DefinitionAnswerOrigin rejectedOrigin = form.AnswerOrigin(path);

        foreach (Action change in new Action[]
            { harness.Window.ToggleOptionalEditors, harness.Window.ToggleOptionalEditors, harness.Window.CycleExperience })
        {
            await harness.ChangeView(change);
            byte[] bytes = masked ? harness.Secret.CopyUtf8Bytes() : [];
            try
            {
                await Assert.That(masked ? System.Text.Encoding.UTF8.GetString(bytes) : harness.Window.PublicInput.Text)
                    .IsEqualTo(draft);
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
            if (masked)
            {
                await Assert.That(harness.Window.MaskedInput.Text).DoesNotContain(draft);
                await Assert.That(harness.Window.PublicInput.Text).IsEmpty();
            }
            await Assert.That(harness.Window.CommitField()).IsFalse();
            await Assert.That(form.ReadDraftText(path)).IsEqualTo(draft);
            await Assert.That(form.Snapshot().Values.ContainsKey("value")).IsEqualTo(masked);
            await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/value"), out var effectiveValue)).IsTrue();
            await Assert.That(effectiveValue.GetRawText()).IsEqualTo(accepted);
            await Assert.That(form.Snapshot().Values.ContainsKey("after")).IsFalse();
            await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/after"), out var effectiveAfter)).IsTrue();
            await Assert.That(effectiveAfter.GetString()).IsEqualTo("untouched");
            await Assert.That(form.AnswerOrigin(path)).IsEqualTo(rejectedOrigin);
            await Assert.That(form.AnswerOrigin(["after"])).IsEqualTo(DefinitionAnswerOrigin.Defaulted);
            await Assert.That(form.GetEditorIdentity(path)).IsEqualTo(identity);
            await Assert.That(form.InputRevision).IsEqualTo(revision);
        }

        if (masked) harness.Window.MaskedInput.TryReplaceSensitiveInput("11");
        else harness.Window.PublicInput.Text = "11";
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.ReadDraftText(path)).IsNull();
        await Assert.That(form.AnswerOrigin(path)).IsEqualTo(DefinitionAnswerOrigin.Explicit);
        await Assert.That(form.AnswerOrigin(["after"])).IsEqualTo(DefinitionAnswerOrigin.Defaulted);
        await Assert.That(form.GetEditorIdentity(path)).IsEqualTo(identity);
        if (shape == "list")
        {
            await Assert.That(form.Snapshot().Values["value"].GetRawText()).IsEqualTo("[7,11]");
            await Assert.That(form.AnswerOrigin(["value", "0"])).IsEqualTo(origin);
        }
        if (shape == "nested")
        {
            await Assert.That(form.Snapshot().Values["value"].GetRawText()).IsEqualTo("""{"leaf":11,"sibling":"kept"}""");
            await Assert.That(form.AnswerOrigin(["value", "sibling"])).IsEqualTo(origin);
        }
    }

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DisclosureChangesLeaveUntouchedAppendSlotsAbsent(bool existingItem)
    {
        using var form = new DefinitionFormModel(EditingDefinition("""
            "items":{"title":"Items","type":"array","items":{"type":"integer","x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public","detail":"basic"}}
            """));
        if (existingItem)
        {
            using var items = System.Text.Json.JsonDocument.Parse("[7]");
            form.SetValue(["items"], items.RootElement);
        }
        using var harness = new Harness(initialize: true, form: form, manualDispatch: true);
        if (existingItem)
            harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("NextItem"))
                .InvokeCommand(Command.Accept);
        long revision = form.InputRevision;
        await harness.ChangeView(harness.Window.ToggleOptionalEditors);
        await harness.ChangeView(harness.Window.ToggleOptionalEditors);
        await Assert.That(harness.Window.PublicInput.Text).IsEmpty();
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values.ContainsKey("items")).IsEqualTo(existingItem);
        if (existingItem) await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[7]");
        await Assert.That(form.ReadDraftText(["items", existingItem ? "1" : "0"])).IsNull();
        await Assert.That(form.EditValue(["items", "40"], "invalid")).IsFalse();
        await Assert.That(form.ReadDraftText(["items", "40"])).IsNull();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
    }

    [Test]
    [NotInParallel]
    public async Task DisclosureChangesCannotRestoreSecretListDraftAfterCustodyRevocation()
    {
        using var form = new DefinitionFormModel(CorrectionDefinition("list", true),
            DefinitionSessionPrivacy.BrowserSecretsEnabled);
        using var items = System.Text.Json.JsonDocument.Parse("[7,9]");
        form.SetValue(["value"], items.RootElement);
        using var harness = new Harness(initialize: true, form: form, manualDispatch: true);
        harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("NextItem"))
            .InvokeCommand(Command.Accept);
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("synthetic-invalid")).IsTrue();
        await Assert.That(harness.Window.CommitField()).IsFalse();
        await Assert.That(form.ReadDraftText(["value", "1"])).IsEqualTo("synthetic-invalid");
        form.SetSecretEntryEnabled(false);
        await harness.ChangeView(harness.Window.ToggleOptionalEditors);
        await Assert.That(harness.Secret.Count).IsEqualTo(0);
        await Assert.That(harness.Window.MaskedInput.Text).IsEmpty();
        await Assert.That(harness.Window.PublicInput.Text).IsEmpty();
        await Assert.That(form.ReadDraftText(["value", "1"])).IsNull();
        await Assert.That(form.Snapshot().Values.ContainsKey("value")).IsFalse();
        form.SetSecretEntryEnabled(true);
        await harness.ChangeView(harness.Window.CycleExperience);
        await Assert.That(harness.Secret.Count).IsEqualTo(0);
        await Assert.That(form.ReadDraftText(["value", "1"])).IsNull();
        await Assert.That(form.Snapshot().Values.ContainsKey("value")).IsFalse();
    }

    private static Definition CorrectionDefinition(string shape, bool masked)
    {
        string field = shape switch
        {
            "nested" => """
                "type":"object","additionalProperties":false,"properties":{
                  "leaf":{"title":"Leaf","type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}},
                  "sibling":{"title":"Sibling","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}}
                """,
            "list" => "\"type\":\"array\",\"items\":{\"type\":\"integer\",\"x-configurator\":{\"sensitivity\":\"public\"}}",
            _ => "\"type\":\"integer\""
        };
        string initialValue = shape switch
        {
            "nested" => """{"leaf":7,"sibling":"kept"}""",
            "list" => "[7,9]",
            _ => "7"
        };
        var source = EditingSource($$$$"""
            "value":{"title":"Value","x-configurator":{"sensitivity":"{{{{(masked ? "secret" : "public")}}}}","detail":"basic"{{{{(masked ? "" : ",\"initialValue\":" + initialValue)}}}}},{{{{field}}}}},
            "after":{"title":"After","type":"string","x-configurator":{"initialValue":"untouched","sensitivity":"public","detail":"basic"}}
            """);
        source["presentation"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"defaultJourney":"guided","navigation":"sections",
             "sections":[{"id":"settings","title":"Settings","pointers":["/value","/after"]}],
             "journeys":[
              {"id":"guided","title":"Guided","sections":["settings"]},
              {"id":"complete","title":"Complete","sections":["settings"]}]}
            """);
        return DefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    [Test]
    [NotInParallel]
    [Arguments(0)]
    [Arguments(1)]
    public async Task FullListNavigationNeverEntersAnUncommittableAppend(int maximum)
    {
        using var form = new DefinitionFormModel(EditingDefinition($$$$"""
            "items":{"title":"Items","type":"array","items":{"type":"integer","x-configurator":{"sensitivity":"public"}},"maxItems":{{{{maximum}}}},"x-configurator":{"sensitivity":"public","detail":"basic"}},
            "after":{"title":"After","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}
            """));
        using var items = System.Text.Json.JsonDocument.Parse(maximum == 0 ? "[]" : "[7]");
        form.SetValue(["items"], items.RootElement);
        using var harness = new Harness(initialize: true, form: form);
        Button nextItem = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("NextItem"));
        nextItem.InvokeCommand(Command.Accept);
        harness.Window.MoveField(1);
        harness.Window.PublicInput.Text = "reachable";
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values["after"].GetString()).IsEqualTo("reachable");
    }

    private static Definition EditingDefinition(string properties, string required = "[]") =>
        DefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(EditingSource(properties, required).ToJsonString()));

    private static System.Text.Json.Nodes.JsonObject EditingSource(string properties, string required = "[]")
    {
        var source = System.Text.Json.Nodes.JsonNode.Parse($$$$$$"""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"9282a2b1-84a0-436d-8c4d-1c75b81d314c","revision":1},
             "project":{"id":"example","name":"Example","publisher":"example"},"applicationVersions":["1"],"entryPoints":["setup"],
             "configuration":{"type":"object","additionalProperties":false,"required":{{{{{{required}}}}}},
               "x-configurator":{"sensitivity":"public"},"properties":{ {{{{{{properties}}}}}} }},
             "outputs":{"config.json":{"id":"config","format":"json","targetUpdateMode":"create","restartRequirement":"process",
               "projection":{"op":"object","members":{}}}}}
            """)!.AsObject();
        var pointers = new System.Text.Json.Nodes.JsonArray();
        foreach (string name in source["configuration"]!["properties"]!.AsObject().Select(property => property.Key))
            pointers.Add(DefinitionPath.FromSegments([name]).ToPointer());
        source["presentation"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"defaultJourney":"guided","navigation":"sections",
             "sections":[{"id":"settings","title":"Settings","pointers":[]}],
             "journeys":[{"id":"guided","title":"Guided","sections":["settings"]}]}
            """);
        source["presentation"]!["sections"]![0]!["pointers"] = pointers;
        return source;
    }

    [Test]
    [NotInParallel]
    public async Task ContainerControlsSupplyAndClearEmptyValuesWithoutImplicitPresence()
    {
        using var harness = new Harness(initialize: true, form: new DefinitionFormModel(EditingDefinition("""
            "object":{"title":"Object","type":"object","additionalProperties":false,"properties":{},"x-configurator":{"sensitivity":"public","detail":"basic"}},
            "list":{"title":"List","type":"array","items":{"type":"integer","x-configurator":{"sensitivity":"public"}},"maxItems":0,"x-configurator":{"sensitivity":"public","detail":"basic"}}
            """, """["object","list"]""")));
        Button supply = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("SupplyContainer"));
        Button clear = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("OmitValue"));
        await Assert.That(harness.Window.PublicInput.Visible).IsFalse();
        await Assert.That(supply.Visible).IsTrue();
        supply.InvokeCommand(Command.Accept);
        await Assert.That(harness.Form.Snapshot().Values["object"].GetRawText()).IsEqualTo("{}");
        clear.InvokeCommand(Command.Accept);
        await Assert.That(harness.Form.Snapshot().Values.ContainsKey("object")).IsFalse();
        harness.Window.MoveField(1);
        await Assert.That(harness.Window.PublicInput.Visible).IsFalse();
        supply.InvokeCommand(Command.Accept);
        await Assert.That(harness.Form.Snapshot().Values["list"].GetRawText()).IsEqualTo("[]");
        clear.InvokeCommand(Command.Accept);
        await Assert.That(harness.Form.Snapshot().Values.ContainsKey("list")).IsFalse();
    }

    [Test]
    public async Task BrowsingBlankObjectChildPreservesAbsenceAndRevision()
    {
        using var harness = new Harness(form: new DefinitionFormModel(EditingDefinition("""
            "object":{"title":"Object","type":"object","additionalProperties":false,"required":["child"],
              "x-configurator":{"sensitivity":"public","detail":"basic"},"properties":{
                "child":{"title":"Child","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}}},
            "after":{"title":"After","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}
            """)));
        harness.Window.MoveField(1);
        harness.Window.MoveField(1);
        await Assert.That(harness.Form.Snapshot().Values.ContainsKey("object")).IsFalse();
        await Assert.That(harness.Form.InputRevision).IsEqualTo(0);
    }

    [Test]
    [NotInParallel]
    public async Task SupplyListPreservesRejectedDraftAndCommitsValidVisibleDraft()
    {
        using var form = new DefinitionFormModel(EditingDefinition("""
            "items":{"title":"Items","type":"array","items":{"type":"integer","x-configurator":{"sensitivity":"public"}},"x-configurator":{"sensitivity":"public","detail":"basic"}}
            """));
        using var items = System.Text.Json.JsonDocument.Parse("[7]");
        form.SetValue(["items"], items.RootElement);
        using var harness = new Harness(initialize: true, form: form);
        Button supply = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("SupplyContainer"));
        harness.Window.PublicInput.Text = "invalid";
        supply.InvokeCommand(Command.Accept);
        await Assert.That(harness.Window.PublicInput.Text).IsEqualTo("invalid");
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[7]");
        harness.Window.PublicInput.Text = "8";
        supply.InvokeCommand(Command.Accept);
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[8]");
        Button remove = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("RemoveItem"));
        Button artifact = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("Artifact"));
        remove.InvokeCommand(Command.Accept);
        artifact.InvokeCommand(Command.Accept);
        remove.InvokeCommand(Command.Accept);
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[8]");
        remove.InvokeCommand(Command.Accept);
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[]");
    }

    [Test]
    [NotInParallel]
    public async Task WorkspaceLocaleOverridesHostAndRefreshesChrome()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
            using var harness = new Harness(manualDispatch: true);
            string englishTitle = harness.Window.Title;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            using var english = new Harness();
            await Assert.That(englishTitle).IsEqualTo(english.Window.Title);
            string englishStatus = harness.Window.StatusText;
            await harness.ChangeView(() => harness.Form.SelectLocale("ar"));
            await Assert.That(harness.Window.Title).IsNotEqualTo(englishTitle);
            await Assert.That(harness.Window.StatusText).IsNotEqualTo(englishStatus);
            Label artifact = harness.Window.SubViews.OfType<Label>()
                .Single(label => label.Text.Contains("config.json", StringComparison.Ordinal));
            await Assert.That(artifact.Text).Contains(TerminalStrings.Get("Selected", "ar"));
            await Assert.That(english.Window.Title).IsEqualTo(englishTitle);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Test]
    [NotInParallel]
    public async Task EnglishAndArabicResourcesResolveAllCurrentControls()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            string[] keys =
            [
                "WindowTitle", "ImportFile", "Close", "Ready", "Limitations", "TerminalTooSmall",
                "InvalidSecret", "SecretCommandsDisabled", "InvalidOutput", "Cancelled", "Writing",
                "Choose", "Previous",
                "Next", "ApplyField", "PreviousItem", "NextItem", "RemoveItem", "AddItem", "Artifact",
                "SelectOutput", "Selected", "NotSelected", "ImportSelected", "ImportReady",
                "ImportFailed", "NextOutcome", "NotAttempted", "PartialBundle", "ManualRecovery",
                "PreparePreview", "SaveProtected", "PreviewReady", "DraftInvalid",
                "ProtectedDraftSaved", "ProtectedDraftFailed", "ValidateDraft", "ClearPrivate",
                "PrivateCleared", "InputChanged", "NoFields", "NextAttempt", "PublicationAttempt",
                "PublicationUnknown", "PublicationInProgress", "PublicationFailed", "PublicationCleanupFailed"
            ];
            foreach (string culture in new[] { "en", "ar" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                await Assert.That(keys.All(key => TerminalStrings.Get(key, culture) != key
                    && !string.IsNullOrWhiteSpace(TerminalStrings.Get(key, culture)))).IsTrue();
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Test]
    [NotInParallel]
    public async Task LongPublicScalarLaysOutAsOneRowAndRetainsCompletePreparedOutput()
    {
        Task regression = Task.Run(async () =>
        {
            string initial = new('x', 65536);
            var source = EditingSource("""
                "payload":{"title":"Payload","type":"string",
                  "x-configurator":{"sensitivity":"public","initialValue":""}}
                """);
            source["configuration"]!["properties"]!["payload"]!["x-configurator"]!["initialValue"] = initial;
            source["localization"] = System.Text.Json.Nodes.JsonNode.Parse("""
                {"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{}}}}
                """);
            source["outputs"]!["config.json"]!["projection"] = System.Text.Json.Nodes.JsonNode.Parse("""
                {"op":"object","members":{
                  "a":{"op":"copy","reference":{"scope":"root","path":"/payload"}},
                  "b":{"op":"copy","reference":{"scope":"root","path":"/payload"}},
                  "c":{"op":"copy","reference":{"scope":"root","path":"/payload"}},
                  "d":{"op":"copy","reference":{"scope":"root","path":"/payload"}}}}
                """);
            using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(
                System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())));
            using var harness = new Harness(initialize: true, form: form, manualDispatch: true);
            var window = harness.Window;
            var laidOut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.SubViewsLaidOut += (_, _) => laidOut.TrySetResult();
            window.BeginInit();
            window.EndInit();
            window.Frame = new Rectangle(0, 0, 120, 40);
            window.ApplyViewportPolicy(window.Frame.Size);
            bool resolved = window.Layout(window.Frame.Size);
            await laidOut.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(resolved).IsTrue();
            await Assert.That(window.PublicInput.Visible).IsTrue();
            await Assert.That(window.PublicInput.Frame.Height).IsEqualTo(1);
            await Assert.That(window.SubViews.Where(view => view.Visible && view != window.PublicInput)
                .All(view => Rectangle.Intersect(view.Frame, window.PublicInput.Frame) is { Width: 0 } or { Height: 0 }))
                .IsTrue();
            await Assert.That(window.PublicInput.Text).IsEqualTo(initial);
            window.PublicInput.InsertionPoint = 137;
            await Assert.That(form.SelectLocale("ar")).IsTrue();
            harness.DrainCallbacks();
            await Assert.That(window.PublicInput.Text).IsEqualTo(initial);
            await Assert.That(window.PublicInput.InsertionPoint).IsEqualTo(137);

            string edited = initial[..^1] + "y";
            window.PublicInput.Text = edited;
            await Assert.That(window.CommitField()).IsTrue();
            await Assert.That(form.Snapshot().Values["payload"].GetString()).IsEqualTo(edited);
            window.Prepare();
            await window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            await Assert.That(harness.Operation.IsPrepared).IsTrue();
            using var bundle = Configurator.Core.Artifacts.DefinitionBundleComposer.Compose(
                form.Definition, ["config"], form.Snapshot());
            await Assert.That(bundle.Succeeded).IsTrue();
            string expected = "{\"a\":\"" + edited + "\",\"b\":\"" + edited +
                "\",\"c\":\"" + edited + "\",\"d\":\"" + edited + "\"}";
            await Assert.That(System.Text.Encoding.UTF8.GetString(bundle.Artifacts.Single().Bytes.Span))
                .IsEqualTo(expected);
        });
        await regression.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task MinimumOuterViewportKeepsEveryActionReachable(string locale)
    {
        using var form = TerminalDefinitionFixture.Form();
        form.SelectLocale(locale);
        using var harness = new Harness(initialize: true, form: form);
        var window = harness.Window;
        window.BeginInit();
        window.EndInit();
        window.Frame = new Rectangle(0, 0, 80, 24);
        window.ApplyViewportPolicy(window.Viewport.Size);
        foreach (View view in window.SubViews) view.SetRelativeLayout(window.GetContentSize());
        await Assert.That(window.PublicInput.Visible).IsTrue();
        View[] controls = window.SubViews.Where(view => view.CanFocus && view.Visible && view.Enabled).ToArray();
        foreach (View control in controls)
        {
            await Assert.That(control.SetFocus()).IsTrue();
            await Assert.That(window.Viewport.Contains(control.Frame)).IsTrue();
        }
        await Assert.That(controls.SelectMany((left, index) => controls.Skip(index + 1)
            .Select(right => Rectangle.Intersect(left.Frame, right.Frame)))
            .All(rectangle => rectangle.Width == 0 || rectangle.Height == 0)).IsTrue();
    }

    [Test]
    [Arguments("edit")]
    [Arguments("clear")]
    [Arguments("selection")]
    [Arguments("dispose")]
    public async Task SuspendedPreparationKeepsOwnerResponsiveAndRejectsRetiredReadiness(string transition)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(manualDispatch: true, beforePreparation: async token =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
        });
        harness.Window.Prepare();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(harness.Window.PreparationCompletion.IsCompleted).IsFalse();
        try
        {
            switch (transition)
            {
                case "edit": harness.Window.PublicInput.Text = "new-public-value"; break;
                case "clear": harness.Window.ClearPrivateState(); break;
                case "selection": harness.Operation.ToggleCurrentArtifact(); break;
                case "dispose": harness.Operation.Dispose(); break;
            }
        }
        finally { release.TrySetResult(); }
        await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        harness.DrainCallbacks();
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
        await Assert.That(harness.Window.StatusText).DoesNotContain("privateValue");
    }

    [Test]
    public async Task EditingAfterPreparationStartsCannotRestoreReadiness()
    {
        using var harness = new Harness(manualDispatch: true);
        harness.Window.Prepare();
        harness.Form.Fields.Single(field => field.Definition.Id == "name").Text = "changed-after-start";
        await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        harness.DrainCallbacks();
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
        await Assert.That(harness.Form.Snapshot().Values["name"].GetString()).IsEqualTo("changed-after-start");
    }

    [Test]
    public async Task SmallViewportHidesEditingAndNormalViewportRestoresNativeControls()
    {
        using var harness = new Harness();
        var window = harness.Window;
        window.Frame = new Rectangle(0, 0, 40, 12);
        window.ApplyViewportPolicy(window.Frame.Size);
        await Assert.That(window.SubViews.Where(view => view.CanFocus && view.Visible)).IsEmpty();
        await Assert.That(window.SubViews.OfType<Label>().Any(view =>
            view.Visible && view.Text == TerminalStrings.Get("TerminalTooSmall"))).IsTrue();
        window.Frame = new Rectangle(0, 0, 100, 30);
        window.ApplyViewportPolicy(window.Frame.Size);
        foreach (View view in window.SubViews)
            await Assert.That(view.SetRelativeLayout(window.Viewport.Size)).IsTrue();
        await Assert.That(window.PublicInput.Visible).IsTrue();
        await Assert.That(window.MaskedInput.Visible).IsFalse();
        await Assert.That(window.SubViews.OfType<Button>().Where(view => view.Visible)
            .All(button => button.CanFocus)).IsTrue();
        View[] focusable = window.SubViews.Where(view => view.CanFocus && view.Visible).ToArray();
        await Assert.That(focusable.SelectMany((left, index) => focusable.Skip(index + 1)
            .Select(right => Rectangle.Intersect(left.Frame, right.Frame)))
            .All(rectangle => rectangle.Width == 0 || rectangle.Height == 0)).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task NativeButtonsDoNotOverwriteAdjacentStatusOrArtifactLabels()
    {
        using var harness = new Harness(initialize: true);
        harness.Window.BeginInit();
        harness.Window.EndInit();
        harness.Window.Frame = new Rectangle(0, 0, 100, 30);
        harness.Window.ApplyViewportPolicy(harness.Window.Frame.Size);
        foreach (View view in harness.Window.SubViews)
            view.SetRelativeLayout(harness.Window.Viewport.Size);
        View[] labels = harness.Window.SubViews.OfType<Label>().Where(label => label.Visible).ToArray();
        View[] buttons = harness.Window.SubViews.OfType<Button>().Where(button => button.Visible).ToArray();
        foreach (View button in buttons)
            await Assert.That(button.Viewport.Height).IsGreaterThan(0);
        string[] overlaps = buttons.SelectMany(button => labels.Where(label =>
                Rectangle.Intersect(button.Frame, label.Frame) is { Width: > 0, Height: > 0 })
            .Select(label => $"{button.Text}:{button.Frame} intersects {label.Text}:{label.Frame}")).ToArray();
        await Assert.That(overlaps).IsEmpty();
    }

    [Test]
    [NotInParallel]
    public async Task TypedKeysReachMaskedFieldAndCannotRestoreHistoryAfterNavigation()
    {
        using var harness = new Harness(initialize: true, empty: true);
        harness.Window.Frame = new Rectangle(0, 0, 100, 30);
        harness.Window.ApplyViewportPolicy(harness.Window.Frame.Size);
        foreach (View view in harness.Window.SubViews)
            view.SetRelativeLayout(harness.Window.Viewport.Size);
        await Assert.That(harness.Window.PublicInput.SetFocus()).IsTrue();
        harness.Window.PublicInput.Text = "sample";
        harness.Window.MoveField(1);
        await Assert.That(harness.Window.MaskedInput.Secret).IsTrue();
        await Assert.That(harness.Window.MaskedInput.HasFocus).IsTrue();
        foreach (char character in "privateValue")
            await Assert.That(harness.Window.MaskedInput.NewKeyDownEvent(new Key(character))).IsTrue();
        await Assert.That(harness.Window.MaskedInput.Text).DoesNotContain("privateValue");
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(harness.Form.Snapshot().Values["token"].GetString()).IsEqualTo("privateValue");
        harness.Window.MoveField(1);
        await Assert.That(harness.Secret.Count).IsEqualTo(0);
        await Assert.That(harness.Window.MaskedInput.Text).IsEmpty();
        harness.Window.ClearPrivateState();
        await Assert.That(harness.Form.Fields.Single(input => input.Definition.Id == "token").Text).IsEmpty();
        await Assert.That(harness.Window.MaskedInput.InvokeCommand(Command.Undo)).IsTrue();
        await Assert.That(harness.Window.MaskedInput.Text).IsEmpty();
    }

    [Test]
    [NotInParallel]
    public async Task NativePrepareAndSaveButtonsUseTheSharedWorkspace()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-native-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var harness = new Harness(initialize: true, directory: directory, manualDispatch: true);
            var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Workspace.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PresentationWorkspace.IsBusy) && !harness.Workspace.IsBusy)
                    settled.TrySetResult();
            };
            Button prepare = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("PreparePreview"));
            Button save = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("SaveProtected"));
            await Assert.That(prepare.InvokeCommand(Command.Accept)).IsTrue();
            await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            await Assert.That(harness.Operation.IsPrepared).IsTrue();
            await Assert.That(harness.Window.StatusText).DoesNotContain("privateValue");
            await Assert.That(save.InvokeCommand(Command.Accept)).IsTrue();
            await settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(directory, "config.json")))
                .IsEqualTo("""{"name":"sample","token":"privateValue"}""");
            await Assert.That(harness.Secret.Count).IsEqualTo(0);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [NotInParallel]
    [Arguments("2")]
    [Arguments("synthetic-invalid-draft")]
    public async Task CompareAndFindingControlsPreserveTheActualEditorAndPreparedState(string text)
    {
        string directory = Directory.CreateTempSubdirectory("terminal-comparison-").FullName;
        try
        {
            string input = Path.Combine(directory, "existing.json");
            await File.WriteAllTextAsync(input, """{"count":"synthetic-private-import"}""");
            var source = EditingSource("""
                "count":{"title":"Count","type":"integer","x-configurator":{"sensitivity":"public","detail":"basic"}}
                """);
            source["outputs"]!["config.json"]!["projection"] = System.Text.Json.Nodes.JsonNode.Parse("""
                {"op":"object","members":{"count":{"op":"copy","reference":{"scope":"root","path":"/count"}}}}
                """);
            using var form = new DefinitionFormModel(DefinitionSourceCodec.ParseJson(
                System.Text.Encoding.UTF8.GetBytes(source.ToJsonString())));
            form.SetValue(["count"], System.Text.Json.JsonSerializer.SerializeToElement(1));
            using var harness = new Harness(initialize: true, form: form, directory: directory, manualDispatch: true);
            harness.Window.PublicInput.Text = text;
            AnswerSnapshot original = form.Snapshot();
            bool prepared = harness.Operation.AdoptPreparation(await harness.Operation.PrepareAsync()) == TerminalPreparationCode.Ready;
            TextField path = harness.Window.SubViews.OfType<TextField>().Single(field => field.Y == 14);
            path.Text = input;
            Button compare = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("ImportSelected"));
            await Assert.That(compare.InvokeCommand(Command.Accept)).IsTrue();
            await harness.CompleteImport();

            await Assert.That(form.ImportComparison).IsNotNull();
            await Assert.That(form.InputRevision).IsEqualTo(original.InputRevision);
            await Assert.That(form.Snapshot().Values["count"].GetRawText()).IsEqualTo(original.Values["count"].GetRawText());
            await Assert.That(harness.Window.PublicInput.Text).IsEqualTo(text);
            await Assert.That(harness.Operation.IsPrepared).IsEqualTo(prepared);
            Button next = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("NextFinding"));
            await Assert.That(next.InvokeCommand(Command.Accept)).IsTrue();
            await Assert.That(form.InputRevision).IsEqualTo(original.InputRevision);
            await Assert.That(harness.Window.PublicInput.Text).IsEqualTo(text);
            await Assert.That(harness.Window.StatusText).DoesNotContain("synthetic-private-import");
            await Assert.That(harness.Operation.IsPrepared).IsEqualTo(prepared);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    [NotInParallel]
    public async Task NativeImportAndOutputSelectionPrepareTheDeclaredBundleWithoutRenderingPrivateAnswers()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-guided-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "existing.json");
            await File.WriteAllTextAsync(source, """{"name":"sample","token":"privateValue"}""");
            using var harness = new Harness(initialize: true, empty: true, directory: directory, manualDispatch: true);
            TextField importPath = harness.Window.SubViews.OfType<TextField>()
                .Single(field => field.Y == 14);
            importPath.Text = source;
            Button import = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("FillFields"));
            await Assert.That(import.InvokeCommand(Command.Accept)).IsTrue();
            await harness.CompleteImport();
            await Assert.That(harness.Form.Snapshot().Values["token"].GetString()).IsEqualTo("privateValue");
            Button cycle = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("Artifact"));
            Button select = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("SelectOutput"));
            await Assert.That(cycle.InvokeCommand(Command.Accept)).IsTrue();
            await Assert.That(select.InvokeCommand(Command.Accept)).IsTrue();
            await Assert.That(harness.Operation.AdoptPreparation(harness.Operation.PrepareAsync().GetAwaiter().GetResult())).IsEqualTo(TerminalPreparationCode.Ready);
            await Assert.That(harness.Window.StatusText).DoesNotContain("privateValue");
            await harness.Workspace.ExecuteAsync(Guid.NewGuid());
            await Assert.That(Directory.EnumerateFiles(directory).Select(Path.GetFileName))
                .Contains("config.json");
            await Assert.That(Directory.EnumerateFiles(directory).Select(Path.GetFileName))
                .Contains("config.env");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [NotInParallel]
    public async Task ConflictingNativeImportLeavesAnswersAndStatusValueFree()
    {
        string directory = Path.Combine(Path.GetTempPath(), "configurator-import-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "existing.json");
            await File.WriteAllTextAsync(source, """{"name":"sample","token":"differentPrivateValue"}""");
            using var harness = new Harness(initialize: true, directory: directory, manualDispatch: true);
            TextField importPath = harness.Window.SubViews.OfType<TextField>()
                .Single(field => field.Y == 14);
            importPath.Text = source;
            Button import = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("FillFields"));
            await Assert.That(import.InvokeCommand(Command.Accept)).IsTrue();
            await harness.CompleteImport();
            await Assert.That(harness.Form.Snapshot().Values["token"].GetString()).IsEqualTo("privateValue");
            await Assert.That(harness.Window.StatusText).DoesNotContain("privateValue");
            await Assert.That(harness.Window.StatusText).DoesNotContain("differentPrivateValue");
            await Assert.That(harness.Window.StatusText).DoesNotContain(directory);
            await Assert.That(importPath.Text).IsEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task ReceiptActionsRemainVisibleAndEnableOnlyForAvailableFacts(string locale)
    {
        using var form = TerminalDefinitionFixture.Form();
        form.SelectLocale(locale);
        using var harness = new Harness(initialize: true, form: form);
        Button outcome = harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get("NextOutcome", locale));
        Button attempt = harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get("NextAttempt", locale));
        await Assert.That(outcome.Visible && attempt.Visible).IsTrue();
        await Assert.That(outcome.Enabled || attempt.Enabled).IsFalse();
        string initial = harness.Window.StatusText;
        outcome.InvokeCommand(Command.Accept);
        attempt.InvokeCommand(Command.Accept);
        await Assert.That(harness.Window.StatusText).IsEqualTo(initial);

        harness.Window.PresentReceipt(new(Guid.NewGuid(), [new("config", null)], false));
        await Assert.That(outcome.Visible && attempt.Visible).IsTrue();
        await Assert.That(outcome.Enabled).IsFalse();
        await Assert.That(attempt.Enabled).IsTrue();
        harness.Window.PresentReceipt(new(Guid.NewGuid(),
            [new("config", ProtectedArtifactCommitResult.Installed(true)), new("yaml", null)], true));
        await Assert.That(outcome.Visible && attempt.Visible && outcome.Enabled && attempt.Enabled).IsTrue();
        outcome.InvokeCommand(Command.Accept);
        await Assert.That(harness.Window.StatusText).Contains("yaml: " + TerminalStrings.Get("NotAttempted", locale));
    }

    [Test]
    [NotInParallel]
    public async Task PartialSaveStatusIdentifiesEachCommittedOutputAndManualRecovery()
    {
        using var harness = new Harness(initialize: true);
        var receipt = new ProtectedArtifactPublicationReceipt(Guid.NewGuid(),
        [
            new("config", ProtectedArtifactCommitResult.Installed(verificationSucceeded: true)),
            new("environment", ProtectedArtifactCommitResult.NotInstalled(ProtectedArtifactStatus.TargetExists)),
            new("yaml", null)
        ], true);
        harness.Window.PresentReceipt(receipt);
        Button next = harness.Window.SubViews.OfType<Button>()
            .Single(button => button.Text == TerminalStrings.Get("NextOutcome"));
        await Assert.That(next.Visible).IsTrue();
        string first = harness.Window.StatusText;
        await Assert.That(next.InvokeCommand(Command.Accept)).IsTrue();
        string second = harness.Window.StatusText;
        await Assert.That(next.InvokeCommand(Command.Accept)).IsTrue();
        string third = harness.Window.StatusText;
        await Assert.That(first).Contains("config: " + TerminalStrings.Get("OutputWritten"));
        await Assert.That(second).Contains("environment: " + TerminalStrings.Get("OutputTargetExists"));
        await Assert.That(third).Contains("yaml: " + TerminalStrings.Get("NotAttempted"));
        await Assert.That(first).Contains(TerminalStrings.Get("ManualRecovery"));
        await Assert.That(first).DoesNotContain("privateValue");
    }

    [Test]
    [NotInParallel]
    public async Task UnexpectedPublicationFaultAfterSuccessResetsExitCode()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("terminal-fault-").FullName;
        int attempt = 0;
        try
        {
            using var harness = new Harness(initialize: true, directory: directory, manualDispatch: true,
                beforePublication: _ => ++attempt == 1 ? Task.CompletedTask
                    : throw new InvalidOperationException("synthetic-private-fault"));
            harness.Window.Prepare();
            await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            harness.Window.Save();
            await harness.Window.SaveCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            await Assert.That(harness.Window.ExitCode).IsEqualTo(0);
            harness.Form.Fields.Single(field => field.Definition.Id == "token").Text = "synthetic-retry";
            harness.Window.Prepare();
            await harness.Window.PreparationCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            harness.Window.Save();
            await harness.Window.SaveCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            await Assert.That(harness.Operation.PublicationHistory[^1].Failed).IsTrue();
            await Assert.That(harness.Window.StatusText).DoesNotContain("synthetic-private-fault");
            await Assert.That(harness.Window.ExitCode).IsEqualTo(4);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [NotInParallel]
    public async Task SaveHistorySurvivesPrivateClearWithoutRestoringReadiness()
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Directory.CreateTempSubdirectory("terminal-history-").FullName;
        try
        {
            using var harness = new Harness(initialize: true, directory: directory, manualDispatch: true);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                await Assert.That(harness.Operation.AdoptPreparation(await harness.Operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
                await harness.Operation.ExecuteAsync(CancellationToken.None);
                harness.DrainCallbacks();
            }
            harness.Window.ClearPrivateState();
            Button history = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("NextAttempt"));
            await Assert.That(history.Visible).IsTrue();
            await Assert.That(history.InvokeCommand(Command.Accept)).IsTrue();
            string first = harness.Window.StatusText;
            await Assert.That(history.InvokeCommand(Command.Accept)).IsTrue();
            string second = harness.Window.StatusText;
            await Assert.That(first + second).Contains("config: " + TerminalStrings.Get("OutputWritten"));
            await Assert.That(first + second).Contains("config: " + TerminalStrings.Get("OutputTargetExists"));
            await Assert.That(first + second).DoesNotContain("privateValue");
            await Assert.That(first + second).DoesNotContain(directory);
            await Assert.That(harness.Operation.IsPrepared).IsFalse();
            await Assert.That(harness.Workspace.Result).IsNull();
            await Assert.That(harness.Window.ExitCode).IsEqualTo(4);
            await Assert.That(harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == TerminalStrings.Get("SaveProtected")).Enabled).IsFalse();
            await Assert.That(Directory.GetFiles(directory)).Count().IsEqualTo(1);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ClearOrSignalCancelsBeforeBundlePublication(bool signal)
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-cancel-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            using var harness = new Harness(initialize: true, directory: directory,
                beforePublication: async token => { reached.TrySetResult(); await resume.Task.WaitAsync(token); },
                manualDispatch: true);
            await Assert.That(harness.Operation.AdoptPreparation(await harness.Operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
            Task execution = harness.Workspace.ExecuteAsync(Guid.NewGuid());
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (signal)
            {
                var cleared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                harness.Form.InputChanged += (_, _) =>
                {
                    if (harness.Form.Fields.Single(input => input.Definition.Id == "token").Text.Length == 0)
                        cleared.TrySetResult();
                };
                harness.Window.RequestStopFromSignal();
                await harness.PumpUntil(cleared.Task);
            }
            else harness.Window.ClearPrivateState();
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(harness.Workspace.IsBusy).IsFalse();
            await Assert.That(harness.Operation.IsPrepared).IsFalse();
            await Assert.That(harness.Form.Fields.Single(input => input.Definition.Id == "token").Text).IsEmpty();
            await Assert.That(harness.Secret.Count).IsEqualTo(0);
            await Assert.That(Directory.EnumerateFiles(directory)).IsEmpty();
        }
        finally { Directory.Delete(directory); }
    }

    [Test]
    public async Task UncommittedFieldEditsInvalidatePreparedArtifactImmediately()
    {
        using var harness = new Harness();
        await Assert.That(harness.Operation.AdoptPreparation(await harness.Operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        harness.Window.PublicInput.Text = "changed but not applied";
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
        await Assert.That(harness.Workspace.Output).IsEmpty();
    }

    [Test]
    public async Task ObjectListAdapterExpandsTypedLeavesAndRemovesWholeRows()
    {
        using var form = new DefinitionFormModel(ObjectListDefinition());
        var editor = new TerminalFieldEditor(form);
        var collection = editor.Slots.Single(slot => slot.IsObjectList);
        await Assert.That(TerminalFieldEditor.AddItem(collection)).IsTrue();
        var name = editor.Slots.Single(slot => slot.Title == "Row name");
        var password = editor.Slots.Single(slot => slot.Title == "Row password");
        await Assert.That(TerminalFieldEditor.Write(name, "first")).IsTrue();
        await Assert.That(TerminalFieldEditor.Write(password, "synthetic-secret")).IsTrue();
        await Assert.That(password.RequiresMasking).IsTrue();
        await Assert.That(TerminalFieldEditor.AddItem(collection)).IsTrue();
        var secondName = editor.Slots.Single(slot => slot.Title == "Row name" && slot.Path[0] == "1");
        await Assert.That(TerminalFieldEditor.Write(secondName, "second")).IsTrue();
        await Assert.That(TerminalFieldEditor.AddItem(collection)).IsFalse();
        var aliases = editor.Slots.Single(slot => slot.Title == "Aliases" && slot.Path[0] == "0");
        await Assert.That(TerminalFieldEditor.Write(aliases, "alpha", 0)).IsTrue();
        await Assert.That(TerminalFieldEditor.Write(aliases, "beta", 1)).IsTrue();
        TerminalFieldEditor.Remove(aliases, 0);
        await Assert.That(TerminalFieldEditor.Read(aliases)).IsEqualTo("beta");
        var localizations = editor.Slots.Single(slot => slot.Title == "Localizations" && slot.Path[0] == "0");
        await Assert.That(TerminalFieldEditor.AddItem(localizations)).IsTrue();
        var locale = editor.Slots.Single(slot => slot.Title == "Locale");
        await Assert.That(TerminalFieldEditor.Write(locale, "en")).IsTrue();
        await Assert.That(TerminalFieldEditor.AddItem(localizations)).IsTrue();
        var secondLocale = editor.Slots.Single(slot => slot.Title == "Locale" && slot.Path[2] == "1");
        await Assert.That(TerminalFieldEditor.Write(secondLocale, "ar")).IsTrue();
        TerminalFieldEditor.Remove(localizations, 0);
        await Assert.That(form.Snapshot().Values["rows"][0].GetProperty("localizations")[0].GetProperty("locale").GetString()).IsEqualTo("ar");
        await Assert.That(form.Snapshot().Values["rows"][0].GetProperty("name").GetString()).IsEqualTo("first");
        await Assert.That(form.Snapshot().Values["rows"][1].GetProperty("name").GetString()).IsEqualTo("second");
        TerminalFieldEditor.Remove(collection, 0);
        var rows = form.Snapshot().Values["rows"];
        await Assert.That(rows.GetArrayLength()).IsEqualTo(1);
        await Assert.That(rows[0].GetProperty("name").GetString()).IsEqualTo("second");
        await Assert.That(rows[0].TryGetProperty("settings", out _)).IsFalse();
        // Appending each scalar alias supplies a row and then records its edit.
        await Assert.That(form.InputRevision).IsEqualTo(16);
    }

    [Test]
    [NotInParallel]
    public async Task NativeObjectRowButtonsAddFocusEditAndRemoveUsingTheSharedForm()
    {
        using var harness = new Harness(initialize: true, form: new DefinitionFormModel(ObjectListDefinition()));
        var add = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("AddItem"));
        var remove = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == TerminalStrings.Get("RemoveItem"));
        await Assert.That(add.Visible).IsTrue();
        await Assert.That(harness.Window.PublicInput.Visible).IsFalse();
        await Assert.That(add.InvokeCommand(Command.Accept)).IsTrue();
        // Exact property names are visited in canonical order: aliases, localizations, name, password, settings.
        harness.Window.MoveField(2);
        await Assert.That(harness.Window.MaskedInput.Visible).IsTrue();
        await Assert.That(harness.Window.MaskedInput.HasFocus).IsTrue();
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("first")).IsTrue();
        await Assert.That(harness.Window.CommitField()).IsTrue();
        harness.Window.MoveField(-4);
        await Assert.That(add.InvokeCommand(Command.Accept)).IsTrue();
        harness.Window.MoveField(2);
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("second")).IsTrue();
        await Assert.That(harness.Window.CommitField()).IsTrue();
        // Pass the second row's password and settings to return to the outer collection.
        for (int navigation = 0; navigation < 3; navigation++) harness.Window.MoveField(1);
        await Assert.That(add.Visible).IsTrue();
        await Assert.That(remove.InvokeCommand(Command.Accept)).IsTrue();
        await Assert.That(harness.Form.Snapshot().Values["rows"].GetArrayLength()).IsEqualTo(2);
        await Assert.That(harness.Window.StatusText).DoesNotContain("first");
        await Assert.That(remove.InvokeCommand(Command.Accept)).IsTrue();
        await Assert.That(add.HasFocus).IsTrue();
        var rows = harness.Form.Snapshot().Values["rows"];
        await Assert.That(rows.GetArrayLength()).IsEqualTo(1);
        await Assert.That(rows[0].GetProperty("name").GetString()).IsEqualTo("second");
        await Assert.That(harness.Secret.Count).IsEqualTo(0);
    }

    private static Definition ObjectListDefinition()
    {
        var source = EditingSource("""
            "rows":{"title":"Rows","type":"array","maxItems":2,
              "x-configurator":{"sensitivity":"public","detail":"basic"},
              "items":{"type":"object","additionalProperties":false,"required":["name","settings","aliases","localizations"],
                "x-configurator":{"sensitivity":"sensitive"},"properties":{
                  "name":{"title":"Row name","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}},
                  "password":{"title":"Row password","type":"string","x-configurator":{"sensitivity":"secret","detail":"basic"}},
                  "settings":{"title":"Settings","type":"object","additionalProperties":false,"properties":{},
                    "x-configurator":{"sensitivity":"public","detail":"basic"}},
                  "aliases":{"title":"Aliases","type":"array","items":{"type":"string","x-configurator":{"sensitivity":"public"}},
                    "x-configurator":{"sensitivity":"public","detail":"basic"}},
                  "localizations":{"title":"Localizations","type":"array","x-configurator":{"sensitivity":"public","detail":"basic"},
                    "items":{"type":"object","additionalProperties":false,"required":["locale"],"x-configurator":{"sensitivity":"public"},"properties":{
                      "locale":{"title":"Locale","type":"string","x-configurator":{"sensitivity":"public","detail":"basic"}}}}}}}}
            """);
        source["outputs"]!["config.json"]!["projection"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"op":"object","members":{"rows":{"op":"copy","reference":{"scope":"root","path":"/rows"}}}}
            """);
        return DefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    private sealed class Harness : IDisposable
    {
        private readonly IApplication _application;
        private readonly PresentationSession _session;
        private readonly System.Threading.Channels.Channel<Action> _dispatch = System.Threading.Channels.Channel.CreateUnbounded<Action>();
        internal Harness(bool initialize = false, bool empty = false, string? directory = null,
            Func<CancellationToken, Task>? beforePublication = null, DefinitionFormModel? form = null,
            bool manualDispatch = false, Func<CancellationToken, Task>? beforePreparation = null)
        {
            _application = Application.Create();
            if (initialize) _application.Init("dotnet");
            Form = form ?? (empty ? new DefinitionFormModel(TerminalDefinitionFixture.Definition()) : TerminalDefinitionFixture.Form());
            Secret = new TerminalSecretBuffer(262144, urlSafeOnly: false);
            Operation = new TerminalArtifactOperation(Form, new ProtectedArtifactWriter(), directory ?? Path.GetTempPath(), beforePublication,
                beforePreparation: beforePreparation);
            _session = new PresentationSession(new StrongReferenceMessenger());
            WorkspaceId.TryCreate("definition", out var id);
            Workspace = _session.CreateWorkspace(id, Operation);
            Workspace.Activate();
            Window = new TerminalWindow(_application, Workspace, Operation, Secret,
                dispatch: manualDispatch ? callback => _dispatch.Writer.TryWrite(callback) : null,
                cooperativeYield: _ => ValueTask.CompletedTask);
            Window.Frame = new Rectangle(0, 0, 100, 30);
            Window.ApplyViewportPolicy(Window.Frame.Size);
        }
        internal DefinitionFormModel Form { get; }
        internal TerminalSecretBuffer Secret { get; }
        internal TerminalArtifactOperation Operation { get; }
        internal PresentationWorkspace Workspace { get; }
        internal TerminalWindow Window { get; }
        internal void DrainCallbacks()
        {
            while (_dispatch.Reader.TryRead(out Action? callback)) callback();
        }
        internal async Task ChangeView(Action change)
        {
            DrainCallbacks();
            Task<Action> dispatched = _dispatch.Reader.ReadAsync().AsTask();
            change();
            (await dispatched.WaitAsync(TimeSpan.FromSeconds(5)))();
            DrainCallbacks();
        }
        internal async Task CompleteImport()
        {
            while (Window.IsImportBusy)
            {
                Action callback = await _dispatch.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                callback();
            }
            await Window.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            while (_dispatch.Reader.TryRead(out Action? callback)) callback();
        }
        internal async Task PumpUntil(Task completed)
        {
            while (!completed.IsCompleted)
            {
                Action callback = await _dispatch.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                callback();
            }
            await completed;
        }
        public void Dispose()
        {
            Window.Dispose();
            Workspace.Dispose();
            Operation.Dispose();
            Secret.Dispose();
            Form.Dispose();
            _session.Dispose();
            _application.Dispose();
        }
    }
}
