namespace Configurator.Terminal.Tests;

using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Presentation;
using Configurator.Terminal;
using global::Terminal.Gui.App;

public sealed class TerminalEventRevision4WorkflowTests
{
    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task EventRevision4BilingualWorkbenchExercisesGuidedCompactDocumentUndoAndFiles(string locale)
    {
        Definition definition = LoadRepositoryDefinition("store/islamu-event/releases/4.json");
        await Assert.That(definition.Revision).IsEqualTo(4);

        using var form = new DefinitionFormModel(definition);
        form.SelectLocale(locale);
        await Assert.That(form.Locale).IsEqualTo(locale);

        string tempDir = Directory.CreateTempSubdirectory("configurator-terminal-rev4-").FullName;
        try
        {
            using var application = Application.Create();
            application.Init("dotnet");
            using var secret = new TerminalSecretBuffer(65536, urlSafeOnly: false);
            using var session = new PresentationSession(new StrongReferenceMessenger());
            using var operation = new TerminalArtifactOperation(form, new ProtectedArtifactWriter(), tempDir);
            if (!WorkspaceId.TryCreate("event-rev4-" + locale, out WorkspaceId id))
                throw new InvalidOperationException("workspace-id-invalid");
            using var workspace = session.CreateWorkspace(id, operation);
            workspace.Activate();
            using var window = new TerminalWindow(application, workspace, operation, secret);
            window.BeginInit();
            window.EndInit();
            window.Layout(new Size(120, 40));

            // Section ownership check + localized navigator title.
            await Assert.That(window.SectionNavigator.Sections.Any(s => s.SectionId == "basics")).IsTrue();
            await Assert.That(window.SectionNavigator.Sections.Any(s => s.SectionId == "manifest")).IsTrue();
            string basicsTitle = window.SectionNavigator.Sections.Single(s => s.SectionId == "basics").Title;
            await Assert.That(basicsTitle).Contains(locale == "ar" ? "الإعدادات الأساسية" : "Essentials");

            // Guided editing + Undo on /apiEndpoint after selecting compatible deployment context.
            await Assert.That(form.SelectConfigurationContext("separate-images", "all")).IsTrue();
            window.JumpToSection("basics");
            window.FocusSlotPath(DefinitionPath.FromSegments(["apiEndpoint"]));
            window.PublicInput.Text = "https://terminal-one.example.org";
            await Assert.That(window.CommitField()).IsTrue();
            window.PublicInput.Text = "https://terminal-two.example.org";
            await Assert.That(window.CommitField()).IsTrue();
            await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://terminal-two.example.org");

            await Assert.That(window.UndoLastEdit()).IsTrue();
            await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://terminal-one.example.org");

            // Compact table toggle + search + cell edit + return to Guided.
            window.SetConfigureMode(TerminalConfigureMode.Compact);
            await Assert.That(window.ConfigureMode).IsEqualTo(TerminalConfigureMode.Compact);
            window.CompactTableView.RefreshRows(query: "API");
            await Assert.That(window.CompactTableView.SelectRowByPath(DefinitionPath.FromSegments(["apiEndpoint"]))).IsTrue();
            await Assert.That(window.CompactTableView.TryEditSelected("https://terminal-compact.example.org")).IsTrue();
            window.SetConfigureMode(TerminalConfigureMode.Guided);
            await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://terminal-compact.example.org");

            // Document (JSON) preview & apply.
            byte[] initialDocBytes;
            using (DefinitionDocumentSession readSession = form.OpenDocument())
                initialDocBytes = readSession.ReadSource();
            using (var docDialog = new TerminalDocumentDialog(form))
            {
                docDialog.BeginInit();
                docDialog.EndInit();
                JsonObject docJson = JsonNode.Parse(Encoding.UTF8.GetString(initialDocBytes))!.AsObject();
                docJson["values"]!["apiEndpoint"] = "https://terminal-doc.example.org";
                byte[] utf8 = Encoding.UTF8.GetBytes(docJson.ToJsonString());
                using DefinitionDocumentParseResult preview = docDialog.TryPreparePreview(utf8);
                await Assert.That(preview.Succeeded).IsTrue();
                DefinitionDocumentApplyResult applied = docDialog.ApplyPrepared();
                await Assert.That(applied.Succeeded).IsTrue();
            }
            await Assert.That(form.Snapshot().Values["apiEndpoint"].GetString()).IsEqualTo("https://terminal-doc.example.org");

            // Switch to configuration-manifest, populate required fields, move to Review & Files, and prepare.
            await Assert.That(form.SelectOutput("configuration-manifest")).IsTrue();
            form.EditValue(["configurationManifest", "metadata", "name"], "terminal-rev4-" + locale);
            form.SupplyObject(["configurationManifest", "spec", "instance", "settings"]);
            form.EditValue(["configurationManifest", "spec", "instance", "settings", "modules.tech_enabled"], "true");
            form.SupplyList(["configurationManifest", "tenants"]);

            window.SetStage(TerminalWorkbenchStage.ReviewAndImport);
            await Assert.That(window.ActiveStage).IsEqualTo(TerminalWorkbenchStage.ReviewAndImport);
            window.SetStage(TerminalWorkbenchStage.FilesAndSave);
            await Assert.That(window.ActiveStage).IsEqualTo(TerminalWorkbenchStage.FilesAndSave);

            TerminalPreparationOutcome prepared = await operation.PrepareAsync();
            await Assert.That(operation.AdoptPreparation(prepared)).IsEqualTo(TerminalPreparationCode.Ready);
            await Assert.That(operation.ArtifactId).IsEqualTo("configuration-manifest");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task GrafanaAndSecretGeneratorDialogRunOnTerminalWithoutEventBranches(string locale)
    {
        Definition grafana = LoadRepositoryDefinition("examples/grafana-13.2.2/setup-definition.json");
        string tempDir = Directory.CreateTempSubdirectory("configurator-terminal-grafana-").FullName;
        try
        {
            using var grafanaForm = new DefinitionFormModel(grafana);
            using var application = Application.Create();
            application.Init("dotnet");
            using var secret = new TerminalSecretBuffer(32768, urlSafeOnly: false);
            using var session = new PresentationSession(new StrongReferenceMessenger());
            using var operation = new TerminalArtifactOperation(grafanaForm, new ProtectedArtifactWriter(), tempDir);
            if (!WorkspaceId.TryCreate("grafana-" + locale, out WorkspaceId id))
                throw new InvalidOperationException("workspace-id-invalid");
            using var workspace = session.CreateWorkspace(id, operation);
            workspace.Activate();
            using var window = new TerminalWindow(application, workspace, operation, secret);
            window.BeginInit();
            window.EndInit();
            window.Layout(new Size(120, 40));

            await Assert.That(window.SectionNavigator.Sections.Select(s => s.SectionId).ToArray())
                .IsEquivalentTo(["server", "datasource"]);

            window.JumpToSection("server");
            window.SubViews.OfType<global::Terminal.Gui.Views.Button>()
                .Single(button => button.Text == TerminalStrings.Get("SupplyContainer"))
                .InvokeCommand(global::Terminal.Gui.Input.Command.Accept);
            window.FocusSlotPath(DefinitionPath.FromSegments(["server", "domain"]));
            window.PublicInput.Text = "grafana-one.example.org";
            await Assert.That(window.CommitField()).IsTrue();
            window.FocusSlotPath(DefinitionPath.FromSegments(["server", "domain"]));
            window.PublicInput.Text = "grafana-two.example.org";
            await Assert.That(window.CommitField()).IsTrue();
            await Assert.That(window.UndoLastEdit()).IsTrue();
            await Assert.That(grafanaForm.Snapshot().Values["server"].GetProperty("domain").GetString())
                .IsEqualTo("grafana-one.example.org");

            window.SetConfigureMode(TerminalConfigureMode.Compact);
            await Assert.That(window.CompactTableView.SelectRowByPath(DefinitionPath.FromSegments(["server", "domain"]))).IsTrue();
            await Assert.That(window.CompactTableView.TryEditSelected("grafana-compact.example.org")).IsTrue();
            window.SetConfigureMode(TerminalConfigureMode.Guided);

            grafanaForm.EditValue(["datasource", "name"], "Graphite");
            grafanaForm.EditValue(["datasource", "url"], "https://graphite.example.org");
            TerminalPreparationOutcome prepared = await operation.PrepareAsync();
            await Assert.That(operation.AdoptPreparation(prepared)).IsEqualTo(TerminalPreparationCode.Ready);

            // Modal secret generator works cleanly for generic secret-generation definition in EN & AR.
            Definition secretGen = LoadRepositoryDefinition("examples/secret-generation/setup-definition.json");
            using var secretForm = new DefinitionFormModel(secretGen, DefinitionSessionPrivacy.NativeComplete);
            secretForm.SelectLocale(locale);
            DefinitionPath passwordPath = DefinitionPath.FromSegments(["password"]);
            using DefinitionSecretGeneratorSession generatorSession = secretForm.CreateSecretGeneratorSession(passwordPath)!;
            using var generatorDialog = new TerminalSecretGeneratorDialog(
                secretForm, generatorSession, application, dispatch: callback => callback());
            generatorDialog.BeginInit();
            generatorDialog.EndInit();
            await Assert.That(await generatorDialog.PreviewCandidateAsync()).IsTrue();
            string visibleBeforeApply = generatorDialog.ExportAllVisibleText();
            string previewCandidate = generatorDialog.PreviewBuffer.CopyText();
            await Assert.That(secretForm.Snapshot().Values.ContainsKey("password")).IsFalse();
            await Assert.That(visibleBeforeApply).DoesNotContain(previewCandidate);
            await Assert.That(visibleBeforeApply).DoesNotContain("\x1b]52;");

            await Assert.That(await generatorDialog.ApplyCandidateAsync()).IsTrue();
            await Assert.That(secretForm.Snapshot().Values["password"].GetString()).IsEqualTo(previewCandidate);
            await Assert.That(secretForm.AnswerOrigin(["password"])).IsEqualTo(DefinitionAnswerOrigin.Generated);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    private static Definition LoadRepositoryDefinition(string relativePath)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Configurator.slnx")))
            current = current.Parent;
        if (current is null) throw new InvalidOperationException("Repository root not found.");
        return DefinitionCodec.ParseJson(File.ReadAllBytes(Path.Combine(current.FullName, relativePath)));
    }
}
