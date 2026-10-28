namespace Configurator.Terminal.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;

public sealed class SetupTerminalOfflineWorkflowTests
{
    private static readonly string[] EqualChoiceLabels = ["Equal", "Equal"];
    private static readonly string[] TranslatedChoiceLabels = ["Translated", "Equal"];
    [Test]
    [NotInParallel]
    public async Task NativeButtonsEditTypedMapNullAndBranchesWithoutLocaleAnswerChanges()
    {
        using var form = new DefinitionFormModel(TypedDefinition());
        form.SetAllSectionsExpanded(true);
        using var harness = new ExperienceHarness(form, initialize: true);
        void Click(string key) => harness.Window.SubViews.OfType<global::Terminal.Gui.Views.Button>()
            .Single(button => button.Text == SetupTerminalText.Get(key))
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
            .Single(button => button.Text.StartsWith(SetupTerminalText.Get("Locale") + ":", StringComparison.Ordinal))
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
        var editor = new SetupTerminalFieldEditor(form);
        var choice = editor.Slots.Single(slot => slot.Input.Definition.Id == "choice");
        await Assert.That(SetupTerminalFieldEditor.Choices(choice)).IsEquivalentTo(EqualChoiceLabels);
        await Assert.That(SetupTerminalFieldEditor.SelectChoice(choice, 1, 0)).IsTrue();
        await Assert.That(form.Snapshot().Values["choice"].GetInt32()).IsEqualTo(2);
        var map = editor.Slots.Single(slot => slot.Input.Definition.Id == "map" && slot.Path.Length == 0);
        await Assert.That(SetupTerminalFieldEditor.AddMapEntry(map, "literal/key")).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.AddMapEntry(map, "literal/key")).IsFalse();
        var entry = editor.Slots.Single(slot => slot.Path.SequenceEqual(new[] { "literal/key" }));
        await Assert.That(SetupTerminalFieldEditor.Write(entry, "7")).IsTrue();
        await Assert.That(form.Snapshot().Values["map"].GetProperty("literal/key").GetInt32()).IsEqualTo(7);
        await Assert.That(SetupTerminalFieldEditor.Write(entry, "-")).IsFalse();
        await Assert.That(form.HasInputErrors).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.Read(entry)).IsEqualTo("-");
        await Assert.That(SetupTerminalFieldEditor.Write(entry, "8")).IsTrue();
        await Assert.That(form.HasInputErrors).IsFalse();
        await Assert.That(SetupTerminalFieldEditor.AddMapEntry(map, "second")).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.AddMapEntry(map, "third")).IsFalse();
        long revision = form.InputRevision;
        form.SelectLocale("ar");
        await Assert.That(form.InputRevision).IsEqualTo(revision);
        await Assert.That(SetupTerminalFieldEditor.Choices(choice)).IsEquivalentTo(TranslatedChoiceLabels);
        await Assert.That(SetupTerminalFieldEditor.ChoiceIndex(choice, 0)).IsEqualTo(1);
        await Assert.That(form.Snapshot().Values["map"].GetProperty("literal/key").GetInt32()).IsEqualTo(8);
    }

    [Test]
    public async Task BranchNavigationUsesOnlySelectedChildrenAndNullRemainsPresent()
    {
        using var form = new DefinitionFormModel(TypedDefinition());
        form.SetAllSectionsExpanded(true);
        var editor = new SetupTerminalFieldEditor(form);
        form.SelectVariant(["provider"], 0);
        var count = editor.Slots.Single(slot => slot.Path.SequenceEqual(new[] { "count" }));
        await Assert.That(SetupTerminalFieldEditor.Write(count, "3")).IsTrue();
        form.SelectVariant(["provider"], 0);
        await Assert.That(form.Snapshot().Values["provider"].GetProperty("count").GetInt32()).IsEqualTo(3);
        form.SelectVariant(["provider"], 1);
        await Assert.That(SetupTerminalFieldEditor.Write(count, "9")).IsFalse();
        await Assert.That(form.Snapshot().Values["provider"].TryGetProperty("count", out _)).IsFalse();
        await Assert.That(editor.Slots.Any(slot => slot.Path.SequenceEqual(new[] { "count" }))).IsFalse();
        await Assert.That(editor.Slots.Any(slot => slot.Path.SequenceEqual(new[] { "other" }))).IsTrue();
        form.SetNull(["nullable"]);
        var nullable = editor.Slots.Single(slot => slot.Input.Definition.Id == "nullable");
        await Assert.That(form.Snapshot().Values["nullable"].ValueKind).IsEqualTo(JsonValueKind.Null);
        SetupTerminalFieldEditor.Clear(nullable);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out _)).IsFalse();
        form.ResetValue(["nullable"]);
        await Assert.That(form.Validate().TryGetEffectiveValue(DefinitionPath.Parse("/nullable"), out var initialized)).IsTrue();
        await Assert.That(initialized.GetInt32()).IsEqualTo(4);
    }

    private static SetupDefinition TypedDefinition() => SetupDefinitionSourceCodec.ParseJson("""
        {"$schema":"urn:islamu:configurator:authoring:v2",
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
            using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), directory);
            if (id != operation.ArtifactId)
            {
                operation.ToggleCurrentArtifact();
                operation.ArtifactId = id;
                operation.ToggleCurrentArtifact();
            }
            using var session = new SetupPresentationSession(new StrongReferenceMessenger());
            SetupWorkspaceId.TryCreate("definition", out var workspaceId);
            using var workspace = session.CreateWorkspace(workspaceId, operation);
            workspace.Activate();
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            await Assert.That(operation.Preparation!.ToString()).DoesNotContain("privateValue");
            await workspace.ExecuteAsync(Guid.NewGuid());
            var result = (SetupTerminalArtifactResult)workspace.Result!;
            await Assert.That(result.Outcomes[0].Result?.Succeeded).IsEqualTo(true);
            string path = Path.Combine(directory, form.Definition.Artifacts.Single(item => item.Id == id).FileName);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(expected);
            await Assert.That(File.GetUnixFileMode(path)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await Assert.That(operation.IsPrepared).IsFalse();
            await Assert.That(workspace.Output).IsEmpty();
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            var repeated = await operation.ExecuteAsync(CancellationToken.None);
            await Assert.That(((SetupTerminalArtifactResult)repeated.CoreResult).Outcomes[0].Result?.Status)
                .IsEqualTo(ProtectedArtifactStatus.TargetExists);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(expected);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task AnswerAndSelectionChangesInvalidatePreparationWhileInspectionPreservesIt()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), Path.GetTempPath());
        await Assert.That(await operation.PrepareAsync()).IsTrue();
        form.Fields.Single(input => input.Definition.Id == "name").Text = "changed";
        await Assert.That(operation.IsPrepared).IsFalse();
        var result = await operation.ExecuteAsync(CancellationToken.None);
        await Assert.That(((SetupTerminalArtifactResult)result.CoreResult).Outcomes[0].Result?.Status)
            .IsEqualTo(ProtectedArtifactStatus.InvalidRequest);
        await Assert.That(await operation.PrepareAsync()).IsTrue();
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
        using var operation = new SetupTerminalArtifactOperation(form, new ProtectedArtifactWriter(), Path.GetTempPath());
        form.Fields.Single(input => input.Definition.Id == "count").Text = "not an integer";
        await Assert.That(await operation.PrepareAsync()).IsFalse();
        form.Fields.Single(input => input.Definition.Id == "count").Text = "2";
        form.Fields.Single(input => input.Definition.Id == "mode").Text = "external";
        await Assert.That(await operation.PrepareAsync()).IsFalse();
        form.Fields.Single(input => input.Definition.Id == "endpoint").Text = "example.test";
        await Assert.That(await operation.PrepareAsync()).IsTrue();
    }

    [Test]
    [Arguments("enabled", "false", JsonValueKind.False)]
    [Arguments("count", "27", JsonValueKind.Number)]
    [Arguments("ratio", "2.75", JsonValueKind.Number)]
    [Arguments("mode", "external", JsonValueKind.String)]
    public async Task ScalarFieldsRetainDeclaredTypes(string id, string text, JsonValueKind kind)
    {
        using var form = TerminalDefinitionFixture.Form();
        var slot = new SetupTerminalFieldEditor(form).Slots.Single(item => item.Input.Definition.Id == id);
        await Assert.That(SetupTerminalFieldEditor.Write(slot, text)).IsTrue();
        await Assert.That(form.Snapshot().Values[id].ValueKind).IsEqualTo(kind);
        await Assert.That(SetupTerminalFieldEditor.Read(slot)).IsEqualTo(text);
    }

    [Test]
    public async Task NestedObjectLeavesAndListItemsAreEditedWithoutRawJson()
    {
        using var form = TerminalDefinitionFixture.Form();
        form.SetAllSectionsExpanded(true);
        form.SupplyObject(["profile"]);
        var slots = new SetupTerminalFieldEditor(form).Slots;
        var display = slots.Single(item => item.Title == "Display");
        var password = slots.Single(item => item.Title == "Password");
        var tags = slots.Single(item => item.Title == "Tags");
        await Assert.That(display.RequiresMasking).IsTrue();
        await Assert.That(password.RequiresMasking).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.Write(display, "Operator")).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.Write(password, "opaque")).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.Write(tags, "first", 0)).IsTrue();
        await Assert.That(SetupTerminalFieldEditor.Write(tags, "second", 1)).IsTrue();
        var values = form.Snapshot().Values;
        await Assert.That(values["profile"].GetProperty("display").GetString()).IsEqualTo("Operator");
        await Assert.That(values["profile"].GetProperty("password").GetString()).IsEqualTo("opaque");
        await Assert.That(values["tags"].GetArrayLength()).IsEqualTo(2);
        SetupTerminalFieldEditor.Remove(tags, 0);
        await Assert.That(SetupTerminalFieldEditor.Read(tags)).IsEqualTo("second");
    }

    [Test]
    [NotInParallel]
    public async Task AuthoredExperiencesAndDisclosurePreserveAnswersAndPreparation()
    {
        using var form = ExperienceForm();
        using var harness = new ExperienceHarness(form);
        await Assert.That(await harness.Operation.PrepareAsync()).IsTrue();
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
        await Assert.That(new SetupTerminalFieldEditor(form).Slots
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
        await Assert.That(await harness.Operation.PrepareAsync()).IsFalse();
    }

    [Test]
    [NotInParallel]
    public async Task UnauthoredDefinitionHasOneNeutralExperienceWithoutChangingAnswers()
    {
        JsonObject source = JsonNode.Parse(TerminalDefinitionFixture.Json)!.AsObject();
        source.Remove("presentation");
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(
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
        using var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
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
            var selected = new SetupTerminalDefinitionSelection(path).Load();
            await Assert.That(selected.ApplicationId).IsEqualTo("neutral.service");
            var registry = new SetupTerminalDefinitionSelection(directory, selected.FamilyId, "1.0").Load();
            await Assert.That(registry.Digest).IsEqualTo(selected.Digest);
            await File.WriteAllTextAsync(Path.Combine(directory, "revision.json"),
                TerminalDefinitionFixture.Json.Replace("\"revision\":1", "\"revision\":2", StringComparison.Ordinal));
            await Assert.That(() => new SetupTerminalDefinitionSelection(directory, selected.FamilyId).Load())
                .Throws<SetupDefinitionException>();
            await Assert.That(new SetupTerminalDefinitionSelection(directory, selected.FamilyId, "1.0").Load().Revision)
                .IsEqualTo(2);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task UnavailableProtectedWriterFailsClosedForGenericArtifacts()
    {
        using var form = TerminalDefinitionFixture.Form();
        using var operation = new SetupTerminalArtifactOperation(form, new UnavailableWriter(), Path.GetTempPath());
        await Assert.That(await operation.PrepareAsync()).IsTrue();
        await Assert.That(operation.ProtectedOutputAvailable).IsFalse();
        var outcome = await operation.ExecuteAsync(CancellationToken.None);
        await Assert.That(((SetupTerminalArtifactResult)outcome.CoreResult).Outcomes[0].Result?.Status)
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
                {"$schema":"urn:islamu:configurator:authoring:v2",
                 "identity":{"familyId":"f93a6b9f-c210-47a9-b90b-434954040282","revision":1},
                 "project":{"id":"local.service","name":"Local Service"},"applicationVersions":["1.0"],
                 "configuration":{"type":"object","additionalProperties":false,"x-configurator":{"sensitivity":"public"},
                   "properties":{"HOST":{"type":"string","x-configurator":{"sensitivity":"public","initialValue":"example.test"}}}},
                 "outputs":{"settings.json":{"format":"json"}}}
                """);
            var definition = new SetupTerminalDefinitionSelection(path).Load();
            await Assert.That(definition.ApplicationId).IsEqualTo("local.service");
            await Assert.That(definition.EntryPoints[0]).IsEqualTo("setup");
            await Assert.That(definition.Locales[0]).IsEqualTo("en");
            var selected = new SetupTerminalDefinitionSelection(directory, definition.FamilyId, "1.0").Load();
            await Assert.That(selected.Digest).IsEqualTo(definition.Digest);
            string yamlPath = Path.Combine(directory, "source.yaml");
            await File.WriteAllTextAsync(yamlPath, """
                $schema: urn:islamu:configurator:authoring:v2
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
            await Assert.That(new SetupTerminalDefinitionSelection(yamlPath).Load().Digest).IsEqualTo(definition.Digest);
            using var form = new DefinitionFormModel(definition);
            using var artifact = Configurator.Core.Artifacts.DefinitionArtifactComposer.Compose(
                definition, definition.Artifacts[0].Id, form.Snapshot()).Artifact;
            await Assert.That(artifact).IsNotNull();
            await Assert.That(System.Text.Encoding.UTF8.GetString(artifact!.Bytes.Span)).IsEqualTo("""{"HOST":"example.test"}""");
            await Assert.That(artifact!.Sensitivity).IsEqualTo(Configurator.Core.SetupArtifactSensitivity.Restricted);
            using var operation = new SetupTerminalArtifactOperation(form, new UnavailableWriter(), directory);
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            await Assert.That(operation.ProtectedOutputAvailable).IsFalse();
            var outcome = await operation.ExecuteAsync(CancellationToken.None);
            await Assert.That(((SetupTerminalArtifactResult)outcome.CoreResult).Outcomes[0].Result?.Status)
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
        var form = new DefinitionFormModel(SetupDefinitionCodec.ParseJson(Encoding.UTF8.GetBytes(source.ToJsonString())));
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
        private readonly SetupPresentationSession _session = new(new StrongReferenceMessenger());
        private readonly SetupPresentationWorkspace _workspace;
        private readonly SetupTerminalSecretBuffer _secret = new(262144, urlSafeOnly: false);

        internal ExperienceHarness(DefinitionFormModel form, bool initialize = false)
        {
            if (initialize) _application.Init("dotnet");
            Operation = new(form, new ProtectedArtifactWriter(), Path.GetTempPath());
            SetupWorkspaceId.TryCreate("definition", out var id);
            _workspace = _session.CreateWorkspace(id, Operation);
            _workspace.Activate();
            Window = new(_application, _workspace, Operation, _secret, dispatch: callback => callback());
            Window.Frame = new System.Drawing.Rectangle(0, 0, 100, 30);
            Window.ApplyViewportPolicy(Window.Frame.Size);
        }

        internal SetupTerminalArtifactOperation Operation { get; }
        internal SetupTerminalWindow Window { get; }

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
        public Task<ProtectedArtifactPreparation> PrepareAsync(Configurator.Core.SetupArtifactKind kind,
            string targetPath, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("protected-output-unavailable");
    }
}
