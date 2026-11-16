namespace Configurator.Desktop.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Resources;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;
using Configurator.Presentation;

public sealed class DesktopEventRevision4WorkflowTests
{
    public static AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    [Arguments("en")]
    [Arguments("ar")]
    public async Task EventRevision4BilingualWorkbenchExercisesGuidedCompactDocumentUndoAndReview(string locale)
    {
        byte[] revision4Bytes = ReadRepositoryFile("store/islamu-event/releases/4.json");
        Definition parsed = DefinitionCodec.ParseJson(revision4Bytes);
        await Assert.That(parsed.Revision).IsEqualTo(4);

        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopEventRevision4WorkflowTests));
        try
        {
            var observed = await session.Dispatch(async () =>
            {
                using var shell = DesktopTestContext.Root(protectedOutputAvailable: true);
                if (!shell.Loader.LoadDefinition(revision4Bytes))
                    throw new InvalidOperationException("LoadDefinition failed: " + shell.Status);

                var window = new MainWindow { DataContext = shell, Width = 1360, Height = 860 };
                try
                {
                    shell.Workspace.SelectedLocale = locale;
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    bool flowDirectionMatchesLocale = (window.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft) == (locale == "ar");
                    DesktopSectionViewModel basicsSection = shell.Navigation.Sections.Single(s => s.Id == "basics");
                    bool basicsTitleLocalized = locale == "ar"
                        ? basicsSection.Title.Contains("الإعدادات الأساسية", StringComparison.Ordinal)
                        : basicsSection.Title.Contains("Essentials", StringComparison.Ordinal);
                    bool sectionOwnershipClean = shell.Workspace.Form!.Projection.Sections.Single(s => s.Id == "basics").Fields.Contains("apiEndpoint")
                        && shell.Workspace.Form.Projection.Sections.Single(s => s.Id == "basics").Fields.Contains("platform")
                        && shell.Workspace.Form.Projection.Sections.Single(s => s.Id == "manifest").Fields.Contains("configurationManifest");

                    // Activate /apiEndpoint via compatible deployment pair ("separate-images", "all").
                    shell.Workspace.SelectedConfigurationContext = shell.Workspace.ConfigurationChoices
                        .Single(c => c.RecipeId == "separate-images" && c.ScopeId == "all");
                    Dispatcher.UIThread.RunJobs();
                    sectionOwnershipClean &= shell.Navigation.Sections.Single(s => s.Id == "basics").Fields.Any(f => f.Id == "apiEndpoint");

                    FieldViewModel apiEndpoint = shell.Workspace.Fields.Single(f => f.Id == "apiEndpoint");
                    apiEndpoint.Text = "https://api-first.example.org";
                    apiEndpoint.Text = "https://api-second.example.org";

                    bool canUndoBefore = shell.Workspace.UndoCommand.CanExecute(null);
                    shell.Workspace.UndoCommand.Execute(null);
                    string? afterUndo = shell.Workspace.Form!.Snapshot().Values["apiEndpoint"].GetString();
                    shell.Workspace.RedoCommand.Execute(null);
                    string? afterRedo = shell.Workspace.Form.Snapshot().Values["apiEndpoint"].GetString();

                    // All-sections unified workbench search + direct FieldViewModel edit + round-trip to SectionBySection.
                    shell.Navigation.LayoutMode = ConfigureLayoutMode.AllSections;
                    shell.Navigation.SearchText = "API";
                    Dispatcher.UIThread.RunJobs();
                    var unifiedApiField = shell.Navigation.AllSectionsFields
                        .Single(f => f.Id == "apiEndpoint");
                    unifiedApiField.Text = "https://api-compact.example.org";
                    shell.Navigation.SearchText = string.Empty;
                    shell.Navigation.CloseCommandPaletteCommand.Execute(null);
                    shell.Navigation.LayoutMode = ConfigureLayoutMode.SectionBySection;
                    Dispatcher.UIThread.RunJobs();
                    string? guidedAfterCompact = shell.Workspace.Form.Snapshot().Values["apiEndpoint"].GetString();

                    // Document (JSON) preview & apply.
                    shell.Navigation.IsDocumentOpen = true;
                    Dispatcher.UIThread.RunJobs();
                    JsonObject docJson = JsonNode.Parse(shell.DocumentEditor.DocumentText)!.AsObject();
                    docJson["values"]!["apiEndpoint"] = "https://api-document.example.org";
                    shell.DocumentEditor.DocumentText = docJson.ToJsonString();
                    bool docPreviewOk = shell.DocumentEditor.PreviewChanges();
                    bool docApplied = shell.DocumentEditor.ApplyPreview();
                    string? afterDocument = shell.Workspace.Form.Snapshot().Values["apiEndpoint"].GetString();

                    // Switch to configuration-manifest, populate required manifest structure, validate & prepare.
                    shell.Navigation.IsDocumentOpen = false;
                    shell.Navigation.LayoutMode = ConfigureLayoutMode.SectionBySection;
                    shell.Workspace.Form.SelectOutput("configuration-manifest");
                    Dispatcher.UIThread.RunJobs();
                    sectionOwnershipClean &= shell.Navigation.Sections.Single(s => s.Id == "manifest").Fields.Any(f => f.Id == "configurationManifest");
                    var manifestObj = (ObjectFieldViewModel)shell.Workspace.Fields.Single(f => f.Id == "configurationManifest");
                    var metadataObj = (ObjectFieldViewModel)manifestObj.Children.Single(f => f.Id == "metadata");
                    metadataObj.Children.Single(f => f.Id == "name").Text = "desktop-rev4-" + locale;
                    var specObj = (ObjectFieldViewModel)manifestObj.Children.Single(f => f.Id == "spec");
                    var instanceObj = (ObjectFieldViewModel)specObj.Children.Single(f => f.Id == "instance");
                    var settingsObj = (ObjectFieldViewModel)instanceObj.Children.Single(f => f.Id == "settings");
                    settingsObj.SupplyCommand.Execute(null);
                    settingsObj.Children.Single(f => f.Id == "modules.tech_enabled").Text = "true";
                    var tenantsList = (ListFieldViewModel)manifestObj.Children.Single(f => f.Id == "tenants");
                    tenantsList.SupplyCommand.Execute(null);

                    shell.Navigation.ReviewCommand.Execute(null);
                    bool valid = shell.Workspace.Validate();
                    DesktopPreparationOutcome prepared = await shell.Publisher.PrepareAsync();
                    string? preparedFileName = shell.Publisher.Preparation?.FileName;

                    return (
                        flowDirectionMatchesLocale,
                        basicsTitleLocalized,
                        sectionOwnershipClean,
                        canUndoBefore,
                        afterUndo,
                        afterRedo,
                        guidedAfterCompact,
                        docPreviewOk,
                        docApplied,
                        afterDocument,
                        valid,
                        prepared,
                        preparedFileName);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(observed.flowDirectionMatchesLocale).IsTrue();
            await Assert.That(observed.basicsTitleLocalized).IsTrue();
            await Assert.That(observed.sectionOwnershipClean).IsTrue();
            await Assert.That(observed.canUndoBefore).IsTrue();
            await Assert.That(observed.afterUndo).IsEqualTo("https://api-first.example.org");
            await Assert.That(observed.afterRedo).IsEqualTo("https://api-second.example.org");
            await Assert.That(observed.guidedAfterCompact).IsEqualTo("https://api-compact.example.org");
            await Assert.That(observed.docPreviewOk).IsTrue();
            await Assert.That(observed.docApplied).IsTrue();
            await Assert.That(observed.afterDocument).IsEqualTo("https://api-document.example.org");
            await Assert.That(observed.valid).IsTrue();
            await Assert.That(observed.prepared).IsEqualTo(DesktopPreparationOutcome.Prepared);
            await Assert.That(observed.preparedFileName).IsEqualTo("configuration-manifest.json");
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task NonEventGrafanaAndSecretGeneratorDialogRunWithoutEventBranches()
    {
        byte[] grafanaBytes = ReadRepositoryFile("examples/grafana-13.2.2/setup-definition.json");
        byte[] secretGenBytes = ReadRepositoryFile("examples/secret-generation/setup-definition.json");

        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopEventRevision4WorkflowTests));
        try
        {
            var observed = await session.Dispatch(async () =>
            {
                // 1. Grafana 13.2.2: Guided, Undo/Redo, Compact, Document, Multi-file bundle prepare.
                using var grafanaShell = DesktopTestContext.Root(protectedOutputAvailable: true);
                if (!grafanaShell.Loader.LoadDefinition(grafanaBytes))
                    throw new InvalidOperationException("Grafana load failed: " + grafanaShell.Status);

                bool onlyGrafanaSections = grafanaShell.Navigation.Sections.Select(s => s.Id)
                    .SequenceEqual(["server", "datasource"]);
                bool noDeploymentChoices = grafanaShell.Workspace.ConfigurationChoices.Count == 0;

                var server = (ObjectFieldViewModel)grafanaShell.Workspace.Fields.Single(f => f.Id == "server");
                var domain = server.Children.Single(f => f.Id == "domain");
                domain.Text = "grafana-a.example.org";
                domain.Text = "grafana-b.example.org";
                grafanaShell.Workspace.UndoCommand.Execute(null);
                string? grafanaUndo = grafanaShell.Workspace.Form!.Snapshot().Values["server"].GetProperty("domain").GetString();
                grafanaShell.Workspace.RedoCommand.Execute(null);

                var datasource = (ObjectFieldViewModel)grafanaShell.Workspace.Fields.Single(f => f.Id == "datasource");
                datasource.Children.Single(f => f.Id == "name").Text = "Graphite";
                datasource.Children.Single(f => f.Id == "url").Text = "https://graphite.example.org";

                grafanaShell.Publisher.SelectArtifact("environment", true);
                grafanaShell.Publisher.SelectArtifact("datasource", true);
                DesktopPreparationOutcome grafanaPrepared = await grafanaShell.Publisher.PrepareBundleAsync();
                bool grafanaBundleReady = grafanaShell.Publisher.CanSaveBundle;

                // 2. Modal Secret Generator on real example schema using headless dialog lifecycle.
                using var secretForm = new DefinitionFormModel(
                    DefinitionCodec.ParseJson(secretGenBytes),
                    DefinitionSessionPrivacy.NativeComplete);
                using DefinitionSecretGeneratorSession generatorSession = secretForm
                    .CreateSecretGeneratorSession(DefinitionPath.FromSegments(["password"]))!;
                using var generatorViewModel = new SecretGeneratorDialogViewModel(
                    secretForm, generatorSession, new DesktopStrings("en"));
                var dialog = new SecretGeneratorDialog { DataContext = generatorViewModel };
                var owner = new Window { Width = 880, Height = 640 };
                owner.Show();
                Dispatcher.UIThread.RunJobs();

                Task<bool> dialogTask = dialog.ShowDialog<bool>(owner);
                Dispatcher.UIThread.RunJobs();
                await generatorViewModel.GeneratePreviewAsync();
                string previewText = generatorViewModel.PreviewText;
                bool previewExcludedPriorToApply = !secretForm.Snapshot().Values.ContainsKey("password");
                DefinitionSecretOperationResult applied = await generatorViewModel.ApplyAsync();
                bool dialogClosed = await dialogTask.WaitAsync(TimeSpan.FromSeconds(5));
                string? appliedPassword = secretForm.Snapshot().Values["password"].GetString();
                DefinitionAnswerOrigin origin = secretForm.AnswerOrigin(["password"]);
                owner.Close();
                Dispatcher.UIThread.RunJobs();

                return (
                    onlyGrafanaSections,
                    noDeploymentChoices,
                    grafanaUndo,
                    grafanaPrepared,
                    grafanaBundleReady,
                    previewLength: previewText.Length,
                    previewExcludedPriorToApply,
                    appliedSucceeded: applied.Succeeded,
                    dialogClosed,
                    appliedMatchesPreview: appliedPassword == previewText,
                    origin);
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(observed.onlyGrafanaSections).IsTrue();
            await Assert.That(observed.noDeploymentChoices).IsTrue();
            await Assert.That(observed.grafanaUndo).IsEqualTo("grafana-a.example.org");
            await Assert.That(observed.grafanaPrepared).IsEqualTo(DesktopPreparationOutcome.Prepared);
            await Assert.That(observed.grafanaBundleReady).IsTrue();
            await Assert.That(observed.previewLength).IsEqualTo(32);
            await Assert.That(observed.previewExcludedPriorToApply).IsTrue();
            await Assert.That(observed.appliedSucceeded).IsTrue();
            await Assert.That(observed.dialogClosed).IsTrue();
            await Assert.That(observed.appliedMatchesPreview).IsTrue();
            await Assert.That(observed.origin).IsEqualTo(DefinitionAnswerOrigin.Generated);
        }
        finally { await Task.Run(session.Dispose); }
    }

    private static byte[] ReadRepositoryFile(string relativePath)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Configurator.slnx")))
            current = current.Parent;
        if (current is null) throw new InvalidOperationException("Repository root not found.");
        return File.ReadAllBytes(Path.Combine(current.FullName, relativePath));
    }
}
