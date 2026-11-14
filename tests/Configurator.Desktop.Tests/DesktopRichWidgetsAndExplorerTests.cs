namespace Configurator.Desktop.Tests;

using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Core.Definitions;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.ViewModels.Fields;
using Configurator.Desktop.Views;
using Configurator.Presentation.Store;

public sealed class DesktopRichWidgetsAndExplorerTests
{
    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task InlineGuidanceAndRichWidgetsRenderDirectlyInVisualTreeWithoutTooltipHiding()
    {
        byte[] schema = Encoding.UTF8.GetBytes("""
            {
              "$schema": "urn:islamu:configurator:authoring:v0.0.1",
              "identity": { "familyId": "a1b2c3d4-1111-4222-8333-444455556666", "revision": 1 },
              "project": { "id": "rich-desktop", "name": "Rich Desktop Fixture" },
              "applicationVersions": [ "1.0.0" ],
              "configuration": {
                "type": "object",
                "additionalProperties": false,
                "x-configurator": { "sensitivity": "public" },
                "properties": {
                  "hostTimeout": {
                    "type": "integer",
                    "minimum": 100,
                    "maximum": 5000,
                    "deprecated": true,
                    "title": "Host Timeout",
                    "description": "Maximum duration before socket abort",
                    "x-configurator": {
                      "sensitivity": "public",
                      "detail": "basic",
                      "units": "ms",
                      "placeholder": "1000",
                      "defaultText": "1000 ms",
                      "deprecationMessage": "Use gatewayTimeout in v2",
                      "widget": "slider"
                    }
                  },
                  "notes": {
                    "type": "string",
                    "title": "Operator Notes",
                    "x-configurator": { "sensitivity": "public", "detail": "basic", "widget": "multiline", "rows": 4 }
                  },
                  "mode": {
                    "type": "string",
                    "enum": [ "local", "relay" ],
                    "x-configurator": {
                      "sensitivity": "public",
                      "detail": "basic",
                      "widget": "radio",
                      "choices": [
                        { "value": "local", "label": "Local Only", "explanation": "Runs embedded listener only" },
                        { "value": "relay", "label": "Relay Bridge", "explanation": "Connects through upstream relay" }
                      ]
                    }
                  },
                  "strictTls": {
                    "type": "boolean",
                    "title": "Strict TLS",
                    "x-configurator": { "sensitivity": "public", "detail": "basic", "widget": "switch" }
                  },
                  "routes": {
                    "type": "array",
                    "title": "Routes",
                    "x-configurator": {
                      "sensitivity": "public",
                      "detail": "basic",
                      "itemTitlePointer": "/name",
                      "uniqueBy": "/name"
                    },
                    "items": {
                      "type": "object",
                      "additionalProperties": false,
                      "x-configurator": { "sensitivity": "public" },
                      "required": [ "name" ],
                      "properties": {
                        "name": { "type": "string", "minLength": 1, "x-configurator": { "sensitivity": "public", "detail": "basic" } }
                      }
                    }
                  },
                  "apiSecret": {
                    "type": "string",
                    "minLength": 8,
                    "title": "API Secret",
                    "x-configurator": { "sensitivity": "secret", "detail": "basic", "secret": { "kind": "password", "acquisition": "local", "generation": { "length": 24 } } }
                  }
                },
                "required": [ "apiSecret" ]
              },
              "presentation": {
                "defaultJourney": "default",
                "navigation": "sections",
                "sections": [
                  {
                    "id": "main",
                    "title": "Main",
                    "pointers": [ "/hostTimeout", "/notes", "/mode", "/strictTls", "/routes", "/apiSecret" ],
                    "groups": [
                      {
                        "id": "collapsed-secrets",
                        "title": "Collapsed Group",
                        "detail": "basic",
                        "collapsed": true,
                        "pointers": [ "/apiSecret" ]
                      }
                    ]
                  }
                ],
                "journeys": [
                  { "id": "default", "title": "Standard", "detail": "basic", "sections": [ "main" ] }
                ]
              },
              "outputs": { "config.json": { "id": "config", "format": "json" } }
            }
            """);

        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopRichWidgetsAndExplorerTests));
        try
        {
            var result = await session.Dispatch(() =>
            {
                var clipboard = new RecordingClipboard();
                var messenger = new WeakReferenceMessenger();
                var dialogs = new NoDialogs();
                var loader = DesktopTestContext.Loader(messenger, dialogs);
                var workspace = new FormWorkspaceViewModel(messenger, clipboard);
                var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                var importReview = new DesktopImportReviewViewModel(messenger, dialogs, publisher);
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher, importReview);
                shell.Loader.LoadDefinition(DefinitionSourceCodec.ParseJson(schema).CanonicalBytes);

                var routes = (ListFieldViewModel)shell.Workspace.Fields.Single(f => f.Id == "routes");
                routes.SupplyCommand.Execute(null);
                routes.AddItemCommand.Execute(null);
                routes.AddItemCommand.Execute(null);
                ((ObjectFieldViewModel)routes.Items[0]).Children.Single(f => f.Id == "name").Text = "dup";
                ((ObjectFieldViewModel)routes.Items[1]).Children.Single(f => f.Id == "name").Text = "dup";

                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 900 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var visibleTexts = window.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Where(tb => tb.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(tb.Text))
                        .Select(tb => tb.Text!)
                        .ToArray();

                    var timeoutField = shell.Workspace.Fields.Single(f => f.Id == "hostTimeout");
                    var notesField = shell.Workspace.Fields.Single(f => f.Id == "notes");
                    var notesBox = window.GetVisualDescendants().OfType<TextBox>()
                        .Single(tb => ReferenceEquals(tb.DataContext, notesField));
                    var radioChoices = window.GetVisualDescendants().OfType<RadioButton>()
                        .Where(rb => rb.DataContext is EnumChoice choice && (choice.Label == "Local Only" || choice.Label == "Relay Bridge"))
                        .ToArray();
                    var boolField = (BooleanFieldViewModel)shell.Workspace.Fields.Single(f => f.Id == "strictTls");
                    var switchControl = window.GetVisualDescendants().OfType<ToggleSwitch>()
                        .Single(ts => ReferenceEquals(ts.DataContext, boolField));
                    var sliderControl = window.GetVisualDescendants().OfType<Slider>()
                        .Single(sl => ReferenceEquals(sl.DataContext, timeoutField));

                    Button? publicCopyBtn = window.GetVisualDescendants().OfType<Button>()
                        .FirstOrDefault(btn => ReferenceEquals(btn.DataContext, timeoutField) && btn.Name == "CopyWireKey" && btn.IsVisible);
                    var secretField = shell.Workspace.Fields.Single(f => f.Id == "apiSecret");
                    Button? secretCopyBtn = window.GetVisualDescendants().OfType<Button>()
                        .FirstOrDefault(btn => ReferenceEquals(btn.DataContext, secretField) && btn.Name == "CopyWireKey" && btn.IsVisible);
                    publicCopyBtn?.Command?.Execute(publicCopyBtn.CommandParameter);

                    shell.Workspace.Validate();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    bool groupAutoExpanded = shell.Navigation.SelectedSection?.Groups
                        .FirstOrDefault(g => g.Id == "collapsed-secrets")?.IsExpanded == true;

                    return (
                        hasUnits: visibleTexts.Any(t => t.Contains("ms", StringComparison.Ordinal)),
                        hasDefault: visibleTexts.Any(t => t.Contains("1000 ms", StringComparison.Ordinal)),
                        hasDeprecation: visibleTexts.Any(t => t.Contains("Use gatewayTimeout in v2", StringComparison.Ordinal)),
                        hasChoiceExplanation: visibleTexts.Any(t => t.Contains("Runs embedded listener only", StringComparison.Ordinal)),
                        multilineAcceptsReturn: notesBox.AcceptsReturn,
                        radioCount: radioChoices.Length,
                        omittedSwitchStartsNull: !boolField.Value.HasValue && switchControl.IsChecked is null,
                        sliderBounds: sliderControl.Minimum == 100d && sliderControl.Maximum == 5000d,
                        itemTitleRendered: routes.Items[0].Title.Contains("dup", StringComparison.Ordinal),
                        uniqueDuplicateDetected: routes.HasDuplicateKeys,
                        groupAutoExpanded,
                        copiedPublicKey: clipboard.CopiedValues.Contains("hostTimeout"),
                        secretCopySuppressed: secretCopyBtn is null);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.hasUnits).IsTrue();
            await Assert.That(result.hasDefault).IsTrue();
            await Assert.That(result.hasDeprecation).IsTrue();
            await Assert.That(result.hasChoiceExplanation).IsTrue();
            await Assert.That(result.multilineAcceptsReturn).IsTrue();
            await Assert.That(result.radioCount).IsGreaterThanOrEqualTo(2);
            await Assert.That(result.omittedSwitchStartsNull).IsTrue();
            await Assert.That(result.sliderBounds).IsTrue();
            await Assert.That(result.itemTitleRendered).IsTrue();
            await Assert.That(result.uniqueDuplicateDetected).IsTrue();
            await Assert.That(result.groupAutoExpanded).IsTrue();
            await Assert.That(result.copiedPublicKey).IsTrue();
            await Assert.That(result.secretCopySuppressed).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    [Test]
    [NotInParallel]
    public async Task DefinitionExplorerViewInspectsStoreProjectReadOnlyWithoutCreatingWorkspace()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(DesktopRichWidgetsAndExplorerTests));
        try
        {
            var result = await session.Dispatch(async () =>
            {
                using var shell = DesktopTestContext.Root(storeProvider: new FakeStoreProvider());
                await shell.Loader.LoadStoreAsync();
                var card = shell.Loader.StoreCatalog.Projects.Single();
                await shell.Loader.InspectStoreProjectCommand.ExecuteAsync(card);

                var window = new MainWindow { DataContext = shell, Width = 1280, Height = 820 };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    var explorer = window.FindNamedControl<DefinitionExplorerView>("DefinitionExplorer");
                    return (
                        explorerOpen: shell.Loader.Explorer.IsOpen,
                        explorerVisible: explorer?.IsVisible == true,
                        sectionCount: shell.Loader.Explorer.Sections.Count,
                        outputCount: shell.Loader.Explorer.Outputs.Count,
                        formStillNull: shell.Workspace.Form is null,
                        stillOnOpenStage: shell.Navigation.Stage == DesktopWorkspaceStage.Open);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(result.explorerOpen).IsTrue();
            await Assert.That(result.explorerVisible).IsTrue();
            await Assert.That(result.sectionCount).IsGreaterThanOrEqualTo(1);
            await Assert.That(result.outputCount).IsGreaterThanOrEqualTo(1);
            await Assert.That(result.formStillNull).IsTrue();
            await Assert.That(result.stillOnOpenStage).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private sealed class RecordingClipboard : ISecretClipboardService
    {
        public List<string> CopiedValues { get; } = [];
        public Task<bool> CopyAsync(string value)
        {
            CopiedValues.Add(value);
            return Task.FromResult(true);
        }
    }

    private sealed class NoDialogs : IStorageDialogService
    {
        public Task<StorageSelection<Uri>> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<Uri>(StorageSelectionStatus.Cancelled));
        public Task<StorageSelection<BoundedFileInput>> ReadImportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageSelection<BoundedFileInput>(StorageSelectionStatus.Cancelled));
    }

    private sealed class FakeStoreProvider : IDefinitionStoreProvider
    {
        private static readonly byte[] DefinitionBytes = ViewModels.DefinitionEditingTests.Definition().CanonicalBytes.ToArray();

        public Task<IReadOnlyList<DefinitionStoreProject>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DefinitionStoreProject>>(
            [
                new DefinitionStoreProject
                {
                    ProjectId = "synthetic-store-app",
                    ApplicationId = "synthetic-store-app",
                    FamilyId = Guid.Parse("7cb18fb4-3795-4b09-ab10-e9286b7c0f01"),
                    Name = "Synthetic Store App",
                    Description = "Synthetic preview definition",
                    Tags = ["service"],
                    Revisions =
                    [
                        new DefinitionStoreRevision
                        {
                            Revision = 1,
                            Title = "Rev 1",
                            Digest = "synthetic-sha"
                        }
                    ]
                }
            ]);

        public Task<DefinitionStoreResult> OpenAsync(string projectId, long revision, CancellationToken cancellationToken = default) =>
            Task.FromResult<DefinitionStoreResult>(new DefinitionStoreResult.Success(DefinitionSourceCodec.ParseJson(DefinitionBytes)));
    }
}
