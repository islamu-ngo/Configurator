namespace Configurator.Terminal.Tests;

using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;

public sealed class SetupTerminalOfflineWorkflowTests
{
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
            await Assert.That(result.Outcomes[0].Status).IsEqualTo(ProtectedArtifactStatus.Written);
            string path = Path.Combine(directory, form.Definition.Artifacts.Single(item => item.Id == id).FileName);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(expected);
            await Assert.That(File.GetUnixFileMode(path)).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await Assert.That(operation.IsPrepared).IsFalse();
            await Assert.That(workspace.Output).IsEmpty();
            await Assert.That(await operation.PrepareAsync()).IsTrue();
            var repeated = await operation.ExecuteAsync(CancellationToken.None);
            await Assert.That(((SetupTerminalArtifactResult)repeated.CoreResult).Outcomes[0].Status)
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
        await Assert.That(((SetupTerminalArtifactResult)result.CoreResult).Outcomes[0].Status)
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
    public async Task DisclosureHidesOptionalMetadataButNotProtectedOrRequiredDecisions()
    {
        using var form = TerminalDefinitionFixture.Form();
        var editor = new SetupTerminalFieldEditor(form);
        await Assert.That(editor.Slots.Any(slot => slot.Input.Definition.Id == "advanced")).IsFalse();
        await Assert.That(editor.Slots.Any(slot => slot.Input.Definition.Id == "token")).IsTrue();
        await Assert.That(editor.Slots.Any(slot => slot.Input.Definition.Id == "endpoint")).IsFalse();
        form.DisplayMode = SetupDisplayMode.Advanced;
        await Assert.That(editor.Slots.Any(slot => slot.Input.Definition.Id == "advanced")).IsTrue();
        form.Fields.Single(input => input.Definition.Id == "mode").Text = "external";
        form.DisplayMode = SetupDisplayMode.Guided;
        await Assert.That(editor.Slots.Any(slot => slot.Input.Definition.Id == "endpoint")).IsTrue();
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
        await Assert.That(((SetupTerminalArtifactResult)outcome.CoreResult).Outcomes[0].Status)
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
                {"$schema":"urn:islamu:configurator:authoring:v1",
                 "identity":{"familyId":"f93a6b9f-c210-47a9-b90b-434954040282","revision":1},
                 "project":{"id":"local.service","name":"Local Service"},"applicationVersions":["1.0"],
                 "fields":{"HOST":{"type":"string","sensitivity":"public","default":"example.test"}},
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
                $schema: urn:islamu:configurator:authoring:v1
                identity:
                  familyId: f93a6b9f-c210-47a9-b90b-434954040282
                  revision: 1
                project:
                  id: local.service
                  name: Local Service
                applicationVersions: ["1.0"]
                fields:
                  HOST:
                    type: string
                    sensitivity: public
                    default: example.test
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
            await Assert.That(((SetupTerminalArtifactResult)outcome.CoreResult).Outcomes[0].Status)
                .IsEqualTo(ProtectedArtifactStatus.UnsupportedPlatform);
            await Assert.That(Directory.GetFiles(directory)).Count().IsEqualTo(2);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class UnavailableWriter : IProtectedArtifactWriter
    {
        public bool IsAvailable => false;
        public Task<ProtectedArtifactPreparation> PrepareAsync(Configurator.Core.SetupArtifactKind kind,
            string targetPath, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("protected-output-unavailable");
    }
}
