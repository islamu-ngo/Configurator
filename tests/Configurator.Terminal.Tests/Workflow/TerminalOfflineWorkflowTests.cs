namespace Configurator.Terminal.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;

public sealed class TerminalOfflineWorkflowTests
{
    [Test]
    public async Task RootMapEditsLiteralCoordinatesWithoutPropertyProjection()
    {
        Definition definition = DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"e2871ba9-4ab8-43f6-b552-e1e6c4225bdd","revision":1},
             "project":{"id":"root-map","name":"Root map"},"applicationVersions":["1"],
             "configuration":{"type":"object","maxProperties":3,"x-configurator":{"sensitivity":"public"},
               "additionalProperties":{"type":["integer","null"],"x-configurator":{"sensitivity":"public"}}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """));
        using var form = new DefinitionFormModel(definition);
        var editor = new TerminalFieldEditor(form);
        var root = editor.Slots.Single(slot => slot.FullPath.Length == 0);
        foreach ((string key, string text) in new[] { ("a/b", "7"), ("a~b", "30"), ("0", "0") })
        {
            await Assert.That(TerminalFieldEditor.AddMapEntry(root, key)).IsTrue();
            var leaf = editor.Slots.Single(slot => slot.FullPath.SequenceEqual([key]));
            await Assert.That(TerminalFieldEditor.Write(leaf, text)).IsTrue();
        }
        await Assert.That(TerminalFieldEditor.AddMapEntry(root, "a/b")).IsFalse();
        await Assert.That(TerminalFieldEditor.AddMapEntry(root, "extra")).IsFalse();
        string identity = form.GetEditorIdentity(DefinitionPath.FromSegments(["0"]));
        var slash = editor.Slots.Single(slot => slot.FullPath.SequenceEqual(["a/b"]));
        await Assert.That(TerminalFieldEditor.Clear(slash)).IsTrue();
        await Assert.That(form.GetEditorIdentity(DefinitionPath.FromSegments(["0"]))).IsEqualTo(identity);
        await Assert.That(form.Snapshot().Values.Keys).IsEquivalentTo(["0", "a~b"]);
        await Assert.That(form.Snapshot().Values["0"].GetInt32()).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values["a~b"].GetInt32()).IsEqualTo(30);
        await Assert.That(form.Fields).IsEmpty();
    }

    [Test]
    public async Task RootContainerDescendantsKeepSurvivingRowIdentityAndLiteralMapKeys()
    {
        Definition definition = DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"6b140cf6-bd9c-4fd8-9cee-fc00cc28d1fe","revision":1},
             "project":{"id":"root-container","name":"Root container"},"applicationVersions":["1"],
             "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},
              "additionalProperties":{"type":"object","additionalProperties":false,"properties":{
               "rows":{"type":"array","maxItems":2,"items":{"type":"object","additionalProperties":false,
                "properties":{"options":{"type":"object","maxProperties":2,"additionalProperties":{"type":"integer"}}}}}}}},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """u8.ToArray());
        using var form = new DefinitionFormModel(definition);
        var editor = new TerminalFieldEditor(form);
        var root = editor.Slots.Single(slot => slot.FullPath.Length == 0);
        await Assert.That(TerminalFieldEditor.AddMapEntry(root, "0")).IsTrue();
        var rows = editor.Slots.Single(slot => slot.FullPath.SequenceEqual(["0", "rows"]));
        await Assert.That(TerminalFieldEditor.AddItem(rows)).IsTrue();
        await Assert.That(TerminalFieldEditor.AddItem(rows)).IsTrue();
        await Assert.That(TerminalFieldEditor.AddItem(rows)).IsFalse();
        var map = editor.Slots.Single(slot => slot.FullPath.SequenceEqual(["0", "rows", "1", "options"]));
        await Assert.That(TerminalFieldEditor.AddMapEntry(map, "a/b")).IsTrue();
        var leaf = editor.Slots.Single(slot => slot.FullPath.SequenceEqual(["0", "rows", "1", "options", "a/b"]));
        await Assert.That(TerminalFieldEditor.Write(leaf, "7")).IsTrue();
        string identity = leaf.Identity;
        TerminalFieldEditor.Remove(rows, 0);
        var survivor = editor.Slots.Single(slot => slot.FullPath.SequenceEqual(["0", "rows", "0", "options", "a/b"]));
        await Assert.That(survivor.Identity).IsEqualTo(identity);
        await Assert.That(TerminalFieldEditor.Value(survivor)!.Value.GetInt32()).IsEqualTo(7);
        await Assert.That(form.Snapshot().Values["0"].GetProperty("rows").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task RootVariantNestedMapEntriesUseSelectedBranchBounds()
    {
        Definition definition = DefinitionSourceCodec.ParseJson("""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"0c9b57a1-aedd-40e8-94e9-058087d7a740","revision":1},
             "project":{"id":"root-variant-map","name":"Root variant map"},"applicationVersions":["1"],
             "configuration":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
              {"type":"object","maxProperties":2,"required":["kind"],"x-configurator":{"sensitivity":"public"},
               "properties":{"kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}},
                "entries":{"type":"object","maxProperties":1,"additionalProperties":{"type":"integer"}}},
               "additionalProperties":false},
              {"type":"object","maxProperties":2,"required":["kind"],"x-configurator":{"sensitivity":"public"},
               "properties":{"kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}},
                "entries":{"type":"object","maxProperties":1,"additionalProperties":{"type":"integer"}}},
               "additionalProperties":false}]},
             "outputs":{"config.json":{"id":"config","format":"json"}}}
            """u8.ToArray());
        using var form = new DefinitionFormModel(definition);
        var editor = new TerminalFieldEditor(form);
        await Assert.That(form.SelectVariant(DefinitionPath.FromSegments([]), 0)).IsTrue();
        var map = editor.Slots.Single(slot => slot.Location.Segments.SequenceEqual(["entries"]));
        await Assert.That(TerminalFieldEditor.AddMapEntry(map, "a/b")).IsTrue();
        var leaf = editor.Slots.Single(slot => slot.Location.Segments.SequenceEqual(["entries", "a/b"]));
        await Assert.That(TerminalFieldEditor.Write(leaf, "7")).IsTrue();
        await Assert.That(TerminalFieldEditor.AddMapEntry(map, "extra")).IsFalse();
        await Assert.That(form.Snapshot().Values["entries"].GetProperty("a/b").GetInt32()).IsEqualTo(7);
        await Assert.That(form.Validate().IsReady).IsTrue();
    }

    private static readonly string[] EqualChoiceLabels = ["Equal", "Equal"];
    private static readonly string[] TranslatedChoiceLabels = ["Translated", "Equal"];
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RootRendererAdmitsLiteralRootWithoutCollectingAnswers(bool variant)
    {
        using JsonDocument original = JsonDocument.Parse(TypedDefinition().CanonicalBytes);
        string schema = variant ? original.RootElement.GetProperty("configuration").GetProperty("properties").GetProperty("provider").GetRawText()
            : """{"type":"object","additionalProperties":{"type":"integer"}}""";
        Definition definition = DefinitionSourceCodec.ParseJson(Encoding.UTF8.GetBytes($$$$"""
            {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
             "identity":{"familyId":"198d708d-caba-4e48-a00d-b06242151620","revision":1},
             "project":{"id":"root-admission","name":"Root admission"},"applicationVersions":["1"],
             "configuration":{{{{schema}}}},"outputs":{"config.json":{"id":"config","format":"json"}}}
            """));
        using var form = new DefinitionFormModel(definition);
        var editor = new TerminalFieldEditor(form);
        await Assert.That(editor.Slots[0].FullPath).IsEmpty();
        await Assert.That(form.Fields).IsEmpty();
        await Assert.That(form.InputRevision).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DocumentAdoptionUpdatesExistingOrdinaryTerminalSlots()
    {
        using var form = new DefinitionFormModel(TypedDefinition());
        var editor = new TerminalFieldEditor(form);
        TerminalFieldEditor.Slot slot = editor.Slots.Single(item => item.FullPath.SequenceEqual(["choice"]));
        using var document = form.OpenDocument();
        using var parsed = document.Parse(
            """{"answerContractVersion":"0.0.1","values":{"choice":2},"omitted":[]}"""u8.ToArray());
        await Assert.That(parsed.Succeeded).IsTrue();
        await Assert.That(document.Apply(parsed.Candidate!).Succeeded).IsTrue();
        await Assert.That(TerminalFieldEditor.Value(slot)!.Value.GetInt32()).IsEqualTo(2);
        await Assert.That(ReferenceEquals(slot.Input, form.Fields.Single(field => field.Definition.Name == "choice"))).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task NativeButtonsEditTypedMapNullAndBranchesWithoutLocaleAnswerChanges()
    {
        using var form = new DefinitionFormModel(TypedDefinition());
        form.SetAllSectionsExpanded(true);
        using var harness = new ExperienceHarness(form, initialize: true);
        void Click(string key) => harness.Window.SubViews.OfType<global::Terminal.Gui.Views.Button>()
            .Single(button => button.Text == TerminalStrings.Get(key))
            .InvokeCommand(global::Terminal.Gui.Input.Command.Accept);
        Click("Choose");
        Click("Choose");
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
        harness.Window.MoveField(1);
        await Assert.That(harness.Window.MaskedInput.TryReplaceSensitiveInput("literal/key")).IsTrue();
        Click("AddMapEntry");
        harness.Window.PublicInput.Text = "7";
        await Assert.That(harness.Window.CommitField()).IsTrue();
        await Assert.That(form.Snapshot().Values["map"].GetProperty("literal/key").GetInt32()).IsEqualTo(7);
        harness.Window.MoveField(1);
        Click("SetNull");
        await Assert.That(form.Snapshot().Values["nullable"].ValueKind).IsEqualTo(JsonValueKind.Null);
        Click("OmitValue");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out _)).IsFalse();
        Click("ResetValue");
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out var reset)).IsTrue();
        await Assert.That(reset.GetInt32()).IsEqualTo(4);
        harness.Window.MoveField(1);
        Click("ChooseVariant");
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("kind").GetString()).IsEqualTo("first");
        Click("ChooseVariant");
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("kind").GetString()).IsEqualTo("second");
        long revision = form.InputRevision;
        harness.Window.SubViews.OfType<global::Terminal.Gui.Views.Button>()
            .Single(button => button.Text.StartsWith(TerminalStrings.Get("Locale") + ":", StringComparison.Ordinal))
            .InvokeCommand(global::Terminal.Gui.Input.Command.Accept);
        await Assert.That(form.Locale).IsEqualTo("ar");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(harness.Window.StatusText).DoesNotContain("literal/key");
    }

    [Test]
    public async Task TypedMapAndEqualLabelChoicesKeepLiteralKeysAndExactValues()
    {
        using var form = new DefinitionFormModel(TypedDefinition());
        form.SetAllSectionsExpanded(true);
        var editor = new TerminalFieldEditor(form);
        var choice = editor.Slots.Single(slot => slot.Input.Definition.Id == "choice");
        await Assert.That(TerminalFieldEditor.Choices(choice)).IsEquivalentTo(EqualChoiceLabels);
        await Assert.That(TerminalFieldEditor.SelectChoice(choice, 1, 0)).IsTrue();
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
        var map = editor.Slots.Single(slot => slot.Input.Definition.Id == "map" && slot.Path.Length == 0);
        await Assert.That(TerminalFieldEditor.AddMapEntry(map, "literal/key")).IsTrue();
        await Assert.That(TerminalFieldEditor.AddMapEntry(map, "literal/key")).IsFalse();
        var entry = editor.Slots.Single(slot => slot.Path.SequenceEqual(new[] { "literal/key" }));
        await Assert.That(TerminalFieldEditor.Write(entry, "7")).IsTrue();
        await Assert.That(form.Snapshot().Values["map"].GetProperty("literal/key").GetInt32()).IsEqualTo(7);
        await Assert.That(TerminalFieldEditor.Write(entry, "-")).IsFalse();
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(TerminalFieldEditor.Read(entry)).IsEqualTo("-");
        await Assert.That(TerminalFieldEditor.Write(entry, "8")).IsTrue();
        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(TerminalFieldEditor.AddMapEntry(map, "second")).IsTrue();
        await Assert.That(TerminalFieldEditor.AddMapEntry(map, "third")).IsFalse();
        long revision = form.InputRevision;
        form.SelectLocale("ar");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(TerminalFieldEditor.Choices(choice)).IsEquivalentTo(TranslatedChoiceLabels);
        await Assert.That(TerminalFieldEditor.ChoiceIndex(choice, 0)).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values["map"].GetProperty("literal/key").GetInt32()).IsEqualTo(8);
    }

    [Test]
    public async Task BranchNavigationUsesOnlySelectedChildrenAndNullRemainsPresent()
    {
        using var form = new DefinitionFormModel(TypedDefinition());
        form.SetAllSectionsExpanded(true);
        var editor = new TerminalFieldEditor(form);
        form.SelectVariant(["provider"], 0);
        var count = editor.Slots.Single(slot => slot.Path.SequenceEqual(new[] { "count" }));
        await Assert.That(TerminalFieldEditor.Write(count, "3")).IsTrue();
        form.SelectVariant(["provider"], 0);
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("count").GetInt32()).IsEqualTo(3);
        form.SelectVariant(["provider"], 1);
        await Assert.That(TerminalFieldEditor.Write(count, "9")).IsFalse();
        await Assert.That(form.Snapshot().Values["provider"].TryGetProperty("count", out _)).IsFalse();
        await Assert.That(editor.Slots.Any(slot => slot.Path.SequenceEqual(new[] { "count" }))).IsFalse();
        await Assert.That(editor.Slots.Any(slot => slot.Path.SequenceEqual(new[] { "other" }))).IsTrue();
        form.SetNull(["nullable"]);
        var nullable = editor.Slots.Single(slot => slot.Input.Definition.Id == "nullable");
        await Assert.That(form.Snapshot().Values["nullable"].ValueKind).IsEqualTo(JsonValueKind.Null);
        TerminalFieldEditor.Clear(nullable);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out _)).IsFalse();
        form.ResetValue(["nullable"]);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out var initialized)).IsTrue();
        await Assert.That(initialized.GetInt32()).IsEqualTo(4);
    }

    private static Definition TypedDefinition() => DefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
         "identity":{"familyId":"69cdb122-41d2-4fab-9b82-7929a889c726","revision":2},
         "project":{"id":"typed-terminal","name":"Typed Terminal"},"applicationVersions":["1"],
         "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},"properties":{
           "map":{"type":"object","maxProperties":2,"x-configurator":{"sensitivity":"public"},
             "additionalProperties":{"type":"integer","x-configurator":{"sensitivity":"public"}}},
           "choice":{"type":"integer","enum":[1,2],"x-configurator":{"sensitivity":"public","detail":"basic",
             "choices":[{"value":1,"label":"Equal"},{"value":2,"label":"Equal"}]}},
           "nullable":{"type":["integer","null"],"x-configurator":{"sensitivity":"public","detail":"basic","initialValue":4}},
           "provider":{"type":"object","x-configurator":{"sensitivity":"public"},"oneOf":[
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"first","x-configurator":{"sensitivity":"public"}},
               "count":{"type":"integer","x-configurator":{"sensitivity":"public"}}}},
             {"type":"object","additionalProperties":false,"required":["kind"],"x-configurator":{"sensitivity":"public"},"properties":{
               "kind":{"type":"string","const":"second","x-configurator":{"sensitivity":"public"}},
               "other":{"type":"string","x-configurator":{"sensitivity":"public"}}}}]}}},
         "localization":{"defaultLocale":"en","translations":{"ar":{"direction":"rtl","overrides":{
           "/configuration/properties/choice/x-configurator/choices/0/label":"Translated"}}}},
         "outputs":{"config.json":{"format":"json"}}}
        """u8.ToArray());

    [Test]
    [Arguments("config", """{"name":"sample","token":"privateValue"}""")]
    [Arguments("environment", "NAME=sample\nTOKEN=privateValue\n")]
    [Arguments("yaml", "\"name\": \"sample\"\n\"token\": \"privateValue\"\n")]
    [Arguments("legal", "Operator: privateValue")]
    [Arguments("identity", """{"legalName":"privateValue"}""")]
    public async Task EveryDeclaredArtifactUsesProtectedCreateOnlyOutput(string id, string expected)
    {
        if (!OperatingSystem.IsLinux()) return;
        string directory = Path.Combine(Path.GetTempPath(), "configurator-terminal-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var form = TerminalDefinitionFixture.Form();
            using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), directory);
            if (id != operation.ArtifactId)
            {
                operation.ToggleCurrentArtifact();
                operation.ArtifactId = id;
                operation.ToggleCurrentArtifact();
            }
            using var session = new PresentationSession(new StrongReferenceMessenger());
            WorkspaceId.TryCreate("definition", out var workspaceId);
            using var workspace = session.CreateWorkspace(workspaceId, operation);
            workspace.Activate();
            await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
            await Assert.That(operation.Preparation!.ToString()).DoesNotContain("privateValue");
            await workspace.ExecuteAsync(Guid.NewGuid());
            var result = (TerminalArtifactResult)workspace.Result!;
            await Assert.That(result.Outcomes[0].Result?.Succeeded).IsEqualTo(true);
            string path = Path.Combine(directory, form.Definition.Artifacts.Single(item => item.Id == id).FileName);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(expected);
            await Assert.That(File.GetUnixFileMode(path)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await Assert.That(operation.IsPrepared).IsFalse();
            await Assert.That(workspace.Output).IsEmpty();
            await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
            var repeated = await operation.ExecuteAsync(CancellationToken.None);
            await Assert.That(((TerminalArtifactResult)repeated.CoreResult).Outcomes[0].Result?.Status)
                .IsEqualTo(ProtectedArtifactStatus.TargetExists);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(expected);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task AnswerAndSelectionChangesInvalidatePreparationWhileInspectionPreservesIt()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), Path.GetTempPath());
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        form.Fields.Single(input => input.Definition.Id == "name").Text = "changed";
        await Assert.That(operation.IsPrepared).IsFalse();
        var result = await operation.ExecuteAsync(CancellationToken.None);
        await Assert.That(((TerminalArtifactResult)result.CoreResult).Outcomes[0].Result?.Status)
            .IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        operation.ArtifactId = "legal";
        await Assert.That(operation.IsPrepared).IsTrue();
        await Assert.That(form.Definition.Artifacts.Single(item => item.Id == operation.ArtifactId).FileName)
            .IsEqualTo("legal.md");
        operation.ToggleCurrentArtifact();
        await Assert.That(operation.IsPrepared).IsFalse();
    }

    [Test]
    public async Task ActiveRequiredAndMalformedTypedInputsBlockPreparation()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), Path.GetTempPath());
        form.Fields.Single(input => input.Definition.Id == "count").Text = "not an integer";
        await Assert.That((await operation.PrepareAsync()).Code).IsEqualTo(TerminalPreparationCode.Invalid);
        form.Fields.Single(input => input.Definition.Id == "count").Text = "2";
        form.Fields.Single(input => input.Definition.Id == "mode").Text = "external";
        await Assert.That((await operation.PrepareAsync()).Code).IsEqualTo(TerminalPreparationCode.Invalid);
        form.Fields.Single(input => input.Definition.Id == "endpoint").Text = "example.test";
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
    }

    [Test]
    [Arguments("enabled", "false", JsonValueKind.False)]
    [Arguments("count", "27", JsonValueKind.Number)]
    [Arguments("ratio", "2.75", JsonValueKind.Number)]
    [Arguments("mode", "external", JsonValueKind.String)]
    public async Task ScalarFieldsRetainDeclaredTypes(string id, string text, JsonValueKind kind)
    {
        using var form = TerminalDefinitionFixture.Form();
        var slot = new TerminalFieldEditor(form).Slots.Single(item => item.Input.Definition.Id == id);
        await Assert.That(TerminalFieldEditor.Write(slot, text)).IsTrue();
        await Assert.That(form.Snapshot().Values[id].ValueKind).IsEqualTo(kind);
        await Assert.That(TerminalFieldEditor.Read(slot)).IsEqualTo(text);
    }

    [Test]
    public async Task NestedObjectLeavesAndListItemsAreEditedWithoutRawJson()
    {
        using var form = TerminalDefinitionFixture.Form();
        form.SetAllSectionsExpanded(true);
        form.SupplyObject(["profile"]);
        var slots = new TerminalFieldEditor(form).Slots;
        var display = slots.Single(item => item.Title == "Display");
        var password = slots.Single(item => item.Title == "Password");
        var tags = slots.Single(item => item.Title == "Tags");
        await Assert.That(display.RequiresMasking).IsTrue();
        await Assert.That(password.RequiresMasking).IsTrue();
        await Assert.That(TerminalFieldEditor.Write(display, "Operator")).IsTrue();
        await Assert.That(TerminalFieldEditor.Write(password, "opaque")).IsTrue();
        await Assert.That(TerminalFieldEditor.Write(tags, "first", 0)).IsTrue();
        await Assert.That(TerminalFieldEditor.Write(tags, "second", 1)).IsTrue();
        var values = form.Snapshot().Values;
        await Assert.That(values["profile"].GetProperty("display").GetString()).IsEqualTo("Operator");
        await Assert.That(values["profile"].GetProperty("password").GetString()).IsEqualTo("opaque");
        await Assert.That(values["tags"].GetArrayLength()).IsEqualTo(2);
        TerminalFieldEditor.Remove(tags, 0);
        await Assert.That(TerminalFieldEditor.Read(tags)).IsEqualTo("second");
    }

    [Test]
    [NotInParallel]
    public async Task AuthoredExperiencesAndDisclosurePreserveAnswersAndPreparation()
    {
        using var form = ExperienceForm();
        using var harness = new ExperienceHarness(form);
        await Assert.That(harness.Operation.AdoptPreparation(await harness.Operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        var preparation = harness.Operation.Preparation;
        long revision = form.InputRevision;
        int answerChanges = 0;
        form.InputChanged += (_, _) => answerChanges++;
        await Assert.That(form.SelectedExperienceId).IsEqualTo("guided");
        await Assert.That(form.IsEditorVisible(["advanced"])).IsFalse();
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["profile", "password"])).IsFalse();
        await Assert.That(form.IsEditorVisible(["endpoint"])).IsFalse();

        harness.Window.CycleExperience();

        await Assert.That(form.SelectedExperienceId).IsEqualTo("complete");
        // Journeys select layout; optional detail is an explicit view action.
        harness.Window.ToggleOptionalEditors();
        await Assert.That(harness.Window.SubViews.OfType<global::Terminal.Gui.Views.Button>()
            .Any(button => button.Text == "All settings")).IsTrue();
        await Assert.That(form.IsEditorVisible(["advanced"])).IsTrue();
        await Assert.That(form.IsEditorVisible(["profile", "password"])).IsTrue();
        await Assert.That(new TerminalFieldEditor(form).Slots
            .Single(slot => slot.Input.Definition.Id == "token").RequiresMasking).IsTrue();
        harness.Window.CycleExperience();
        await Assert.That(form.SelectedExperienceId).IsEqualTo("guided");
        harness.Window.ToggleOptionalEditors();
        await Assert.That(form.OptionalEditorsExpanded).IsTrue();
        await Assert.That(form.IsEditorVisible(["advanced"])).IsTrue();
        harness.Window.ToggleOptionalEditors();
        await Assert.That(form.OptionalEditorsExpanded).IsFalse();
        await Assert.That(form.IsEditorVisible(["advanced"])).IsFalse();
        await Assert.That(form.SelectExperience("unknown")).IsFalse();
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(answerChanges).IsEqualTo(0);
        await Assert.That(form.Snapshot().Values["name"].GetString()).IsEqualTo("sample");
        await Assert.That(form.Snapshot().Values["token"].GetString()).IsEqualTo("privateValue");
        await Assert.That(harness.Operation.IsPrepared).IsTrue();
        await Assert.That(ReferenceEquals(harness.Operation.Preparation, preparation)).IsTrue();
        await Assert.That(harness.Window.PublicInput.Text).DoesNotContain("privateValue");
        await Assert.That(harness.Window.StatusText).DoesNotContain("privateValue");

        form.Fields.Single(input => input.Definition.Id == "mode").Text = "external";
        await Assert.That(form.IsEditorVisible(["endpoint"])).IsTrue();
        await Assert.That(harness.Operation.IsPrepared).IsFalse();
        form.EditValue(["token"], null);
        await Assert.That(form.IsEditorVisible(["token"])).IsTrue();
        await Assert.That((await harness.Operation.PrepareAsync()).Code).IsEqualTo(TerminalPreparationCode.Invalid);
    }

    [Test]
    [NotInParallel]
    public async Task UnauthoredDefinitionHasOneNeutralExperienceWithoutChangingAnswers()
    {
        JsonObject source = JsonNode.Parse(TerminalDefinitionFixture.Json)!.AsObject();
        source.Remove("presentation");
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(
            Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["name"], "sample");
        form.EditValue(["token"], "privateValue");
        using var harness = new ExperienceHarness(form);
        long revision = form.InputRevision;
        await Assert.That(form.Experiences.Count).IsEqualTo(1);
        await Assert.That(form.Experiences.Single().Id).IsEqualTo("configure");
        await Assert.That(form.Experiences.Single().Title).IsEqualTo("Configure");

        harness.Window.CycleExperience();

        await Assert.That(form.SelectedExperienceId).IsEqualTo("configure");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(form.Snapshot().Values["token"].GetString()).IsEqualTo("privateValue");
    }

    [Test]
    [NotInParallel]
    public async Task ValidationDisplaysLiteralNestedDiagnosticSegmentsWithoutPrivateAnswers()
    {
        JsonObject source = JsonNode.Parse(TerminalDefinitionFixture.Json)!.AsObject();
        source["configuration"]!["properties"]!["literal.root.field"] = JsonNode.Parse("""
            {"title":"Literal root","type":"object","additionalProperties":false,
             "x-configurator":{"sensitivity":"public","detail":"basic"},"required":["child.with.dot"],
             "properties":{"child.with.dot":{"title":"Child","type":"string",
               "x-configurator":{"sensitivity":"public","detail":"basic"}}}}
            """);
        using var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["name"], "sample");
        form.EditValue(["token"], "privateValue");
        form.SupplyObject(["literal.root.field"]);
        using var harness = new ExperienceHarness(form);

        harness.Window.Validate();

        await Assert.That(harness.Window.StatusText)
            .Contains("""["literal.root.field","child.with.dot"]""");
        await Assert.That(harness.Window.StatusText).DoesNotContain("privateValue");
    }

    [Test]
    public async Task DefinitionLoadAndRegistrySelectionHaveNoImplicitTarget()
    {
        string directory = Path.Combine(Path.GetTempPath(), "configurator-registry-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "neutral.json");
            await File.WriteAllTextAsync(path, TerminalDefinitionFixture.Json);
            var selected = new TerminalDefinitionSelection(path).Load();
            await Assert.That(selected.ApplicationId).IsEqualTo("neutral.service");
            var registry = new TerminalDefinitionSelection(directory, selected.FamilyId, "1.0").Load();
            await Assert.That(registry.Digest).IsEqualTo(selected.Digest);
            await File.WriteAllTextAsync(Path.Combine(directory, "revision.json"),
                TerminalDefinitionFixture.Json.Replace("\"revision\":1", "\"revision\":2", StringComparison.Ordinal));
            await Assert.That(() => new TerminalDefinitionSelection(directory, selected.FamilyId).Load())
                .Throws<DefinitionException>();
            await Assert.That(new TerminalDefinitionSelection(directory, selected.FamilyId, "1.0").Load().Revision)
                .IsEqualTo(2);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task UnavailableProtectedWriterFailsClosedForGenericArtifacts()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new TerminalArtifactOperation(form, new UnavailableWriter(), Path.GetTempPath());
        await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
        await Assert.That(operation.ProtectedOutputAvailable).IsFalse();
        var outcome = await operation.ExecuteAsync(CancellationToken.None);
        await Assert.That(((TerminalArtifactResult)outcome.CoreResult).Outcomes[0].Result?.Status)
            .IsEqualTo(ProtectedArtifactStatus.UnsupportedPlatform);
        await Assert.That(operation.IsPrepared).IsFalse();
    }

    [Test]
    public async Task CompactLocalSourceSelectsExplicitIdentityAndPreparesRestrictedOutput()
    {
        string directory = Directory.CreateTempSubdirectory("configurator-terminal-authoring-").FullName;
        try
        {
            string path = Path.Combine(directory, "source.json");
            await File.WriteAllTextAsync(path, """
                {"$schema":"urn:islamu:configurator:authoring:v0.0.1",
                 "identity":{"familyId":"f93a6b9f-c210-47a9-b90b-434954040282","revision":1},
                 "project":{"id":"local.service","name":"Local Service"},"applicationVersions":["1.0"],
                 "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                   "properties":{"HOST":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"example.test"}}}},
                 "outputs":{"settings.json":{"format":"json"}}}
                """);
            var definition = new TerminalDefinitionSelection(path).Load();
            await Assert.That(definition.ApplicationId).IsEqualTo("local.service");
            await Assert.That(definition.EntryPoints[0]).IsEqualTo("setup");
            await Assert.That(definition.Locales[0]).IsEqualTo("en");
            var selected = new TerminalDefinitionSelection(directory, definition.FamilyId, "1.0").Load();
            await Assert.That(selected.Digest).IsEqualTo(definition.Digest);
            string yamlPath = Path.Combine(directory, "source.yaml");
            await File.WriteAllTextAsync(yamlPath, """
                $schema: urn:islamu:configurator:authoring:v0.0.1
                identity:
                  familyId: f93a6b9f-c210-47a9-b90b-434954040282
                  revision: 1
                project:
                  id: local.service
                  name: Local Service
                applicationVersions: ["1.0"]
                configuration:
                  type: object
                  additionalProperties: false
                  x-configurator:
                    sensitivity: public
                  properties:
                    HOST:
                      type: string
                      x-configurator:
                        sensitivity: public
                        initialValue: example.test
                outputs:
                  settings.json:
                    format: json
                """);
            await Assert.That(new TerminalDefinitionSelection(yamlPath).Load().Digest).IsEqualTo(definition.Digest);
            using var form = new DefinitionFormModel(definition);
            using var artifact = Configurator.Core.Artifacts.DefinitionArtifactComposer.Compose(
                definition, definition.Artifacts[0].Id, form.Snapshot()).Artifact;
            await Assert.That(artifact).IsNotNull();
            await Assert.That(System.Text.Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo("""{"HOST":"example.test"}""");
            await Assert.That(artifact!.Sensitivity).IsEqualTo(Configurator.Core.ArtifactSensitivity.Restricted);
            using var operation = new TerminalArtifactOperation(form, new UnavailableWriter(), directory);
            await Assert.That(operation.AdoptPreparation(await operation.PrepareAsync())).IsEqualTo(TerminalPreparationCode.Ready);
            await Assert.That(operation.ProtectedOutputAvailable).IsFalse();
            var outcome = await operation.ExecuteAsync(CancellationToken.None);
            await Assert.That(((TerminalArtifactResult)outcome.CoreResult).Outcomes[0].Result?.Status)
                .IsEqualTo(ProtectedArtifactStatus.UnsupportedPlatform);
            await Assert.That(Directory.GetFiles(directory)).Count().IsEqualTo(2);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static DefinitionFormModel ExperienceForm()
    {
        JsonObject source = JsonNode.Parse(TerminalDefinitionFixture.Json)!.AsObject();
        source["presentation"] = JsonNode.Parse("""
            {"defaultJourney":"guided","navigation":"sections","sections":[
               {"id":"settings","title":"Settings","pointers":["/name","/token","/enabled","/count","/ratio",
                 "/mode","/endpoint","/tags","/profile","/advanced"]}],
             "journeys":[
               {"id":"guided","title":"Get started","sections":["settings"]},
               {"id":"complete","title":"All settings","sections":["settings"]}]}
            """);
        var form = new DefinitionFormModel(DefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
        form.EditValue(["name"], "sample");
        form.EditValue(["token"], "privateValue");
        form.SupplyObject(["profile"]);
        // Seeding required answers is edit intent and promotes corrected editors.
        // Start the navigation scenario from the authored disclosure instead.
        form.SelectExperience("complete");
        form.SelectExperience("guided");
        return form;
    }

    private sealed class ExperienceHarness : IDisposable
    {
        private readonly global::Terminal.Gui.App.IApplication _application = global::Terminal.Gui.App.Application.Create();
        private readonly PresentationSession _session = new(new StrongReferenceMessenger());
        private readonly PresentationWorkspace _workspace;
        private readonly TerminalSecretBuffer _secret = new(262144, urlSafeOnly: false);

        internal ExperienceHarness(DefinitionFormModel form, bool initialize = false)
        {
            if (initialize) _application.Init("dotnet");
            Operation = new(form, new ProtectedArtifactWriter(), Path.GetTempPath());
            WorkspaceId.TryCreate("definition", out var id);
            _workspace = _session.CreateWorkspace(id, Operation);
            _workspace.Activate();
            Window = new(_application, _workspace, Operation, _secret, dispatch: callback => callback(),
                cooperativeYield: _ => ValueTask.CompletedTask);
            Window.Frame = new System.Drawing.Rectangle(0, 0, 100, 30);
            Window.ApplyViewportPolicy(Window.Frame.Size);
        }

        internal TerminalArtifactOperation Operation { get; }
        internal TerminalWindow Window { get; }

        public void Dispose()
        {
            Window.Dispose();
            _workspace.Dispose();
            _session.Dispose();
            Operation.Dispose();
            _secret.Dispose();
            _application.Dispose();
        }
    }

    private sealed class UnavailableWriter : IProtectedArtifactWriter
    {
        public bool IsAvailable => false;
        public Task<ProtectedArtifactPreparation> PrepareAsync(Configurator.Core.ArtifactKind kind,
            string targetPath, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("protected-output-unavailable");
    }
}
