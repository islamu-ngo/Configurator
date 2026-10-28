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
using global::Terminal.Gui.ViewBase;
using global::Terminal.Gui.Views;

public sealed class SetupTerminalGuiAdapterTests
{
    [Test]
    public async Task ExplicitEmptyEnumChoiceIsNotOmission()
    {
        using var form = new DefinitionFormModel(EditingDefinition("""
            "choice":{"title":"Choice","type":"string","enum":["","other"],"x-configurator":{"sensitivity":"public","detail":"basic"}}
            """));
        var slot = new SetupTerminalFieldEditor(form).Slots.Single();
        await Assert.That(SetupTerminalFieldEditor.Write(slot, "")).IsTrue();
        await Assert.That(form.Snapshot().Values["choice"].GetRawText()).IsEqualTo("\"\"");
        await Assert.That(SetupTerminalFieldEditor.Clear(slot)).IsTrue();
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
        Button add = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("AddItem"));
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
        var slot = new SetupTerminalFieldEditor(form).Slots.Single();
        await Assert.That(SetupTerminalFieldEditor.Write(slot, string.Empty)).IsTrue();
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
        Button choice = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("Choose"));
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
            harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("NextItem"))
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
            harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("NextItem"))
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
        harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("NextItem"))
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

    private static SetupDefinition CorrectionDefinition(string shape, bool masked)
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
        return SetupDefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(source.ToJsonString()));
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
        Button nextItem = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("NextItem"));
        nextItem.InvokeCommand(Command.Accept);
        harness.Window.MoveField(1);
        harness.Window.PublicInput.Text = "reachable";
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values["after"].GetString()).IsEqualTo("reachable");
    }

    private static SetupDefinition EditingDefinition(string properties, string required = "[]") =>
        SetupDefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(EditingSource(properties, required).ToJsonString()));

    private static System.Text.Json.Nodes.JsonObject EditingSource(string properties, string required = "[]")
    {
        var source = System.Text.Json.Nodes.JsonNode.Parse($$$$$$"""
            {"$schema":"urn:islamu:configurator:authoring:v2",
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
        Button supply = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("SupplyContainer"));
        Button clear = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("OmitValue"));
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
        Button supply = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("SupplyContainer"));
        harness.Window.PublicInput.Text = "invalid";
        supply.InvokeCommand(Command.Accept);
        await Assert.That(harness.Window.PublicInput.Text).IsEqualTo("invalid");
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[7]");
        harness.Window.PublicInput.Text = "8";
        supply.InvokeCommand(Command.Accept);
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[8]");
        Button remove = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("RemoveItem"));
        Button artifact = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("Artifact"));
        remove.InvokeCommand(Command.Accept);
        artifact.InvokeCommand(Command.Accept);
        remove.InvokeCommand(Command.Accept);
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[8]");
        remove.InvokeCommand(Command.Accept);
        await Assert.That(form.Snapshot().Values["items"].GetRawText()).IsEqualTo("[]");
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
                "DisplayMode", "ModeGuided", "ModeAdvanced", "ModeAllDeclared", "Choose", "Previous",
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
                await Assert.That(keys.All(key => SetupTerminalText.Get(key) != key
                    && !string.IsNullOrWhiteSpace(SetupTerminalText.Get(key)))).IsTrue();
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; }
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
            view.Visible && view.Text == SetupTerminalText.Get("TerminalTooSmall"))).IsTrue();
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
            using var harness = new Harness(initialize: true, directory: directory);
            var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Workspace.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SetupPresentationWorkspace.IsBusy) && !harness.Workspace.IsBusy)
                    settled.TrySetResult();
            };
            Button prepare = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("PreparePreview"));
            Button save = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("SaveProtected"));
            await Assert.That(prepare.InvokeCommand(Command.Accept)).IsTrue();
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
                .Single(button => button.Text == SetupTerminalText.Get("ImportSelected"));
            await Assert.That(import.InvokeCommand(Command.Accept)).IsTrue();
            await harness.CompleteImport();
            await Assert.That(harness.Form.Snapshot().Values["token"].GetString()).IsEqualTo("privateValue");
            Button cycle = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == SetupTerminalText.Get("Artifact"));
            Button select = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == SetupTerminalText.Get("SelectOutput"));
            await Assert.That(cycle.InvokeCommand(Command.Accept)).IsTrue();
            await Assert.That(select.InvokeCommand(Command.Accept)).IsTrue();
            await Assert.That(harness.Operation.PrepareAsync().GetAwaiter().GetResult()).IsTrue();
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
                .Single(button => button.Text == SetupTerminalText.Get("ImportSelected"));
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
            .Single(button => button.Text == SetupTerminalText.Get("NextOutcome"));
        await Assert.That(next.Visible).IsTrue();
        string first = harness.Window.StatusText;
        await Assert.That(next.InvokeCommand(Command.Accept)).IsTrue();
        string second = harness.Window.StatusText;
        await Assert.That(next.InvokeCommand(Command.Accept)).IsTrue();
        string third = harness.Window.StatusText;
        await Assert.That(first).Contains("config: Written");
        await Assert.That(second).Contains("environment: TargetExists");
        await Assert.That(third).Contains("yaml: " + SetupTerminalText.Get("NotAttempted"));
        await Assert.That(first).Contains(SetupTerminalText.Get("ManualRecovery"));
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
            harness.Window.Save();
            await harness.Window.SaveCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DrainCallbacks();
            await Assert.That(harness.Window.ExitCode).IsEqualTo(0);
            harness.Form.Fields.Single(field => field.Definition.Id == "token").Text = "synthetic-retry";
            harness.Window.Prepare();
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
                await Assert.That(await harness.Operation.PrepareAsync()).IsTrue();
                await harness.Operation.ExecuteAsync(CancellationToken.None);
                harness.DrainCallbacks();
            }
            harness.Window.ClearPrivateState();
            Button history = harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == SetupTerminalText.Get("NextAttempt"));
            await Assert.That(history.Visible).IsTrue();
            await Assert.That(history.InvokeCommand(Command.Accept)).IsTrue();
            string first = harness.Window.StatusText;
            await Assert.That(history.InvokeCommand(Command.Accept)).IsTrue();
            string second = harness.Window.StatusText;
            await Assert.That(first + second).Contains("config: Written");
            await Assert.That(first + second).Contains("config: TargetExists");
            await Assert.That(first + second).DoesNotContain("privateValue");
            await Assert.That(first + second).DoesNotContain(directory);
            await Assert.That(harness.Operation.IsPrepared).IsFalse();
            await Assert.That(harness.Workspace.Result).IsNull();
            await Assert.That(harness.Window.ExitCode).IsEqualTo(4);
            await Assert.That(harness.Window.SubViews.OfType<Button>()
                .Single(button => button.Text == SetupTerminalText.Get("SaveProtected")).Enabled).IsFalse();
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
            await Assert.That(await harness.Operation.PrepareAsync()).IsTrue();
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
        await Assert.That(await harness.Operation.PrepareAsync()).IsTrue();
        harness.Window.PublicInput.Text = "changed but not applied";
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
        await Assert.That(harness.Workspace.Output).IsEmpty();
    }

    [Test]
    public async Task ObjectListAdapterExpandsTypedLeavesAndRemovesWholeRows()
    {
        using var form = new DefinitionFormModel(ObjectListDefinition());
        var editor = new SetupTerminalFieldEditor(form);
        var collection = editor.Slots.Single(slot => slot.IsObjectList);
        await Assert.That(SetupTerminalFieldEditor.AddItem(collection)).IsTrue();
        var name = editor.Slots.Single(slot => slot.Title == "Row name");
        var password = editor.Slots.Single(slot => slot.Title == "Row password");
        await Assert.That(SetupTerminalFieldEditor.Write(name, "first")).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.Write(password, "synthetic-secret")).IsTrue();
        await Assert.That(password.RequiresMasking).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.AddItem(collection)).IsTrue();
        var secondName = editor.Slots.Single(slot => slot.Title == "Row name" && slot.Path[0] == "1");
        await Assert.That(SetupTerminalFieldEditor.Write(secondName, "second")).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.AddItem(collection)).IsFalse();
        var aliases = editor.Slots.Single(slot => slot.Title == "Aliases" && slot.Path[0] == "0");
        await Assert.That(SetupTerminalFieldEditor.Write(aliases, "alpha", 0)).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.Write(aliases, "beta", 1)).IsTrue();
        SetupTerminalFieldEditor.Remove(aliases, 0);
        await Assert.That(SetupTerminalFieldEditor.Read(aliases)).IsEqualTo("beta");
        var localizations = editor.Slots.Single(slot => slot.Title == "Localizations" && slot.Path[0] == "0");
        await Assert.That(SetupTerminalFieldEditor.AddItem(localizations)).IsTrue();
        var locale = editor.Slots.Single(slot => slot.Title == "Locale");
        await Assert.That(SetupTerminalFieldEditor.Write(locale, "en")).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.AddItem(localizations)).IsTrue();
        var secondLocale = editor.Slots.Single(slot => slot.Title == "Locale" && slot.Path[2] == "1");
        await Assert.That(SetupTerminalFieldEditor.Write(secondLocale, "ar")).IsTrue();
        SetupTerminalFieldEditor.Remove(localizations, 0);
        await Assert.That(form.Snapshot().Values["rows"][0].GetProperty("localizations")[0].GetProperty("locale").GetString()).IsEqualTo("ar");
        await Assert.That(form.Snapshot().Values["rows"][0].GetProperty("name").GetString()).IsEqualTo("first");
        await Assert.That(form.Snapshot().Values["rows"][1].GetProperty("name").GetString()).IsEqualTo("second");
        SetupTerminalFieldEditor.Remove(collection, 0);
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
        var add = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("AddItem"));
        var remove = harness.Window.SubViews.OfType<Button>().Single(button => button.Text == SetupTerminalText.Get("RemoveItem"));
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

    private static SetupDefinition ObjectListDefinition()
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
        return SetupDefinitionSourceCodec.ParseJson(System.Text.Encoding.UTF8.GetBytes(source.ToJsonString()));
    }

    private sealed class Harness : IDisposable
    {
        private readonly IApplication _application;
        private readonly SetupPresentationSession _session;
        private readonly System.Threading.Channels.Channel<Action> _dispatch = System.Threading.Channels.Channel.CreateUnbounded<Action>();
        internal Harness(bool initialize = false, bool empty = false, string? directory = null,
            Func<CancellationToken, Task>? beforePublication = null, DefinitionFormModel? form = null,
            bool manualDispatch = false)
        {
            _application = Application.Create();
            if (initialize) _application.Init("dotnet");
            Form = form ?? (empty ? new DefinitionFormModel(TerminalDefinitionFixture.Definition()) : TerminalDefinitionFixture.Form());
            Secret = new SetupTerminalSecretBuffer(262144, urlSafeOnly: false);
            Operation = new SetupTerminalArtifactOperation(Form, new ProtectedArtifactWriter(), directory ?? Path.GetTempPath(), beforePublication);
            _session = new SetupPresentationSession(new StrongReferenceMessenger());
            SetupWorkspaceId.TryCreate("definition", out var id);
            Workspace = _session.CreateWorkspace(id, Operation);
            Workspace.Activate();
            Window = new SetupTerminalWindow(_application, Workspace, Operation, Secret,
                dispatch: manualDispatch ? callback => _dispatch.Writer.TryWrite(callback) : null);
            Window.Frame = new Rectangle(0, 0, 100, 30);
            Window.ApplyViewportPolicy(Window.Frame.Size);
        }
        internal DefinitionFormModel Form { get; }
        internal SetupTerminalSecretBuffer Secret { get; }
        internal SetupTerminalArtifactOperation Operation { get; }
        internal SetupPresentationWorkspace Workspace { get; }
        internal SetupTerminalWindow Window { get; }
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
