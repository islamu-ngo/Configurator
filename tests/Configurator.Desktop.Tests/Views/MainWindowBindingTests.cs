namespace Configurator.Desktop.Tests.Views;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Configurator.Artifacts;
using Configurator.Desktop.Services;
using Configurator.Desktop.ViewModels;
using Configurator.Desktop.Views;

public sealed class MainWindowBindingTests
{
    private static readonly string[] ActionNames =
    [
        "LoadDefinition", "Validate", "Prepare", "Save", "PrepareBundle", "SaveBundle",
        "Import", "Cancel", "SelectDefinition", "SelectImport", "SelectOutput"
    ];

    public static Avalonia.AppBuilder BuildAvaloniaApp() => DesktopApplicationTests.BuildAvaloniaApp();

    [Test]
    [NotInParallel]
    public async Task RenderedActionsUseCommandsAndFieldsUseDeclarativeItems()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowBindingTests));
        try
        {
            var state = await session.Dispatch(() =>
            {
                var messenger = new WeakReferenceMessenger();
                var dialogs = new NoDialogs();
                using var loader = new DefinitionLoaderViewModel(messenger, dialogs);
                using var workspace = new FormWorkspaceViewModel(messenger);
                using var publisher = new ArtifactPublisherViewModel(messenger, dialogs, new ProtectedArtifactWriter());
                using var shell = new DesktopShellViewModel(messenger, loader, workspace, publisher);
                var window = new MainWindow { DataContext = shell };
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    loader.LoadDefinition(DesktopApplicationTests.Definition());
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    return (
                        commands: ActionNames
                            .All(name => window.GetControl<Button>(name).Command is not null),
                        declarative: window.FindControl<ItemsControl>("Fields") is { ItemCount: > 0 },
                        artifactLabel: window.GetControl<ComboBox>("Artifact").SelectedIndex >= 0 &&
                            window.GetControl<ComboBox>("Artifact").SelectionBoxItem is not null &&
                            window.GetControl<ComboBox>("Artifact").GetVisualDescendants()
                                .OfType<TextBlock>().Any(control => control.Text == publisher.ArtifactId &&
                                    control.IsEffectivelyVisible && control.Bounds.Width > 0),
                        gated: !window.GetControl<Button>("Save").IsEffectivelyEnabled &&
                            !window.GetControl<Button>("SaveBundle").IsEffectivelyEnabled);
                }
                finally { window.Close(); }
            }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
            await Assert.That(state.commands).IsTrue();
            await Assert.That(state.declarative).IsTrue();
            await Assert.That(state.artifactLabel).IsTrue();
            await Assert.That(state.gated).IsTrue();
        }
        finally { await Task.Run(session.Dispose); }
    }

    private sealed class NoDialogs : IStorageDialogService
    {
        public Task<Uri?> SelectAsync(DesktopPathSelectionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<Uri?>(null);

        public Task<BoundedFileInput?> ReadImportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<BoundedFileInput?>(null);
    }
}
